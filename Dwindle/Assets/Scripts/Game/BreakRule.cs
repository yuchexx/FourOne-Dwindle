using System;
using System.Collections.Generic;
using UnityEngine;

// which tile a delivery breaks, the same every time (the game and the Level Maker's solver both use this):
//   1. the delivered object's path tiles still on the board; only the ones a waiting object can still reach along the
//      tiles ahead of it, if any (a tile every waiting object has already passed is safe, so building ahead protects it)
//   2. junctions (T, +) first, then tiles next to a door or in a board's middle, then the rest
//   3. ties: the one a waiting object would reach soonest (once the empty squares ahead of it are filled), then the
//      one the delivered object passed last (nearest its ring)
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

    // the tile a delivered object breaks (path: every square it went through), given the objects still waiting: where
    // each stands, the way it faces, and whether it just came out of a door. the game and the break preview both use
    // this. neededBy: the waiting object (its place in waiting) that still had the tile ahead of it, or -1
    public static Vector2Int? For(LevelData l, IList<Vector2Int> path, IList<(Vector2Int pos, Dir heading, bool viaDoor)> waiting,
        Func<Vector2Int, TileType?> tileAt, Func<Vector2Int, bool> blocked, out int neededBy,
        Func<int, bool, Dictionary<Vector2Int, int>> ahead = null) // the break preview: Ahead for waiting[i], from its cache
    {
        ahead ??= (i, throughEmpty) => Ahead(l, waiting[i].pos, waiting[i].heading, waiting[i].viaDoor, tileAt, blocked, throughEmpty);
        var stillNeeded = new Dictionary<Vector2Int, int>(); // tiles a waiting object can reach now: the fewest steps
        var soonest = new Dictionary<Vector2Int, int>();     // the same with the gaps ahead counted as filled
        var who = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < waiting.Count; i++)
        {
            foreach (var kv in ahead(i, false))
                if (!stillNeeded.TryGetValue(kv.Key, out var s) || kv.Value < s) { stillNeeded[kv.Key] = kv.Value; who[kv.Key] = i; }
            foreach (var kv in ahead(i, true))
                if (!soonest.TryGetValue(kv.Key, out var s) || kv.Value < s) soonest[kv.Key] = kv.Value;
        }
        var pick = Pick(l, path, tileAt, stillNeeded, soonest);
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
        int bestRank = int.MaxValue, bestSteps = int.MaxValue, bestPass = -1;
        foreach (var kv in lastPass)
        {
            if (anyNeeded && !stillNeeded.ContainsKey(kv.Key)) continue;
            int rank = Rank(l, kv.Key, tileAt(kv.Key).Value);
            int steps = anyNeeded ? stillNeeded[kv.Key] : soonest.TryGetValue(kv.Key, out var s) ? s : int.MaxValue;
            bool better = rank < bestRank
                || (rank == bestRank && steps < bestSteps)
                || (rank == bestRank && steps == bestSteps && kv.Value > bestPass);
            if (better) { best = kv.Key; bestRank = rank; bestSteps = steps; bestPass = kv.Value; }
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
