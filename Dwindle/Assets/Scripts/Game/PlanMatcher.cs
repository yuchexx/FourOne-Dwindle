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
}

// an arrow the player picked: at a junction (square) or coming out of a door (door's letter)
public struct ArrowRecord
{
    public Vector2Int? square;
    public string door;
    public Dir way;
}

// one tile of a hint's plan, in the order it should go down
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

    // the k-th delivery happened as the plan's k-th: the same objects, on one of its squares, after its "before" tiles
    public static bool DeliveryFits(LevelData l, int family, int order, int k, DeliveryRecord d)
    {
        var deliveries = l.solution.families[family].orders[order].deliveries;
        if (k >= deliveries.Length) return false;
        var planned = deliveries[k];
        if (planned.objects.Length != d.objects.Length || planned.objects.Except(d.objects).Any()) return false;
        var completion = planned.completedBy.FirstOrDefault(c => G(l, c.at) == d.at);
        if (completion == null) return false;
        return completion.before.All(b => d.occupied.Contains(G(l, b)));
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

    // stage 3's ghosts: the steps not done yet. a step is done when its exact tile is on its square, unless that tile
    // made a delivery the plan didn't have (the first delivery off the plan, and every one after it)
    public static List<int> PendingSteps(LevelData l, int family, int order, List<PlanStep> steps, PlayerState st)
    {
        int ok = MatchingDeliveries(l, family, order, st.deliveries);
        var wrongMoment = new HashSet<Vector2Int>(st.deliveries.Skip(ok).Select(d => d.at));
        var res = new List<int>();
        for (int i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            bool done = st.tiles.TryGetValue(s.square, out var t) && t == s.tile && !wrongMoment.Contains(s.square);
            if (!done) res.Add(i);
        }
        return res;
    }

    // a plan's whole sequence from an empty board, with the level's own supply
    public static List<PlanStep> FromStart(LevelData l, int family, int order) =>
        NextSteps(l, family, order, new PlayerState { supply = Tiles.All.ToDictionary(t => t, t => l.inventory.GetValueOrDefault(t)) });

    // stage 2: who arrives in which delivery (1 = first; the same number = together), and the squares where groups meet
    public static List<(string objectName, int number)> Badges(LevelData l, int family, int order)
    {
        var res = new List<(string, int)>();
        var deliveries = l.solution.families[family].orders[order].deliveries;
        for (int k = 0; k < deliveries.Length; k++)
            foreach (var o in deliveries[k].objects) res.Add((o, k + 1));
        return res;
    }

    public static List<Vector2Int> Junctions(LevelData l, int family, int order) =>
        l.solution.families[family].orders[order].deliveries.Where(d => d.objects.Length >= 2)
            .SelectMany(d => d.completedBy.Select(c => G(l, c.at))).Distinct().ToList();

    // stage 3: the tiles still to place on this plan, in order: for each delivery still to come, its "before" tiles,
    // then the arriving objects' roads, then the tile that completes it. each gets a tile the supply allows at that
    // point (a + earned by delivering together only after that delivery). null when the supply can't cover it
    public static List<PlanStep> NextSteps(LevelData l, int family, int order, PlayerState st)
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
            foreach (var t in allowed[step.square].OrderBy(t => t == TileType.Cross))
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

    // the fixed answer as steps (from an empty board)
    public static List<PlanStep> AnswerSteps(LevelData l) =>
        l.solution.answer.Where(a => a.kind == GameActionKind.PlaceTile)
            .Select(a => new PlanStep { square = l.boards[a.board].origin + a.square, tile = a.tile.Value, delivery = -1 }).ToList();
}
