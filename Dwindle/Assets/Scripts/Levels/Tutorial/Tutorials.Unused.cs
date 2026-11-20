using System.Collections.Generic;

// the "Unused" tutorial (on the level with l.tutorial = "Unused"): one step on the L tile's card at the start; once
// the level ends with everyone delivered, one step on what the tiles left unused scored, before the end window shows
public static partial class Tutorials
{
    // one step at the start: the L tile's card framed; then the level plays as usual until its end
    // (TutorialController.AfterSteps)
    static List<TutorialStep> Unused(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "This is the L tile.",
            // the L's card in the supply bar, found by its tile type, so it's framed wherever it is (Compact
            // supply leaves some cards out, which moves it)
            targets = new[] { Target.Slot(game, TileType.CornerES) },
            buttonText = "End",
        },
    };

    // at the level's end: each supply card with tiles left framed, its points over it ("+75", or "+75×2"), and the
    // unused row in the score's history (logged last, so the newest). none left (all used): no step
    static List<TutorialStep> UnusedEnd(GameManager game)
    {
        var slots = new List<System.Func<UnityEngine.RectTransform>>();
        var pops = new List<TutorialPop>();
        foreach (var t in Tiles.SupplyOrder)
        {
            int n = game.TilesLeft(t);
            if (n <= 0) continue;
            var slot = Target.Slot(game, t);
            slots.Add(slot);
            string each = $"+{game.pointsPerUnusedTile}";
            pops.Add(new TutorialPop { text = n == 1 ? each : $"{each}×{n}", near = new[] { slot } });
        }
        if (slots.Count == 0) return null;
        var row = Target.ScoreRow(game, 0); // the unused row: the last one logged
        slots.Add(row);
        return new()
        {
            new TutorialStep
            {
                text = $"Congrats!\nEach tile left unused\nscores {game.pointsPerUnusedTile} points.",
                targets = slots.ToArray(),
                bubbleBy = new[] { row }, // left of the row, out of the board's way
                bubble = BubbleSide.Left,
                pops = pops.ToArray(),
                buttonText = "End",
            },
        };
    }
}
