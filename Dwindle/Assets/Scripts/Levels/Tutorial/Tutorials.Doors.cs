using System;
using System.Collections.Generic;
using System.Linq;

// the "Doors" tutorial (on the level with l.tutorial = "Doors"): one step framing both pairs of doors; then the player
// plays the level, and its coach shows, once each, that Blue coming out of the right board's door B (not on the
// board's edge) can go either way (a wrong way is undone back to its arrows), and that Red coming out of door A (on
// the board's edge) goes inward by itself; and as the first one is delivered, what using doors scored (no level-end
// steps)
public static partial class Tutorials
{
    static List<TutorialStep> Doors(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Doors teleport objects.",
            targets = Target.Door(game, "A", 0).Concat(Target.Door(game, "A", 1))
                .Concat(Target.Door(game, "B", 0)).Concat(Target.Door(game, "B", 1)).ToArray(), // both ends of A and of B
            buttonText = "End",
        },
    };

    // no guides. once each: Blue's way out of the right board's door B, as its arrows come up (a wrong way, E, goes
    // back to those arrows: a popup the first time, the corner note after), and Red coming out of door A on the right
    // board's edge, held there facing in; and the first delivery through doors (whoever arrives first), held as its
    // tile breaks, on what it scored. Done once Blue has picked its way (W, its only arrows on this level) and both
    // other steps have shown, in any order (the hooks stay on till then). any tile but the corner (L E-S) on Blue's
    // square past door B, the right board's bottom-left, is undone too (the whole placement), with its own popup and note
    static TutorialCoach DoorsCoach(GameManager game)
    {
        bool blueShown = false, redShown = false, blueRight = false, scoreShown = false;
        const string wrongWay = "Blue can't reach its ring this way.";
        const string wrongTile = "Blue can't reach its ring with this tile.";
        var wrongTileSteps = new List<TutorialStep> // one list, so it shows once (TutorialCoach.firstUndoStepsFor)
        {
            new TutorialStep
            {
                text = wrongTile,
                bubbleWidth = -1, // as wide as its text, on one line (a middle step is otherwise the Style's width, and wraps)
                // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
                buttonText = "Try again",
            },
        };
        bool WrongTile(GameAction a) => a.kind == GameActionKind.PlaceTile && a.tile != TileType.CornerES &&
                                        a.board == 1 && a.square == new UnityEngine.Vector2Int(0, 0);
        return new TutorialCoach
        {
            onAction = a =>
            {
                if (WrongTile(a)) return CoachReply.Undo;
                if (a.kind == GameActionKind.ChooseDirection && a.objectName == "Blue")
                {
                    if (a.direction != Dir.W) return CoachReply.Undo;
                    blueRight = true;
                }
                return blueRight && redShown && scoreShown ? CoachReply.Done : CoachReply.None;
            },
            undoToChoice = true, // a wrong way: back to Blue at the door with its arrows, not the whole placement
            undoNote = wrongWay,
            undoNoteFor = a => WrongTile(a) ? wrongTile : null, // null: undoNote
            firstUndoStepsFor = a => WrongTile(a) ? wrongTileSteps : null, // null: firstUndoSteps (the wrong way's)
            noteAbove = new[] { Target.Supply(game), Target.Undo(game) }, // the supply bar, and Undo and Restart beside it
            firstUndoSteps = new List<TutorialStep>
            {
                new TutorialStep
                {
                    text = wrongWay,
                    bubbleWidth = -1, // as wide as its text, on one line (a middle step is otherwise the Style's width, and wraps)
                    // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
                    buttonText = "Try again",
                },
            },
            doorSteps = (name, door) =>
            {
                if (name == "Blue" && door == "B" && !blueShown)
                {
                    blueShown = true;
                    return new List<TutorialStep>
                    {
                        new TutorialStep
                        {
                            text = "This door isn't on the board's edge,\nso you can choose which way to come out.",
                            targets = new[] { Target.Object(game, "Blue"), Target.Arrow(game, Dir.W), Target.Arrow(game, Dir.E) },
                            buttonText = "End",
                        },
                    };
                }
                if (name == "Red" && door == "A" && !redShown)
                {
                    redShown = true;
                    return new List<TutorialStep>
                    {
                        new TutorialStep
                        {
                            text = "This door is on the board's edge,\nso objects coming out of it go inward automatically.",
                            targets = new[] { Target.Object(game, "Red") },
                            buttonText = "End",
                        },
                    };
                }
                return null;
            },
            // the first delivery that used doors: its ring ("+100" over it), the door it came out of ("+50") and its row in
            // the score's history (the newest: it's logged as it arrives, before its tile breaks), held open meanwhile
            removalSteps = (arrival, together) =>
            {
                if (scoreShown || arrival.doors <= 0) return null;
                var log = game.hud.GetScoreLog();
                if (log.Count == 0 || log[0].objects == null || log[0].objects.Length == 0) return null;
                scoreShown = true;
                var color = log[0].objects[0]; // the rows know objects by color
                var door = new Func<UnityEngine.RectTransform>[] { () => game.DoorOutRect(color, false), () => game.DoorOutRect(color, true) };
                var row = Target.ScoreRow(game, 0);
                return new List<TutorialStep>
                {
                    new TutorialStep
                    {
                        text = $"Congrats!\nDelivering an object using doors\nscores {game.pointsPerTeleport} extra.",
                        targets = door.Append(Target.Ring(game, arrival.objectName)).Append(row).ToArray(),
                        bubbleBy = new[] { row }, // left of the row, out of the board's way (as IntroEnd's)
                        bubble = BubbleSide.Left,
                        pops = new[] // as its row reads, "100 + 50 =": the delivery's over its ring, the door's over the door
                        {
                            new TutorialPop { text = $"+{game.pointsPerDelivery}", near = new[] { Target.Ring(game, arrival.objectName) } },
                            new TutorialPop { text = $"+{game.pointsPerTeleport}", near = door },
                        },
                        delay = 0.3f, // the delivery is seen first
                        onShow = () => game.hud.HoldScoreHistory(true), // open, so its row shows
                        onDone = () => game.hud.HoldScoreHistory(false),
                        buttonText = "End",
                    },
                };
            },
        };
    }
}
