using System.Collections.Generic;

// the "Routing" tutorial (on the level with l.tutorial = "Routing"): one step saying tiles can be limited, then the
// player plays the level on their own (no coach, no recap)
public static partial class Tutorials
{
    static List<TutorialStep> Routing(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Sometimes tiles are limited.\nTry delivering Red and Blue separately.",
            // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
            buttonText = "End",
        },
    };
}
