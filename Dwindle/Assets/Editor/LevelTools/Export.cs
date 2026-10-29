using System; using System.Collections.Generic; using System.IO; using System.Linq; using UnityEngine;

// the end-window nudge toward the 3rd star (bestPlanHint) and tile names, for the Level Maker
static class Export
{
    public static string TileText(TileType t) => t switch
    {
        TileType.Cross => "+", TileType.Bridge => "bridge", TileType.StraightH => "I-h", TileType.StraightV => "I-v",
        TileType.CornerNE => "L N-E", TileType.CornerES => "L E-S", TileType.CornerSW => "L S-W", TileType.CornerWN => "L W-N",
        TileType.TeeNES => "T N-E-S", TileType.TeeESW => "T E-S-W", TileType.TeeSWN => "T S-W-N", _ => "T W-N-E",
    };

    const string PairsHint = "Splitting into pairs breaks a junction someone still needs. The best plan brings more of them in together.";
    const string DetourHint = "The best plan uses a door as a shortcut back to an object's own board.";
    const string TwoRoutesHint = "The best plan builds parts of two routes early, so both pass shared tiles before anyone is delivered.";
    const string OneRouteHint = "The best plan builds part of one route early, so it passes a shared tile before anyone is delivered.";
    const string EarnedHint = "The best plan delivers two objects together early to earn a + tile.";
    const string GroupHint = "The best plan delivers some objects together.";
    const string FewerTilesHint = "The best plan uses fewer tiles.";

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
