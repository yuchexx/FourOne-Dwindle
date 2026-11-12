using System.Collections.Generic;

// the "Pairs" tutorial (on the level with l.tutorial = "Pairs"): one step saying to deliver everyone at once at one
// junction, then the player plays the level on their own (no coach, no recap)
public static partial class Tutorials
{
    static List<TutorialStep> Pairs(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Try delivering all four at once.\nMake them meet at one junction.",
            // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
            buttonText = "End",
        },
    };
}
