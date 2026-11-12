using System;
using System.Collections.Generic;
using UnityEngine;

// where a step's text bubble goes, next to its area
public enum BubbleSide { Auto, Below, Above, Left, Right, Center }

// one page of a tutorial: its text in a bubble over a dim, and a Next button that ends it (nothing else can be clicked)
public class TutorialStep
{
    public string text;
    // what's left undimmed and framed: game elements (see Target), each framed where it is right now, so a layout
    // change doesn't move the frames off. none = dim everything, the bubble in the middle of the screen
    public Func<RectTransform>[] targets;
    public BubbleSide bubble = BubbleSide.Auto; // Auto = below the area, or above it if there's no room
    public Func<RectTransform>[] bubbleBy; // the bubble goes next to these only, not all the frames (see Target)
    public float delay;          // seconds to wait before the step shows; nothing can be clicked meanwhile
    public float bubbleWidth;    // 0 = as wide as its text, up to the Bubble Width in TutorialController's Style; below 0 = no limit (one line)
    public string buttonText;    // the Next button's label; null = "Next"
    public bool skippable;       // a Skip button next to Next, passing this run of Next steps (see TutorialController.SkipFrom)
    public bool backable;        // a Back button left of Next (never on the first step): the step before shows again
    public Action onShow;        // runs as the step shows, to set the game up for what the step teaches
    public Action onDone;        // runs as the step ends, e.g. to undo what onShow changed
}

// game elements for TutorialStep.targets. each is looked up again every frame, so it can move, and it can be
// missing for a while (not made yet, or hidden): it just isn't framed then. objects and rings go by name, as in Levels.cs
public static class Target
{
    // the supply bar with every tile slot
    public static Func<RectTransform> Supply(GameManager game) =>
        () => game.hud.supplyDock != null ? game.hud.supplyDock : game.hud.slotsRoot;

    // an object's circle, wherever it is now
    public static Func<RectTransform> Object(GameManager game, string name) => () => game.ObjectRect(name);

    // an object's ring
    public static Func<RectTransform> Ring(GameManager game, string name) => () => game.RingRect(name);

    // a board's square, as (board, x, y) as in Levels.cs
    public static Func<RectTransform> Cell(GameManager game, int board, int x, int y) => () => game.CellRect(board, x, y);

    // a tile type's card in the supply bar, its count included
    public static Func<RectTransform> Slot(GameManager game, TileType tile) => () => game.hud.SlotRect(tile);

    // a door, by its letter and which end of the pair (0 = the first in Levels.cs): its bar and its letter's badge,
    // which overlap, so they make one frame
    public static Func<RectTransform>[] Door(GameManager game, string label, int which) => new Func<RectTransform>[]
    {
        () => game.DoorRect(label, which, false),
        () => game.DoorRect(label, which, true),
    };

    // the score history's row `index` from the top (0 = the newest), while the history is open
    public static Func<RectTransform> ScoreRow(GameManager game, int index) => () => game.hud.ScoreRow(index);

    // the hint button. nothing in the HUD points at it, so it's found in the scene the first time and kept
    static HintButton hintButton;
    public static Func<RectTransform> Hint(GameManager game) => () =>
    {
        if (hintButton == null) hintButton = UnityEngine.Object.FindAnyObjectByType<HintButton>();
        return hintButton != null && hintButton.button != null ? (RectTransform)hintButton.button.transform : null;
    };

    // the Undo button (next to Restart, in the bottom-right corner)
    public static Func<RectTransform> Undo(GameManager game) =>
        () => game.hud.undoButton != null ? (RectTransform)game.hud.undoButton.transform : null;
}

// tutorials by name, one file each (Tutorials.Basics.cs, Tutorials.Doors.cs, Tutorials.Bridge.cs, Tutorials.Routing.cs, Tutorials.Pairs.cs, Tutorials.BuildAhead.cs); a level picks one with LevelData.tutorial
public static partial class Tutorials
{
    // a tutorial's name -> its steps, made with the running game so steps can check it or set it up. a level picks its
    // tutorial by name (LevelData.tutorial, set in Levels.cs), so it stays with its level when levels are inserted or moved
    static readonly Dictionary<string, Func<GameManager, List<TutorialStep>>> ByName = new()
    {
        { "Basics", Basics },
        { "Doors", Doors },
        { "Bridge", Bridge },
        { "Routing", Routing },
        { "Pairs", Pairs },
        { "BuildAhead", BuildAhead },
    };

    // a tutorial's name -> its coach, for tutorials that have one (it takes over once the steps end)
    static readonly Dictionary<string, Func<GameManager, TutorialCoach>> CoachByName = new()
    {
        { "Basics", BasicsCoach },
        { "Doors", DoorsCoach },
    };

    // a tutorial's name -> its score recap, for tutorials that have one (see TutorialRecap)
    static readonly Dictionary<string, TutorialRecap> RecapByName = new()
    {
        { "Basics", BasicsRecap },
        { "Doors", DoorsRecap },
    };

    // null when the level has no tutorial
    public static List<TutorialStep> For(LevelData level, GameManager game) =>
        !string.IsNullOrEmpty(level.tutorial) && ByName.TryGetValue(level.tutorial, out var make) ? make(game) : null;

    // null when the level's tutorial has no coach
    public static TutorialCoach CoachFor(LevelData level, GameManager game) =>
        !string.IsNullOrEmpty(level.tutorial) && CoachByName.TryGetValue(level.tutorial, out var make) ? make(game) : null;

    // null when the level's tutorial has no recap
    public static TutorialRecap RecapFor(LevelData level) =>
        !string.IsNullOrEmpty(level.tutorial) && RecapByName.TryGetValue(level.tutorial, out var recap) ? recap : null;
}

// the score played back before the end window, once the level ends with everyone delivered (TutorialController): a
// step for each thing that scored, framing what scored and its rows in the score's history. it waits for that level
// end from when the coach is Done, or from when the steps end if the tutorial has no coach
public class TutorialRecap
{
    public string intro;                 // a first step in the middle of the screen, with Skip; null = none
    public Func<ScoreEntry, bool> which; // the score's entries it shows; null = all of them
}

// what a coach makes of a player action
public enum CoachReply
{
    None,  // nothing to do
    Reset, // the attempt went the wrong way: the level starts over (the steps don't) and resetNote shows
    Undo,  // a slip: the last placement is undone (as with the Undo button) and undoNote shows
    Done,  // the player did what it teaches: its guides come down for good, doneSteps show; it watches on (Undo can
           // still come) until the level ends
}

// guidance that stays after a tutorial's steps end, until the player has done what it teaches: no dim and no bubble,
// just guides on the board. it's set up again each time its level loads (a restart, or its own reset); leaving the
// level pauses it, coming back picks it up again. TutorialController runs it
public class TutorialCoach
{
    public Action onLoaded;                       // the level's board is ready (just loaded, or the steps just ended): put the guides up
    public Func<GameAction, CoachReply> onAction; // each player action, once it has taken effect (GameManager.ActionDone)
    public string resetNote;                      // shown in the corner after a Reset
    public string undoNote;                       // shown in the corner after an Undo
    public Func<GameAction, string> undoNoteFor;  // the note for the action that called for the Undo; null (or null back) = undoNote
    public Func<RectTransform>[] noteAbove;       // what the note stays above, so it covers none of it (see Target)
    public Action onDone;                         // on Done: take the guides down
    public List<TutorialStep> doneSteps;          // shown after Done, like the tutorial's steps (e.g. what the player just earned)
    // until Done: as each delivered object's tile breaks (the delivered object, everyone delivered with it), steps to
    // show it before the next one breaks; null = carry on (GameManager.onRemoval)
    public Func<Arrival, string[], List<TutorialStep>> removalSteps;
}
