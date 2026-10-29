using System;
using System.Collections.Generic;
using UnityEngine;

// how a tutorial step ends.
// Action: the game reports the player action the step asks for (see TutorialStep.action). use it for every step
// where the player plays: a click that misses changes nothing in the game, so it doesn't end the step either.
// ClickInArea: any click released inside the area, whether the game acted on it or not
public enum Advance { NextButton, ClickInArea, Condition, Action }

// where a step's text bubble goes, next to its area
public enum BubbleSide { Auto, Below, Above, Left, Right, Center }

// one page of a tutorial
public class TutorialStep
{
    public string text;
    public Rect? area;           // left undimmed, in 1600x900 units from the top-left corner; null = dim everything
    public Rect[] areas;         // several areas at once, each framed; used instead of area when set
    public bool clickable;       // can the player click inside the area
    public bool hoverOnly;       // the player can hover inside the area, but the game ignores clicks (GameManager.LockInput)
    public Advance advance = Advance.NextButton;
    public Func<bool> condition; // for Advance.Condition: the step ends once this is true
    public Func<GameAction, bool> action; // for Advance.Action: the step ends on a reported action this accepts (see Did)
    public BubbleSide bubble = BubbleSide.Auto; // Auto = below the area, or above it if there's no room
    public float delay;          // seconds to wait before the step shows; nothing can be clicked meanwhile
    public float bubbleWidth;    // 0 = the Bubble Width in TutorialController's Style
    public string buttonText;    // the Next button's label; null = "Next"
    public Action onShow;        // runs as the step shows, to set the game up for what the step teaches
    public Action onDone;        // runs as the step ends, e.g. to undo what onShow changed
}

// the player actions Advance.Action steps wait for. squares are in their board's own coordinates, as in Levels.cs
public static class Did
{
    public static Func<GameAction, bool> Select(TileType tile) =>
        a => a.kind == GameActionKind.SelectTile && a.tile == tile;

    public static Func<GameAction, bool> Place(TileType tile, int board, int x, int y) =>
        a => a.kind == GameActionKind.PlaceTile && a.tile == tile && a.board == board && a.square == new Vector2Int(x, y);

    public static Func<GameAction, bool> TakeBack(int board, int x, int y) =>
        a => a.kind == GameActionKind.TakeBack && a.board == board && a.square == new Vector2Int(x, y);

    // at a junction, or coming out of a door
    public static Func<GameAction, bool> Choose(string objectName, Dir direction) =>
        a => a.kind == GameActionKind.ChooseDirection && a.objectName == objectName && a.direction == direction;

    public static Func<GameAction, bool> Undo() => a => a.kind == GameActionKind.Undo;

    public static Func<GameAction, bool> ClickDoor(string label) =>
        a => a.kind == GameActionKind.ClickDoor && a.door == label;
}

// tutorials by name, one file each (Tutorials.Basics.cs, Tutorials.Doors.cs); a level picks one with LevelData.tutorial. get an area from TutorialController's
// "Log Selected Area" (with a 16:9 Game view)
public static partial class Tutorials
{
    // a tutorial's name -> its steps, made with the running game so steps can check it or set it up. a level picks its
    // tutorial by name (LevelData.tutorial, set in Levels.cs), so it stays with its level when levels are inserted or moved
    static readonly Dictionary<string, Func<GameManager, List<TutorialStep>>> ByName = new()
    {
        { "Basics", Basics },
        { "Doors", Doors },
    };

    // null when the level has no tutorial
    public static List<TutorialStep> For(LevelData level, GameManager game) =>
        !string.IsNullOrEmpty(level.tutorial) && ByName.TryGetValue(level.tutorial, out var make) ? make(game) : null;
}
