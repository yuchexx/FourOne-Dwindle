using System; using System.Collections.Generic; using System.IO; using System.Linq; using UnityEngine;

// the end window's tip toward the 3rd star (bestPlanHint, shown after "Tip: ") and tile names, for the Level Maker
static class Export
{
    public static string TileText(TileType t) => t switch
    {
        TileType.Cross => "+", TileType.Bridge => "bridge", TileType.StraightH => "I-h", TileType.StraightV => "I-v",
        TileType.CornerNE => "L N-E", TileType.CornerES => "L E-S", TileType.CornerSW => "L S-W", TileType.CornerWN => "L W-N",
        TileType.TeeNES => "T N-E-S", TileType.TeeESW => "T E-S-W", TileType.TeeSWN => "T S-W-N", _ => "T W-N-E",
    };

    const string PairsHint = "pairs break a junction. Bring more in together.";
    const string DetourHint = "a door can be a shortcut home.";
    const string TwoRoutesHint = "start two routes early.";
    const string OneRouteHint = "start one route early.";
    const string EarnedHint = "deliver two together early to earn a +.";
    const string GroupHint = "deliver some together.";
    const string FewerTilesHint = "use fewer tiles.";

    // the end-window nudge for a player who finished below the target. without the level: the first trick the best
    // plan uses. with it: the first trick a player can skip and still finish, just not at the target, since that's
    // what someone who finished is missing (a trick needed just to finish, they've already done)
    public static string Hint(Props p, LevelData l = null)
    {
        if (l == null)
            return p.pairTrap ? PairsHint : p.detour ? DetourHint : p.buildsAhead ? OneRouteHint
                : p.earnedCrossUsed ? EarnedHint : p.groups.Any(g => g > 1) ? GroupHint : FewerTilesHint;

        // finishable without the trick, but only below this score
        bool ForTarget(Func<Plan, bool> without = null, Action<Solver2> setup = null)
        {
            var solver = new Solver2(l) { filter = without };
            setup?.Invoke(solver);
            var w = solver.Best();
            return w != null && w.score < p.score;
        }
        if (p.pairTrap) return PairsHint;
        if (p.detour && ForTarget(setup: s => s.noDetours = true)) return DetourHint;
        if (p.buildsAhead && Gen.Abcd(l, p.best) && ForTarget(x => !Gen.Abcd(l, x))) return TwoRoutesHint;
        if (p.buildsAhead && ForTarget(x => !x.BuildsAhead)) return OneRouteHint;
        if (p.earnedCrossUsed && ForTarget(x => !LevelMaker.UsesEarnedPlus(l, x))) return EarnedHint;
        return p.groups.Any(g => g > 1) ? GroupHint : FewerTilesHint;
    }

}
