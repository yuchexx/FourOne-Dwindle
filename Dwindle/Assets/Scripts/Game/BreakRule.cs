using System;
using System.Collections.Generic;
using UnityEngine;

// which tile a delivery breaks, the same every time (the game and the Level Maker's solver both use this):
//   1. the delivered object's path tiles still on the board; only the ones a waiting object could still use, if any: on a
//      road from it to its ring (empty squares as any tile, no square twice, its passed squares closed except a + or bridge)
//   2. must-pass first: tiles on every such road of a waiting object (closing the square leaves no way to its ring)
//   3. then junctions (T, +), then tiles next to a door or in a board's middle, then the rest
//   4. ties: the smallest detour (how much longer than the object's shortest road), then the fewest steps to it, then
//      the one the delivered object passed last (nearest its ring)
public static class BreakRule
{
    // the tiles a waiting object could reach going forward from where it is, each with the fewest steps to it.
    // throughEmpty false: only along the tiles that are down (it stops at the first gap), as it would move now.
    // throughEmpty true: empty squares count too, as if filled with any tile (for "soonest" when it's stopped at a gap)
    public static Dictionary<Vector2Int, int> Ahead(LevelData l, Vector2Int pos, Dir heading, bool viaDoor,
        Func<Vector2Int, TileType?> tileAt, Func<Vector2Int, bool> blocked, bool throughEmpty)
    {
        var steps = new Dictionary<Vector2Int, int>();
        var seen = new HashSet<(Vector2Int, Dir, bool)>();
        var queue = new Queue<(Vector2Int pos, Dir heading, bool viaDoor, int steps)>();
        queue.Enqueue((pos, heading, viaDoor, 0));
        while (queue.Count > 0)
        {
            var (p, h, via, n) = queue.Dequeue();
            if (!via && l.DoorOnEdge(p, h) != null) continue; // a door's edge is only crossed by teleporting
            var next = p + DirUtil.Offset(h);
            if (l.BoardOf(next) < 0 || blocked(next)) continue;
            var entry = DirUtil.Opposite(h);
            var t = tileAt(next);
            IEnumerable<Dir> exits;
            if (t != null)
            {
                if (!Tiles.Has(t.Value, entry)) continue;
                if (!steps.ContainsKey(next)) steps[next] = n + 1; // first found = fewest steps (a breadth-first search)
                exits = Tiles.Exits(t.Value, entry);
            }
            else if (throughEmpty) exits = OtherSides(entry);
            else continue;
            foreach (var e in exits)
            {
                var door = l.DoorOnEdge(next, e);
                if (door == null) { if (seen.Add((next, e, false))) queue.Enqueue((next, e, false, n + 1)); continue; }
                var partner = l.Partner(door);
                foreach (var into in Into(l, partner))
                {
                    var from = into == partner.side ? partner.cell : partner.Neighbor;
                    if (seen.Add((from, into, true))) queue.Enqueue((from, into, true, n + 1));
                }
            }
        }
        return steps;
    }

    // a waiting object, for Needed: where it stands, the way it faces, just out of a door, its ring, the squares it went through
    public struct Waiting
    {
        public Vector2Int pos, ring; public Dir heading; public bool viaDoor; public IList<Vector2Int> passed;
        public Waiting(Vector2Int pos, Dir heading, bool viaDoor, Vector2Int ring, IList<Vector2Int> passed)
        { this.pos = pos; this.heading = heading; this.viaDoor = viaDoor; this.ring = ring; this.passed = passed; }
    }

    // the candidates a waiting object could still use: each on a road from it to its ring (empty squares as any tile, no
    // square twice, its own passed squares closed except a + or bridge). value: detour * 1000 + steps, where detour = how
    // much longer the shortest such road through it is than the object's shortest road, steps = squares walked to reach it.
    // its ring can't be reached at all: what it can reach now along the tiles, as before
    public static Dictionary<Vector2Int, int> Needed(LevelData l, Waiting w, ICollection<Vector2Int> candidates,
        Func<Vector2Int, TileType?> tileAt, Func<Vector2Int, bool> blocked)
    {
        var result = new Dictionary<Vector2Int, int>();
        bool Recrossable(Vector2Int c) { var t = tileAt(c); return t != null && (Tiles.Category(t.Value) == TileCategory.Cross || t.Value == TileType.Bridge); }
        var closed = new HashSet<Vector2Int>();
        if (w.passed != null) foreach (var c in w.passed) if (!Recrossable(c)) closed.Add(c);
        closed.Remove(w.pos);
        var start = (w.pos, w.heading, w.viaDoor);

        // 1. every way out from the object, shortest first (parents kept, to read each road back)
        var parent = new Dictionary<(Vector2Int, Dir, bool), ((Vector2Int, Dir, bool) from, Vector2Int cell)>();
        var seen = new HashSet<(Vector2Int, Dir, bool)> { start };
        var dist = new Dictionary<(Vector2Int, Dir, bool), int> { [start] = 0 };
        int shortest = int.MaxValue;
        var queue = new Queue<(Vector2Int, Dir, bool)>(); queue.Enqueue(start);
        var arrivals = new List<(Vector2Int cell, (Vector2Int, Dir, bool) from)>(); // first time each candidate is entered from each side
        var arrivedFrom = new HashSet<(Vector2Int, Dir)>();
        bool ringReachable = false;
        while (queue.Count > 0)
        {
            var s = queue.Dequeue();
            var (next, entry, ok) = Next(l, s, w.ring, blocked);
            if (next == w.ring && ok) { ringReachable = true; shortest = Math.Min(shortest, dist[s] + 1); continue; }
            if (!ok || closed.Contains(next)) continue;
            var t = tileAt(next);
            if (t != null && !Tiles.Has(t.Value, entry)) continue;
            if (t != null && candidates.Contains(next) && arrivedFrom.Add((next, entry))) arrivals.Add((next, s));
            foreach (var to in Outs(l, next, entry, t))
                if (seen.Add(to)) { parent[to] = (s, next); dist[to] = dist[s] + 1; queue.Enqueue(to); }
        }
        if (!ringReachable)
        {
            foreach (var kv in Ahead(l, w.pos, w.heading, w.viaDoor, tileAt, blocked, false))
                if (candidates.Contains(kv.Key)) result[kv.Key] = Tier + kv.Value;
            return result;
        }

        // 2. for each arrival: the road to it, then is the ring still reachable without using those squares again
        foreach (var (cell, from) in arrivals)
        {
            var used = new HashSet<Vector2Int>();
            for (var s = from; !s.Equals(start); s = parent[s].from) used.Add(parent[s].cell);
            used.Add(w.pos);
            int steps = dist[from] + 1; // squares walked to reach it
            var (_, entry, _) = Next(l, from, w.ring, blocked);
            var t = tileAt(cell).Value;
            var seen2 = new HashSet<(Vector2Int, Dir, bool)>();
            var dist2 = new Dictionary<(Vector2Int, Dir, bool), int>();
            var q2 = new Queue<(Vector2Int, Dir, bool)>();
            foreach (var to in Outs(l, cell, entry, t)) if (seen2.Add(to)) { dist2[to] = 0; q2.Enqueue(to); }
            used.Add(cell);
            int rest = -1; // squares from this one to the ring
            while (q2.Count > 0)
            {
                var s = q2.Dequeue();
                var (next, e2, ok) = Next(l, s, w.ring, blocked);
                if (next == w.ring && ok) { rest = dist2[s] + 1; break; }
                if (!ok || closed.Contains(next)) continue;
                if (used.Contains(next) && !Recrossable(next)) continue;
                var t2 = tileAt(next);
                if (t2 != null && !Tiles.Has(t2.Value, e2)) continue;
                foreach (var to in Outs(l, next, e2, t2)) if (seen2.Add(to)) { dist2[to] = dist2[s] + 1; q2.Enqueue(to); }
            }
            if (rest < 0) continue;
            int value = Tier + Math.Max(0, steps + rest - shortest) * 1000 + steps;
            if (!result.TryGetValue(cell, out var have) || value < have) result[cell] = value;
        }
        // must-pass: with the square closed, the object can't reach its ring at all
        foreach (var cell in new List<Vector2Int>(result.Keys))
            if (!ReachesRing(l, w, closed, cell, tileAt, blocked)) result[cell] -= Tier;
        return result;
    }

    // Needed's values: must-pass tiles below Tier, the other needed ones from Tier up (Pick reads the tier first)
    public const int Tier = 1000000;

    // whether the object can still reach its ring with `shut` closed (empty squares as any tile, its passed squares closed)
    static bool ReachesRing(LevelData l, Waiting w, HashSet<Vector2Int> closed, Vector2Int shut, Func<Vector2Int, TileType?> TileNow, Func<Vector2Int, bool> blocked)
    {
        var start = (w.pos, w.heading, w.viaDoor);
        var seen = new HashSet<(Vector2Int, Dir, bool)> { start };
        var queue = new Queue<(Vector2Int, Dir, bool)>(); queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var s = queue.Dequeue();
            var (next, entry, ok) = Next(l, s, w.ring, blocked);
            if (next == w.ring && ok) return true;
            if (!ok || next == shut || closed.Contains(next)) continue;
            var t = TileNow(next);
            if (t != null && !Tiles.Has(t.Value, entry)) continue;
            foreach (var to in Outs(l, next, entry, t)) if (seen.Add(to)) queue.Enqueue(to);
        }
        return false;
    }

    // the square a state steps into and the side it enters by; ok false: off the board, blocked, or across a door edge
    // without teleporting. the ring counts as ok
    static (Vector2Int next, Dir entry, bool ok) Next(LevelData l, (Vector2Int p, Dir h, bool via) s, Vector2Int ring, Func<Vector2Int, bool> blocked)
    {
        var next = s.p + DirUtil.Offset(s.h);
        var entry = DirUtil.Opposite(s.h);
        if (!s.via && l.DoorOnEdge(s.p, s.h) != null) return (next, entry, false);
        if (next == ring) return (next, entry, true);
        if (l.BoardOf(next) < 0 || blocked(next)) return (next, entry, false);
        return (next, entry, true);
    }

    // the states leaving a square entered from `entry`: its tile's other openings (any side for an empty square), through doors
    static IEnumerable<(Vector2Int, Dir, bool)> Outs(LevelData l, Vector2Int cell, Dir entry, TileType? t)
    {
        var exits = t != null ? Tiles.Exits(t.Value, entry) : OtherSides(entry);
        foreach (var e in exits)
        {
            var door = l.DoorOnEdge(cell, e);
            if (door == null) { yield return (cell, e, false); continue; }
            var partner = l.Partner(door);
            foreach (var into in Into(l, partner))
                yield return (into == partner.side ? partner.cell : partner.Neighbor, into, true);
        }
    }

    static IEnumerable<Dir> OtherSides(Dir entry)
    {
        foreach (var d in DirUtil.All) if (d != entry) yield return d;
    }

    // the ways an object can come out of a door into a board
    static List<Dir> Into(LevelData l, DoorSpec door)
    {
        var r = new List<Dir>();
        if (l.BoardOf(door.cell) >= 0) r.Add(DirUtil.Opposite(door.side));
        if (l.BoardOf(door.Neighbor) >= 0) r.Add(door.side);
        return r;
    }

    // the tile a delivered object breaks (path: every square it went through), given the objects still waiting. the game
    // and the break preview both use this. neededBy: the waiting object (its place in waiting) that still needed the tile, or -1
    public static Vector2Int? For(LevelData l, IList<Vector2Int> path, IList<Waiting> waiting,
        Func<Vector2Int, TileType?> tileAt, Func<Vector2Int, bool> blocked, out int neededBy,
        Func<int, ICollection<Vector2Int>, Dictionary<Vector2Int, int>> needed = null) // the break preview: Needed, from its cache
    {
        needed ??= (i, cands) => Needed(l, waiting[i], cands, tileAt, blocked);
        var candidates = new HashSet<Vector2Int>();
        foreach (var c in path) if (tileAt(c) != null) candidates.Add(c);
        var stillNeeded = new Dictionary<Vector2Int, int>(); // candidates a waiting object could still use: the fewest steps
        var who = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < waiting.Count; i++)
            foreach (var kv in needed(i, candidates))
                if (!stillNeeded.TryGetValue(kv.Key, out var s) || kv.Value < s) { stillNeeded[kv.Key] = kv.Value; who[kv.Key] = i; }
        var pick = Pick(l, path, tileAt, stillNeeded, new Dictionary<Vector2Int, int>());
        neededBy = pick != null && who.TryGetValue(pick.Value, out var n) ? n : -1;
        return pick;
    }

    // stillNeeded: tiles a waiting object can reach now along the tiles (Ahead, throughEmpty false), fewest steps.
    // soonest: the same with empty squares counted as filled (Ahead, throughEmpty true), for the tie-break
    public static Vector2Int? Pick(LevelData l, IList<Vector2Int> path, Func<Vector2Int, TileType?> tileAt,
        IDictionary<Vector2Int, int> stillNeeded, IDictionary<Vector2Int, int> soonest)
    {
        // each tile still down, with the last time the object passed it
        var lastPass = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < path.Count; i++)
            if (tileAt(path[i]) != null) lastPass[path[i]] = i;
        if (lastPass.Count == 0) return null;

        bool anyNeeded = false;
        foreach (var c in lastPass.Keys) if (stillNeeded.ContainsKey(c)) { anyNeeded = true; break; }

        Vector2Int? best = null;
        int bestTier = int.MaxValue, bestRank = int.MaxValue, bestSteps = int.MaxValue, bestPass = -1;
        foreach (var kv in lastPass)
        {
            if (anyNeeded && !stillNeeded.ContainsKey(kv.Key)) continue;
            int rank = Rank(l, kv.Key, tileAt(kv.Key).Value);
            int steps = anyNeeded ? stillNeeded[kv.Key] : soonest.TryGetValue(kv.Key, out var s) ? s : int.MaxValue;
            int tier = anyNeeded ? steps / Tier : 0; // must-pass first (Needed)
            if (anyNeeded) steps %= Tier;
            bool better = tier < bestTier || tier == bestTier && (rank < bestRank
                || (rank == bestRank && steps < bestSteps)
                || (rank == bestRank && steps == bestSteps && kv.Value > bestPass));
            if (better) { best = kv.Key; bestTier = tier; bestRank = rank; bestSteps = steps; bestPass = kv.Value; }
        }
        return best;
    }

    // 0 = junction, 1 = next to a door or in a board's middle, 2 = the rest
    public static int Rank(LevelData l, Vector2Int c, TileType t)
    {
        var cat = Tiles.Category(t);
        if (cat == TileCategory.Cross || cat == TileCategory.Tee) return 0;
        if (NextToDoor(l, c) || NearMiddle(l, c)) return 1;
        return 2;
    }

    static bool NextToDoor(LevelData l, Vector2Int c)
    {
        foreach (var d in DirUtil.All) if (l.DoorOnEdge(c, d) != null) return true;
        return false;
    }

    // the inner squares of a board (not on its outer edge), on boards bigger than 2x2
    static bool NearMiddle(LevelData l, Vector2Int c)
    {
        int b = l.BoardOf(c);
        if (b < 0) return false;
        var bd = l.boards[b];
        var p = c - bd.origin;
        return bd.width > 2 && bd.height > 2 && p.x > 0 && p.y > 0 && p.x < bd.width - 1 && p.y < bd.height - 1;
    }
}
