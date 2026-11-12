using System.Collections.Generic;

// the "BuildAhead" tutorial (on the level with l.tutorial = "BuildAhead"): one step saying not to always aim for
// everyone at once, then the player plays the level on their own (no coach, no recap)
public static partial class Tutorials
{
    static List<TutorialStep> BuildAhead(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Don't always try to deliver everyone at once!",
            // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
            buttonText = "End",
        },
    };
}
