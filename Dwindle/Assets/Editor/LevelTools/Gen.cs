using System; using System.Collections.Generic; using System.Linq; using UnityEngine;

// the layout the Level Maker asks for (LevelMaker.ToSpec)
class Spec
{
    public (int, int)[] boards; public int objects, doorPairs, obstacles, startCross, startBridge, slack = 2;
    public bool crossBoard;  // door levels: most rings on another board
    public bool noTees;      // no T tiles in the supply, so junctions have to be + tiles
    public int earnedNeeded; // take this many tiles placed after the first multi-delivery out of the supply, so the earned + has to fill them
    public int minRoute = 2;  // every object needs at least this many tiles, so none of them is filler
    public bool requireBridge; // the best plan uses the bridge, and the level is worse or unsolvable without it
    public bool spread;        // starts at least 3 squares apart, so the objects don't obviously gather
    public bool cluster;       // starts close together and rings close together, so meeting up looks natural
    public int insideEnds = -1; // exactly this many door ends sit inside a board, the rest on a board's outer edge (-1 = a random mix)
}

class Props
{
    public Plan best; public int score, tiles, unsafeRaw; public bool earnedCrossUsed, detour, trap;
    public List<int> groups; public bool replayOk; public bool buildsAhead; public bool pairTrap;
    public override string ToString() =>
        $"pairTrap {pairTrap} " + $"buildsAhead {buildsAhead} " + $"score {score} tiles {tiles} groups [{string.Join(",", groups)}] earned+ {earnedCrossUsed} detour {detour} trap {trap} (unsafe raw {unsafeRaw}) replay {replayOk}";
}

static class Gen
{
    public static Action<string> Trace = null; // timing notes for the command line

    // spots just outside a board's edge, facing in
    static List<(Vector2Int pos, Dir inward, int board)> Ring(LevelData l, int b)
    {
        var res = new List<(Vector2Int, Dir, int)>(); var bd = l.boards[b];
        for (int i = 0; i < bd.width; i++) { res.Add((bd.origin + new Vector2Int(i, -1), Dir.N, b)); res.Add((bd.origin + new Vector2Int(i, bd.height), Dir.S, b)); }
        for (int j = 0; j < bd.height; j++) { res.Add((bd.origin + new Vector2Int(-1, j), Dir.E, b)); res.Add((bd.origin + new Vector2Int(bd.width, j), Dir.W, b)); }
        return res;
    }

    public static LevelData Make(Spec sp, System.Random rnd, string title)
    {
        var l = LB.New(title, sp.boards);
        int nb = l.boards.Count;
        // doors: one end inside a board (between two squares), the other on a board's outer edge, on two different boards
        var usedEdges = new HashSet<(Vector2Int, Dir)>();
        var insideAt = Enumerable.Range(0, 2 * sp.doorPairs).Select(i => i < sp.insideEnds).OrderBy(_ => rnd.Next()).ToList();
        for (int d = 0; d < sp.doorPairs; d++)
        {
            string label = ((char)('A' + d)).ToString();
            int b1 = rnd.Next(nb), b2 = (b1 + 1 + rnd.Next(nb - 1)) % nb;
            if (nb == 1) b2 = b1;
            for (int end = 0; end < 2; end++)
            {
                int b = end == 0 ? b1 : b2; var bd = l.boards[b];
                bool inside = sp.insideEnds >= 0 ? insideAt[2 * d + end] : end == 0 ? rnd.Next(2) == 0 : rnd.Next(3) == 0;
                for (int tries = 0; tries < 50; tries++)
                {
                    int x = rnd.Next(bd.width), y = rnd.Next(bd.height); var side = DirUtil.All[rnd.Next(4)];
                    var cell = new Vector2Int(x, y); var nbr = cell + DirUtil.Offset(side);
                    bool nbrInside = nbr.x >= 0 && nbr.y >= 0 && nbr.x < bd.width && nbr.y < bd.height;
                    if (inside != nbrInside) continue;
                    var gc = bd.origin + cell;
                    if (usedEdges.Contains((gc, side)) || usedEdges.Contains((gc + DirUtil.Offset(side), DirUtil.Opposite(side)))) continue;
                    usedEdges.Add((gc, side));
                    LB.Door(l, label, b, x, y, side); break;
                }
            }
            if (l.doors.Count(x => x.label == label) != 2) { l.doors.RemoveAll(x => x.label == label); }
        }
        // obstacles
        for (int o = 0; o < sp.obstacles; o++)
        {
            int b = rnd.Next(nb); var bd = l.boards[b];
            var c = l.At(b, rnd.Next(bd.width), rnd.Next(bd.height));
            if (!l.obstacles.Contains(c)) l.obstacles.Add(c);
        }
        // objects: a start and a ring outside the boards, never right outside a door
        var spots = Enumerable.Range(0, nb).SelectMany(b => Ring(l, b)).Where(r => l.DoorOnEdge(r.pos, r.inward) == null && !l.obstacles.Contains(r.pos + DirUtil.Offset(r.inward))).ToList();
        var taken = new HashSet<Vector2Int>();
        for (int i = 0; i < sp.objects; i++)
        {
            var free = spots.Where(s => !taken.Contains(s.pos)).ToList();
            if (sp.cluster && l.objects.Count > 0) free = free.Where(f => Mathf.Abs(l.objects[0].start.x - f.pos.x) + Mathf.Abs(l.objects[0].start.y - f.pos.y) <= 3).ToList();
            if (sp.spread) free = free.Where(f => l.objects.All(o => Mathf.Abs(o.start.x - f.pos.x) + Mathf.Abs(o.start.y - f.pos.y) >= 3)).ToList();
            if (free.Count == 0) return null;
            var s0 = free[rnd.Next(free.Count)]; taken.Add(s0.pos);
            var dests = spots.Where(s => !taken.Contains(s.pos)).ToList();
            if (nb > 1 && sp.crossBoard && rnd.Next(4) != 0) dests = dests.Where(s => s.board != s0.board).ToList();
            else if (nb > 1) dests = dests.Where(s => s.board == s0.board || rnd.Next(2) == 0).ToList();
            // a ring not on the same edge as its start
            dests = dests.Where(s => s.inward != s0.inward || s.board != s0.board).ToList();
            if (dests.Count == 0) return null;
            if (sp.cluster && l.objects.Count > 0) dests = dests.Where(d => Mathf.Abs(l.objects[0].dest.x - d.pos.x) + Mathf.Abs(l.objects[0].dest.y - d.pos.y) <= 3).ToList();
            if (dests.Count == 0) return null;
            var d0 = dests[rnd.Next(dests.Count)]; taken.Add(d0.pos);
            var (nm, col) = LB.Palette[i];
            var sl = s0.pos - l.boards[s0.board].origin; var dl = d0.pos - l.boards[d0.board].origin;
            LB.Obj(l, nm, col, s0.board, sl.x, sl.y, s0.inward, d0.board, dl.x, dl.y);
        }
        return l;
    }

    static Dictionary<TileType, int> Generous(Spec sp) =>
        Tiles.All.ToDictionary(t => t, t => t == TileType.Cross ? sp.startCross : t == TileType.Bridge ? sp.startBridge : (sp.noTees && Tiles.Category(t) == TileCategory.Tee) ? 0 : 3);

    // slack: how much longer than the shortest route a route may be (shorter is much faster, for rough sizing only)
    public static Props Analyze(LevelData l, bool full = true, int slack = 4, bool quick = false)
    {
        var s = new Solver2(l, slack) { quick = quick }; var p = s.Best();
        if (p == null) return null;
        var pr = new Props { best = p, score = p.score, tiles = p.cells, groups = p.events.Select(g => g.Count).ToList() };
        pr.earnedCrossUsed = Tricks.EarnedPlus.uses(l, p);
        pr.detour = Tricks.Detour.uses(l, p);
        pr.replayOk = new Replay2(l).Run(p).ok;
        pr.buildsAhead = p.BuildsAhead;
        if (full)
        {
            // the best plan on paper, ignoring that its deliveries break tiles others still need
            var rp = new Solver2(l) { requireSafe = false, penalize = false }.Best();
            pr.unsafeRaw = rp?.score ?? 0;
            pr.trap = rp != null && rp.hits > 0 && rp.score > p.score;
            // on paper the best plan splits into more multi-deliveries, but the safe plan has to group them differently
            pr.pairTrap = pr.trap && rp.events.Count(e => e.Count >= 2) > p.events.Count(e => e.Count >= 2);
            // and it stays worse once the broken tiles are re-placed, so falling for it really costs
            if (pr.pairTrap) { var rep = new Solver2(l) { requireSafe = false }.Best(); pr.pairTrap = rep == null || rep.score <= p.score; }
        }
        return pr;
    }

    // a random layout, its supply set to its best plan's tiles plus some spares, then re-checked
    public static (LevelData, Props) Candidate(Spec sp, System.Random rnd, string title)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var l = Make(sp, rnd, title);
        if (l == null) return (null, null);
        l.inventory = Generous(sp);
        var g = Analyze(l, false, slack: 2, quick: true); // only sizes the supply: the generous supply makes long routes very slow, and the real check below uses them
        Trace?.Invoke($"  sized the supply in {clock.ElapsedMilliseconds} ms ({(g == null ? "no plan" : "ok")})");
        if (g != null && sp.requireBridge && !g.best.tiles.Values.Contains(TileType.Bridge)) return (null, null); // even the rough plan skips the bridge: not worth the full check
        if (g == null) return (null, null);
        { var probe = new Solver2(l); if (l.objects.Any(o => probe.Fewest(o) < sp.minRoute)) return (null, null); }
        // supply = what the best plan places (its earned + tiles aside) plus `slack` spare plain tiles
        var inv = Tiles.All.ToDictionary(t => t, t => 0);
        foreach (var t in g.best.tiles.Values) inv[t]++;
        inv[TileType.Cross] = Math.Min(inv[TileType.Cross], sp.startCross);
        var taken = new HashSet<TileType>();
        if (sp.earnedNeeded > 0)
        {
            // tiles placed after the first multi-delivery that doesn't end the level: the earned + can stand in for them
            var rp = new Replay2(l).Run(g.best);
            int first = rp.stepEvents.FindIndex(e => e.Any(x => x.Contains("arrive together")));
            if (first < 0 || first == rp.placements.Count - 1) return (null, null);
            var later = rp.placements.Skip(first + 1).Where(x => x.t != TileType.Cross && x.t != TileType.Bridge).OrderBy(_ => rnd.Next()).Take(sp.earnedNeeded).ToList();
            if (later.Count < sp.earnedNeeded) return (null, null);
            foreach (var (_, t) in later) { inv[t]--; taken.Add(t); }
        }
        var plain = new[] { TileType.StraightH, TileType.StraightV, TileType.CornerNE, TileType.CornerES, TileType.CornerSW, TileType.CornerWN }.Where(t => !taken.Contains(t)).ToArray();
        for (int i = 0; i < sp.slack; i++) inv[plain[rnd.Next(plain.Length)]]++;
        l.inventory = inv;
        var pr = Analyze(l);
        Trace?.Invoke($"  analyzed in {clock.ElapsedMilliseconds} ms ({(pr == null ? "no plan" : pr.ToString())})");
        // the best plan uses the bridge, and the level is worse (or can't be finished) without it
        if (pr != null && sp.requireBridge && !(Tricks.Bridge.uses(l, pr.best) && Tricks.NeededFor(l, Tricks.Bridge, pr.score))) return (null, null);
        return pr == null ? (null, null) : (l, pr);
    }

    // an object comes out of a door between two squares, so the facing choice matters
    public static bool Facing(LevelData l, Plan p) => p.routes.Any(r => r.steps.Any(st => l.DoorOnEdge(st.cell, st.exit) is DoorSpec d && l.BoardOf(l.Partner(d).cell) >= 0 && l.BoardOf(l.Partner(d).Neighbor) >= 0));

    // even one extra tile of any type wouldn't let everyone arrive together (or only for clearly less)
    public static bool NoNearMiss(LevelData l, int best) =>
        Tiles.All.All(t => !(new Solver2(l, slack: 6) { oneEvent = true, supply = Solver2.Supply(l, t, add: 1) }.Best() is Plan one && one.score > best - 150));

    // one delivery needs two different later objects to be built ahead past its tiles (fixing one isn't enough)
    public static bool Abcd(LevelData l, Plan p)
    {
        for (int k = 0; k < p.events.Count - 1; k++)
        {
            var later = p.events.Skip(k + 1).SelectMany(e => e);
            if (later.Count(j => p.pre[k].Any(c => p.routes[j].cells.Contains(c))) >= 2) return true;
        }
        return false;
    }

    public static bool AllBoardsUsed(LevelData l, Plan p) => Enumerable.Range(0, l.boards.Count).All(b => p.tiles.Keys.Any(c => l.BoardOf(c) == b));
}

static class EarnedUse
{
    // what each earned + stands in for: "I" (straight), "L" (corner), "T" (a junction of three sides), "+" (all four)
    public static List<string> Roles(LevelData l, Plan p)
    {
        var rp = new Replay2(l).Run(p);
        int startCross = l.inventory.GetValueOrDefault(TileType.Cross), seen = 0;
        var roles = new List<string>();
        foreach (var (c, t) in rp.placements)
        {
            if (t != TileType.Cross || ++seen <= startCross) continue;
            var through = Enumerable.Range(0, l.objects.Count).SelectMany(j => p.routes[j].steps.Where(s => s.cell == c)).ToList();
            if (through.Count == 1) roles.Add(through[0].exit == DirUtil.Opposite(through[0].entry) ? "I" : "L");
            else roles.Add(new HashSet<Dir>(through.SelectMany(s => new[] { s.entry, s.exit })).Count == 4 ? "+" : "T");
        }
        return roles;
    }
}
