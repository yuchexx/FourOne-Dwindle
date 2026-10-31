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
        hud.onSupplyBarPressed = SupplyBarPressed;
        board.canDragTile = p =>
        {
            bool ok = DragAllowed && CanAct() && Cells[p.x, p.y].tile != null && !Cells[p.x, p.y].locked;
            if (ok) DropPickedUp(); // a drag replaces a click pick-up
            return ok;
        };
        board.dockIcon = hud.SlotIconRect; // a tile dragged from the board is as big as in the dock
        board.onDock = hud.OnDock;
        board.onTileMoved = OnTileMoved;
        // boards with several parts fit between the top bar and the dock
        board.keepBelow = new[] { hud.levelTitleText ? hud.levelTitleText.rectTransform : null, hud.scoreText ? hud.scoreText.rectTransform : null };
        board.keepAbove = new[] { hud.supplyDock };
        HintsChanged += DrawHints;
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
        supplyDrag = false;
        hud.ResetSlotFlashes();
        menu.ShowGame();
        levelOver = false;
        levelIndex = index;
        level = Levels.All[index];
        inventory = Tiles.All.ToDictionary(t => t, t => level.inventory.TryGetValue(t, out var c) ? c : 0);
        selected = null;
        busy = false;
        pickedUp = null; // the board is rebuilt
        hud.SetScoreLog(null);
        deliveryPoints = comboPoints = teleportPoints = unusedPoints = 0;
        history.Clear();
        forcedRemoval = null; // for the tutorial: a forced removal never outlives its level
        endWindowHeld = false; // for the tutorial: nor does a held end window
        LockInput(false);      // for the tutorial: nor does locked input

        // every tile type (x0 for the ones this level doesn't hand out), or only its own with Compact supply
        hud.BuildSlots(SupplyTypes(), SelectTile, SupplyDragStart, e => board.MoveDrag(e), SupplyDragEnd);
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

        if (tutorial != null && Session.showTutorials) tutorial.TryStart(index, this); // the tutorial's steps can call back into the game
    }

    // player input
    void SelectTile(TileType t)
    {
        if (levelOver) return;
        if (busy) return; // objects are moving or waiting for a direction: nothing can be placed, so nothing is picked
        if (inputLocked) return; // for the tutorial (see LockInput)
        if (!ClickAllowed) return; // Settings: drag only
        if (returnedOnPress) { returnedOnPress = false; return; } // this press returned a picked-up tile (SupplyBarPressed)
        board.FinishUndo(); // an undo rewind still going: it finishes first (the supply bar's counts catch up)
        if (inventory[t] <= 0) return;
        selected = selected == t ? (TileType?)null : t; // clicking the selected tile again deselects it
        hud.RefreshInventory(inventory, selected);
        Report(new GameAction { kind = GameActionKind.SelectTile, tile = selected }); // for the tutorial
    }

    // a click on a square: puts down a board tile picked up by clicking (the same square: where it was), picks one up
    // (a tile no object has used), or places the tile picked in the supply
    void OnCellClicked(Vector2Int p)
    {
        if (!CanAct() || !ClickAllowed) return;
        var cell = Cells[p.x, p.y];
        if (pickedUp is Vector2Int from)
        {
            if (p == from) { DropPickedUp(); return; }
            if (!cell.obstacle && cell.tile == null) { DropPickedUp(); OnTileMoved(from, p); return; }
        }
        if (cell.tile != null && !cell.locked) { PickUp(p); return; }
        PlaceSelected(p);
    }

    // the tile picked in the supply goes on the square
    void PlaceSelected(Vector2Int p)
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

    // the supply dock's tile types, in the dock's order
    IEnumerable<TileType> SupplyTypes() => Tiles.SupplyOrder.Where(t =>
        !Session.compactSupply || t == TileType.Cross || level.inventory.GetValueOrDefault(t) > 0);

    // tiles can be picked and placed: the level is on, nothing moving, not locked by the tutorial
    bool CanAct() => !levelOver && !busy && !inputLocked;

    // Settings: click a tile then a square, drag it, or either
    static bool ClickAllowed => Session.placement != Session.Placement.Drag;
    static bool DragAllowed => Session.placement != Session.Placement.Click;

    // a board tile picked up by clicking it (no object has used it): the next click on an empty square moves it there,
    // on the dock returns it, on it again or off the board puts it back. its preview shows on the squares; nothing follows the mouse
    Vector2Int? pickedUp;

    void PickUp(Vector2Int p)
    {
        DropPickedUp();
        selected = null; // instead of a supply tile
        hud.RefreshInventory(inventory, selected);
        pickedUp = p;
        board.SetPicked(p);
    }

    void DropPickedUp()
    {
        if (pickedUp == null) return;
        pickedUp = null;
        board.SetPicked(null);
    }

    // a press off the squares and the dock: a picked-up tile stays where it was
    void CancelPickUpOffBoard()
    {
        var pointer = UnityEngine.InputSystem.Pointer.current;
        var events = UnityEngine.EventSystems.EventSystem.current;
        if (pickedUp == null || pointer == null || events == null || !pointer.press.wasPressedThisFrame) return;
        var hits = new List<UnityEngine.EventSystems.RaycastResult>();
        events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events) { position = pointer.position.ReadValue() }, hits);
        var top = hits.Count > 0 ? hits[0].gameObject : null;
        if (top != null && (top.GetComponentInParent<CellView>() != null || hud.OnDock(top))) return;
        DropPickedUp();
    }

    // any press on the dock, empty cards and the bar too: finishes an undo rewind, and returns a picked-up board tile
    // (the card's own click then does nothing)
    bool returnedOnPress;
    void SupplyBarPressed()
    {
        returnedOnPress = false;
        board.FinishUndo();
        if (pickedUp is not Vector2Int from || !CanAct()) return;
        DropPickedUp();
        OnCellRightClicked(from);
        returnedOnPress = true;
    }

    // dragging a tile from the supply: it's picked, and dropping it on a square places it like a click
    bool supplyDrag;
    void SupplyDragStart(TileType t, UnityEngine.EventSystems.PointerEventData e)
    {
        if (!DragAllowed || !CanAct() || inventory[t] <= 0) return;
        DropPickedUp();
        board.FinishUndo();
        if (selected != t)
        {
            selected = t;
            hud.RefreshInventory(inventory, selected);
            Report(new GameAction { kind = GameActionKind.SelectTile, tile = selected }); // for the tutorial
        }
        supplyDrag = true;
        var card = e.pointerDrag != null ? e.pointerDrag.GetComponent<TileCard>() : null;
        board.StartDrag(t, e, card != null ? card.iconRoot : null); // as big as the card's tile
    }

    void SupplyDragEnd(UnityEngine.EventSystems.PointerEventData e)
    {
        if (!supplyDrag) return;
        supplyDrag = false;
        board.EndDrag();
        if (BoardView.SquareUnder(e) is Vector2Int p) PlaceSelected(p);
        if (!ClickAllowed) { selected = null; hud.RefreshInventory(inventory, selected); } // drag only: nothing stays picked for clicks
    }

    // a board tile dragged onto another empty square: taken back and placed there in one move (one undo)
    void OnTileMoved(Vector2Int from, Vector2Int to)
    {
        if (!CanAct()) return;
        Cell a = Cells[from.x, from.y], b = Cells[to.x, to.y];
        if (a.tile == null || a.locked || b.obstacle || b.tile != null) return;
        board.FinishUndo();

        var t = a.tile.Value;
        var snapshot = Capture();
        snapshot.move = true;
        snapshot.rewind = board.StartRecording(to, t);
        snapshot.action = OnSquare(GameActionKind.PlaceTile, to, t); // what an undo of it reports
        history.Record(snapshot);
        board.TakeBackTile(from);
        board.SetTile(to, t);
        RefreshAll();
        lastPlaced = to; // for the hints
        StartCoroutine(ResolveRound());
        Report(OnSquare(GameActionKind.TakeBack, from, t)); // for the tutorial and analytics: a take-back, then a placement
        Report(snapshot.action);
    }

    // tiles on the board no object has gone through: they count as unused at the end
    int UntravelledTiles()
    {
        int n = 0;
        for (int x = 0; x < Cells.GetLength(0); x++)
            for (int y = 0; y < Cells.GetLength(1); y++)
                if (Cells[x, y]?.tile != null && !Cells[x, y].locked) n++;
        return n;
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
        DropPickedUp();
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
    TileType? PreviewTile() => levelOver || busy ? null : pickedUp is Vector2Int q ? Cells[q.x, q.y].tile : selected;

    // the level is over: count the unused tiles and show the end window
    void FinishLevel(string reason, bool showNow = false)
    {
        stuck = false;
        HideStuckNotice();
        levelOver = true;
        selected = null;
        DropPickedUp();

        // unused tiles only score when everything was delivered (otherwise ending early, e.g. with End level,
        // would score every tile not spent on the objects left behind)
        if (objects.All(o => o.completed))
        {
            int unusedTiles = inventory.Values.Sum() + UntravelledTiles(); // the supply, and tiles nobody went through
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
        CancelPickUpOffBoard();
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
