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
        bool ToFinish(LevelTrick t) => Tricks.NeededToFinish(l, t);
        bool ForTarget(LevelTrick t) => Tricks.OnlyForScore(l, t, p.score);

        bool together = p.groups.Any(g => g > 1);
        bool bridge = Tricks.Bridge.uses(l, p.best);
        bool two = p.buildsAhead && Gen.Abcd(l, p.best);
        bool togetherToFinish = together && ToFinish(Tricks.Together);
        bool detourToFinish = p.detour && ToFinish(Tricks.Detour);
        bool bridgeToFinish = bridge && ToFinish(Tricks.Bridge);
        bool aheadToFinish = p.buildsAhead && ToFinish(Tricks.BuildAhead);
        bool earnedToFinish = p.earnedCrossUsed && ToFinish(Tricks.EarnedPlus);
        // pairs trap: pairs can't deliver everyone (the tip at 0 or 1 star), or they can but below the target (the tip at 2)
        bool pairsToFinish = p.pairTrap && ToFinish(Tricks.BigGroup);
        bool pairsForScore = p.pairTrap && !pairsToFinish && ForTarget(Tricks.BigGroup);

        string finish =
            pairsToFinish ? Tips.Pairs
            : bridgeToFinish && detourToFinish ? Tips.BridgeAndDetour
            : detourToFinish ? Tips.DetourToFinish
            : bridgeToFinish ? Tips.Bridge
            : aheadToFinish ? (two ? Tips.TwoRoutes : Tips.OneRoute)
            : earnedToFinish ? Tips.EarnedPlus
            : togetherToFinish ? Tips.TogetherToFinish
            : null;

        string best =
            pairsForScore ? Tips.Pairs
            : p.detour && !detourToFinish && ForTarget(Tricks.Detour) ? Tips.DetourForScore
            : two && !aheadToFinish && ForTarget(Tricks.DoubleBuildAhead) ? Tips.TwoRoutes
            : p.buildsAhead && !aheadToFinish && ForTarget(Tricks.BuildAhead) ? Tips.OneRoute
            : p.earnedCrossUsed && !earnedToFinish && ForTarget(Tricks.EarnedPlus) ? Tips.EarnedPlus
            : together && !togetherToFinish && ForTarget(Tricks.Together) ? Tips.TogetherForScore
            : finish != null ? Tips.FewerTiles
            : together || p.buildsAhead || p.detour || bridge || p.earnedCrossUsed ? Tips.FewerTiles
            : Tips.Routing;
        return (finish, best);
    }

    // a tip as Levels.cs writes it: Tips.Name for one of Tips.cs's sentences, else the sentence in quotes
    public static string TipCode(string tip) =>
        Tips.NameOf(tip) is string name ? "Tips." + name : "\"" + tip.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
