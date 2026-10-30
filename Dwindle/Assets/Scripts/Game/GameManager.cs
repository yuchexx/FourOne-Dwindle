using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// holds the state of the level, handles clicks, and runs the level from start to end
// the rules are in GameManager.Rules.cs, score and the end window in GameManager.Score.cs, undo in GameManager.Undo.cs
public partial class GameManager : MonoBehaviour
{
    [Header("Views")]
    public BoardView board;
    public HudView hud;
    public MenuView menu;

    [Header("Tutorial")]
    public TutorialController tutorial;

    [Header("Timing")]
    public float endPopupDelay = 1.2f; // seconds between the level ending and the end window

    [Header("Scoring")]
    public int pointsPerDelivery = 100;
    public int comboPointsPerObject = 50; // per object, when one placement delivers 2 or more
    public int pointsPerTeleport = 50;    // once per delivered object that went through at least one door
    public int pointsPerUnusedTile = 75;  // per tile left in the supply, only if every object was delivered

    [Header("Stuck notice")]
    [Tooltip("Seconds without placing a tile, while stuck close to a ring, before the notice shows (0 = right away)")]
    [Min(0f)] public float stuckSecondsBeforeNotice = 8f;
    [Tooltip("Close = filling this many squares or fewer would connect some object to its ring. used when the level's biggest board is smaller than Large Board Squares")]
    [Min(1)] public int closeGapSmallBoards = 2;
    [Tooltip("Close = filling this many squares or fewer would connect some object to its ring. used when the level's biggest board has at least Large Board Squares")]
    [Min(1)] public int closeGapLargeBoards = 3;
    [Tooltip("A board with at least this many squares counts as large (4x3 = 12)")]
    [Min(1)] public int largeBoardSquares = 12;
    [Tooltip("Show the notice too when stuck far from every ring")]
    public bool noticeWhenFar = true;
    [Tooltip("Seconds without placing a tile, while stuck far from every ring, before the notice shows")]
    [Min(0f)] public float farSecondsBeforeNotice = 20f;

    int levelIndex;
    LevelData level;
    Dictionary<TileType, int> inventory;
    TileType? selected;
    bool busy; // true while objects are moving or waiting for a choice
    List<MovingObject> objects;
    bool levelOver;
    Vector2Int? forcedRemoval; // for the tutorial (see ForceNextRemoval): the cell the next delivery breaks

    // for the tutorial (TutorialStep.hoverOnly): while locked, the supply, the board, its arrows, doors and undo ignore
    // clicks, so a slip can't change the game. hovering still works (the door highlight, the tile preview)
    bool inputLocked;
    public void LockInput(bool locked)
    {
        inputLocked = locked;
        board.LockInput(locked);
    }

    // for the tutorial (TutorialController): every player action, once it has taken effect. a click that changed
    // nothing (blocked, missed, ignored while objects move) reports nothing
    public event System.Action<GameAction> ActionDone;
    void Report(GameAction action) => ActionDone?.Invoke(action);

    // for the tutorial: an action on a square, in its board's own coordinates
    GameAction OnSquare(GameActionKind kind, Vector2Int p, TileType tile)
    {
        int b = level.BoardOf(p);
        return new GameAction { kind = kind, tile = tile, board = b, square = p - level.boards[b].origin };
    }

    // for the tutorial and the hints: an object's direction, just chosen at a junction (door null) or coming out of a door
    void ReportChoice(MovingObject o, string door = null)
    {
        RecordArrow(o, door);
        Report(new GameAction { kind = GameActionKind.ChooseDirection, objectName = o.spec.name, direction = o.heading });
    }

    Cell[,] Cells => board.Cells;

    void Start()
    {
        hud.onSupplyBarPressed = () => board.FinishUndo(); // a press on the supply bar finishes an undo rewind
        if (menu.levelSelect != null) menu.levelSelect.Build(Levels.All, LoadLevel); // the Levels page's cards
        ShowStartScreen();
    }

    public void LoadLevel(int index)
    {
        StopAllCoroutines();
        if (tutorial != null) tutorial.Stop();
        TelemetryManager.Instance.StartLevel(index);
        levelReported = false;
        stuck = false;
        HideStuckNotice();
        board.ClearChoices();
        hud.ResetSlotFlashes();
        menu.ShowGame();
        levelOver = false;
        levelIndex = index;
        level = Levels.All[index];
        inventory = Tiles.All.ToDictionary(t => t, t => level.inventory.TryGetValue(t, out var c) ? c : 0);
        selected = null;
        busy = false;
        hud.SetScoreLog(null);
        deliveryPoints = comboPoints = teleportPoints = unusedPoints = 0;
        history.Clear();
        forcedRemoval = null; // for the tutorial: a forced removal never outlives its level
        endWindowHeld = false; // for the tutorial: nor does a held end window
        LockInput(false);      // for the tutorial: nor does locked input

        hud.BuildSlots(Tiles.SupplyOrder, SelectTile); // every tile type, x0 for the ones this level doesn't hand out
        objects = board.Build(level, OnCellClicked, OnDoorClicked, OnCellRightClicked, PreviewTile);
        ResetHintsForLoad();
        RefreshAll();

        // the old text pop-up, replaced by TutorialController. kept until the new tutorial covers these points
        // if (index == 0 && !level1TutorialShown)
        // {
        //     level1TutorialShown = true;
        //
        //     menu.ShowTutorial(
        //         "Planning Tip",
        //         "Each delivery breaks one tile of its path - often a junction someone else still needs.\n" +
        //         "Delivering two or more objects at once earns you a + tile.\n\n" +
        //         "Right-click a tile no object has used yet to put it back.\n\n" +
        //         "You can review all rules anytime using the Rules button in the upper-right corner."
        //     );
        // }

        if (tutorial != null) tutorial.TryStart(index, this); // the tutorial's steps can call back into the game
    }

    // player input
    void SelectTile(TileType t)
    {
        if (levelOver) return;
        if (busy) return; // objects are moving or waiting for a direction: nothing can be placed, so nothing is picked
        if (inputLocked) return; // for the tutorial (see LockInput)
        board.FinishUndo(); // an undo rewind still going: it finishes first (the supply bar's counts catch up)
        if (inventory[t] <= 0) return;
        selected = selected == t ? (TileType?)null : t; // clicking the selected tile again deselects it
        hud.RefreshInventory(inventory, selected);
        Report(new GameAction { kind = GameActionKind.SelectTile, tile = selected }); // for the tutorial
    }

    void OnCellClicked(Vector2Int p)
    {
        if (levelOver) return;
        if (busy) return;
        if (inputLocked) return; // for the tutorial (see LockInput)
        var cell = Cells[p.x, p.y];
        if (cell.obstacle || cell.tile != null || selected == null) return;
        board.FinishUndo(); // an undo rewind still going: everything to its place before anything moves

        var t = selected.Value;
        var snapshot = Capture(); // the only point undo saves at
        snapshot.rewind = board.StartRecording(p, t); // this tile and the moves it causes, so undo can play them backwards
        snapshot.action = OnSquare(GameActionKind.PlaceTile, p, t); // what an undo of it reports
        history.Record(snapshot);
        inventory[t]--;
        board.SetTile(p, t);
        TelemetryManager.Instance.AddPiece();
        RefreshAll();
        lastPlaced = p; // for the hints: the square whose round this is
        StartCoroutine(ResolveRound());
        Report(snapshot.action); // for the tutorial
    }

    // clicking a door names it
    void OnDoorClicked(DoorSpec door)
    {
        if (inputLocked) return; // for the tutorial (see LockInput)
        Report(new GameAction { kind = GameActionKind.ClickDoor, door = door.label }); // for the tutorial
    }

    // right-click puts a tile back in the supply, as long as no object has gone through it yet
    void OnCellRightClicked(Vector2Int p)
    {
        if (levelOver || busy) return;
        if (inputLocked) return; // for the tutorial (see LockInput)
        board.FinishUndo(); // an undo rewind still going: it finishes first
        menu.HideMenu(); // pressed from the paused menu: the end window replaces it
        var cell = Cells[p.x, p.y];
        if (cell.tile == null || cell.locked) return;

        var t = cell.tile.Value;
        var snapshot = Capture(); // undo can bring it back
        snapshot.takeBack = true;
        snapshot.action = OnSquare(GameActionKind.TakeBack, p, t); // what an undo of it reports
        history.Record(snapshot);
        board.StopRecording(); // nothing moves on a take-back
        board.TakeBackTile(p);
        inventory[t]++;
        TelemetryManager.Instance.RemovePiece();
        UpdateBlocked(); // a tile in an object's way may have been the one taken back
        UpdateStuck();
        RefreshAll();
        HintsChanged?.Invoke(); // a ghost comes back where its tile was taken back
        Report(snapshot.action); // for the tutorial
    }

    // the tile the hover preview shows: the selected one, while the player can place
    TileType? PreviewTile() => levelOver || busy ? null : selected;

    // the level is over: count the unused tiles and show the end window
    void FinishLevel(string reason, bool showNow = false)
    {
        stuck = false;
        HideStuckNotice();
        levelOver = true;
        selected = null;

        // unused tiles only score when everything was delivered (otherwise ending early, e.g. with End level,
        // would score every tile not spent on the objects left behind)
        if (objects.All(o => o.completed))
        {
            int unusedTiles = inventory.Values.Sum();
            unusedPoints = unusedTiles * pointsPerUnusedTile;
            if (unusedTiles > 0)
                hud.LogText($"{unusedTiles} unused", unusedPoints, $"{pointsPerUnusedTile}×{unusedTiles} =");
        }
        RefreshAll();
        if (showNow && !endWindowHeld) ShowMenu(true); // from the Leave screen: no wait
        else StartCoroutine(ShowEndMenuAfterDelay());
        int stars = Stars();
        bool purple = SaveResult(stars); // this session's best for the level
        Report(new GameAction { kind = GameActionKind.LevelEnd, score = Score, stars = stars, purple = purple, hintStage = hints.Stage,
            tilesUsed = history.TilesUsed, delivered = Delivered, reason = reason }); // for analytics
    }

    IEnumerator ShowEndMenuAfterDelay()
    {
        yield return new WaitForSeconds(endPopupDelay);
        while (endWindowHeld) yield return null; // for the tutorial (see HoldEndWindow)
        ShowMenu(true);
    }

    // for the tutorial (Tutorials.cs): while held, a finished level waits to show its end window, so a step can
    // point at the board or the top bar first (the window's backdrop dims the whole screen)
    bool endWindowHeld;
    public void HoldEndWindow(bool hold) => endWindowHeld = hold;

    // the wait, while stuck and the notice isn't up yet
    void Update()
    {
        if (hintQueued && !busy) UseHint(); // a hint pressed during a move
        if (stuck && !levelOver && !busy && !hud.StuckNoticeShowing && Time.time - stuckSince >= StuckWait) ShowStuckNotice();

        // the supply cards don't light up under the mouse while a tile can't be picked (see SelectTile)
        if (busy != supplyPaused)
        {
            supplyPaused = busy;
            hud.SetSupplyPaused(busy);
        }
    }
    bool supplyPaused;

    // the End level button: finish now and count what's been earned
    public void EndLevelNow()
    {
        if (levelOver || busy) return;
        bool fromLeave = menu.leaveDialog != null && menu.leaveDialog.panel.activeSelf;
        menu.HideMenu(); // the end window replaces the Leave screen
        FinishLevel("You ended the level.", showNow: fromLeave);
    }

    public void ShowStartScreen()
    {
        ReportLevel(); // Home from a level: a finished last level has no Next, so this is its only report
        StopAllCoroutines();
        if (tutorial != null) tutorial.Stop();
        stuck = false; // nothing keeps timing the stuck notice on the start screen
        HideStuckNotice();
        board.ClearChoices();
        int next = Session.StartLevel(Levels.All.Count);
        menu.ShowStart(next == 0 && Session.Get(0).stars == 0 ? "Start" : $"Continue: Level {next + 1}");
    }

    // Start: Level 1 until it's done, then the first level without a star this session
    public void StartGame() => LoadLevel(Session.StartLevel(Levels.All.Count));
    public void Restart()
    {
        // for analytics: the attempt being thrown away, before the level reloads
        Report(new GameAction { kind = GameActionKind.Restart, score = Score, tilesUsed = history.TilesUsed, delivered = Delivered });
        ReportLevel();
        LoadLevel(levelIndex);
    }
    public void NextLevel()
    {
        if (levelIndex + 1 < Levels.All.Count)
        {
            ReportLevel();
            LoadLevel(levelIndex + 1);
        }
    }

    // the analytics row for the level being left (Next, Restart or Home), sent once per attempt
    bool levelReported = true; // nothing to report until a level is loaded
    void ReportLevel()
    {
        if (levelReported) return;
        levelReported = true;
        TelemetryManager.Instance.EndLevel(Score, Stars());
    }
    public void OpenMenu() => ShowMenu(levelOver);
    public void Resume() => menu.HideMenu();

    public void DismissMenuIfPaused()
    {
        if (!levelOver) menu.HideMenu();
    }

    public void ShowRules() => menu.SetRulesVisible(true);
    public void HideRules() => menu.SetRulesVisible(false);

    // HUD
    void RefreshAll()
    {
        if (selected != null && inventory[selected.Value] <= 0) selected = null;
        hud.RefreshInventory(inventory, selected);
        hud.RefreshInfo(level, inventory.Values.Sum(), history.TilesUsed, Score);
        hud.SetUndoAvailable(!levelOver && history.CanUndo);
    }

}
