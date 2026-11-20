using System.Collections.Generic;

// the "Detour" tutorial (on the level with l.tutorial = "Detour"): one step asking for a detour through the doors
// (out to the other board and back); then the player plays the level on their own (no coach, no level-end steps)
public static partial class Tutorials
{
    static List<TutorialStep> Detour(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Try a detour through the doors!",
            bubbleWidth = -1, // as wide as its text, on one line (a middle step is otherwise the Style's width, and wraps)
            // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
            buttonText = "End",
        },
    };
}
