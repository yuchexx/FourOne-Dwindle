using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// the game's rules: moving, doors, deliveries and the tile each one breaks, and when the level ends on its own or
// can no longer be won (blocked objects, the stuck notice). part of GameManager
public partial class GameManager
{
    // movement that happens after one tile placement. resume: undo by step went back to this arrow question, so the
    // round picks up there (the objects before it have already moved)
    IEnumerator ResolveRound(ChoicePoint resume = null)
    {
        busy = true;
        roundDelivered = resume == null ? new List<MovingObject>() : resume.delivered.Select(i => objects[i]).ToList();
        while (resume != null && board.UndoPlaying) yield return null; // the rewind plays first, then the arrows show again
        if (resume == null) MarkRound(); // show tile breaks: what this placement breaks (a resumed round re-marks at its question)
        HintsChanged?.Invoke(); // stage 3: the tile just placed shows how it stands from the start of the move
        for (int i = resume?.obj ?? 0; i < objects.Count; i++)
        {
            var o = objects[i];
            if (o.completed) continue;
            yield return MoveObject(o, resume != null && i == resume.obj ? resume : null);
            board.LayoutWaitingObjects(objects);
            if (o.completed)
            {
                roundDelivered.Add(o);
                deliveryPoints += pointsPerDelivery;
                if (o.teleports > 0) teleportPoints += pointsPerTeleport; // once per delivered object

                // in the score history
                int earned = pointsPerDelivery + (o.teleports > 0 ? pointsPerTeleport : 0);
                hud.LogDelivered(o.spec.color, o.teleports > 0, earned, o.teleports > 0 ? $"{pointsPerDelivery} + {pointsPerTeleport} =" : null);

                RefreshAll();
            }
        }

        if (roundDelivered.Count > 0) yield return HandleDeliveries(roundDelivered);
        ClearRoundMarks(); // tints of tiles that didn't break

        redoAnswers.Clear();
        busy = false;
        RefreshAll();
        HintsChanged?.Invoke(); // the placed tile's ghost goes, or stays if it came at the wrong moment
        CheckLevelEnd();
    }

    List<MovingObject> roundDelivered = new List<MovingObject>(); // delivered so far this round
    Snapshot roundSnapshot; // the undo snapshot of the placement whose round this is: its arrow questions go in it

    // undo by step: an arrow question is about to show; saved with the placement so undo can come back to it
    ChoicePoint SaveChoice(MovingObject o, List<Dir> ways, DoorSpec door)
    {
        if (roundSnapshot == null) return null;
        var choice = new ChoicePoint
        {
            state = Capture(), obj = objects.IndexOf(o), ways = ways, door = door,
            delivered = roundDelivered.Select(x => objects.IndexOf(x)).ToArray(), rewindSteps = board.RecordedSteps,
        };
        roundSnapshot.choices.Add(choice);
        return choice;
    }

    // moves one object as far as the path goes, through doors too. resume: it starts by asking that question again
    IEnumerator MoveObject(MovingObject o, ChoicePoint resume = null)
    {
        if (resume != null)
        {
            if (resume.door == null) yield return LeaveSquare(o, resume.ways);
            else
            {
                yield return ComeOut(o, resume.door);
                yield return board.Slide(o, board.WaitPos(o));
            }
        }
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
                    AudioManager.Play(AudioManager.Sfx.Delivered, AudioManager.Note(roundDelivered.Count)); // each next one this move higher
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
            AudioManager.Play(AudioManager.Sfx.Step);

            yield return LeaveSquare(o, Tiles.Exits(cell.tile.Value, entry));
        }
    }

    // out of the square it's on: L, I and the bridge have one way out; + and T tiles let the player choose.
    // a way out onto a door teleports; then it goes to where it waits
    IEnumerator LeaveSquare(MovingObject o, List<Dir> ways)
    {
        if (ways.Count == 1)
        {
            o.heading = ways[0];
        }
        else
        {
            var choice = SaveChoice(o, ways, null); // undo by step comes back here
            MarkQuestion(o, null, ways); // show tile breaks: what these arrows could break
            AudioManager.Play(AudioManager.Sfx.ArrowsAppear);
            HintsChanged?.Invoke(); // stage 3: the junction just placed shows how it stands while the arrow is picked
            yield return AskDirectionWithRedo(o, ways, null); // sets o.heading from the arrow the player clicks
            MarkPicked(o);
            if (choice != null)
            {
                choice.direction = o.heading;
                choice.answered = true;
            }
            ReportChoice(o); // for the tutorial
            HintsChanged?.Invoke(); // a wrong way shows at once
        }
        o.view.Face(o.heading);

        var door = level.DoorOnEdge(o.pos, o.heading);
        if (door != null)
        {
            yield return board.Slide(o, board.EdgePos(o.pos, o.heading));
            yield return Teleport(o, door);
        }
        yield return board.Slide(o, board.WaitPos(o));
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
        AudioManager.Play(AudioManager.Sfx.Door);
        yield return board.Teleport(o, board.EdgePos(partner.cell, partner.side));
        yield return ComeOut(o, door);
    }

    // at the partner of the door it went into: which way it faces (asked when both sides are on a board)
    IEnumerator ComeOut(MovingObject o, DoorSpec door)
    {
        var partner = level.Partner(door);
        var into = ArrivalHeadings(partner);
        if (into.Count == 1) o.heading = into[0];
        else
        {
            var choice = SaveChoice(o, into, door); // undo by step comes back here
            MarkQuestion(o, door, into);
            AudioManager.Play(AudioManager.Sfx.ArrowsAppear);
            HintsChanged?.Invoke(); // stage 3: the board as it stands while the way out is picked
            yield return AskDirectionWithRedo(o, into, door);
            MarkPicked(o);
            if (choice != null)
            {
                choice.direction = o.heading;
                choice.answered = true;
            }
            ReportChoice(o, door.label); // for the tutorial and the hints
            HintsChanged?.Invoke(); // a wrong way shows at once
        }

        // stand on the far side of the door's edge, facing the square it'll enter next
        o.pos = o.heading == partner.side ? partner.cell : partner.Neighbor;
        o.viaDoor = true;
        o.view.Face(o.heading);

        // for the tutorial (GameManager.Tutorial.cs, onDoorExit): out of a door with one way to go (no arrows), it
        // may wait here so a step can show it, until ReleaseDoorExit
        if (into.Count == 1 && onDoorExit != null && onDoorExit(o.spec.name, door.label))
        {
            doorExitHeld = true;
            while (doorExitHeld) yield return null;
        }
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
        var together = deliveredNow.Select(x => x.spec.name).ToArray(); // for the tutorial (onRemoval)
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
            board.ClearBreakMark(c.Value); // its tint goes with it
            AudioManager.Play(AudioManager.Sfx.TileBreak);

            // for the tutorial (GameManager.Tutorial.cs, onRemoval): it may hold here to show this tile gone, before
            // the next one breaks
            if (onRemoval != null && onRemoval(arrival, together))
            {
                RefreshAll();
                removalHeld = true;
                while (removalHeld) yield return null;
            }
        }

        RefreshAll();

        if (deliveredNow.Count >= 2)
        {
            int bonus = comboPointsPerObject * deliveredNow.Count;
            comboPoints += bonus;
            inventory[TileType.Cross]++;

            if (!earnedFlashHeld) hud.FlashSlot(TileType.Cross, hud.earnedFlashColor); // held: for the tutorial (HoldEarnedFlash)
            hud.LogTogether(deliveredNow.Select(o => o.spec.color).ToArray(), bonus, $"{comboPointsPerObject}×{deliveredNow.Count} =");

            RefreshAll();
        }

        Report(new GameAction { kind = GameActionKind.Delivered, arrivals = arrivals.ToArray(), score = Score }); // for analytics
        yield break;
    }
    // which tile of a delivered object's path breaks: the same every time (BreakRule). tiles an object that hasn't
    // arrived can still reach come first, then junctions, then the one a waiting object reaches soonest
    (Vector2Int?, string) PickRemoval(MovingObject o)
    {
        TileType? TileAt(Vector2Int c) => Cells[c.x, c.y]?.tile;
        bool Blocked(Vector2Int c) => Cells[c.x, c.y] == null || Cells[c.x, c.y].obstacle;
        var waiting = objects.Where(w => !w.completed).ToList();
        var pick = BreakRule.For(level, o.path, waiting.Select(w => new BreakRule.Waiting(w.pos, w.heading, w.viaDoor, w.spec.dest, w.path)).ToList(), TileAt, Blocked, out int who);
        return (pick, who >= 0 ? waiting[who].spec.name : null);
    }
    // end of level and stars
    void CheckLevelEnd()
    {
        if (levelOver) return;
        if (levelEndHeld) return; // for the tutorial: the level is about to be reset (see HoldLevelEnd)
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
        AudioManager.Play(AudioManager.Sfx.StuckNotice);
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
