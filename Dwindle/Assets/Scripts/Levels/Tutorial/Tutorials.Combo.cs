using System.Collections.Generic;
using System.Linq;

// the "Combo" tutorial (on the level with l.tutorial = "Combo"): two steps on delivering Red and Blue together, the +
// placed last; then its coach draws their roads and undoes a wrong way or starts the level over when only one of them
// arrives; at the level's end, a step on what the combo scored. written for Red and Blue crossing on one +.
// the "TCombo" and "BridgeCombo" tutorials are here too: one step each, a combo with T tiles only / with a bridge
public static partial class Tutorials
{
    static List<TutorialStep> Combo(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Let's look at how to make a combo!\nA combo scores more points.",
            // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
        },
        new TutorialStep
        {
            text = "Place the \"+\" tile last\nto deliver Red and Blue together.",
            // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
            buttonText = "End",
        },
    };

    // the "TCombo" tutorial (on the level with l.tutorial = "TCombo"): one step asking for a combo with T tiles only;
    // then the player plays the level on their own (no coach, no level-end steps)
    static List<TutorialStep> TCombo(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Try making a combo using only T tiles!",
            bubbleWidth = -1, // as wide as its text, on one line (a middle step is otherwise the Style's width, and wraps)
            // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
            buttonText = "End",
        },
    };

    // the "BridgeCombo" tutorial (on the level with l.tutorial = "BridgeCombo"): one step asking for a combo with the
    // bridge; then the player plays the level on their own (no coach, no level-end steps)
    static List<TutorialStep> BridgeCombo(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Try making a combo with a bridge!",
            bubbleWidth = -1, // as wide as its text, on one line (a middle step is otherwise the Style's width, and wraps)
            // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
            buttonText = "End",
        },
    };

    // the way each object has to go at the +: toward its ring (Red's is to the right, Blue's above)
    static readonly Dictionary<string, Dir> ComboWays = new() { { "Red", Dir.E }, { "Blue", Dir.N } };

    // after the steps, until Red and Blue are delivered together (as the UseEarned coach's): their roads drawn on the
    // board; a wrong way at the + is undone, and delivering only one of them starts the level over, each with a step
    // the first time and a note after that
    static TutorialCoach ComboCoach(GameManager game) => new()
    {
        onLoaded = () =>
        {
            game.ShowRoadLine("Red");  // straight right to its ring
            game.ShowRoadLine("Blue"); // straight up to its ring
            game.HoldEarnedFlash(true); // the + slot flashes once the step framing it is up (ComboEnd)
        },
        onAction = a =>
        {
            if (a.kind == GameActionKind.ChooseDirection && a.objectName != null &&
                ComboWays.TryGetValue(a.objectName, out var way) && a.direction != way)
                return CoachReply.Undo;
            if (a.kind != GameActionKind.Delivered || a.arrivals == null) return CoachReply.None;
            bool red = a.arrivals.Any(x => x.objectName == "Red"), blue = a.arrivals.Any(x => x.objectName == "Blue");
            return red && blue ? CoachReply.Done : red || blue ? CoachReply.Reset : CoachReply.None;
        },
        resetNote = "Place the + last to deliver both together. Give it a try!",
        undoNote = "Please choose the right direction.",
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
    };

    // at the level's end: first the + the combo earned (as the UseEarned coach's done step: its slot framed, and the earned
    // flash held till then, a moment after the step is up), then the combo's rings framed, its share over each
    // ("+50"), and its row in the score's history (found by its entry, wherever it is). no combo row: no steps.
    // the + step is here, not in the coach's doneSteps: delivering both ends the level, and the level-end steps would
    // cut doneSteps short
    static List<TutorialStep> ComboEnd(GameManager game)
    {
        var log = game.hud.GetScoreLog(); // newest first, as the history shows it
        int at = log.FindIndex(e => e.combo && e.objects != null);
        if (at < 0) return null;
        var row = Target.ScoreRow(game, at);
        var targets = new List<System.Func<UnityEngine.RectTransform>> { row };
        var pops = new List<TutorialPop>();
        foreach (var c in log[at].objects)
        {
            System.Func<UnityEngine.RectTransform> ring = () => game.RingRectOf(c);
            targets.Add(ring);
            pops.Add(new TutorialPop { text = $"+{game.comboPointsPerObject}", near = new[] { ring } });
        }
        return new()
        {
            new TutorialStep
            {
                text = "Delivering together earned you\nan extra + tile!",
                targets = new[] { Target.Slot(game, TileType.Cross) }, // now x1 again: the + used, plus the one earned
                onShow = () => game.FlashEarnedPlus(0.4f), // the + slot's flash, a moment after the step is up
            },
            new TutorialStep
            {
                text = $"Congrats!\nDelivering together scores\na combo bonus of {game.comboPointsPerObject} each.",
                targets = targets.ToArray(),
                bubbleBy = new[] { row }, // left of the row, out of the board's way
                bubble = BubbleSide.Left,
                pops = pops.ToArray(),
                buttonText = "End",
            },
        };
    }
}
