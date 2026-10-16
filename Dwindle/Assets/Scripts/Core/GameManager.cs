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
    public int pointsPerTeleport = 50;    // per door gone through, paid when the object reaches a ring on another board
    public int pointsPerUnusedTile = 75;  // per tile left in the supply, only if every object was delivered

    int levelIndex;
    LevelData level;
    Dictionary<TileType, int> inventory;
    TileType? selected;
    bool busy; // true while objects are moving or waiting for a choice
    List<MovingObject> objects;
    List<RemovedEntry> removed;
    TileType? pendingRecovery; // the card the player clicked during a recovery choice
    string finalComboNote; // set when the last objects arrive together
    bool levelOver;
    bool level1TutorialShown;

    // score, kept per source so the end window can show where it came from
    int deliveryPoints, comboPoints, teleportPoints, unusedPoints;
    int comboObjects, teleports, unusedTiles; // how many of each, for the breakdown
    int Score => deliveryPoints + comboPoints + teleportPoints + unusedPoints;

    Cell[,] Cells => board.Cells;

    void Start()
    {
        ShowStartScreen();
    }

    public void LoadLevel(int index)
    {
        StopAllCoroutines();
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
        objects = board.Build(level, OnCellClicked, OnDoorClicked);
        Log($"{level.title}: pick a tile, then click an empty cell.");
        if (level.doors.Count > 0)
            Log("Doors with the same letter are linked: a road into one comes out of the other.");
        RefreshAll();

        if (index == 0 && !level1TutorialShown)
        {
            level1TutorialShown = true;

            menu.ShowTutorial(
                "Planning Tip",
                "Delivering an object removes one tile from its path and one unused tile from your supply.\n\nPlan your delivery order carefully!"
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
                // doors only score for an object whose ring is on another board, once it gets there
                if (o.spec.destBoard != o.spec.startBoard && o.teleports > 0)
                {
                    int bonus = pointsPerTeleport * o.teleports;
                    teleports += o.teleports;
                    teleportPoints += bonus;
                    Log($"{o.spec.name} crossed boards through {o.teleports} door(s). +{bonus}");
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

    // tile loss and recovery
    IEnumerator HandleDeliveries(List<MovingObject> deliveredNow)
    {
        // the move that completes the level loses no tiles (and so gets none back)
        bool levelComplete = objects.All(o => o.completed);
        var removedNow = new List<RemovedEntry>();
        foreach (var o in deliveredNow)
        {
            if (levelComplete) break;
            // one random tile from the completed path.
            var candidates = o.path.Distinct()
                .Where(c => Cells[c.x, c.y].tile != null)
                .ToList();
            if (candidates.Count > 0)
            {
                var c = candidates[Random.Range(0, candidates.Count)];
                var entry = new RemovedEntry { type = Cells[c.x, c.y].tile.Value, source = $"{o.spec.name}'s path" };
                board.ClearTile(c);
                removed.Add(entry);
                removedNow.Add(entry);
                Log($"Lost a {Tiles.Name(entry.type)} from the path.");
            }

            // one random unused tile from the supply.
            var lost = TakeRandomFromSupply();
            if (lost != null)
            {
                var entry = new RemovedEntry { type = lost.Value, source = "unused supply" };
                removed.Add(entry);
                removedNow.Add(entry);
                Log($"Lost a {Tiles.Name(entry.type)} from the supply.");
                hud.FlashSlot(lost.Value);
            }
        }
        RefreshAll();

        // 2 or more deliveries from one placement - combo points, and the player gets one removed tile back
        if (deliveredNow.Count >= 2)
        {
            int bonus = comboPointsPerObject * deliveredNow.Count;
            comboPoints += bonus;
            comboObjects += deliveredNow.Count;
            Log($"{deliveredNow.Count} delivered in one move! Combo +{bonus}");
            RefreshAll();
            if (objects.All(o => o.completed))
            {
                finalComboNote = string.Join(" and ", deliveredNow.Select(o => o.spec.name)) + " arrived together!";
                Log(finalComboNote);
            }
            else if (removed.Count > 0) yield return RecoveryChoice();
        }
    }

    // the player picks a tile straight from the Removed panel - any tile lost so far this level
    IEnumerator RecoveryChoice()
    {
        pendingRecovery = null;
        hud.SetChoiceMode(true);
        hud.RefreshRemoved(removed, type => pendingRecovery = type);
        Log("Multiple deliveries with one tile! Pick any tile in the Removed panel to get it back.");

        while (pendingRecovery == null) yield return null; // wait for a click on a card
        yield return new WaitForSeconds(0.25f); // let the highlight register

        hud.SetChoiceMode(false);

        var picked = removed.First(e => e.type == pendingRecovery.Value);
        inventory[picked.type]++;
        removed.Remove(picked);
        Log($"Got a {Tiles.Name(picked.type)} back to your supply.");
        RefreshAll();
    }

    // picks a random unused tile, weighted by how many of each you hold.
    TileType? TakeRandomFromSupply()
    {
        int total = inventory.Values.Sum();
        if (total == 0) return null;
        int r = Random.Range(0, total);
        foreach (var t in Tiles.All)
        {
            if (r < inventory[t]) { inventory[t]--; return t; }
            r -= inventory[t];
        }
        return null;
    }

    // end of level and stars
    void CheckLevelEnd()
    {
        if (levelOver) return;
        UpdateBlocked();
        string reason = null;
        if (objects.All(o => o.completed)) reason = "Every object was delivered!";
        else if (objects.All(o => o.completed || o.blocked)) reason = "Every remaining object is blocked.";
        else if (objects.All(o => o.completed || !CanStillReach(o))) reason = "No remaining object can reach its destination.";
        else if (inventory.Values.Sum() == 0) reason = "You ran out of tiles.";
        else if (!AnyEmptyCell()) reason = "There are no empty cells left.";
        if (reason == null) return;

        levelOver = true;
        selected = null;
        Log(reason);

        // unused tiles only score when everything was delivered
        if (objects.All(o => o.completed))
        {
            // a multi-delivery that ends the level counts as getting one tile back
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

    // could the object still reach its ring by some route, doors included - breadth-first search
    bool CanStillReach(MovingObject o)
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
            if (cell.tile != null && !Tiles.Has(cell.tile.Value, entry)) continue;

            // an empty cell could still hold any tile: every way except straight back
            var exits = cell.tile == null ? DirUtil.All.Where(d => d != entry).ToList() : Tiles.Exits(cell.tile.Value, entry);
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
            (isLast && stars > 0 ? "\nThat was the last level!" : "");

        menu.ShowMenu(
            level.title + (ended ? " complete" : " paused"),
            summary,
            Delivered >= level.Required,
            Delivered >= objects.Count,
            ScoreStar,
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
            Line("Doors to other boards", $"{teleports} x {pointsPerTeleport}", teleportPoints) +
            unused +
            $"<b>Total<pos=86%>{Score}</b>\n" +
            $"<color=#A0A2A8>3rd star target<pos=86%>{level.targetScore}</color>" +
            "</indent></align>";
    }

    public void ShowStartScreen()
    {
        StopAllCoroutines();
        board.ClearChoices();
        hud.SetChoiceMode(false);
        menu.ShowStart();
        menu.BuildLevelButtons(Levels.All.Count, LoadLevel);
    }

    public void StartGame() => LoadLevel(0);
    public void Restart() => LoadLevel(levelIndex);
    public void NextLevel() { if (levelIndex + 1 < Levels.All.Count) LoadLevel(levelIndex + 1); }
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
        hud.RefreshRemoved(removed);
        hud.SetUndoAvailable(!levelOver && history.CanUndo);
    }

    void Log(string msg) => hud.Log(msg);
}
