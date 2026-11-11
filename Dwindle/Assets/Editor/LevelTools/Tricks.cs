using System; using System.Collections.Generic; using System.Linq; using System.Runtime.CompilerServices; using UnityEngine;

// a trick a best plan can use: whether a plan uses it, and how to search without it
class LevelTrick
{
    public string name;                        // as the solutions list it
    public Func<LevelData, Plan, bool> uses;
    public Action<Solver2, LevelData> forbid;  // the search without it
}

// every trick in one place: the Level Maker's checks, its "Has" list, the end window's tips and the solutions' trick
// list all ask here, and each level's search without a trick runs once (kept per level)
static class Tricks
{
    public static readonly LevelTrick Together = new LevelTrick
    {
        name = "Delivered together", uses = (l, p) => p.events.Any(e => e.Count >= 2), forbid = (s, l) => s.allowCombos = false,
    };
    // out through a door and back to the object's own board
    public static readonly LevelTrick Detour = new LevelTrick
    {
        name = "Detour", forbid = (s, l) => s.noDetours = true,
        uses = (l, p) => Enumerable.Range(0, l.objects.Count).Any(i => l.objects[i].startBoard == l.objects[i].destBoard && p.routes[i].doors > 0),
    };
    public static readonly LevelTrick BuildAhead = new LevelTrick
    {
        name = "Build ahead", uses = (l, p) => p.BuildsAhead, forbid = (s, l) => s.filter = x => !x.BuildsAhead,
    };
    // one delivery needs two later objects built ahead past its tiles
    public static readonly LevelTrick DoubleBuildAhead = new LevelTrick
    {
        name = "Double build ahead", uses = Gen.Abcd, forbid = (s, l) => s.filter = x => !Gen.Abcd(l, x),
    };
    public static readonly LevelTrick EarnedPlus = new LevelTrick
    {
        name = "Earned +", uses = UsesEarnedPlus, forbid = (s, l) => s.filter = x => !UsesEarnedPlus(l, x),
    };
    // placing the bridge at all (on some levels it's laid as a straight, so an I is free elsewhere)
    public static readonly LevelTrick Bridge = new LevelTrick
    {
        name = "Bridge", uses = (l, p) => p.tiles.Values.Contains(TileType.Bridge),
        forbid = (s, l) => s.supply = Solver2.Supply(l, TileType.Bridge, set: 0),
    };
    // two routes pass through the same square (one tile serves both)
    public static readonly LevelTrick SharedTiles = new LevelTrick
    {
        name = "Shared tiles", uses = (l, p) => SharesTiles(p), forbid = (s, l) => s.noSharing = true,
    };

    // three or more arriving together (the way out of a pairs trap); without it, groups of two at most
    public static readonly LevelTrick BigGroup = new LevelTrick
    {
        name = "Big group", uses = (l, p) => p.events.Any(e => e.Count >= 3), forbid = (s, l) => s.filter = x => x.events.All(e => e.Count <= 2),
    };

    // the solutions' trick list, in its order
    public static readonly LevelTrick[] InSolutions = { Together, Detour, BuildAhead, EarnedPlus, Bridge };

    // the plan uses more + tiles than the level starts with, so at least one is earned
    public static bool UsesEarnedPlus(LevelData l, Plan x) => x.tiles.Values.Count(t => t == TileType.Cross) > l.inventory.GetValueOrDefault(TileType.Cross);

    static bool SharesTiles(Plan x)
    {
        var seen = new HashSet<Vector2Int>();
        foreach (var r in x.routes)
            foreach (var c in r.cells)
                if (!seen.Add(c)) return true;
        return false;
    }

    // the level's best plan without the trick; null: it can't be finished without it. kept per level object (a level's
    // layout and supply don't change once it's made, so the answer doesn't either)
    static readonly ConditionalWeakTable<LevelData, Dictionary<LevelTrick, Plan>> found = new();
    public static Plan BestWithout(LevelData l, LevelTrick t)
    {
        var known = found.GetValue(l, _ => new Dictionary<LevelTrick, Plan>());
        lock (known) if (known.TryGetValue(t, out var kept)) return kept;
        var solver = new Solver2(l);
        t.forbid(solver, l);
        var best = solver.Best();
        lock (known) known[t] = best;
        return best;
    }

    // the level can't be finished without it
    public static bool NeededToFinish(LevelData l, LevelTrick t) => BestWithout(l, t) == null;
    // without it, no plan reaches this score (or none finishes)
    public static bool NeededFor(LevelData l, LevelTrick t, int score) => !(BestWithout(l, t) is Plan w) || w.score < score;
    // without it the level can still be finished, but only below this score
    public static bool OnlyForScore(LevelData l, LevelTrick t, int score) => BestWithout(l, t) is Plan w && w.score < score;
}
