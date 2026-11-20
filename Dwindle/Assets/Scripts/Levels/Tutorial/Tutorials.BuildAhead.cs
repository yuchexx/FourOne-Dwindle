using System.Collections.Generic;

// the "BuildAhead" tutorial (on the level with l.tutorial = "BuildAhead"): one step saying to build every route that
// shares tiles before a delivery, then the player plays the level on their own (no coach, no level-end steps)
public static partial class Tutorials
{
    static List<TutorialStep> BuildAhead(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Build all routes that share tiles before triggering a delivery.",
            // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
            buttonText = "End",
        },
    };
}
