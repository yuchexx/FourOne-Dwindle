using System; using System.Collections.Generic; using System.Linq; using UnityEngine;

// plays a plan move by move with the game's rules (movement, doors, deliveries, the removal rule, the + reward,
// and the ways a level ends early) and follows every tile a delivery could break:
//   safe (the default): the plan works whichever tile the game picks each time
//   lucky (any = true): it works for at least one pick each time, which undo's re-roll lets a player get to
class Replay2
{
    class O
    {
        public ObjectSpec s; public Vector2Int pos; public Dir h; public bool done, via; public int tp, idx;
        public List<Vector2Int> path = new();
        public O Copy() => new O { s = s, pos = pos, h = h, done = done, via = via, tp = tp, idx = idx, path = new List<Vector2Int>(path) };
    }

    class State
    {
        public Dictionary<Vector2Int, TileType> board; public Dictionary<TileType, int> inv; public List<O> objs; public int comboPts;
        public List<List<string>> events = new(); public List<List<Vector2Int>> breaks = new(); public List<List<int>> arrivals = new();
        public State Copy() => new State
        {
            board = new Dictionary<Vector2Int, TileType>(board), inv = new Dictionary<TileType, int>(inv), objs = objs.Select(o => o.Copy()).ToList(), comboPts = comboPts,
            events = new List<List<string>>(events), breaks = new List<List<Vector2Int>>(breaks), arrivals = new List<List<int>>(arrivals),
        };
    }

    readonly LevelData L;
    public readonly List<(Vector2Int cell, TileType t)> placements = new();
    public readonly Dictionary<string, Dir> choices = new();
    public readonly List<string> log = new();
    public List<List<string>> stepEvents = new();     // what happens after each placement, for the solution text
    public List<List<Vector2Int>> stepBreaks = new(); // the tiles each placement's deliveries break (a harmless pick where there is one)
    public List<List<int>> stepArrivals = new();      // the objects delivered by each placement
    public int score; public bool ok; public string problem = "";
    public int maxBranches = 20000; // removal sequences followed before giving up (and calling the plan unsafe)

    Plan plan; bool any, recorded; int branches; int cellCount;

    public Replay2(LevelData l) { L = l; }
    bool InGrid(Vector2Int c) => L.BoardOf(c) >= 0;
    List<Dir> Into(DoorSpec p) { var r = new List<Dir>(); if (InGrid(p.cell)) r.Add(DirUtil.Opposite(p.side)); if (InGrid(p.Neighbor)) r.Add(p.side); return r; }

    public Replay2 Run(Plan plan, bool any = false)
    {
        this.plan = plan; this.any = any;
        var objsSpec = L.objects; int n = objsSpec.Count;
        // the placement order: each event's new cells, its delivering cell last
        var placed = new HashSet<Vector2Int>();
        for (int k = 0; k < plan.events.Count; k++)
        {
            var g = plan.events[k];
            // later objects built ahead first (so they pass the shared tiles), then this event's own tiles
            var fresh = plan.pre[k].Concat(g.SelectMany(j => plan.routes[j].order)).Where(c => !placed.Contains(c)).Distinct().ToList();
            var last = plan.lastCells[k];
            foreach (var c in fresh.Where(c => c != last)) { placements.Add((c, plan.tiles[c])); placed.Add(c); }
            placements.Add((last, plan.tiles[last])); placed.Add(last);
        }
        // arrows: at a junction, the way the route goes (per visit, since a route can cross a square twice);
        // coming out of a door, the way it faces (per door taken)
        for (int i = 0; i < n; i++)
        {
            var steps = plan.routes[i].steps; int doors = 0;
            for (int s = 0; s < steps.Count; s++)
            {
                var st = steps[s];
                choices[$"{objsSpec[i].name}@{st.cell}/{st.entry}"] = st.exit;
                var door = L.DoorOnEdge(st.cell, st.exit);
                if (door != null && s + 1 < steps.Count)
                    choices[$"{objsSpec[i].name}<{++doors}"] = DirUtil.Opposite(steps[s + 1].entry);
            }
        }
        cellCount = Enumerable.Range(0, L.width).SelectMany(x => Enumerable.Range(0, L.height).Select(y => new Vector2Int(x, y))).Count(c => InGrid(c) && !L.obstacles.Contains(c));

        var start = new State
        {
            board = new Dictionary<Vector2Int, TileType>(),
            inv = Tiles.All.ToDictionary(t => t, t => L.inventory.GetValueOrDefault(t)),
            objs = L.objects.Select((s, i) => new O { s = s, pos = s.start, h = s.heading, idx = i }).ToList(),
        };
        try { ok = Step(start, 0); }
        catch (Exception e) { ok = false; problem = e.Message; }
        if (ok) problem = "";
        return this;
    }

    void Fail(string why) { if (problem.Length == 0) problem = why; }

    Dir Pick(string key, List<Dir> ways)
    {
        if (ways.Count == 1) return ways[0];
        if (choices.TryGetValue(key, out var d) && ways.Contains(d)) return d;
        throw new Exception($"no choice for {key} among {string.Join(",", ways)}");
    }

    // placement i onward
    bool Step(State st, int i)
    {
        if (i == placements.Count)
        {
            if (!st.objs.All(o => o.done)) { Fail("not every object arrived"); return false; }
            if (!recorded)
            {
                recorded = true; stepEvents = st.events; stepBreaks = st.breaks; stepArrivals = st.arrivals;
                int doorObjs = st.objs.Count(o => o.done && o.tp > 0);
                score = Solver2.Delivery * st.objs.Count + st.comboPts + Solver2.DoorPer * doorObjs + Solver2.Unused * st.inv.Values.Sum();
            }
            return true;
        }
        var (cell, t) = placements[i];
        if (st.board.ContainsKey(cell) || L.obstacles.Contains(cell)) { Fail($"{LB.Local(L, cell)} is taken"); return false; }
        if (st.inv[t] <= 0) { Fail($"no {LB.Short(t)} left in the supply at step {i + 1}"); return false; }
        st.inv[t]--; st.board[cell] = t;
        var ev = new List<string>(); var now = new List<int>();
        foreach (var o in st.objs.Where(o => !o.done)) { Move(o, st.board, ev); if (o.done) now.Add(o.idx); }
        return Break(st, i, now, 0, ev, new List<Vector2Int>(), st.objs.All(o => o.done));
    }

    // the delivered objects break one tile each, one after another; every pick the game could make is followed
    bool Break(State st, int i, List<int> now, int k, List<string> ev, List<Vector2Int> broken, bool finished)
    {
        if (k == now.Count)
        {
            if (now.Count >= 2) { st.comboPts += Solver2.ComboPer * now.Count; st.inv[TileType.Cross]++; ev.Add($"{string.Join(" and ", now.Select(j => L.objects[j].name))} arrive together, +1 + tile"); }
            else if (now.Count == 1) ev.Add($"{L.objects[now[0]].name} arrives");
            st.events.Add(ev); st.breaks.Add(broken); st.arrivals.Add(now);
            if (!finished)
            {
                // the game ends the level here, before everyone is in
                if (st.inv.Values.Sum() == 0) { Fail($"the supply runs out after step {i + 1}"); return false; }
                if (st.board.Count >= cellCount) { Fail($"the board is full after step {i + 1}"); return false; }
                if (st.objs.Where(o => !o.done).All(o => Blocked(o, st.board))) { Fail($"every remaining object is blocked after step {i + 1}"); return false; }
            }
            return Step(st, i + 1);
        }
        var d = st.objs[now[k]];
        var cand = d.path.Distinct().Where(st.board.ContainsKey).ToList();
        if (cand.Count == 0) return Break(st, i, now, k + 1, ev, broken, finished);
        var reach = new HashSet<Vector2Int>(st.objs.Where(w => !w.done).SelectMany(w => Reachable(w, st.board)));
        var group = cand.Where(reach.Contains).ToList();
        if (group.Count == 0) group = cand;
        if (finished) { st.board.Remove(group[0]); broken.Add(group[0]); return Break(st, i, now, k + 1, ev, broken, finished); } // nothing left to hurt
        // the rest of each waiting object's planned route
        var needed = new HashSet<Vector2Int>(st.objs.Where(w => !w.done).SelectMany(w => plan.routes[w.idx].order.Skip(w.path.Count)));
        bool anyOk = false;
        foreach (var c in group.OrderBy(needed.Contains))
        {
            if (needed.Contains(c))
            {
                Fail($"{d.s.name}'s delivery can break {LB.Local(L, c)}, which {string.Join(" and ", st.objs.Where(w => !w.done && plan.routes[w.idx].order.Skip(w.path.Count).Contains(c)).Select(w => w.s.name))} still needs");
                if (!any) return false;
                continue;
            }
            if (++branches > maxBranches) { Fail("too many ways the tiles could break to check them all"); return false; }
            var copy = group.Count == 1 ? st : st.Copy();
            copy.board.Remove(c);
            bool ok = Break(copy, i, now, k + 1, new List<string>(ev), new List<Vector2Int>(broken) { c }, finished);
            if (ok) { anyOk = true; if (any) return true; }
            else if (!any) return false;
        }
        return anyOk;
    }

    void Move(O o, Dictionary<Vector2Int, TileType> board, List<string> ev)
    {
        for (int g = 0; g < 400; g++)
        {
            var next = o.pos + DirUtil.Offset(o.h);
            if (!o.via && L.DoorOnEdge(o.pos, o.h) != null) return;
            if (!InGrid(next)) { if (next == o.s.dest) { o.done = true; o.pos = next; } return; }
            var entry = DirUtil.Opposite(o.h);
            if (!board.TryGetValue(next, out var t) || !Tiles.Has(t, entry)) return;
            o.via = false; o.pos = next; o.path.Add(next);
            var ways = Tiles.Exits(t, entry);
            o.h = Pick($"{o.s.name}@{next}/{entry}", ways);
            if (ways.Count > 1) ev.Add($"{o.s.name} picks {o.h} at {LB.Local(L, next)}");
            var door = L.DoorOnEdge(next, o.h);
            if (door != null)
            {
                var p = L.Partner(door); o.tp++;
                var into = Into(p);
                o.h = Pick($"{o.s.name}<{o.tp}", into);
                o.pos = o.h == p.side ? p.cell : p.Neighbor; o.via = true;
                ev.Add($"{o.s.name} goes through door {door.label}" + (into.Count > 1 ? $", comes out facing {o.h}" : ""));
            }
        }
        throw new Exception($"{o.s.name} loops");
    }

    // the game's IsBlocked: the square in front can't be entered right now
    bool Blocked(O o, Dictionary<Vector2Int, TileType> board)
    {
        if (!o.via && L.DoorOnEdge(o.pos, o.h) != null) return true;
        var next = o.pos + DirUtil.Offset(o.h);
        if (!InGrid(next)) return next != o.s.dest;
        if (L.obstacles.Contains(next)) return true;
        return board.TryGetValue(next, out var t) && !Tiles.Has(t, DirUtil.Opposite(o.h));
    }

    // the placed tiles an object could still reach by following tiles ahead of it (every branch, through doors)
    IEnumerable<Vector2Int> Reachable(O o, Dictionary<Vector2Int, TileType> board)
    {
        var seen = new HashSet<(Vector2Int, Dir, bool)>(); var cells = new HashSet<Vector2Int>();
        var q = new Queue<(Vector2Int, Dir, bool)>(); q.Enqueue((o.pos, o.h, o.via));
        while (q.Count > 0)
        {
            var (pos, h, via) = q.Dequeue();
            if (!via && L.DoorOnEdge(pos, h) != null) continue;
            var next = pos + DirUtil.Offset(h);
            if (!InGrid(next) || !board.TryGetValue(next, out var t) || !Tiles.Has(t, DirUtil.Opposite(h))) continue;
            cells.Add(next);
            foreach (var e in Tiles.Exits(t, DirUtil.Opposite(h)))
            {
                var door = L.DoorOnEdge(next, e);
                if (door == null) { if (seen.Add((next, e, false))) q.Enqueue((next, e, false)); continue; }
                var p = L.Partner(door);
                foreach (var hh in Into(p)) { var from = hh == p.side ? p.cell : p.Neighbor; if (seen.Add((from, hh, true))) q.Enqueue((from, hh, true)); }
            }
        }
        return cells;
    }
}
