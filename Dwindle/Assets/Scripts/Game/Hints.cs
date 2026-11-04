using System.Linq;

// the hint button's three stages for the level being played (GameManager.Hints.cs uses them):
//   1. how many tiles the best plans use, under the score
//   2. the delivery order of the plan closest to the board: who arrives first and who together, and where groups meet
//   3. that plan's tiles as numbered ghosts, one more each press
// stage 3 costs the purple stars. undo never changes the hints; restarting, replaying or leaving the level clears them
public class Hints
{
    public const int Stages = 3;
    public int Stage { get; set; }    // 0 = none used yet
    public int Revealed { get; set; } // stage 3: how many ghosts have been shown
    public bool PurpleLost => Stage >= Stages;

    public void Clear() { Stage = 0; Revealed = 0; }

    // stage 1: how many tiles the best plans place, and how many are left in the supply at the end (the supply plus the
    // + tiles earned by delivering together, on the answer's plan)
    public static string TilesText(LevelData l)
    {
        int left = TilesLeft(l);
        return left >= 0 ? $"Best plans use {l.bestTiles} tiles and leave {left} unused" : $"Best plans use {l.bestTiles} tiles";
    }

    public static int TilesLeft(LevelData l)
    {
        var s = l.solution;
        if (s == null) return -1;
        int earned = s.families[s.answerFamily].orders[s.answerOrder].deliveries.Count(d => d.objects.Length >= 2);
        return l.inventory.Values.Sum() + earned - l.bestTiles;
    }
}
