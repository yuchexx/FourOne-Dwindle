using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// the "Basics" tutorial (on the level with l.tutorial = "Basics"): for now only the welcome, the objects, their rings
// (the goal) and the tiles, a line each; then its coach guides the player to deliver Red and Blue together, and
// shows the + it earns
public static partial class Tutorials
{
    static List<TutorialStep> Basics(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Welcome!\nThis is a tutorial level.",
            skippable = true, // Skip: past the objects, the rings and the tiles too
            // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
        },
        new TutorialStep
        {
            text = "These are the objects.",
            targets = new[] { Target.Object(game, "Red"), Target.Object(game, "Blue"), Target.Object(game, "Green") },
        },
        new TutorialStep
        {
            text = "Bring each object to the ring of its color.",
            targets = new[] { Target.Ring(game, "Red"), Target.Ring(game, "Blue"), Target.Ring(game, "Green") },
        },
        new TutorialStep
        {
            text = "Place tiles to build their paths.",
            targets = new[] { Target.Supply(game) },
            buttonText = "End",
        },
    };

    // the way each object has to go at a + (its road: Red straight up, Blue straight left, Green right to its ring)
    static readonly Dictionary<string, Dir> BasicsWays = new() { { "Red", Dir.N }, { "Blue", Dir.W }, { "Green", Dir.E } };

    // after the steps, until Red and Blue are delivered together: their roads drawn on the board, and Green's square
    // closed, so the one + goes where both roads cross (Green's + is the one the combo earns). delivering only one of
    // them starts the level over, with a note on what to try. once they're delivered together (their lines go as they
    // arrive), each tile their deliveries break gets a step as it breaks, one for Red's and one for Blue's; then a step
    // shows the + earned, and one in the middle sends the player on to Green: its square opens, its X fading out, and
    // its road is drawn
    static TutorialCoach BasicsCoach(GameManager game)
    {
        return new TutorialCoach
        {
            onLoaded = () =>
            {
                game.ShowRoadLine("Red");  // straight up to its ring
                game.ShowRoadLine("Blue"); // straight left to its ring
                game.CloseSquare(0, 1, 0); // the bottom-right square, Green's
                game.HoldEarnedFlash(true); // the + slot flashes once the step framing it is up (doneSteps)
            },
            onAction = a =>
            {
                // at a +, wherever it is: the only right way for each is toward its ring (Red up, Blue left, Green right)
                if (a.kind == GameActionKind.ChooseDirection && a.objectName != null &&
                    BasicsWays.TryGetValue(a.objectName, out var way) && a.direction != way)
                    return CoachReply.Undo;
                if (a.kind != GameActionKind.Delivered || a.arrivals == null) return CoachReply.None;
                bool red = a.arrivals.Any(x => x.objectName == "Red"), blue = a.arrivals.Any(x => x.objectName == "Blue");
                return red && blue ? CoachReply.Done : red || blue ? CoachReply.Reset : CoachReply.None;
            },
            resetNote = "Place the + last to deliver both together. Give it a try!",
            undoNote = "Please choose the right direction.",
            noteAbove = new[] { Target.Supply(game), Target.Undo(game) }, // the supply bar, and Undo and Restart beside it
            // Red and Blue delivered together: as each one's tile breaks, before the next, the square it was on
            removalSteps = (arrival, together) =>
                !arrival.broke || !together.Contains("Red") || !together.Contains("Blue") ? null : new List<TutorialStep>
                {
                    new TutorialStep
                    {
                        text = $"{arrival.objectName} is delivered,\nso a random tile from its path is removed.",
                        targets = new[] { Target.Cell(game, arrival.board, arrival.square.x, arrival.square.y) },
                        delay = 0.3f, // the tile is seen to go first
                    },
                },
            doneSteps = new()
            {
                new TutorialStep
                {
                    text = "Delivering together earned you\nan extra + tile!",
                    targets = new[] { Target.Slot(game, TileType.Cross) }, // now x1 again: the + used, plus the one earned
                    onShow = () => game.FlashEarnedPlus(0.4f), // the + slot's flash, a moment after the step is up
                },
                new TutorialStep
                {
                    text = "Now deliver Green!",
                    // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
                    buttonText = "End",
                    onDone = () =>
                    {
                        game.OpenSquare(0, 1, 0); // Green's square, closed till now: its X fades out
                        // Green's road: up into the bottom-right square, and right to its ring, on the + just earned
                        game.ShowRoadLine("Green", (0, 1, 0));
                    },
                },
            },
        };
    }

    // once Green is in too: everything that scored
    static TutorialRecap BasicsRecap => new() { intro = "Well done!\nLet's look at your score." };
}
