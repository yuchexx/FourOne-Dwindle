using System.Collections.Generic;

// the "Junction" tutorial (on the level with l.tutorial = "Junction"): one step on the +'s card, saying an object at
// a junction needs its way chosen; then its coach undoes a wrong way, with a step the first time and a note after that.
// the "TJunction" tutorial is here too: the T's card, then the same coach (WrongWayCoach)
public static partial class Tutorials
{
    static List<TutorialStep> Junction(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "This is the + tile.\nWhen an object reaches a junction,\nchoose which way it goes.",
            targets = new[] { Target.Slot(game, TileType.Cross) }, // the +'s card in the supply bar, wherever it is
            buttonText = "End",
        },
    };

    // the way each object has to go at the +: toward its ring (Red's is above the square)
    static TutorialCoach JunctionCoach(GameManager game) => WrongWayCoach(game, new() { { "Red", Dir.N } });

    // the "TJunction" tutorial (on the level with l.tutorial = "TJunction"): one step framing the T's card, naming it;
    // then the same coach as at the +
    static List<TutorialStep> TJunction(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "This is the T tile.",
            targets = new[] { Target.Slot(game, TileType.TeeESW) }, // the T's card in the supply bar, wherever it is
            buttonText = "End",
        },
    };

    // the way each object has to go at the T: toward its ring (Red's is right of the square)
    static TutorialCoach TJunctionCoach(GameManager game) => WrongWayCoach(game, new() { { "Red", Dir.E } });

    // until an object is delivered: a way chosen at a junction that isn't the object's way in `ways` (toward its
    // ring) is undone (the placement goes back, as with Undo), with a step in the middle the first time and a note in
    // the corner after that. objects not in `ways` can go any way
    static TutorialCoach WrongWayCoach(GameManager game, Dictionary<string, Dir> ways) => new()
    {
        onAction = a =>
        {
            if (a.kind == GameActionKind.ChooseDirection && a.objectName != null &&
                ways.TryGetValue(a.objectName, out var way) && a.direction != way)
                return CoachReply.Undo;
            return a.kind == GameActionKind.Delivered ? CoachReply.Done : CoachReply.None;
        },
        undoNote = "Please choose the right direction.",
        firstUndoSteps = new()
        {
            new TutorialStep
            {
                text = "That's the wrong direction.\nEach object has to head to its ring.",
                // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
                buttonText = "Try again",
            },
        },
        noteAbove = new[] { Target.Supply(game), Target.Undo(game) }, // the supply bar, and Undo and Restart beside it
    };
}
