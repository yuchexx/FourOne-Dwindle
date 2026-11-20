using System.Collections.Generic;

// the "Intro" tutorial (on the level with l.tutorial = "Intro"): the welcome, the object, its ring (the goal), the
// supply bar and the 3 stars a tutorial level needs, a line each; then the player plays the level, and its coach
// shows the tile Red's delivery removes; at the level's end, a step on what the delivery scored.
// written for a level with one object, Red
public static partial class Tutorials
{
    static List<TutorialStep> Intro(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Welcome!\nThis is a tutorial level.",
            skippable = true, // Skip: the rest of the steps go by unseen
            // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
        },
        new TutorialStep
        {
            text = "This is an object.",
            targets = new[] { Target.Object(game, "Red") },
        },
        new TutorialStep
        {
            text = "Bring it to the ring of its color.",
            targets = new[] { Target.Ring(game, "Red") },
        },
        new TutorialStep
        {
            text = "Place tiles to build its path.",
            targets = new[] { Target.Supply(game) },
        },
        new TutorialStep
        {
            text = "Tutorial levels need 3 stars to go on.\nUse Hint if needed.",
            targets = new[] { Target.Hint(game) },
            buttonText = "End",
        },
    };

    // after the steps, until Red is delivered: as its delivery breaks its tile, a step on the square it was on (as the
    // UseEarned coach's) and one saying Settings can turn that preview on or off, before the level ends and IntroEnd
    // shows the score
    static TutorialCoach IntroCoach(GameManager game) => new()
    {
        onAction = a => a.kind == GameActionKind.Delivered ? CoachReply.Done : CoachReply.None,
        removalSteps = (arrival, together) => !arrival.broke ? null : new List<TutorialStep>
        {
            new TutorialStep
            {
                text = $"{arrival.objectName} is delivered,\nso the most important tile on its path is removed.\nSo plan ahead!",
                targets = new[] { Target.Cell(game, arrival.board, arrival.square.x, arrival.square.y) },
                delay = 0.3f, // the tile is seen to go first
            },
            new TutorialStep
            {
                text = "You can choose in Settings\nwhether to preview which tiles will be removed.",
                targets = new[] { Target.Settings(game) },
                // right of it: the menu opens upward from the bottom-left corner, so above it (Auto) would cover its top item
                bubble = BubbleSide.Right,
                onShow = () => game.hud.HoldMoreMenu(true), // the "..." menu opened, so its Settings button shows
                onDone = () => game.hud.HoldMoreMenu(false),
            },
        },
    };

    // once Red is delivered and the level ends: what that delivery scored, framed on its ring (its points over it) and
    // on its row in the score's history, before the end window shows
    static List<TutorialStep> IntroEnd(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Congrats!\nThis is the score for\ndelivering an object.",
            targets = new[] { Target.Ring(game, "Red"), Target.ScoreRow(game, 0) }, // the only row: the newest
            bubbleBy = new[] { Target.ScoreRow(game, 0) }, // left of the row, out of the board's way
            bubble = BubbleSide.Left,
            pops = new[] { new TutorialPop { text = $"+{game.pointsPerDelivery}", near = new[] { Target.Ring(game, "Red") } } },
            buttonText = "End",
        },
    };
}
