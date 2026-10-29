using System; using System.Collections.Generic; using System.Linq; using UnityEngine;

class Step { public Vector2Int cell; public Dir entry, exit; }
class Route { public List<Step> steps = new(); public int doors; public HashSet<Vector2Int> cells; public List<Vector2Int> order; }

// one way to play a level: a route per object, a tile per used cell, and the deliveries in order
class Plan
{
    public int score, cells, combos, comboObjects, doorObjects, hits;
    public Route[] routes;
    public Dictionary<Vector2Int, TileType> tiles;
    public List<List<int>> events;     // groups of objects that arrive together, in order
    public List<Vector2Int> lastCells; // the cell placed last in each event (the one that delivers it)
    public List<List<Vector2Int>> pre;  // tiles of later objects built ahead in each event, so they pass shared tiles before the delivery
    public bool BuildsAhead => pre != null && pre.Any(x => x.Count > 0);
}

// the redesign's rules:
//   +100 per delivery, +50 per object in a multi-delivery, +50 per delivered object that used a door,
//   +75 per unused tile at the end; the supply never shrinks, and every multi-delivery earns a +.
//   each delivery removes one tile of its path. a plan is safe when no tile the game could break is one a later
//   object still needs: Order filters with a quick test, and every plan that would become the best is played
//   out by Replay2 (every possible break, and the ways the game ends a level early) before it counts
class Solver2
{
    public const int Delivery = 100, ComboPer = 50, DoorPer = 50, Unused = 75;
    public const int WaysSlack = 2; // "ways to finish" counts layouts whose routes are at most this much longer than the shortest (not wasteful ones)
    readonly LevelData L; readonly int slack, maxRoutes;
    public bool requireSafe = true;   // false: unsafe deliveries are allowed, at -75 for each tile they break
    public bool lucky;                // with requireSafe and penalize false: a plan counts if SOME sequence of breaks works (undo re-rolls them)
    public bool allowRevisits = true; // a route may cross a square a second time, entering from another side (a + or a bridge)
    public int floor = int.MinValue;  // only look for plans scoring at least this (prove: the target)
    public bool lookahead = false;    // prune harder: also count the new squares the objects still to route need (slower per step, for long routes)
    public Func<Plan, bool> filter;   // only plans this accepts count (e.g. "without a build-ahead", to see whether a trick is really needed)
    public bool noSharing;            // no two routes may pass through the same square
    public int maxVisits = 2;         // how many times a route may cross one square (up to 4: once from each side)
    public int maxRouteCells = int.MaxValue; // a route may cover at most this many different squares
    public bool quick;                // rough sizing only: the quick safety test, one tile assignment per route set, nothing played out
    public int maxReplays = 3000;     // plans played out per search; past this it stops and returns the best so far
    public bool limitHit;             // set when that happened, so a "no solution" or best may be incomplete
    public int maxLeaves = 300000;    // finished plans considered per search (the cost when no quick-safe plan exists)
    int replays, leaves;
    class ReplayLimit : Exception { }
    public bool allowCombos = true;   // false: every object arrives on its own
    public bool penalize = true;      // false: broken tiles cost nothing (the plan as it looks on paper)
    public bool noDetours;            // objects whose ring is on their own board may not use doors
    public bool oneEvent;             // every object arrives on the same placement
    public List<(string layout, int score)> collect; // when set, every finishing layout's best score is added here
    public List<Plan> every; // when set, every plan scoring at least `floor` is added here: each tile assignment, grouping, order and delivering cell, played out
    bool Exhaustive => collect != null || every != null; // nothing is skipped for not beating the best so far
    const int EveryCap = 100000; // tile mixes per road set when listing every plan
    public static volatile bool Abort; // set from another thread to stop every running solve (the level maker's Cancel and time limit)

    public Solver2(LevelData l, int slack = 4, int maxRoutes = 20000) { L = l; this.slack = slack; this.maxRoutes = maxRoutes; }

    // every finishing layout and its best score (in .collect): routes up to WaysSlack longer than the shortest,
    // or as long as the best plan's longest detour if that's more, so the best plan is always among them
    public static Solver2 CountWays(LevelData l)
    {
        int slack = WaysSlack;
        var b = new Solver2(l).Best();
        if (b != null) { var probe = new Solver2(l); slack = Math.Max(slack, Enumerable.Range(0, l.objects.Count).Max(i => b.routes[i].steps.Count - probe.Fewest(l.objects[i]))); }
        var sv = new Solver2(l, slack) { collect = new List<(string, int)>(), maxReplays = 200000, maxLeaves = 5000000 }; // every layout gets played out
        sv.Best();
        return sv;
    }

    bool InGrid(Vector2Int c) => L.BoardOf(c) >= 0;
    List<Dir> Into(DoorSpec p) { var r = new List<Dir>(); if (InGrid(p.cell)) r.Add(DirUtil.Opposite(p.side)); if (InGrid(p.Neighbor)) r.Add(p.side); return r; }

    // fewest cells an object must pass through, with any tile anywhere (doors and obstacles respected)
    public int Fewest(ObjectSpec s)
    {
        var dist = new Dictionary<(Vector2Int, Dir, bool), int>();
        var q = new Queue<(Vector2Int, Dir, bool)>(); var st0 = (s.start, s.heading, false); dist[st0] = 0; q.Enqueue(st0);
        while (q.Count > 0)
        {
            var st = q.Dequeue(); var (pos, h, via) = st; int d = dist[st];
            if (!via && L.DoorOnEdge(pos, h) != null) continue;
            var next = pos + DirUtil.Offset(h);
            if (!InGrid(next)) { if (next == s.dest) return d; continue; }
            if (L.obstacles.Contains(next)) continue;
            foreach (var e in DirUtil.All.Where(x => x != DirUtil.Opposite(h)))
            {
                var door = L.DoorOnEdge(next, e);
                var targets = new List<(Vector2Int, Dir, bool)>();
                if (door == null) targets.Add((next, e, false));
                else { var p = L.Partner(door); foreach (var hh in Into(p)) targets.Add((hh == p.side ? p.cell : p.Neighbor, hh, true)); }
                foreach (var tg in targets) if (!dist.ContainsKey(tg)) { dist[tg] = d + 1; q.Enqueue(tg); }
            }
        }
        return -1;
    }

    // every route up to `fewest + slack` cells long, visiting a cell at most twice and never entering it twice from the same side
    public List<Route> Routes(ObjectSpec s, int fewest)
    {
        var res = new List<Route>(); var steps = new List<Step>(); var used = new Dictionary<Vector2Int, List<Dir>>(); // the sides each cell was entered from
        void Go(Vector2Int pos, Dir h, bool via, int doors)
        {
            if (Abort) throw new OperationCanceledException();
            if (res.Count >= maxRoutes) return;
            if (!via && L.DoorOnEdge(pos, h) != null) return;
            var next = pos + DirUtil.Offset(h);
            if (!InGrid(next))
            {
                if (next == s.dest && !(noDetours && doors > 0 && s.startBoard == s.destBoard)) res.Add(new Route { steps = steps.ToList(), doors = doors, cells = new HashSet<Vector2Int>(used.Keys), order = steps.Select(x => x.cell).ToList() });
                return;
            }
            if (L.obstacles.Contains(next) || steps.Count >= fewest + slack) return;
            var e = DirUtil.Opposite(h);
            if (used.TryGetValue(next, out var entries) && (!allowRevisits || entries.Count >= maxVisits || entries.Contains(e))) return;
            if (entries == null) { if (used.Count >= maxRouteCells) return; used[next] = entries = new List<Dir>(); }
            entries.Add(e);
            foreach (var x in DirUtil.All.Where(d => d != e))
            {
                steps.Add(new Step { cell = next, entry = e, exit = x });
                var door = L.DoorOnEdge(next, x);
                if (door == null) Go(next, x, false, doors);
                else { var p = L.Partner(door); foreach (var hh in Into(p)) Go(hh == p.side ? p.cell : p.Neighbor, hh, true, doors + 1); }
                steps.RemoveAt(steps.Count - 1);
            }
            entries.RemoveAt(entries.Count - 1);
            if (entries.Count == 0) used.Remove(next);
        }
        if (fewest >= 0) Go(s.start, s.heading, false, 0);
        return res;
    }

    static bool Fits(TileType t, IEnumerable<Step> pairs) => pairs.All(p => Tiles.Has(t, p.entry) && Tiles.Exits(t, p.entry).Contains(p.exit));

    // every way to give these cells tiles from `left`, one per set of + cells (the only thing the delivery order
    // cares about: an earned + can only go on a cell placed after the group delivery that earns it).
    // simplest tile first, cells with the fewest options first, in a fixed cell order so results don't depend on search history.
    // everyTile: every tile that fits each cell, not only one per set of + cells (so a spare T where an I would do counts too)
    static List<Dictionary<Vector2Int, TileType>> Assign(Dictionary<Vector2Int, List<Step>> byCell, Dictionary<TileType, int> left, int cap = 64, bool everyTile = false)
    {
        var cand = byCell.ToDictionary(kv => kv.Key, kv => Tiles.All.Where(t => Fits(t, kv.Value))
            .OrderBy(t => DirUtil.All.Count(d => Tiles.Has(t, d)) + (t == TileType.Bridge ? 1 : 0) + (t == TileType.Cross ? 2 : 0)).ToList());
        var order = cand.OrderBy(kv => kv.Value.Count).ThenBy(kv => kv.Key.x).ThenBy(kv => kv.Key.y).Select(kv => kv.Key).ToList();
        var res = new List<Dictionary<Vector2Int, TileType>>(); var seen = new HashSet<string>();
        var pick = new Dictionary<Vector2Int, TileType>();
        void Rec(int i)
        {
            if (res.Count >= cap) return;
            if (i == order.Count)
            {
                var crosses = string.Join(";", order.Where(c => pick[c] == TileType.Cross).OrderBy(c => c.x).ThenBy(c => c.y));
                if (everyTile || seen.Add(crosses)) res.Add(new Dictionary<Vector2Int, TileType>(pick));
                return;
            }
            foreach (var t in cand[order[i]])
            {
                if (left[t] <= 0) continue;
                left[t]--; pick[order[i]] = t;
                Rec(i + 1);
                left[t]++;
            }
            pick.Remove(order[i]);
        }
        Rec(0);
        return res;
    }

    public Plan Best()
    {
        var objs = L.objects; int n = objs.Count;
        var routes = objs.Select(o => Routes(o, Fewest(o))).ToList();
        if (routes.Any(r => r.Count == 0)) return null;
        if (every != null && routes.Any(r => r.Count >= maxRoutes)) limitHit = true; // some roads were never tried, so the list could be short
        foreach (var rs in routes) rs.Sort((a, b) => a.steps.Count.CompareTo(b.steps.Count));
        int S = L.inventory.Values.Sum();
        int doorable = Enumerable.Range(0, n).Count(i => routes[i].Any(r => r.doors > 0));
        int maxCombos = allowCombos ? n / 2 : 0; // each group delivery earns a + (and needs two objects); none without groups
        int ceilingWithoutCells = Delivery * n + (allowCombos ? ComboPer * n : 0) + DoorPer * doorable + Unused * (S + maxCombos);

        Plan best = null;
        // start from the quick test's best plan when it really works, so the full search can skip what can't beat it
        if ((requireSafe || lucky) && !quick && !Exhaustive)
        {
            var q = new Solver2(L, slack, maxRoutes) { requireSafe = requireSafe, penalize = penalize, allowCombos = allowCombos, noDetours = noDetours, oneEvent = oneEvent, allowRevisits = allowRevisits, maxVisits = maxVisits, maxRouteCells = maxRouteCells, filter = filter, noSharing = noSharing, quick = true }.Best();
            if (q != null && new Replay2(L).Run(q, any: lucky).ok) best = q;
            // and if nothing works even when breaks are ignored, nothing works with them (this is the slow case otherwise)
            else if (new Solver2(L, slack, maxRoutes) { requireSafe = false, penalize = false, allowCombos = allowCombos, noDetours = noDetours, oneEvent = oneEvent, allowRevisits = allowRevisits, maxVisits = maxVisits, maxRouteCells = maxRouteCells, filter = filter, noSharing = noSharing }.Best() == null) return null;
        }
        var chosen = new Route[n];
        var byCell = new Dictionary<Vector2Int, List<Step>>();
        var order = Enumerable.Range(0, n).OrderBy(i => routes[i].Count).ToList();
        bool CellsOk() => byCell.All(kv => Tiles.All.Any(t => Fits(t, kv.Value) && (L.inventory.GetValueOrDefault(t) > 0 || (t == TileType.Cross && maxCombos > 0))));

        void Evaluate()
        {
            // 0 extra + tiles needs no earned +, so any assignment works and nothing more can beat it;
            // otherwise try each placement of the + tiles, with one, two... earned ones
            for (int extra = 0; extra <= maxCombos; extra++)
            {
                var left = Tiles.All.ToDictionary(t => t, t => L.inventory.GetValueOrDefault(t));
                left[TileType.Cross] += extra;
                bool any = false;
                var picks = every != null ? Assign(byCell, left, EveryCap, everyTile: true) : Assign(byCell, left, extra == 0 || quick ? 1 : 64);
                if (every != null && picks.Count >= EveryCap) limitHit = true; // tile mixes left out
                foreach (var pick in picks)
                {
                    var plan = Order(chosen.ToArray(), pick, S, Exhaustive ? null : best);
                    if (plan == null) continue;
                    any = true;
                    if (collect != null) collect.Add((string.Join(";", pick.OrderBy(kv => kv.Key.x).ThenBy(kv => kv.Key.y).Select(kv => $"{kv.Key}={kv.Value}")), plan.score));
                    if (best == null || plan.score > best.score || (plan.score == best.score && plan.cells < best.cells)) best = plan;
                }
                if (extra == 0 && any && every == null) break;
            }
        }

        void Rec(int k)
        {
            if (Abort) throw new OperationCanceledException();
            if (k == n) { Evaluate(); return; }
            int oi = order[k];
            foreach (var r in routes[oi])
            {
                if (noSharing && r.cells.Any(byCell.ContainsKey)) continue; // it would share a square with a route already chosen
                foreach (var st in r.steps) { if (!byCell.TryGetValue(st.cell, out var l)) byCell[st.cell] = l = new(); l.Add(st); }
                // more squares than the supply plus every + that could be earned can never be filled
                bool canBeat = byCell.Count <= S + maxCombos && ceilingWithoutCells - Unused * byCell.Count >= floor && (Exhaustive || best == null || ceilingWithoutCells - Unused * byCell.Count >= best.score);
                // lookahead: every object still to route adds at least its fewest new squares, so count the largest of those too
                if (canBeat && lookahead && k + 1 < n)
                {
                    int limit = Math.Min(S + maxCombos, (ceilingWithoutCells - Math.Max(floor, !Exhaustive && best != null ? best.score : int.MinValue)) / Unused);
                    int need = 0;
                    for (int j = k + 1; j < n && byCell.Count + need <= limit; j++)
                    {
                        int fewestNew = int.MaxValue;
                        foreach (var rr in routes[order[j]]) { int c = rr.cells.Count(x => !byCell.ContainsKey(x)); if (c < fewestNew) fewestNew = c; if (c == 0) break; }
                        need = Math.Max(need, fewestNew);
                    }
                    canBeat = byCell.Count + need <= limit;
                }
                if (canBeat && CellsOk()) { chosen[oi] = r; Rec(k + 1); }
                foreach (var st in r.steps) { var l = byCell[st.cell]; l.RemoveAt(l.Count - 1); if (l.Count == 0) byCell.Remove(st.cell); }
            }
        }
        try { Rec(0); }
        catch (ReplayLimit) { limitHit = true; }
        return best;
    }

    // the best order of deliveries for these routes and tiles, or null if none works.
    // plans that can't beat `beat` (the best so far) aren't played out
    Plan Order(Route[] r, Dictionary<Vector2Int, TileType> pick, int S, Plan beat)
    {
        int n = r.Length, startCross = L.inventory.GetValueOrDefault(TileType.Cross);
        int cells = pick.Count, doorObjects = Enumerable.Range(0, n).Count(i => r[i].doors > 0);
        bool verify = (requireSafe || lucky) && !quick;
        Plan best = null;

        // the cells object j still needs once `placed` are on the board: from the first one missing, onward
        IEnumerable<Vector2Int> Needed(int j, HashSet<Vector2Int> placed)
        {
            int k = r[j].order.FindIndex(c => !placed.Contains(c));
            return k < 0 ? Enumerable.Empty<Vector2Int>() : r[j].order.Skip(k);
        }

        var pres = new List<List<Vector2Int>>();
        void Try(List<List<int>> events, int k, HashSet<Vector2Int> placed, int combos, int comboObjs, int hits, List<Vector2Int> lasts)
        {
            if (k == events.Count)
            {
                if (verify && ++leaves > maxLeaves) throw new ReplayLimit();
                int unused = S + combos - cells;
                if (unused < 0) return;
                int score = Delivery * n + ComboPer * comboObjs + DoorPer * doorObjects + Unused * unused - (penalize && !verify ? Unused * hits : 0);
                if (score < floor || (every == null && best != null && score <= best.score)) return;
                if (beat != null && (score < beat.score || (score == beat.score && cells >= beat.cells))) return; // can't become the overall best
                var plan = new Plan { score = score, cells = cells, combos = combos, comboObjects = comboObjs, doorObjects = doorObjects, hits = hits,
                    routes = r, tiles = pick, events = events.Select(g => g.ToList()).ToList(), lastCells = lasts.ToList(), pre = pres.Select(x => x.ToList()).ToList() };
                if (filter != null && !filter(plan)) return;
                // the quick test above can't see what the game's removal rule really does, so play it out
                if (verify && ++replays > maxReplays) throw new ReplayLimit();
                if (verify)
                {
                    var rp = new Replay2(L).Run(plan, any: lucky);
                    // too many ways the tiles could break to follow them all: the plan isn't known to fail, so the list could be short
                    if (!rp.ok && every != null && rp.problem.StartsWith("too many ways")) limitHit = true;
                    if (!rp.ok) return;
                }
                every?.Add(plan);
                if (best == null || score > best.score) best = plan;
                return;
            }
            for (int i = k; i < events.Count; i++)
            {
                (events[k], events[i]) = (events[i], events[k]);
                var g = events[k];
                var common = new HashSet<Vector2Int>(r[g[0]].cells);
                foreach (var j in g.Skip(1)) common.IntersectWith(r[j].cells);
                common.ExceptWith(placed);
                if (common.Count > 0)
                {
                    var delivered = new HashSet<Vector2Int>();
                    foreach (var j in g) delivered.UnionWith(r[j].cells);
                    var after = new HashSet<Vector2Int>(placed); after.UnionWith(delivered);
                    var later = events.Skip(k + 1).SelectMany(e => e).ToList();
                    bool last = k == events.Count - 1;
                    var pre = new HashSet<Vector2Int>();
                    int hitsHere = 0;
                    if (!last)
                    {
                        // a later object that still has to cross a tile of the delivered paths can be built ahead,
                        // so it moves past those tiles before the delivery (it stops at a later empty square)
                        foreach (var j in later)
                        {
                            var ord = r[j].order;
                            int stop = ord.FindIndex(c => !after.Contains(c) && !pre.Contains(c));
                            if (stop < 0) continue; // checked below
                            if (!ord.Skip(stop).Any(delivered.Contains)) continue;
                            int need = ord.FindLastIndex(delivered.Contains) + 1;
                            int s = Enumerable.Range(need, Math.Max(0, ord.Count - need)).Where(x => !after.Contains(ord[x]) && !pre.Contains(ord[x])).DefaultIfEmpty(-1).First();
                            if (s < 0) { hitsHere++; continue; }
                            foreach (var c in ord.Take(s)) if (!after.Contains(c)) pre.Add(c);
                        }
                        after.UnionWith(pre);
                        var needed = new HashSet<Vector2Int>(later.SelectMany(j => Needed(j, after)));
                        hitsHere = Math.Max(hitsHere, g.Count(o => r[o].cells.Overlaps(needed)));
                    }
                    bool stillOpen = later.All(j => !r[j].cells.IsSubsetOf(after));
                    int crossesNeeded = after.Count(c => pick[c] == TileType.Cross);
                    bool crossOk = crossesNeeded <= startCross + combos;
                    // a quick-test hit isn't the end when every plan is played out: the game breaks a tile an object can
                    // reach right now first, so a needed tile past an empty square is often safe
                    if (stillOpen && crossOk && !(requireSafe && !verify && hitsHere > 0))
                    {
                        bool isCombo = g.Count >= 2;
                        // any square all of them still need can be the one that delivers them
                        foreach (var lastCell in common.OrderBy(c => c.x).ThenBy(c => c.y).Take(verify ? int.MaxValue : 1)) // only matters when the plan is played out
                        {
                            lasts.Add(lastCell);
                            pres.Add(pre.ToList());
                            Try(events, k + 1, after, combos + (isCombo ? 1 : 0), comboObjs + (isCombo ? g.Count : 0), hits + hitsHere, lasts);
                            lasts.RemoveAt(lasts.Count - 1);
                            pres.RemoveAt(pres.Count - 1);
                        }
                    }
                }
                (events[k], events[i]) = (events[i], events[k]);
            }
        }

        void Part(int i, List<List<int>> groups)
        {
            if (Abort) throw new OperationCanceledException();
            if (i == n && oneEvent && groups.Count != 1) return;
            if (i == n) { Try(groups.Select(g => g.ToList()).ToList(), 0, new HashSet<Vector2Int>(), 0, 0, 0, new List<Vector2Int>()); return; }
            int count = groups.Count;
            if (allowCombos)
                for (int gi = 0; gi < count; gi++) { var g = groups[gi]; g.Add(i); Part(i + 1, groups); g.RemoveAt(g.Count - 1); }
            groups.Add(new List<int> { i }); Part(i + 1, groups); groups.RemoveAt(groups.Count - 1);
        }
        Part(0, new List<List<int>>());
        return best;
    }

    public string Describe(Plan p)
    {
        if (p == null) return "  no solution";
        var objs = L.objects;
        var sb = new System.Text.StringBuilder();
        sb.Append($"  score {p.score}, tiles {p.cells}, combos {p.combos} ({p.comboObjects} objects), door objects {p.doorObjects}, hits {p.hits}\n");
        sb.Append("  events: " + string.Join(" then ", p.events.Select((g, i) => "{" + string.Join(",", g.Select(j => objs[j].name)) + "} last " + LB.Local(L, p.lastCells[i]))) + "\n");
        for (int i = 0; i < objs.Count; i++)
            sb.Append($"    {objs[i].name}: " + string.Join(" ", p.routes[i].steps.Select(st => $"{LB.Local(L, st.cell)}={LB.Short(p.tiles[st.cell])}>{st.exit}")) + (p.routes[i].doors > 0 ? $"  [{p.routes[i].doors} doors]" : "") + "\n");
        return sb.ToString();
    }
}
