using System;
using System.Collections.Generic;
using UnityEngine;

// where a step's text bubble goes, next to its area
public enum BubbleSide { Auto, Below, Above, Left, Right, Center, TopLeft } // TopLeft: a to-do under the level title

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
    public bool backable;        // a Back button left of Next (never on the first step): the step before shows again
    public Action onShow;        // runs as the step shows, to set the game up for what the step teaches
    public Action onDone;        // runs as the step ends, e.g. to undo what onShow changed
    public TutorialPop[] pops;   // floating points (e.g. "+100") over game elements while the step shows; null = none
    // a "do it" step: no button, the framed areas take clicks, and the step ends as soon as an action passes this
    public Func<GameAction, bool> doneWhen;
    // a "do it" step: an action that isn't what it asks for gets undone, with this note (null = let it be)
    public Func<GameAction, string> wrong;
    // a "do it" step that ends when this turns true (checked every frame), e.g. Settings opened
    public Func<bool> until;
    // the Next button's icon (Resources/UIIcons), e.g. "nextlvl" on a last step; null = the next arrow
    public string buttonIcon;
    public bool dim;      // darken everything outside the boxes (the screen tour)
    public bool hoverHints; // hint tips still show when the mouse is over the board (clicks stay blocked)
    public bool keepSide; // a "do it" step that keeps its own bubble side instead of the top-left to-do spot
    public bool labelsAsBoxes; // labels drawn as small guide boxes to the left of their boxes (e.g. "Close Settings when done")
    public string labelSides; // one letter per label: L left of its box (middle height), A above (centred), B below (centred); missing = automatic
    public Func<bool> hideWhen; // true: the step's box and target frames are hidden for now (e.g. Settings turned to another tab); its label boxes stay
    public float pictureHeight;              // room for a small picture under the text (drawPicture fills it)
    public bool wrongBackToArrows;           // a "wrong" action is undone only back to its arrow question (a wrong arrow), not the whole placement
    public Func<TutorialStep> chooseWhenShown; // decided as the step comes up: the step to show in its place (null: this one)
    public Action<RectTransform> drawPicture; // draws into a box under the text, as wide as the text, pictureHeight tall
    public Func<RectTransform> alignTop; // the bubble's top lined up with this element's top (e.g. the score)
    public int bubbleUnderLabel = -1; // the bubble goes under this label (its index), lined up with its box's left edge, the same gap as box-to-label
    // label boxes lined up: (box, side L R T B, other box) moves that side of the box to the other box's same side
    public (int box, char side, int to)[] align;
    // boxed groups, each with a short line just outside its box (above it in the lower half of the screen, below in the upper)
    public (Func<RectTransform>[] parts, string text)[] labels;
}

// floating points on a step (TutorialStep.pops): in the tutorial's yellow, centered over
// `near` (see Target), gone when the step ends
public class TutorialPop
{
    public string text;
    public Func<RectTransform>[] near;
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

    // the Settings button in the "..." menu. the menu is closed till hovered, so a step framing it holds it open
    // (game.hud.HoldMoreMenu in onShow, let go in onDone)
    public static Func<RectTransform> Settings(GameManager game) => () => game.hud.SettingsButton();

    // the arrow for one way, while an object's arrows show (at a junction, or coming out of a door); missing otherwise
    public static Func<RectTransform> Arrow(GameManager game, Dir way) => () => game.board.ArrowRect(way);

    // the Undo button (next to Restart, in the bottom-right corner)
    public static Func<RectTransform> Undo(GameManager game) =>
        () => game.hud.undoButton != null ? (RectTransform)game.hud.undoButton.transform : null;
}

// tutorials by name (Tutorials.Plan.cs); a level picks one with LevelData.tutorial
public static partial class Tutorials
{
    // a tutorial's name -> its steps, made with the running game so steps can check it or set it up. a level picks its
    // tutorial by name (LevelData.tutorial, set in Levels.cs), so it stays with its level when levels are inserted or moved
    static readonly Dictionary<string, Func<GameManager, List<TutorialStep>>> ByName = new()
    {
        { "FirstLevel", FirstLevel }, // in Tutorials.Plan.cs
        { "PlusCombo", PlusCombo }, // in Tutorials.Plan.cs
        { "DoorsAcross", DoorsAcross }, // in Tutorials.Plan.cs
    };

    // a tutorial's name -> its coach, for tutorials that have one (it takes over once the steps end)
    static readonly Dictionary<string, Func<GameManager, TutorialCoach>> CoachByName = new()
    {
        { "FirstLevel", FirstLevelCoach },
        { "PlusCombo", PlusComboCoach },
    };

    // a tutorial's name -> its level-end steps: shown once the level ends with everyone delivered, with the end window
    // held till they end. made at the level's end, so they can read the game as it is then (e.g. the score's history).
    // they wait for that level end from when the coach is Done, or from when the steps end if the tutorial has no coach
    static readonly Dictionary<string, Func<GameManager, List<TutorialStep>>> EndStepsByName = new()
    {
        { "FirstLevel", FirstLevelEnd },
        { "PlusCombo", PlusComboEnd },
    };

    // on start: a level whose l.tutorial names no tutorial here (a typo, or one renamed) would just get none, so say so
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CheckNames()
    {
        foreach (var l in Levels.All)
            if (!string.IsNullOrEmpty(l.tutorial) && !ByName.ContainsKey(l.tutorial))
                Debug.LogWarning($"{l.title} asks for the \"{l.tutorial}\" tutorial, but Tutorials.ByName has none by that name.");
    }

    // null when the level has no tutorial
    public static List<TutorialStep> For(LevelData level, GameManager game) =>
        !string.IsNullOrEmpty(level.tutorial) && ByName.TryGetValue(level.tutorial, out var make) ? make(game)
        : !string.IsNullOrEmpty(level.think) ? new List<TutorialStep> { new TutorialStep { text = level.think, buttonText = "Got it" } } // a think guide: one step
        : null;

    // null when the level's tutorial has no coach
    public static TutorialCoach CoachFor(LevelData level, GameManager game) =>
        !string.IsNullOrEmpty(level.tutorial) && CoachByName.TryGetValue(level.tutorial, out var make) ? make(game) : null;

    // null when the level's tutorial has no level-end steps
    public static List<TutorialStep> EndStepsFor(LevelData level, GameManager game) =>
        !string.IsNullOrEmpty(level.tutorial) && EndStepsByName.TryGetValue(level.tutorial, out var make) ? make(game) : null;

    // whether the level's tutorial is its opening steps and nothing else: no coach, no level-end steps
    public static bool StepsOnly(LevelData level) =>
        (!string.IsNullOrEmpty(level.tutorial) && !CoachByName.ContainsKey(level.tutorial) && !HasEnd(level))
        || (string.IsNullOrEmpty(level.tutorial) && !string.IsNullOrEmpty(level.think));

    // whether level-end steps wait for the level's end (TutorialController holds the end window for them)
    public static bool HasEnd(LevelData level) =>
        !string.IsNullOrEmpty(level.tutorial) && EndStepsByName.ContainsKey(level.tutorial);
}

// what a coach makes of a player action
public enum CoachReply
{
    None,  // nothing to do
    Undo,  // a slip: undone at once (a wrong way back to its arrows), with the coach's note in the red box
    Done,  // the player did what it teaches: its steps stop for good; it watches on (Undo can still come) until the
           // level ends
}

// what watches the player after a tutorial's steps end, until they've done what it teaches. it's set up again each
// time its level loads; leaving the level pauses it, coming back picks it up again. TutorialController runs it
public class TutorialCoach
{
    public Func<GameAction, CoachReply> onAction; // each player action, once it has taken effect (GameManager.ActionDone)
    public Func<GameAction, string> undoNoteFor;  // the red box's note for the action that called for the Undo
    // until Done: as each delivered object's tile breaks (the delivered object, everyone delivered with it), steps to
    // show it before the next one breaks; null = carry on (GameManager.onRemoval)
    public Func<Arrival, string[], List<TutorialStep>> removalSteps;
}
