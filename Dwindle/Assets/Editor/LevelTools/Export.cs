using System; using System.Collections.Generic; using System.IO; using System.Linq; using UnityEngine;

// the end window's two tips (finishHint at 0 or 1 star, bestPlanHint at 2; sentences in Tips.cs) and tile names, for the Level Maker
static class Export
{
    public static string TileText(TileType t) => t switch
    {
        TileType.Cross => "+", TileType.Bridge => "bridge", TileType.StraightH => "I-h", TileType.StraightV => "I-v",
        TileType.CornerNE => "L N-E", TileType.CornerES => "L E-S", TileType.CornerSW => "L S-W", TileType.CornerWN => "L W-N",
        TileType.TeeNES => "T N-E-S", TileType.TeeESW => "T E-S-W", TileType.TeeSWN => "T S-W-N", _ => "T W-N-E",
    };

    // finish: the trick a player can't deliver everyone without (null: none). best: the trick they can skip and still
    // deliver everyone, just below the target; if every trick is needed just to finish, "fewer tiles" (they've done
    // the tricks already); with no trick at all, routing. a level needing several picks the first in this order
    public static (string finish, string best) Hints(Props p, LevelData l)
    {
        Plan Without(Func<Plan, bool> filter = null, Action<Solver2> setup = null)
        {
            var solver = new Solver2(l) { filter = filter };
            setup?.Invoke(solver);
            return solver.Best();
        }
        bool ToFinish(Func<Plan, bool> filter = null, Action<Solver2> setup = null) => Without(filter, setup) == null;
        bool ForTarget(Func<Plan, bool> filter = null, Action<Solver2> setup = null)
        {
            var w = Without(filter, setup);
            return w != null && w.score < p.score;
        }
        bool NoBridge()
        {
            var keep = l.inventory;
            l.inventory = new Dictionary<TileType, int>(keep) { [TileType.Bridge] = 0 };
            try { return ToFinish(); } finally { l.inventory = keep; }
        }

        bool together = p.groups.Any(g => g > 1);
        bool bridge = p.best.tiles.Values.Contains(TileType.Bridge);
        bool two = p.buildsAhead && Gen.Abcd(l, p.best);
        bool togetherToFinish = together && ToFinish(setup: s => s.allowCombos = false);
        bool detourToFinish = p.detour && ToFinish(setup: s => s.noDetours = true);
        bool bridgeToFinish = bridge && NoBridge();
        bool aheadToFinish = p.buildsAhead && ToFinish(x => !x.BuildsAhead);
        bool earnedToFinish = p.earnedCrossUsed && ToFinish(x => !LevelMaker.UsesEarnedPlus(l, x));

        string finish =
            p.pairTrap && togetherToFinish ? Tips.Pairs
            : bridgeToFinish && detourToFinish ? Tips.BridgeAndDetour
            : detourToFinish ? Tips.DetourToFinish
            : bridgeToFinish ? Tips.Bridge
            : aheadToFinish ? (two ? Tips.TwoRoutes : Tips.OneRoute)
            : earnedToFinish ? Tips.EarnedPlus
            : togetherToFinish ? Tips.TogetherToFinish
            : null;

        string best =
            p.pairTrap && !togetherToFinish ? Tips.Pairs
            : p.detour && !detourToFinish && ForTarget(setup: s => s.noDetours = true) ? Tips.DetourForScore
            : two && !aheadToFinish && ForTarget(x => !Gen.Abcd(l, x)) ? Tips.TwoRoutes
            : p.buildsAhead && !aheadToFinish && ForTarget(x => !x.BuildsAhead) ? Tips.OneRoute
            : p.earnedCrossUsed && !earnedToFinish && ForTarget(x => !LevelMaker.UsesEarnedPlus(l, x)) ? Tips.EarnedPlus
            : together && !togetherToFinish && ForTarget(setup: s => s.allowCombos = false) ? Tips.TogetherForScore
            : finish != null ? Tips.FewerTiles
            : together || p.buildsAhead || p.detour || bridge || p.earnedCrossUsed ? Tips.FewerTiles
            : Tips.Routing;
        return (finish, best);
    }

    // a tip as Levels.cs writes it: Tips.Name for one of Tips.cs's sentences, else the sentence in quotes
    public static string TipCode(string tip) =>
        Tips.NameOf(tip) is string name ? "Tips." + name : "\"" + tip.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
