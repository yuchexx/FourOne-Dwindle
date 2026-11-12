using System.Collections.Generic;

// the "Bridge" tutorial (on the level with l.tutorial = "Bridge"): one step on the bridge, then the player plays the
// level on their own (no coach, no recap)
public static partial class Tutorials
{
    static List<TutorialStep> Bridge(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "This is the bridge tile.\nUse it to see how it works!",
            // the bridge's card in the supply bar, found by its tile type, so it's framed wherever it is (Compact
            // supply leaves some cards out, which moves it)
            targets = new[] { Target.Slot(game, TileType.Bridge) },
            buttonText = "End",
        },
    };
}
