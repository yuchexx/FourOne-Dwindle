using System.Collections.Generic;
using System.Linq;

// the "Doors" tutorial (on the level with l.tutorial = "Doors"): one step on the doors, then the player plays the level
// with its coach undoing slips; once it's done, its recap shows the points the door earned
public static partial class Tutorials
{
    static List<TutorialStep> Doors(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Doors teleport objects.",
            targets = Target.Door(game, "A", 0).Concat(Target.Door(game, "A", 1)).ToArray(), // door A on each board
            buttonText = "End",
        },
    };

    // the only squares Red can walk through: the left board's left square (into door A) and the right board's right
    // one (out of door A, to the ring). the door is a wall, so the two in between are no use
    static readonly (int board, int x, int y)[] DoorsSquares = { (0, 0, 0), (1, 1, 0) };

    // Red's road dotted on both boards (into door A, and out of its partner to the ring), and slips undone, with a note
    // on each: a tile on a square Red can't use, and the wrong way coming out of the door (left leads off the board,
    // which would end the level). Red's delivery is Done: on to the recap
    static TutorialCoach DoorsCoach(GameManager game) => new()
    {
        onLoaded = () => game.ShowRoadLine("Red", (0, 0, 0), (1, 1, 0)), // the two squares on either side of the doors
        onAction = a =>
        {
            if (a.kind == GameActionKind.PlaceTile && !DoorsSquares.Contains((a.board, a.square.x, a.square.y)))
                return CoachReply.Undo;
            if (a.kind == GameActionKind.ChooseDirection && a.objectName == "Red" && a.direction != Dir.E)
                return CoachReply.Undo;
            if (a.kind == GameActionKind.Delivered) return CoachReply.Done;
            return CoachReply.None;
        },
        undoNote = "Please choose the right direction.",
        undoNoteFor = a => a.kind == GameActionKind.PlaceTile ? "Red can't walk through that square. Try another one." : null,
        noteAbove = new[] { Target.Supply(game), Target.Undo(game) }, // the supply bar, and Undo and Restart beside it
    };

    // the delivery that went through the door: its row, "100 + 50 ="
    static TutorialRecap DoorsRecap => new() { which = e => e.door };
}
