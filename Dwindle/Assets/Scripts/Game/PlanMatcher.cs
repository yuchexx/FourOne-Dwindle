using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// what the player has done so far, as the hints compare it with the best plans. squares are in the shared grid
// (LevelData's coordinates, as the board uses them), not a board's own
public class PlayerState
{
    public Dictionary<Vector2Int, TileType> tiles = new Dictionary<Vector2Int, TileType>(); // on the board now
    public List<DeliveryRecord> deliveries = new List<DeliveryRecord>();                     // in the order they happened
    public Dictionary<string, List<ArrowRecord>> arrows = new Dictionary<string, List<ArrowRecord>>(); // per object, in order
    public Dictionary<TileType, int> supply = new Dictionary<TileType, int>();               // what's left to place
    public Dictionary<string, List<Vector2Int>> passed = new Dictionary<string, List<Vector2Int>>(); // squares each object has gone through
}

// objects arriving on one placement
public struct DeliveryRecord
{
    public string[] objects;
    public Vector2Int at;                // the square placed that delivered them
    public HashSet<Vector2Int> occupied; // the squares with a tile at that moment (before the delivery broke any)
    public bool happened;                // played in the game (its breaks are known), not just foreseen by the hints
}

// an arrow the player picked: at a junction (square) or coming out of a door (door's letter)
public struct ArrowRecord
{
    public Vector2Int? square;
    public string door;
    public Dir way;
}

// one tile of a hint's plan, in the order it should go down
public enum StepState { ToDo, Done, Early }

public struct PlanStep
{
    public Vector2Int square;
    public TileType tile;
    public int delivery;    // which delivery of the plan it belongs to (0 = the first)
    public bool completes;  // placing it should deliver that delivery's objects
}

// which best plans (a family and one of its delivery orders) the player's board still fits, and what's left to do
// on one. a plan fits when every tile on the board is one it allows there, every arrow picked is its arrow, and the
// deliveries so far are the start of its order, each completed on one of its squares after its "before" tiles
public static class PlanMatcher
{
    static Vector2Int G(LevelData l, Square s) => l.boards[s.board].origin + s.square;

    public static bool Fits(LevelData l, int family, int order, PlayerState st)
    {
        if (!FitsSoFar(l, family, order, st)) return false;
        var deliveries = l.solution.families[family].orders[order].deliveries;
        for (int k = st.deliveries.Count; k < deliveries.Length; k++)
            if (MeetingMissed(l, deliveries[k], st) || GroupLost(l, deliveries[k], st)) return false;
        return true;
    }

    // what has happened so far is this plan's: its tiles, its arrows, its first deliveries (no check on the deliveries to
    // come, which mid-move can look missed while objects move one at a time)
    public static bool FitsSoFar(LevelData l, int family, int order, PlayerState st)
    {
        var fam = l.solution.families[family];
        var deliveries = fam.orders[order].deliveries;
        var allowed = fam.squares.ToDictionary(a => G(l, a.at), a => a.tiles);
        foreach (var kv in st.tiles)
            if (!allowed.TryGetValue(kv.Key, out var ts) || !ts.Contains(kv.Value)) return false;
        if (st.deliveries.Count > deliveries.Length) return false;
        for (int k = 0; k < st.deliveries.Count; k++)
            if (!DeliveryFits(l, family, order, k, st.deliveries[k])) return false;
        foreach (var kv in st.arrows)
        {
            var road = fam.roads.FirstOrDefault(r => r.objectName == kv.Key);
            if (road == null || !ArrowsFit(l, road, kv.Value)) return false;
        }
        return true;
    }

    // stage 3: can this plan still be finished its way from the board? plays its remaining steps (NextSteps, in its
    // order) with the objects moving along its roads as far as the tiles go, and checks every delivery happens as the
    // plan's (the same objects together, on one of its squares, after its "before" tiles)
    // placed: mid-move, the square whose round is being played (objects not done moving finish it first, and whoever
    // arrives in it is delivered on that square); null when everything has stopped
    public static bool Reachable(LevelData l, int family, int order, PlayerState st, Vector2Int? placed = null)
    {
        if (!(placed == null ? Fits(l, family, order, st) : FitsSoFar(l, family, order, st))) return false;
        var fam = l.solution.families[family];
        int total = fam.orders[order].deliveries.Length;
        if (st.deliveries.Count == total) return true;
        var sim = new PlayerState
        {
            tiles = new Dictionary<Vector2Int, TileType>(st.tiles),
            deliveries = new List<DeliveryRecord>(st.deliveries),
            arrows = st.arrows,
            supply = new Dictionary<TileType, int>(st.supply),
            passed = st.passed.ToDictionary(kv => kv.Key, kv => new List<Vector2Int>(kv.Value)),
        };
        var delivered = new HashSet<string>(st.deliveries.SelectMany(d => d.objects));
        // how far along its road each object still on its way is (its squares gone through, in order)
        var roads = fam.roads.ToDictionary(r => r.objectName, r => r.squares.Select(q => G(l, q)).ToList());
        var reached = new Dictionary<string, int>();
        foreach (var kv in roads)
        {
            if (delivered.Contains(kv.Key)) continue;
            int p = 0;
            if (sim.passed.TryGetValue(kv.Key, out var path))
                foreach (var sq in path) if (p < kv.Value.Count && kv.Value[p] == sq) p++;
            reached[kv.Key] = p;
        }
        // each object moves on while the next square of its road has a tile; the ones that reach their rings together
        List<string> Move()
        {
            var arrived = new List<string>();
            foreach (var name in reached.Keys.ToList())
            {
                var road = roads[name];
                int p = reached[name];
                if (!sim.passed.TryGetValue(name, out var path)) sim.passed[name] = path = new List<Vector2Int>();
                while (p < road.Count && sim.tiles.ContainsKey(road[p])) path.Add(road[p++]);
                reached[name] = p;
                if (p == road.Count) arrived.Add(name);
            }
            foreach (var name in arrived) reached.Remove(name);
            return arrived;
        }
        // the round being played finishes (mid-move), or nothing moves (everything has stopped)
        var now = Move();
        if (now.Count > 0)
        {
            if (placed == null) return false; // everything has stopped: nobody should still be able to arrive
            sim.deliveries.Add(new DeliveryRecord { objects = now.ToArray(), at = placed.Value, occupied = new HashSet<Vector2Int>(sim.tiles.Keys) });
            if (now.Count >= 2) sim.supply[TileType.Cross] = sim.supply.GetValueOrDefault(TileType.Cross) + 1; // delivering together earns a +
        }
        if (!Fits(l, family, order, sim)) return false;
        if (sim.deliveries.Count == total) return true;
        var next = NextSteps(l, family, order, sim); // the rest, from where this round leaves things
        if (next == null) return false;
        // this plan's number of tiles: the tiles used so far (broken ones too) and the ones still to place can't go past
        // it (e.g. a delivery's break hit a road still needed, so a tile would go down twice)
        int used = l.inventory.Values.Sum() + sim.deliveries.Count(d => d.objects.Length >= 2) - sim.supply.Values.Sum();
        int best = fam.squares.Length; // this plan's own count (one tile per square; best plans can differ, e.g. one places the given +)
        if (used + next.Count > best) return false;
        foreach (var s in next)
        {
            sim.tiles[s.square] = s.tile;
            var arrived = Move();
            if (arrived.Count > 0)
                sim.deliveries.Add(new DeliveryRecord { objects = arrived.ToArray(), at = s.square, occupied = new HashSet<Vector2Int>(sim.tiles.Keys) });
            if (!Fits(l, family, order, sim)) return false;
        }
        return sim.deliveries.Count == total;
    }

    // a group still to come can't arrive together any more: every square that could deliver it already has a tile, so
    // its next tile lets one member through alone (e.g. Level 1's + placed first)
    static bool GroupLost(LevelData l, Delivery d, PlayerState st) =>
        d.objects.Length >= 2 && d.completedBy.All(c => st.tiles.ContainsKey(G(l, c.at)));

    // a delivery still to come can't happen any more: the tile that delivers a group lies on every member's road, so
    // each member waits before it. once a member has gone through every square that could deliver them, it got
    // there ahead of the rest (e.g. Level 2's bridge placed first: Green runs through it alone)
    static bool MeetingMissed(LevelData l, Delivery d, PlayerState st) =>
        !d.completedBy.Any(c => d.objects.All(o => !Passed(st, o, G(l, c.at))));

    static bool Passed(PlayerState st, string objectName, Vector2Int square) =>
        st.passed.TryGetValue(objectName, out var p) && p.Contains(square);

    // the k-th delivery happened as the plan's k-th: the same objects, on one of its squares, after its "before" tiles
    public static bool DeliveryFits(LevelData l, int family, int order, int k, DeliveryRecord d)
    {
        var deliveries = l.solution.families[family].orders[order].deliveries;
        if (k >= deliveries.Length) return false;
        var planned = deliveries[k];
        if (planned.objects.Length != d.objects.Length || planned.objects.Except(d.objects).Any()) return false;
        var completion = planned.completedBy.FirstOrDefault(c => G(l, c.at) == d.at);
        if (completion == null) return false;
        // the "before" tiles keep the delivery's breaks from hitting a road still needed. once it has happened its breaks
        // are known: what's left is judged as it stands (Reachable), so a delivery that got lucky still counts
        return d.happened || completion.before.All(b => d.occupied.Contains(G(l, b)));
    }

    // the arrows picked so far follow the road's: a Turn the game never asked (no junction there) is skipped
    static bool ArrowsFit(LevelData l, Road road, List<ArrowRecord> picked)
    {
        int p = 0;
        foreach (var a in picked)
        {
            while (p < road.choices.Length && !SamePlace(l, road.choices[p], a)) p++;
            if (p == road.choices.Length || road.choices[p].way != a.way) return false;
            p++;
        }
        return true;
    }

    // the turns picked so far are every object's road's turns in some best plan
    static bool TurnsFitSomePlan(LevelData l, PlayerState st) =>
        Plans(l).Any(p => st.arrows.All(kv =>
        {
            var road = l.solution.families[p.family].roads.FirstOrDefault(r => r.objectName == kv.Key);
            return road != null && ArrowsFit(l, road, kv.Value);
        }));

    // the first arrow the object picked that its road doesn't have (as ArrowsFit reads them): the junction's square, or
    // for a door the last square of its road it went through. null when they all fit
    static Vector2Int? WrongTurn(LevelData l, Road road, List<ArrowRecord> picked, PlayerState st)
    {
        int p = 0;
        foreach (var a in picked)
        {
            while (p < road.choices.Length && !SamePlace(l, road.choices[p], a)) p++;
            if (p == road.choices.Length || road.choices[p].way != a.way)
            {
                if (a.square != null) return a.square;
                var onRoad = new HashSet<Vector2Int>(road.squares.Select(q => G(l, q)));
                if (st.passed.TryGetValue(road.objectName, out var path))
                    for (int i = path.Count - 1; i >= 0; i--) if (onRoad.Contains(path[i])) return path[i];
                return null;
            }
            p++;
        }
        return null;
    }

    static bool SamePlace(LevelData l, Choice c, ArrowRecord a) =>
        a.door != null ? c.door == a.door : c.door == null && a.square == G(l, c.at);

    // the plan's arrow for the object at this junction or door, after the arrows it has already picked; null if the
    // plan has none there
    public static Dir? ExpectedArrow(LevelData l, int family, string objectName, ArrowRecord here, List<ArrowRecord> picked)
    {
        var road = l.solution.families[family].roads.FirstOrDefault(r => r.objectName == objectName);
        if (road == null) return null;
        int p = 0;
        foreach (var a in picked ?? new List<ArrowRecord>())
        {
            while (p < road.choices.Length && !SamePlace(l, road.choices[p], a)) p++;
            if (p < road.choices.Length) p++;
        }
        for (; p < road.choices.Length; p++)
            if (SamePlace(l, road.choices[p], here)) return road.choices[p].way;
        return null;
    }

    // the plan the board fits best: the answer's own if it fits, else the first that does. null when none fits
    public static (int family, int order)? Closest(LevelData l, PlayerState st)
    {
        var s = l.solution;
        if (s == null) return null;
        if (Fits(l, s.answerFamily, s.answerOrder, st)) return (s.answerFamily, s.answerOrder);
        for (int f = 0; f < s.families.Length; f++)
            for (int o = 0; o < s.families[f].orders.Length; o++)
                if (Fits(l, f, o, st)) return (f, o);
        return null;
    }

    // when nothing fits exactly: the plan the board is nearest to. first the most deliveries that happened as the plan's
    // (in order, from the first), then the most tiles the plan allows where they are, then the fewest it doesn't; the
    // answer's own plan wins a tie
    public static (int family, int order) Nearest(LevelData l, PlayerState st)
    {
        var s = l.solution;
        (int f, int o) best = (s.answerFamily, s.answerOrder);
        (int, int, int) bestScore = Score(l, best.f, best.o, st);
        for (int f = 0; f < s.families.Length; f++)
            for (int o = 0; o < s.families[f].orders.Length; o++)
            {
                var sc = Score(l, f, o, st);
                if (sc.CompareTo(bestScore) > 0) { best = (f, o); bestScore = sc; }
            }
        return best;
    }

    static (int deliveries, int tiles, int notWrong) Score(LevelData l, int family, int order, PlayerState st)
    {
        var fam = l.solution.families[family];
        var allowed = fam.squares.ToDictionary(a => G(l, a.at), a => a.tiles);
        int common = st.tiles.Count(kv => allowed.TryGetValue(kv.Key, out var ts) && ts.Contains(kv.Value));
        return (MatchingDeliveries(l, family, order, st.deliveries), common, -(st.tiles.Count - common));
    }

    // how many of the deliveries so far, from the first, happened as this plan's
    public static int MatchingDeliveries(LevelData l, int family, int order, List<DeliveryRecord> deliveries)
    {
        int k = 0;
        while (k < deliveries.Count && DeliveryFits(l, family, order, k, deliveries[k])) k++;
        return k;
    }

    // stage 3: how each step stands. Done: its exact tile is on its square at the right moment, or its delivery has
    // already happened the plan's way (its tile may have broken since). Early: its exact tile is there at the wrong
    // moment (it made a delivery the plan didn't have, its group can't meet any more, went
    // down before the group's other tiles, an object turned off its road there, or it's in brokePlan: placing it left no
    // way to finish). ToDo: the square is empty or has another tile
    public static StepState[] StepStates(LevelData l, int family, int order, List<PlanStep> steps, PlayerState st,
        HashSet<Vector2Int> brokePlan = null, bool moving = false)
    {
        int ok = MatchingDeliveries(l, family, order, st.deliveries);
        var offPlan = new HashSet<Vector2Int>(st.deliveries.Skip(ok).Select(d => d.at));
        var deliveries = l.solution.families[family].orders[order].deliveries;
        // where an object turned off its road (an arrow the plan doesn't have): that square's step is wrong too
        var fam = l.solution.families[family];
        // mid-move a turn off this road may still be another plan's (stage 3 moves to it once everything stops): only
        // wrong if no best plan has it
        bool turnsCount = !moving || !TurnsFitSomePlan(l, st);
        var wrongTurns = new HashSet<Vector2Int>();
        foreach (var kv in st.arrows)
        {
            var road = fam.roads.FirstOrDefault(r => r.objectName == kv.Key);
            if (turnsCount && road != null && WrongTurn(l, road, kv.Value, st) is Vector2Int w) wrongTurns.Add(w);
        }
        // a group still to come that can't arrive together any more (GroupLost): the tile that should deliver it came too early
        var lost = new HashSet<int>();
        for (int k = ok; k < deliveries.Length; k++)
            if (GroupLost(l, deliveries[k], st)) lost.Add(k);
        // squares an object still on its way has yet to go through: a step there isn't done while its square is empty, even if
        // its own delivery happened (e.g. a broken tile, or a "before" tile on a later object's road)
        var delivered = new HashSet<string>(st.deliveries.SelectMany(d => d.objects));
        var stillNeeded = new HashSet<Vector2Int>();
        foreach (var road in fam.roads.Where(r => !delivered.Contains(r.objectName)))
        {
            var gone = st.passed.TryGetValue(road.objectName, out var p) ? new List<Vector2Int>(p) : new List<Vector2Int>();
            foreach (var q in road.squares) { var c = G(l, q); if (!gone.Remove(c)) stillNeeded.Add(c); }
        }
        var res = new StepState[steps.Count];
        for (int i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            // the placement that left no way to finish: marked even if its tile broke since, or its delivery happened
            if (brokePlan != null && brokePlan.Contains(s.square)) { res[i] = StepState.Early; continue; }
            bool emptyButNeeded = stillNeeded.Contains(s.square) && !st.tiles.ContainsKey(s.square);
            if (s.delivery >= 0 && s.delivery < ok && !emptyButNeeded) { res[i] = StepState.Done; continue; }
            if (!st.tiles.TryGetValue(s.square, out var t) || t != s.tile) { res[i] = StepState.ToDo; continue; }
            // a group's last tile: wrong once the group can't meet any more (a member went through every square it could meet
            // on, or they all have tiles), not just because a member went through the plan's own meeting square
            bool early = offPlan.Contains(s.square) || wrongTurns.Contains(s.square) || brokePlan != null && brokePlan.Contains(s.square) || s.completes && s.delivery >= 0 && s.delivery < deliveries.Length &&
                         deliveries[s.delivery].objects.Length >= 2 &&
                         !moving && (lost.Contains(s.delivery) || MeetingMissed(l, deliveries[s.delivery], st));
            res[i] = early ? StepState.Early : StepState.Done;
        }
        return res;
    }

    // the same steps (squares, order, numbers) with their tiles picked again for this board: a step not done yet whose
    // square has a tile the plan allows there takes that tile; the empty ones get tiles the supply left (and the +
    // tiles still to be earned) can cover. null when it can't cover them
    public static List<PlanStep> Refit(LevelData l, int family, int order, List<PlanStep> steps, StepState[] states, PlayerState st)
    {
        var allowed = l.solution.families[family].squares.ToDictionary(a => G(l, a.at), a => a.tiles);
        var deliveries = l.solution.families[family].orders[order].deliveries;
        var res = new List<PlanStep>(steps);
        var todo = Enumerable.Range(0, res.Count).Where(i => states[i] != StepState.Done).ToList();
        var left = new Dictionary<TileType, int>(st.supply);
        bool Assign(int k)
        {
            if (k == todo.Count) return true;
            var step = res[todo[k]];
            bool earns = step.completes && step.delivery >= 0 && step.delivery < deliveries.Length && deliveries[step.delivery].objects.Length >= 2;
            // already down and allowed here: it stays (no supply needed)
            bool down = st.tiles.TryGetValue(step.square, out var placed) && allowed[step.square].Contains(placed);
            foreach (var t in down ? new[] { placed } : allowed[step.square].OrderBy(t => t != step.tile).ThenBy(t => t == TileType.Cross).ToArray())
            {
                if (!down && left.GetValueOrDefault(t) <= 0) continue;
                if (!down) left[t]--;
                if (earns) left[TileType.Cross] = left.GetValueOrDefault(TileType.Cross) + 1;
                step.tile = t; res[todo[k]] = step;
                if (Assign(k + 1)) return true;
                if (earns) left[TileType.Cross]--;
                if (!down) left[t]++;
            }
            return false;
        }
        return Assign(0) ? res : null;
    }

    // every best plan, the answer's own first
    public static IEnumerable<(int family, int order)> Plans(LevelData l)
    {
        var s = l.solution;
        yield return (s.answerFamily, s.answerOrder);
        for (int f = 0; f < s.families.Length; f++)
            for (int o = 0; o < s.families[f].orders.Length; o++)
                if (f != s.answerFamily || o != s.answerOrder) yield return (f, o);
    }

    // a plan's whole sequence from an empty board, with the level's own supply. prefer: tiles to pick where the plan
    // allows them (the player's own, so a fitting tile already down counts as done)
    public static List<PlanStep> FromStart(LevelData l, int family, int order, Dictionary<Vector2Int, TileType> prefer = null) =>
        NextSteps(l, family, order, new PlayerState { supply = LevelSupply(l) }, prefer);

    static Dictionary<TileType, int> LevelSupply(LevelData l) => Tiles.All.ToDictionary(t => t, t => l.inventory.GetValueOrDefault(t));

    // the delivery each step belongs to, and the steps that deliver: a delivery happens on the first of its squares
    // that completes every member's road (for steps that don't say, like shown steps carried over to another plan)
    public static List<PlanStep> Tag(LevelData l, int family, int order, List<PlanStep> steps)
    {
        var fam = l.solution.families[family];
        var deliveries = fam.orders[order].deliveries;
        var placed = new HashSet<Vector2Int>();
        var res = new List<PlanStep>();
        int k = 0;
        foreach (var step in steps)
        {
            var s = step;
            placed.Add(s.square);
            s.delivery = k;
            s.completes = k < deliveries.Length && deliveries[k].completedBy.Any(c => G(l, c.at) == s.square) &&
                deliveries[k].objects.All(o => fam.roads.First(r => r.objectName == o).squares.All(q => placed.Contains(G(l, q))));
            if (s.completes) k++;
            res.Add(s);
        }
        return res;
    }

    // another plan carrying on from the steps already shown: null unless those are its first steps too (it allows each
    // of those tiles there, the supply covers them, and the deliveries they make are its first ones). otherwise the
    // shown steps (numbered as before), then its own next ones
    public static List<PlanStep> Continue(LevelData l, int family, int order, List<PlanStep> shown, Dictionary<Vector2Int, TileType> prefer)
    {
        var tagged = Tag(l, family, order, shown);
        var deliveries = l.solution.families[family].orders[order].deliveries;
        var st = new PlayerState { supply = LevelSupply(l) };
        var occupied = new HashSet<Vector2Int>();
        foreach (var s in tagged)
        {
            if (st.supply.GetValueOrDefault(s.tile) <= 0) return null;
            st.supply[s.tile]--;
            st.tiles[s.square] = s.tile;
            occupied.Add(s.square);
            if (!s.completes) continue;
            st.deliveries.Add(new DeliveryRecord { objects = deliveries[s.delivery].objects, at = s.square, occupied = new HashSet<Vector2Int>(occupied) });
            if (deliveries[s.delivery].objects.Length >= 2) st.supply[TileType.Cross]++;
        }
        if (!Fits(l, family, order, st)) return null;
        var next = NextSteps(l, family, order, st, prefer);
        return next == null ? null : tagged.Concat(next).ToList();
    }

    // stage 2: who arrives in which delivery (1 = first; the same number = together), and the squares where groups meet
    public static List<(string objectName, int number)> Badges(LevelData l, int family, int order)
    {
        var res = new List<(string, int)>();
        var deliveries = l.solution.families[family].orders[order].deliveries;
        for (int k = 0; k < deliveries.Length; k++)
            foreach (var o in deliveries[k].objects) res.Add((o, k + 1));
        return res;
    }

    // with the delivery's number (the same as its badges)
    public static List<(Vector2Int square, int number)> Junctions(LevelData l, int family, int order)
    {
        var deliveries = l.solution.families[family].orders[order].deliveries;
        var res = new List<(Vector2Int, int)>();
        for (int k = 0; k < deliveries.Length; k++)
            if (deliveries[k].objects.Length >= 2)
                foreach (var c in deliveries[k].completedBy)
                    if (!res.Any(r => r.Item1 == G(l, c.at))) res.Add((G(l, c.at), k + 1));
        return res;
    }

    // stage 3: the tiles still to place on this plan, in order: for each delivery still to come, its "before" tiles,
    // then the arriving objects' roads, then the tile that completes it. each gets a tile the supply allows at that
    // point (a + earned by delivering together only after that delivery). null when the supply can't cover it
    public static List<PlanStep> NextSteps(LevelData l, int family, int order, PlayerState st, Dictionary<Vector2Int, TileType> prefer = null)
    {
        var fam = l.solution.families[family];
        var deliveries = fam.orders[order].deliveries;
        var allowed = fam.squares.ToDictionary(a => G(l, a.at), a => a.tiles);
        var planned = new HashSet<Vector2Int>(st.tiles.Keys);
        var steps = new List<PlanStep>();
        for (int k = st.deliveries.Count; k < deliveries.Length; k++)
        {
            var d = deliveries[k];
            var completion = d.completedBy.FirstOrDefault(c => !planned.Contains(G(l, c.at))) ?? d.completedBy[0];
            var last = G(l, completion.at);
            int start = steps.Count;
            void Add(Vector2Int c) { if (planned.Add(c)) steps.Add(new PlanStep { square = c, delivery = k }); }
            foreach (var b in completion.before)
                if (!st.passed.Values.Any(p => p.Contains(G(l, b)))) Add(G(l, b)); // already passed: done its job
            foreach (var name in d.objects) AddRoad(name);
            // an object that went ahead already passed some of its squares; a tile broken there since isn't needed again
            void AddRoad(string name)
            {
                var gone = st.passed.TryGetValue(name, out var p) ? new List<Vector2Int>(p) : new List<Vector2Int>();
                foreach (var sq in fam.roads.First(r => r.objectName == name).squares)
                {
                    var c = G(l, sq);
                    if (gone.Remove(c)) continue;
                    if (c != last) Add(c);
                }
            }
            Add(last);
            // the delivery's last tile delivers it (the completing square, or the last missing one if that's already down)
            if (steps.Count > start) { var s = steps[steps.Count - 1]; s.completes = true; steps[steps.Count - 1] = s; }
        }

        // tiles for each square from what's left, + tiles earned along the way; plain tiles before + where both fit
        var left = new Dictionary<TileType, int>(st.supply);
        bool Assign(int i)
        {
            if (i == steps.Count) return true;
            var step = steps[i];
            bool earns = step.completes && deliveries[step.delivery].objects.Length >= 2;
            bool Preferred(TileType t) => prefer != null && prefer.TryGetValue(step.square, out var p) && p == t;
            foreach (var t in allowed[step.square].OrderBy(t => !Preferred(t)).ThenBy(t => t == TileType.Cross))
            {
                if (left.GetValueOrDefault(t) <= 0) continue;
                left[t]--;
                if (earns) left[TileType.Cross] = left.GetValueOrDefault(TileType.Cross) + 1;
                step.tile = t; steps[i] = step;
                if (Assign(i + 1)) return true;
                if (earns) left[TileType.Cross]--;
                left[t]++;
            }
            return false;
        }
        return Assign(0) ? steps : null;
    }

}
