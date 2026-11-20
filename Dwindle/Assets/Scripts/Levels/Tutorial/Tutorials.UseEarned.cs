using System.Collections.Generic;
using System.Linq;

// the "UseEarned" tutorial (on the level with l.tutorial = "UseEarned"): two steps saying the + a combo earns can be
// the key, and to deliver Red and Blue together first; then its coach keeps Green's square closed until they are, so
// the one + goes where their roads cross, and shows the + it earns as the one to deliver Green with (no recap).
// the "UseEarnedT" and "UseEarnedBridge" tutorials are here too: one step each, the + earned with T tiles / a bridge
public static partial class Tutorials
{
    static List<TutorialStep> UseEarned(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Sometimes the + earned from a combo is the key!",
            // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
        },
        new TutorialStep
        {
            text = "First, deliver Red and Blue together with a combo!",
            bubbleWidth = -1, // as wide as its text, on one line (a middle step is otherwise the Style's width, and wraps)
            // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
            buttonText = "End",
        },
    };

    // the "UseEarnedT" tutorial (on the level with l.tutorial = "UseEarnedT"): one step asking for the + to be earned
    // with T tiles; then the player plays the level on their own (no coach, no level-end steps)
    static List<TutorialStep> UseEarnedT(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Try delivering Red and Blue together to earn a + using T tiles!",
            bubbleWidth = -1, // as wide as its text, on one line (a middle step is otherwise the Style's width, and wraps)
            // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
            buttonText = "End",
        },
    };

    // the "UseEarnedBridge" tutorial (on the level with l.tutorial = "UseEarnedBridge"): as UseEarnedT, with the
    // bridge
    static List<TutorialStep> UseEarnedBridge(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Try delivering Red and Blue together to earn a + using a bridge!",
            bubbleWidth = -1, // as wide as its text, on one line (a middle step is otherwise the Style's width, and wraps)
            // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
            buttonText = "End",
        },
    };

    // the way each object has to go at a + (its road: Red straight up, Blue straight left, Green right to its ring)
    static readonly Dictionary<string, Dir> UseEarnedWays = new() { { "Red", Dir.N }, { "Blue", Dir.W }, { "Green", Dir.E } };

    // Green's square, as (board, x, y): closed until Red and Blue are delivered together, then it takes the + earned
    static readonly (int board, int x, int y) UseEarnedGreenSquare = (0, 1, 0);

    // after the steps, until Red and Blue are delivered together: Green's square closed, so the one + goes where both
    // roads cross (Green's + is the one the combo earns). a wrong way at a + is undone, and delivering only one of them
    // starts the level over, each with a step the first time and a note after that. once they're delivered together,
    // a step frames the + earned and sends the player on to Green: its square opens, its X fading out, and its road
    // is drawn. it watches on till the level ends: a wrong way for Green is undone as before (the same first step, so
    // a second wrong way anywhere gets the note), and a tile other than the + on Green's square is undone, with a
    // step of its own the first time and a note after that
    static TutorialCoach UseEarnedCoach(GameManager game)
    {
        var sq = UseEarnedGreenSquare;
        var wrongTileSteps = new List<TutorialStep> // one list, so it shows once (TutorialCoach.firstUndoStepsFor)
        {
            new TutorialStep
            {
                text = "Green can't reach its ring with this tile.",
                // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
                buttonText = "Try again",
            },
        };
        // a tile other than the + on Green's square (it's closed till Red and Blue are in, so only after that)
        bool WrongTile(GameAction a) => a.kind == GameActionKind.PlaceTile && a.tile != TileType.Cross &&
                                        a.board == sq.board && a.square == new UnityEngine.Vector2Int(sq.x, sq.y);
        return new TutorialCoach
        {
            onLoaded = () =>
            {
                game.CloseSquare(sq.board, sq.x, sq.y); // the bottom-right square, Green's
                game.HoldEarnedFlash(true); // the + slot flashes once the step framing it is up (doneSteps)
            },
            onAction = a =>
            {
                // at a +, wherever it is: the only right way for each is toward its ring (Red up, Blue left, Green right)
                if (a.kind == GameActionKind.ChooseDirection && a.objectName != null &&
                    UseEarnedWays.TryGetValue(a.objectName, out var way) && a.direction != way)
                    return CoachReply.Undo;
                if (WrongTile(a)) return CoachReply.Undo;
                if (a.kind != GameActionKind.Delivered || a.arrivals == null) return CoachReply.None;
                bool red = a.arrivals.Any(x => x.objectName == "Red"), blue = a.arrivals.Any(x => x.objectName == "Blue");
                return red && blue ? CoachReply.Done : red || blue ? CoachReply.Reset : CoachReply.None;
            },
            resetNote = "Place the + last to deliver both together. Give it a try!",
            undoNote = "Please choose the right direction.",
            undoNoteFor = a => WrongTile(a) ? "Green can't reach its ring with this tile." : null, // null: undoNote
            firstUndoStepsFor = a => WrongTile(a) ? wrongTileSteps : null, // null: firstUndoSteps (the wrong way's)
            // the first slip of each kind: a step in the middle of the screen instead of the note in the corner
            firstResetSteps = new()
            {
                new TutorialStep
                {
                    text = "Place the \"+\" tile last\nto deliver Red and Blue together.",
                    // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
                    buttonText = "Try again",
                },
            },
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
            doneSteps = new()
            {
                new TutorialStep
                {
                    text = "Now you can deliver Green!",
                    targets = new[] { Target.Slot(game, TileType.Cross) }, // now x1 again: the + used, plus the one earned
                    onShow = () => game.FlashEarnedPlus(0.4f), // the + slot's flash, a moment after the step is up
                    buttonText = "End",
                    onDone = () =>
                    {
                        game.OpenSquare(sq.board, sq.x, sq.y); // Green's square, closed till now: its X fades out
                        // Green's road: up into the bottom-right square, and right to its ring, on the + just earned
                        game.ShowRoadLine("Green", sq);
                    },
                },
            },
        };
    }
}
