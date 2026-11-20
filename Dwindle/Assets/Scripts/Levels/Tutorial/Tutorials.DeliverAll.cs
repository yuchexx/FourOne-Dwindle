using System.Collections.Generic;

// the "DeliverAll" tutorial (on the level with l.tutorial = "DeliverAll"): one step saying to deliver everyone at once
// at one junction, then the player plays the level on their own (no coach, no recap). written for three objects
public static partial class Tutorials
{
    static List<TutorialStep> DeliverAll(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Try delivering all three at once.\nMake them meet at one junction.",
            // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
            buttonText = "End",
        },
    };
}
