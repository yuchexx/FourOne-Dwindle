using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// the game's rules: moving, doors, deliveries and the tile each one breaks, and when the level ends on its own or
// can no longer be won (blocked objects, the stuck notice). part of GameManager
public partial class GameManager
{
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
                if (o.teleports > 0) teleportPoints += pointsPerTeleport; // once per delivered object

                // in the score history
                int earned = pointsPerDelivery + (o.teleports > 0 ? pointsPerTeleport : 0);
                hud.LogDelivered(o.spec.color, o.teleports > 0, earned, o.teleports > 0 ? $"{pointsPerDelivery} + {pointsPerTeleport} =" : null);

                RefreshAll();
            }
        }

        if (deliveredNow.Count > 0) yield return HandleDeliveries(deliveredNow);

        busy = false;
        RefreshAll();
        HintsChanged?.Invoke(); // the placed tile's ghost goes, or stays if it came at the wrong moment
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
                    yield return board.Slide(o, board.OutsidePos(next)); // the ring (see BoardView.endDistance)
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
                yield return board.AskDirection(o, ways); // sets o.heading from the arrow the player clicks
                ReportChoice(o); // for the tutorial
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
        o.doorsUsed.Add(new DoorPass
        {
            door = door.label,
            inBoard = door.board, inSquare = door.cell - level.boards[door.board].origin, inSide = door.side,
            outBoard = partner.board, outSquare = partner.cell - level.boards[partner.board].origin, outSide = partner.side,
        });
        yield return board.Teleport(o, board.EdgePos(partner.cell, partner.side));

        var into = ArrivalHeadings(partner);
        if (into.Count == 1) o.heading = into[0];
        else
        {
            yield return board.AskDirectionAt(o, board.EdgePos(partner.cell, partner.side), into);
            ReportChoice(o, door.label); // for the tutorial and the hints
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
        RecordDelivery(deliveredNow); // for the hints, before any tile breaks
        var arrivals = new List<Arrival>(); // for analytics (the Delivered report)
        foreach (var o in deliveredNow)
        {
            var (c, neededBy) = PickRemoval(o);
            var arrival = new Arrival { objectName = o.spec.name, doors = o.teleports, doorsUsed = o.doorsUsed.ToArray() };
            if (c == null) { arrivals.Add(arrival); continue; }

            var cell = Cells[c.Value.x, c.Value.y];
            int b = level.BoardOf(c.Value);
            arrival.broke = true; arrival.board = b; arrival.square = c.Value - level.boards[b].origin;
            arrival.brokenTile = cell.tile.Value; arrival.neededBy = neededBy;
            arrivals.Add(arrival);

            board.ClearTile(c.Value);
        }

        RefreshAll();

        if (deliveredNow.Count >= 2)
        {
            int bonus = comboPointsPerObject * deliveredNow.Count;
            comboPoints += bonus;
            inventory[TileType.Cross]++;

            hud.FlashSlot(TileType.Cross, hud.earnedFlashColor);
            hud.LogTogether(deliveredNow.Select(o => o.spec.color).ToArray(), bonus, $"{comboPointsPerObject}×{deliveredNow.Count} =");

            RefreshAll();
        }

        Report(new GameAction { kind = GameActionKind.Delivered, arrivals = arrivals.ToArray(), score = Score }); // for analytics
        yield break;
    }
    // for the tutorial (Tutorials.cs), so a scripted lesson can't break the wrong tile: the next delivery whose path
    // has (x, y) on the given board breaks that tile instead of a random one. one time only. it isn't part of undo's
    // snapshots, so undoing that delivery doesn't bring it back: a redone delivery breaks a random tile as usual
    public void ForceNextRemoval(int board, int x, int y) => forcedRemoval = level.At(board, x, y);

    // which tile of a delivered object's path breaks. if some of them are still needed by an object that hasn't
    // arrived (it can reach them along the tiles ahead of it), one of those breaks; otherwise any of them.
    // within that, junctions are the likeliest, then tiles near a board's middle or next to a door
    (Vector2Int?, string) PickRemoval(MovingObject o)
    {
        var candidates = o.path.Distinct().Where(c => Cells[c.x, c.y].tile != null).ToList();
        if (candidates.Count == 0) return (null, null);

        // for the tutorial: a forced cell on this path breaks instead of a random one (see ForceNextRemoval)
        if (forcedRemoval is Vector2Int forced && candidates.Contains(forced))
        {
            forcedRemoval = null;
            return (forced, null);
        }

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
    bool stuckClose;  // some object is only a few squares short of its ring
    float stuckSince; // when the player got stuck, or last placed a tile while stuck

    float StuckWait => stuckClose ? stuckSecondsBeforeNotice : noticeWhenFar ? farSecondsBeforeNotice : float.PositiveInfinity;
    int CloseGap => level.boards.Max(b => b.width * b.height) >= largeBoardSquares ? closeGapLargeBoards : closeGapSmallBoards;

    // afterPlacement: called after a tile was placed (restarts the count)
    void UpdateStuck(bool afterPlacement = false)
    {
        bool now = !levelOver && !busy && objects.Any(o => !o.completed) && !AnyoneCanFinish();
        if (!now) { stuck = false; HideStuckNotice(); return; }
        if (!stuck || afterPlacement) stuckSince = Time.time;
        stuck = true;
        stuckClose = objects.Any(o => !o.completed && GapToRing(o) <= CloseGap);
        if (Time.time - stuckSince >= StuckWait) ShowStuckNotice(); // only when the wait is 0
    }
    // the fewest squares still to fill to connect the object to its ring
    int GapToRing(MovingObject o)
    {
        var best = new Dictionary<(Vector2Int, Dir, bool), int>();
        var queue = new LinkedList<((Vector2Int pos, Dir heading, bool viaDoor) state, int gap)>(); // cheapest first
        void Push((Vector2Int, Dir, bool) state, int gap, bool filled)
        {
            if (best.TryGetValue(state, out var known) && known <= gap) return;
            best[state] = gap;
            if (filled) queue.AddLast((state, gap)); else queue.AddFirst((state, gap));
        }
        Push((o.pos, o.heading, o.viaDoor), 0, false);
        while (queue.Count > 0)
        {
            var ((pos, heading, viaDoor), gap) = queue.First.Value;
            queue.RemoveFirst();
            if (best[(pos, heading, viaDoor)] < gap) continue; // a cheaper way here was found since
            if (!CanCross(pos, heading, viaDoor)) continue;
            var next = pos + DirUtil.Offset(heading);
            if (!board.InGrid(next))
            {
                if (next == o.spec.dest) return gap;
                continue;
            }
            var cell = Cells[next.x, next.y];
            if (cell.obstacle) continue;
            var entry = DirUtil.Opposite(heading);

            // follow the tile that's there for free; fill the square (or swap an unused tile) for 1
            var ways = new List<(Dir exit, bool filled)>();
            if (cell.tile != null && Tiles.Has(cell.tile.Value, entry))
                ways.AddRange(Tiles.Exits(cell.tile.Value, entry).Select(x => (x, false)));
            if (cell.tile == null || !cell.locked)
                ways.AddRange(DirUtil.All.Where(d => d != entry).Select(x => (x, true)));

            foreach (var (exit, filled) in ways)
            {
                int cost = gap + (filled ? 1 : 0);
                var door = level.DoorOnEdge(next, exit);
                if (door == null) { Push((next, exit, false), cost, filled); continue; }
                var partner = level.Partner(door);
                foreach (var h in ArrivalHeadings(partner))
                    Push((h == partner.side ? partner.cell : partner.Neighbor, h, true), cost, filled);
            }
        }
        return int.MaxValue;
    }

    void ShowStuckNotice()
    {
        if (hud.StuckNoticeShowing) return;
        hud.ShowStuckNotice(Undo, Restart, EndLevelNow); // the wording is the notice's own, set in the scene
    }

    void HideStuckNotice()
    {
        hud.HideStuckNotice();
    }
    // can anyone still be delivered?
    bool AnyoneCanFinish()
    {
        var hopeful = objects.Where(o => !o.completed && CanStillReach(o, ignoreUnlocked: true)).ToList();
        bool dropped = true;
        while (dropped && hopeful.Count > 0)
        {
            int before = hopeful.Count;
            hopeful = hopeful.Where(o => CanStillFinish(o, (before - 1) / 2)).ToList(); // pairs among the other hopefuls
            dropped = hopeful.Count < before;
        }
        return hopeful.Count > 0;
    }

    // could the object still reach its ring with the tiles there are? generous on purpose, so it never says no while the level can still be won
    bool CanStillFinish(MovingObject o, int plusFromOthers)
    {
        var left = new Dictionary<TileType, int>(inventory);
        left[TileType.Cross] += plusFromOthers;
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
                // an unused tile can be followed where it is, or taken back and replaced. it's counted in `left` as one
                // that could come back, so following it in place uses that copy up: one tile can't be in both places
                if (cell.tile != null && left[cell.tile.Value] > 0 && Tiles.Has(cell.tile.Value, entry))
                {
                    var t = cell.tile.Value;
                    left[t]--; planned[next] = t;
                    bool ok = Tiles.Exits(t, entry).Any(x => Leave(next, x));
                    left[t]++; planned.Remove(next);
                    if (ok) return true;
                }
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

    // crosses out blocked objects, and records it for undo
    void UpdateBlocked()
    {
        foreach (var o in objects.Where(o => !o.completed))
        {
            bool now = IsBlocked(o);
            if (now != o.blocked)
            {
                board.RecordBlocked(o, o.blocked); // for undo's rewind
            }
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
}
