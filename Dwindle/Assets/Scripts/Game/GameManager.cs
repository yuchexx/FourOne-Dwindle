using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// holds the state of the level, handles clicks, and applies the rules
// the undo part is in GameManager.Undo.cs
public partial class GameManager : MonoBehaviour
{
    [Header("Views")]
    public BoardView board;
    public HudView hud;
    public MenuView menu;

    [Header("Timing")]
    public float endPopupDelay = 1.2f; // seconds between the level ending and the end window

    [Header("Scoring")]
    public int pointsPerDelivery = 100;
    public int comboPointsPerObject = 50; // per object, when one placement delivers 2 or more
    public int pointsPerTeleport = 50;    // once per delivered object that went through at least one door
    public int pointsPerUnusedTile = 75;  // per tile left in the supply, only if every object was delivered

    [Header("Stuck notice")]
    [Tooltip("Seconds without placing a tile, while stuck, before the notice shows (0 = right away)")]
    [Min(0f)] public float stuckSecondsBeforeNotice = 8f;

    int levelIndex;
    LevelData level;
    Dictionary<TileType, int> inventory;
    TileType? selected;
    bool busy; // true while objects are moving or waiting for a choice
    List<MovingObject> objects;
    List<RemovedEntry> removed;
    string finalComboNote; // set when the last objects arrive together
    bool levelOver;
    bool level1TutorialShown;

    // score, kept per source so the end window can show where it came from
    int deliveryPoints, comboPoints, teleportPoints, unusedPoints;
    int comboObjects, teleports, unusedTiles; // how many of each, for the breakdown (teleports: objects that used a door)
    int Score => deliveryPoints + comboPoints + teleportPoints + unusedPoints;

    Cell[,] Cells => board.Cells;

    void Start()
    {
        ShowStartScreen();
    }

    public void LoadLevel(int index)
    {
        StopAllCoroutines();
        TelemetryManager.Instance.StartLevel(index);
        levelReported = false;
        stuck = false;
        HideStuckNotice();
        board.ClearChoices();
        hud.SetChoiceMode(false);
        hud.ResetSlotFlashes();
        menu.ShowGame();
        levelOver = false;
        levelIndex = index;
        level = Levels.All[index];
        inventory = Tiles.All.ToDictionary(t => t, t => level.inventory.TryGetValue(t, out var c) ? c : 0);
        selected = null;
        busy = false;
        hud.ClearLog();
        removed = new List<RemovedEntry>();
        finalComboNote = null;
        deliveryPoints = comboPoints = teleportPoints = unusedPoints = 0;
        comboObjects = teleports = unusedTiles = 0;
        history.Clear();

        hud.BuildSlots(Tiles.All, SelectTile); // every tile type, x0 for the ones this level doesn't hand out
        objects = board.Build(level, OnCellClicked, OnDoorClicked, OnCellRightClicked, PreviewTile);
        Log($"{level.title}: pick a tile, then click an empty cell.");
        if (level.doors.Count > 0)
            Log("Doors with the same letter are linked: a road into one comes out of the other.");
        RefreshAll();

        if (index == 0 && !level1TutorialShown)
        {
            level1TutorialShown = true;

            menu.ShowTutorial(
                "Planning Tip",
                "Each delivery breaks one tile of its path - often a junction someone else still needs.\n" +
                "Delivering two or more objects at once earns you a + tile.\n\n" +
                "Right-click a tile no object has used yet to put it back.\n\n" +
                "You can review all rules anytime using the Rules button in the upper-right corner."
            );
        }
    }

    // player input
    void SelectTile(TileType t)
    {
        if (levelOver) return;
        if (inventory[t] <= 0) { Log($"No {Tiles.Name(t)} tiles left."); return; }
        selected = selected == t ? (TileType?)null : t; // clicking the selected tile again deselects it
        hud.RefreshInventory(inventory, selected);
    }

    void OnCellClicked(Vector2Int p)
    {
        if (levelOver) return;
        if (busy) return;
        var cell = Cells[p.x, p.y];
        if (cell.obstacle) { Log("That space is blocked."); return; }
        if (cell.tile != null) { Log("There's already a tile there."); return; }
        if (selected == null) { Log("Select a tile from the top bar first."); return; }

        var t = selected.Value;
        history.Record(Capture()); // the only point undo saves at
        inventory[t]--;
        board.SetTile(p, t);
        TelemetryManager.Instance.AddPiece();
        Log($"Placed a {Tiles.Name(t)}.");
        RefreshAll();
        StartCoroutine(ResolveRound());
    }

    // a cell as the player sees it: its board's own coordinate, plus the board when there are several
    string Where(Vector2Int p)
    {
        int b = level.BoardOf(p);
        var local = p - level.boards[b].origin;
        return level.boards.Count > 1 ? $"({local.x},{local.y}) on board {b + 1}" : $"({local.x},{local.y})";
    }

    // clicking a door names it
    void OnDoorClicked(DoorSpec door) => Log($"Door {door.label}");

    // right-click puts a tile back in the supply, as long as no object has gone through it yet
    void OnCellRightClicked(Vector2Int p)
    {
        if (levelOver || busy) return;
        menu.HideMenu(); // pressed from the paused menu: the end window replaces it
        var cell = Cells[p.x, p.y];
        if (cell.tile == null) return;
        if (cell.locked) { Log("That tile has been used, so it stays."); return; }

        var t = cell.tile.Value;
        var snapshot = Capture(); // undo can bring it back
        snapshot.takeBack = true;
        history.Record(snapshot);
        board.TakeBackTile(p);
        inventory[t]++;
        TelemetryManager.Instance.RemovePiece();
        Log($"Put a {Tiles.Name(t)} back.");
        UpdateBlocked(); // a tile in an object's way may have been the one taken back
        UpdateStuck();
        RefreshAll();
    }

    // the tile the hover preview shows: the selected one, while the player can place
    TileType? PreviewTile() => levelOver || busy ? null : selected;

    // movement that happens after one tile placement
    IEnumerator ResolveRound()
    {
        busy = true;
        var deliveredNow = new List<MovingObject>();
        foreach (var o in objects)
        {
            if (o.completed) continue;
            yield return MoveObject(o);
            board.LayoutWaitingObjects(objects);
            if (o.completed)
            {
                deliveredNow.Add(o);
                deliveryPoints += pointsPerDelivery;
                Log($"{o.spec.name} reached its destination! +{pointsPerDelivery}");
                // +50 once for an object that went through any door on its way, on whatever board its ring is
                if (o.teleports > 0)
                {
                    teleports++; // objects that used a door
                    teleportPoints += pointsPerTeleport;
                    Log($"{o.spec.name} used a door. +{pointsPerTeleport}");
                }
                RefreshAll();
            }
        }

        if (deliveredNow.Count > 0) yield return HandleDeliveries(deliveredNow);

        busy = false;
        RefreshAll();
        CheckLevelEnd();
    }

    // moves one object as far as the path goes, through doors too
    IEnumerator MoveObject(MovingObject o)
    {
        for (int guard = 0; guard < 500; guard++)
        {
            var next = o.pos + DirUtil.Offset(o.heading);
            if (!CanCross(o.pos, o.heading, o.viaDoor))
                yield break; // a door is a wall, except for the object coming out of it

            if (!board.InGrid(next))
            {
                if (next == o.spec.dest)
                {
                    yield return board.Slide(o, board.CellPos(next));
                    o.pos = next;
                    o.completed = true;
                    board.MarkDelivered(o);
                }
                yield break; // leaving the board anywhere else is a dead end
            }

            var cell = Cells[next.x, next.y];
            var entry = DirUtil.Opposite(o.heading);
            if (cell.tile == null || !Tiles.Has(cell.tile.Value, entry))
                yield break; // the path ends here for now

            // entry edge then the center
            o.viaDoor = false;
            yield return board.Slide(o, board.EdgePos(next, entry));
            yield return board.Slide(o, board.CellPos(next));
            o.pos = next;
            o.path.Add(next);
            board.SetLocked(next, true); // used now, so it can't be put back

            // L, I and the bridge have one way out; + and T tiles let the player choose.
            // a way out onto a door teleports
            var ways = Tiles.Exits(cell.tile.Value, entry);
            if (ways.Count == 1)
            {
                o.heading = ways[0];
            }
            else
            {
                Log(ways.Any(d => level.DoorOnEdge(next, d) != null)
                    ? $"{o.spec.name} is next to a door. Click an arrow: the one on the door teleports."
                    : $"{o.spec.name} is at a junction. Click an arrow to choose its direction.");
                yield return board.AskDirection(o, ways); // sets o.heading from the arrow the player clicks
            }
            o.view.Face(o.heading);

            var door = level.DoorOnEdge(next, o.heading);
            if (door != null)
            {
                yield return board.Slide(o, board.EdgePos(next, o.heading));
                yield return Teleport(o, door);
            }
            yield return board.Slide(o, board.WaitPos(o));
        }
    }

    // through one door and out of its partner, then facing a square beside it
    IEnumerator Teleport(MovingObject o, DoorSpec door)
    {
        var partner = level.Partner(door);
        o.teleports++;
        Log($"{o.spec.name} went through door {door.label} to board {partner.board + 1}.");
        yield return board.Teleport(o, board.EdgePos(partner.cell, partner.side));

        var into = ArrivalHeadings(partner);
        if (into.Count == 1) o.heading = into[0];
        else
        {
            Log($"Pick the way {o.spec.name} faces as it comes out of door {partner.label}.");
            yield return board.AskDirectionAt(o, board.EdgePos(partner.cell, partner.side), into);
        }

        // stand on the far side of the door's edge, facing the square it'll enter next
        o.pos = o.heading == partner.side ? partner.cell : partner.Neighbor;
        o.viaDoor = true;
        o.view.Face(o.heading);
    }

    // the ways an object can face coming out of a door
    List<Dir> ArrivalHeadings(DoorSpec door)
    {
        var into = new List<Dir>();
        if (board.InGrid(door.cell)) into.Add(DirUtil.Opposite(door.side));
        if (board.InGrid(door.Neighbor)) into.Add(door.side);
        return into;
    }

    // the only way across a door's edge is to teleport, or to step off it right after coming out
    bool CanCross(Vector2Int from, Dir d, bool viaDoor) => viaDoor || level.DoorOnEdge(from, d) == null;

    int Delivered => objects.Count(o => o.completed);

    // after deliveries: each delivered object breaks one tile of its path, and delivering 2 or more with one
    // placement scores the combo and earns a + tile. this happens on every delivery, the last one too
    IEnumerator HandleDeliveries(List<MovingObject> deliveredNow)
    {
        foreach (var o in deliveredNow)
        {
            var (c, neededBy) = PickRemoval(o);
            if (c == null) continue;
            var cell = Cells[c.Value.x, c.Value.y];
            var entry = new RemovedEntry { type = cell.tile.Value, source = $"{o.spec.name}'s path" };
            board.ClearTile(c.Value);
            removed.Add(entry);
            Log(neededBy != null
                ? $"Lost the {Tiles.Name(entry.type)} that {neededBy} still needed!"
                : $"Lost a {Tiles.Name(entry.type)} from {o.spec.name}'s path.");
        }
        RefreshAll();

        if (deliveredNow.Count >= 2)
        {
            int bonus = comboPointsPerObject * deliveredNow.Count;
            comboPoints += bonus;
            comboObjects += deliveredNow.Count;
            inventory[TileType.Cross]++;
            hud.FlashSlot(TileType.Cross, hud.earnedFlashColor);
            Log($"{deliveredNow.Count} delivered together! Combo +{bonus}, and a + tile for your supply.");
            if (objects.All(o => o.completed))
            {
                finalComboNote = string.Join(" and ", deliveredNow.Select(o => o.spec.name)) + " arrived together!";
                Log(finalComboNote);
            }
            RefreshAll();
        }
        yield break;
    }

    // which tile of a delivered object's path breaks. if some of them are still needed by an object that hasn't
    // arrived (it can reach them along the tiles ahead of it), one of those breaks; otherwise any of them.
    // within that, junctions are the likeliest, then tiles near a board's middle or next to a door
    (Vector2Int?, string) PickRemoval(MovingObject o)
    {
        var candidates = o.path.Distinct().Where(c => Cells[c.x, c.y].tile != null).ToList();
        if (candidates.Count == 0) return (null, null);

        var neededBy = new Dictionary<Vector2Int, string>();
        foreach (var w in objects.Where(w => !w.completed))
            foreach (var c in ReachableTiles(w))
                if (!neededBy.ContainsKey(c)) neededBy[c] = w.spec.name;
        var group = candidates.Where(neededBy.ContainsKey).ToList();
        if (group.Count == 0) group = candidates;

        var weights = group.Select(RemovalWeight).ToList();
        float r = Random.value * weights.Sum();
        for (int i = 0; i < group.Count; i++)
        {
            r -= weights[i];
            if (r <= 0 || i == group.Count - 1)
                return (group[i], neededBy.TryGetValue(group[i], out var who) ? who : null);
        }
        return (null, null);
    }

    float RemovalWeight(Vector2Int c)
    {
        var t = Cells[c.x, c.y].tile.Value;
        if (Tiles.Category(t) == TileCategory.Cross || Tiles.Category(t) == TileCategory.Tee) return 4f;
        if (NextToDoor(c) || NearMiddle(c)) return 2f;
        return 1f;
    }

    bool NextToDoor(Vector2Int c) => DirUtil.All.Any(d => level.DoorOnEdge(c, d) != null);

    // the inner squares of a board (not on its outer edge), on boards bigger than 2x2
    bool NearMiddle(Vector2Int c)
    {
        int b = level.BoardOf(c);
        var bd = level.boards[b];
        var p = c - bd.origin;
        return bd.width > 2 && bd.height > 2 && p.x > 0 && p.y > 0 && p.x < bd.width - 1 && p.y < bd.height - 1;
    }

    // the placed tiles an object could still reach by following the tiles ahead of it - every branch of a
    // junction, through doors too - before it comes to an empty square
    HashSet<Vector2Int> ReachableTiles(MovingObject o)
    {
        var cells = new HashSet<Vector2Int>();
        var seen = new HashSet<(Vector2Int, Dir, bool)>();
        var queue = new Queue<(Vector2Int pos, Dir heading, bool viaDoor)>();
        queue.Enqueue((o.pos, o.heading, o.viaDoor));
        while (queue.Count > 0)
        {
            var (pos, heading, viaDoor) = queue.Dequeue();
            if (!CanCross(pos, heading, viaDoor)) continue;
            var next = pos + DirUtil.Offset(heading);
            if (!board.InGrid(next)) continue;
            var cell = Cells[next.x, next.y];
            var entry = DirUtil.Opposite(heading);
            if (cell.tile == null || !Tiles.Has(cell.tile.Value, entry)) continue;
            cells.Add(next);
            foreach (var exit in Tiles.Exits(cell.tile.Value, entry))
            {
                var door = level.DoorOnEdge(next, exit);
                if (door == null)
                {
                    if (seen.Add((next, exit, false))) queue.Enqueue((next, exit, false));
                    continue;
                }
                var partner = level.Partner(door);
                foreach (var h in ArrivalHeadings(partner))
                {
                    var from = h == partner.side ? partner.cell : partner.Neighbor;
                    if (seen.Add((from, h, true))) queue.Enqueue((from, h, true));
                }
            }
        }
        return cells;
    }

    // end of level and stars
    void CheckLevelEnd()
    {
        if (levelOver) return;
        UpdateBlocked();
        // the level ends on its own only when it's plainly over and a right-click take-back couldn't change that
        // (a tile no object has used can still be returned); the subtler dead ends get the stuck notice instead
        bool canTakeBack = AnyUnusedTile();
        string reason = null;
        if (objects.All(o => o.completed)) reason = "Every object was delivered!";
        else if (objects.All(o => o.completed || BlockedForGood(o))) reason = "Every remaining object is blocked.";
        else if (objects.All(o => o.completed || !CanStillReach(o, ignoreUnlocked: true))) reason = "No remaining object can reach its destination.";
        else if (inventory.Values.Sum() == 0 && !canTakeBack) reason = "You ran out of tiles.";
        else if (!AnyEmptyCell() && !canTakeBack) reason = "There are no empty cells left.";
        if (reason == null) { UpdateStuck(afterPlacement: true); return; }
        FinishLevel(reason);
    }

    // blocked, and nothing the player can do changes it: a used tile, a blocked square, a door's wall or the board's
    // edge in front. a tile no object has used could be taken back, so that block isn't for good
    bool BlockedForGood(MovingObject o)
    {
        if (!IsBlocked(o)) return false;
        if (!CanCross(o.pos, o.heading, o.viaDoor)) return true;
        var next = o.pos + DirUtil.Offset(o.heading);
        if (!board.InGrid(next)) return true;
        var cell = Cells[next.x, next.y];
        return cell.obstacle || cell.locked;
    }

    // a placed tile no object has gone through, which right-click could still return
    bool AnyUnusedTile()
    {
        foreach (var c in Cells)
            if (c != null && c.tile != null && !c.locked) return true;
        return false;
    }

    // the level is over: count the unused tiles and show the end window
    void FinishLevel(string reason)
    {
        stuck = false;
        HideStuckNotice();
        levelOver = true;
        selected = null;
        Log(reason);

        // unused tiles only score when everything was delivered (otherwise ending early, e.g. with End level,
        // would score every tile not spent on the objects left behind)
        if (objects.All(o => o.completed))
        {
            unusedTiles = inventory.Values.Sum();
            unusedPoints = unusedTiles * pointsPerUnusedTile;
            if (unusedTiles > 0) Log($"{unusedTiles} unused tiles. +{unusedPoints}");
        }
        RefreshAll();
        StartCoroutine(ShowEndMenuAfterDelay(reason));
    }

    IEnumerator ShowEndMenuAfterDelay(string reason)
    {
        yield return new WaitForSeconds(endPopupDelay);
        ShowMenu(true, reason);
    }

    // blocked = the square in front can't be entered right now
    bool IsBlocked(MovingObject o)
    {
        if (!CanCross(o.pos, o.heading, o.viaDoor)) return true;
        var next = o.pos + DirUtil.Offset(o.heading);
        if (!board.InGrid(next)) return next != o.spec.dest;
        var cell = Cells[next.x, next.y];
        if (cell.obstacle) return true;
        return cell.tile != null && !Tiles.Has(cell.tile.Value, DirUtil.Opposite(o.heading));
    }

    // could the object still reach its ring by some route, doors included - breadth-first search.
    // ignoreUnlocked: treat tiles no object has used as empty, since right-click could still take them back
    bool CanStillReach(MovingObject o, bool ignoreUnlocked = false)
    {
        var seen = new HashSet<(Vector2Int, Dir, bool)>();
        var queue = new Queue<(Vector2Int pos, Dir heading, bool viaDoor)>();
        queue.Enqueue((o.pos, o.heading, o.viaDoor));
        while (queue.Count > 0)
        {
            var (pos, heading, viaDoor) = queue.Dequeue();
            if (!CanCross(pos, heading, viaDoor)) continue;
            var next = pos + DirUtil.Offset(heading);
            if (!board.InGrid(next))
            {
                if (next == o.spec.dest) return true;
                continue; // any other edge is a dead end
            }
            var cell = Cells[next.x, next.y];
            if (cell.obstacle) continue;
            var entry = DirUtil.Opposite(heading);
            var tile = ignoreUnlocked && !cell.locked ? null : cell.tile;
            if (tile != null && !Tiles.Has(tile.Value, entry)) continue;

            // an empty cell could still hold any tile: every way except straight back
            var exits = tile == null ? DirUtil.All.Where(d => d != entry).ToList() : Tiles.Exits(tile.Value, entry);
            foreach (var exit in exits)
            {
                var door = level.DoorOnEdge(next, exit);
                if (door == null)
                {
                    if (seen.Add((next, exit, false))) queue.Enqueue((next, exit, false));
                    continue;
                }
                var partner = level.Partner(door);
                foreach (var h in ArrivalHeadings(partner))
                {
                    var from = h == partner.side ? partner.cell : partner.Neighbor;
                    if (seen.Add((from, h, true))) queue.Enqueue((from, h, true));
                }
            }
        }
        return false;
    }

    // stuck: nobody left can be delivered with the tiles there are
    bool stuck;
    float stuckSince; // when the player got stuck, or last placed a tile while stuck

    // afterPlacement: called after a tile was placed (restarts the count)
    void UpdateStuck(bool afterPlacement = false)
    {
        bool now = !levelOver && !busy && objects.Any(o => !o.completed) && !objects.Any(o => !o.completed && CanStillFinish(o));
        if (!now) { stuck = false; HideStuckNotice(); return; }
        if (!stuck || afterPlacement) stuckSince = Time.time;
        stuck = true;
        if (Time.time - stuckSince >= stuckSecondsBeforeNotice) ShowStuckNotice(); // only when the wait is 0
    }

    // the wait, while stuck and the notice isn't up yet
    void Update()
    {
        if (stuck && !levelOver && !busy && !hud.StuckNoticeShowing && Time.time - stuckSince >= stuckSecondsBeforeNotice) ShowStuckNotice();
    }

    void ShowStuckNotice()
    {
        if (hud.StuckNoticeShowing) return;
        hud.ShowStuckNotice(Undo, Restart, EndLevelNow); // the wording is the notice's own, set in the scene
        Log("No object can reach its ring with the tiles left. Undo, Restart, or End level.");
    }

    void HideStuckNotice()
    {
        hud.HideStuckNotice();
    }

    // the End level button: finish now and count what's been earned
    public void EndLevelNow()
    {
        if (levelOver || busy) return;
        menu.HideMenu(); // pressed from the paused menu: the end window replaces it
        FinishLevel("You ended the level.");
    }

    // could the object still reach its ring with the tiles there are? generous on purpose, so it never says no
    // while the level can still be won: it looks at this object alone (ignoring what the others need), adds every
    // + that could still be earned and every placed tile that could still be taken back, and ignores future breaks.
    // a depth-first search over routes, spending supply tiles on empty squares; past its step budget it says yes
    bool CanStillFinish(MovingObject o)
    {
        var left = new Dictionary<TileType, int>(inventory);
        // each group delivery earns a +, but it arrives with the delivery, so it can only help this object if the group is
        // made of OTHER objects arriving before it. count pairs among the others that could still reach their ring at all
        int othersCanArrive = objects.Count(w => w != o && !w.completed && CanStillReach(w, ignoreUnlocked: true));
        left[TileType.Cross] += othersCanArrive / 2;
        foreach (var c in Cells)
            if (c != null && c.tile != null && !c.locked) left[c.tile.Value]++; // right-click could return it

        var planned = new Dictionary<Vector2Int, TileType>(); // tiles this route has put on empty squares
        var onRoute = new HashSet<(Vector2Int, Dir)>();       // (square, side entered) already on this route, so it can't loop
        int budget = 20000;

        bool Step(Vector2Int pos, Dir heading, bool viaDoor)
        {
            if (--budget < 0) return true; // too big to check: assume it can
            if (!CanCross(pos, heading, viaDoor)) return false;
            var next = pos + DirUtil.Offset(heading);
            if (!board.InGrid(next)) return next == o.spec.dest;
            var cell = Cells[next.x, next.y];
            if (cell.obstacle) return false;
            var entry = DirUtil.Opposite(heading);
            if (!onRoute.Add((next, entry))) return false;
            try
            {
                // a used tile (or one this route already put here) has to be followed
                TileType? fixedTile = cell.tile != null && cell.locked ? cell.tile
                    : planned.TryGetValue(next, out var p) ? p : (TileType?)null;
                if (fixedTile != null)
                    return Tiles.Has(fixedTile.Value, entry) && Tiles.Exits(fixedTile.Value, entry).Any(x => Leave(next, x));
                // an unused tile can be followed, or taken back and replaced (it's already counted in `left`)
                if (cell.tile != null && Tiles.Has(cell.tile.Value, entry) && Tiles.Exits(cell.tile.Value, entry).Any(x => Leave(next, x)))
                    return true;
                // an empty square (or an unused tile's square): try each tile that's left and opens toward where the object comes from
                foreach (var t in Tiles.All)
                {
                    if (left[t] <= 0 || !Tiles.Has(t, entry)) continue;
                    left[t]--; planned[next] = t;
                    bool ok = Tiles.Exits(t, entry).Any(x => Leave(next, x));
                    left[t]++; planned.Remove(next);
                    if (ok) return true;
                }
                return false;
            }
            finally { onRoute.Remove((next, entry)); }
        }

        // leaving a square by a side: onward, or through a door and out of its partner facing either way
        bool Leave(Vector2Int cellPos, Dir exit)
        {
            var door = level.DoorOnEdge(cellPos, exit);
            if (door == null) return Step(cellPos, exit, false);
            var partner = level.Partner(door);
            foreach (var h in ArrivalHeadings(partner))
                if (Step(h == partner.side ? partner.cell : partner.Neighbor, h, true)) return true;
            return false;
        }

        return Step(o.pos, o.heading, o.viaDoor);
    }

    // crosses out blocked objects, and logs when one becomes blocked or free again
    void UpdateBlocked()
    {
        foreach (var o in objects.Where(o => !o.completed))
        {
            bool now = IsBlocked(o);
            if (now != o.blocked) Log(now ? $"{o.spec.name} is blocked." : $"{o.spec.name} is free to move again.");
            o.blocked = now;
            o.view.SetBlocked(now);
        }
    }

    bool AnyEmptyCell()
    {
        foreach (var c in Cells)
            if (c != null && !c.obstacle && c.tile == null) return true;
        return false;
    }

    bool ScoreStar => Score >= level.targetScore;

    // 1 = all but one delivered, 2 = all delivered, 3 = all delivered and the target score
    int Stars()
    {
        if (Delivered < level.Required) return 0;
        if (Delivered < objects.Count) return 1;
        return ScoreStar ? 3 : 2;
    }

    void ShowMenu(bool ended, string reason)
    {
        int stars = Stars();
        bool isLast = levelIndex >= Levels.All.Count - 1;
        string summary =
            (reason != null ? reason + "\n" : "") +
            $"Delivered {Delivered} / {objects.Count}\n" +
            ScoreBreakdown(ended) +
            (stars == 0 ? "\nEarn at least 1 star to unlock the next level." : "") +
            // a nudge toward the 3rd star, without giving the solution away
            (ended && stars == 2 && !string.IsNullOrEmpty(level.bestPlanHint) ? "\n" + level.bestPlanHint : "") +
            (isLast && stars > 0 ? "\nThat was the last level!" : "");

        menu.ShowMenu(
            level.title + (ended ? " complete" : " paused"),
            summary,
            Delivered >= level.Required,
            Delivered >= objects.Count,
            stars == 3, // the score only earns the 3rd star once everyone is delivered
            showResume: !ended,
            showNext: ended && stars >= 1 && !isLast);
    }

    // one line per source: what, how many x points, and the subtotal, then the total against the target
    string ScoreBreakdown(bool ended)
    {
        string Line(string what, string count, int points) =>
            $"{what}<pos=58%>{count}<pos=86%>{points}\n";

        bool allDelivered = objects.All(o => o.completed);
        string unused =
            allDelivered ? Line("Unused tiles", $"{unusedTiles} x {pointsPerUnusedTile}", unusedPoints)
            : ended ? "Unused tiles<pos=58%>not counted\n" // only counted when everything is delivered
            : "Unused tiles<pos=58%>counted at the end\n";

        return
            "<align=left><indent=3%>" +
            Line("Deliveries", $"{Delivered} x {pointsPerDelivery}", deliveryPoints) +
            Line("Delivered together", $"{comboObjects} x {comboPointsPerObject}", comboPoints) +
            Line("Used a door", $"{teleports} x {pointsPerTeleport}", teleportPoints) +
            unused +
            $"<b>Total<pos=86%>{Score}</b>\n" +
            $"<color=#A0A2A8>3rd star target<pos=86%>{level.targetScore}</color>" +
            "</indent></align>";
    }

    public void ShowStartScreen()
    {
        ReportLevel(); // Home from a level: a finished last level has no Next, so this is its only report
        StopAllCoroutines();
        board.ClearChoices();
        hud.SetChoiceMode(false);
        menu.ShowStart();
        menu.BuildLevelButtons(Levels.All.Count, LoadLevel);
    }

    public void StartGame() => LoadLevel(0);
    public void Restart()
    {
        ReportLevel();
        LoadLevel(levelIndex);
    }
    public void NextLevel()
    {
        if (levelIndex + 1 < Levels.All.Count)
        {
            ReportLevel();
            LoadLevel(levelIndex + 1);
        }
    }

    // the analytics row for the level being left (Next, Restart or Home), sent once per attempt
    bool levelReported = true; // nothing to report until a level is loaded
    void ReportLevel()
    {
        if (levelReported) return;
        levelReported = true;
        TelemetryManager.Instance.EndLevel(Score, Stars());
    }
    public void OpenMenu() => ShowMenu(levelOver, null);
    public void Resume() => menu.HideMenu();

    public void DismissMenuIfPaused()
    {
        if (!levelOver) menu.HideMenu();
    }

    public void ShowRules() => menu.SetRulesVisible(true);
    public void HideRules() => menu.SetRulesVisible(false);

    // HUD
    void RefreshAll()
    {
        if (selected != null && inventory[selected.Value] <= 0) selected = null;
        hud.RefreshInventory(inventory, selected);
        hud.RefreshInfo(level, Delivered, objects.Count, inventory.Values.Sum(), Score, ScoreStar);
        hud.SetUndoAvailable(!levelOver && history.CanUndo);
    }

    void Log(string msg) => hud.Log(msg);
}
