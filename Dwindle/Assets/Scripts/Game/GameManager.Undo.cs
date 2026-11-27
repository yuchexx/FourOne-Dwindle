using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// undo: a snapshot before every tile placement (with its round's arrow questions), and going back to the newest one
public partial class GameManager
{
    readonly UndoHistory history = new UndoHistory();

    Snapshot Capture()
    {
        var tiles = new TileType?[Cells.GetLength(0), Cells.GetLength(1)];
        var locked = new bool[Cells.GetLength(0), Cells.GetLength(1)];
        for (int x = 0; x < tiles.GetLength(0); x++)
            for (int y = 0; y < tiles.GetLength(1); y++)
            {
                tiles[x, y] = Cells[x, y]?.tile;
                locked[x, y] = Cells[x, y]?.locked ?? false;
            }

        return new Snapshot
        {
            inventory = new Dictionary<TileType, int>(inventory),
            tiles = tiles,
            locked = locked,
            objects = objects.Select(o => new ObjectState
            {
                pos = o.pos,
                heading = o.heading,
                completed = o.completed,
                blocked = o.blocked,
                viaDoor = o.viaDoor,
                teleports = o.teleports,
                doorsUsed = new List<DoorPass>(o.doorsUsed),
                path = new List<Vector2Int>(o.path),
            }).ToArray(),
            deliveryPoints = deliveryPoints,
            comboPoints = comboPoints,
            teleportPoints = teleportPoints,
            unusedPoints = unusedPoints,
            deliveries = new List<DeliveryRecord>(deliveriesSoFar), // for the hints
            arrows = arrowsSoFar.ToDictionary(kv => kv.Key, kv => new List<ArrowRecord>(kv.Value)),
            scoreLog = hud.GetScoreLog(),
        };
    }

    // for the Undo button - works mid-round too (moving, a junction or a recovery choice), not once the level is over.
    // Settings' undo by step (on by default): back to the last arrow question first, then the placement
    public void Undo() => UndoBy(Session.undoByStep);

    // for the tutorial (a coach undoing a wrong move): always the whole placement
    public void UndoPlacement(bool animate = true)
    {
        board.animateUndo = animate && Session.animateUndo; // false: jumps back, no rewind shown
        UndoBy(false);
        board.animateUndo = Session.animateUndo;
    }

    // for the tutorial (a coach undoing a wrong way, TutorialCoach.undoToChoice): back to the last arrow question,
    // which shows again (the tile stays), whatever undo by step is set to
    public void UndoChoiceStep(bool animate = true)
    {
        board.animateUndo = animate && Session.animateUndo;
        UndoBy(true);
        board.animateUndo = Session.animateUndo;
    }

    void UndoBy(bool byStep)
    {
        if (levelOver || !history.CanUndo || redoingChoice) return;

        // a question whose arrows are showing now: going back means the one before it
        var top = history.Peek();
        while (top.choices.Count > 0 && !top.choices[top.choices.Count - 1].answered) top.choices.RemoveAt(top.choices.Count - 1);
        if (byStep && top.choices.Count > 0) { UndoChoice(top); return; }

        RememberRedoPlacement(top);
        var snapshot = history.Pop();
        board.BeginUndo(objects, snapshot.rewind); // where everything is now, to rewind from (see BoardView.animateUndo)
        StopAllCoroutines(); // the round, with the slides, teleports and choices it is waiting on
        board.ClearChoices();
        ClearRoundMarks(); // show tile breaks: the round's tints go (a resumed round marks its question again)
        board.StopFlashes();
        hud.ResetSlotFlashes();
        busy = false;
        board.HideGhost();
        selected = null; // undo leaves no tile selected
        DropPickedUp();

        var shownSupply = new Dictionary<TileType, int>(inventory); // the counts before the undo
        Restore(snapshot);
        // the supply bar keeps its old counts until the rewound tile has traveled back to it (or from it)
        hud.ShowSupplyDuringRewind(shownSupply);
        board.PlayUndo(objects, hud.SlotIconRect, () => hud.RefreshInventory(inventory, selected));
        UpdateStuck(); // the banner goes if this made the level winnable again
        // for the tutorial, and for analytics: what it took back (the placement or take-back, with its tile and square)
        var undone = snapshot.action;
        Report(new GameAction { kind = GameActionKind.Undo, undid = undone.kind, tile = undone.tile, board = undone.board, square = undone.square });
    }

    // undo by step: back to the placement's last arrow question, which shows again (the tile stays). the moves after
    // it play backwards, then the round goes on from the question
    void UndoChoice(Snapshot placement)
    {
        var choice = placement.choices[placement.choices.Count - 1];
        RememberRedoChoice(choice);
        placement.choices.RemoveAt(placement.choices.Count - 1);
        roundSnapshot = placement; // the round's questions from here on go in it again
        board.BeginUndo(objects, board.PartOf(placement.rewind, choice.rewindSteps));
        StopAllCoroutines(); // the round, with the slides, teleports and choices it is waiting on
        board.ClearChoices();
        ClearRoundMarks(); // show tile breaks: the round's tints go (a resumed round marks its question again)
        board.StopFlashes();
        hud.ResetSlotFlashes();
        busy = true; // mid-round: the hints wait for the round to end before judging the board
        board.HideGhost();
        selected = null;
        DropPickedUp();
        stuck = false;
        HideStuckNotice();

        var shownSupply = new Dictionary<TileType, int>(inventory);
        Restore(choice.state);
        board.ResumeRecording(placement.rewind, choice.rewindSteps); // what follows is recorded again, for undoing the placement
        // the asking object waits where it was asked: on its + or T, or at the door it came out of
        var o = objects[choice.obj];
        var partner = choice.door != null ? level.Partner(choice.door) : null;
        o.view.Rect.anchoredPosition = partner != null ? board.EdgePos(partner.cell, partner.side) : board.CellPos(o.pos);
        hud.ShowSupplyDuringRewind(shownSupply);
        board.PlayUndo(objects, hud.SlotIconRect, () => hud.RefreshInventory(inventory, selected));
        if (placement.rewind != null) lastPlaced = placement.rewind.placedAt; // for the hints: the square whose round this is
        placedForHints = true; // stage 3 judges the placement again once this round ends
        StartCoroutine(ResolveRound(choice));

        // for analytics: a step back inside the placement (it stays), at the square where the question was
        var at = partner != null ? partner.cell : o.pos;
        int b = level.BoardOf(at);
        Report(new GameAction { kind = GameActionKind.Undo, undid = GameActionKind.ChooseDirection, board = b, square = at - level.boards[b].origin });
    }

    // the end window's Undo button: back into the level, one step back (its last arrow question, or its last placement). Undo itself refuses
    // once the level is over, so this reopens the level first. it undoes a placement even when the level was ended
    // with End level: the placement before that is what comes back
    public void UndoFromEndWindow()
    {
        if (!levelOver || !history.CanUndo) return;
        menu.HideMenu();
        levelOver = false;
        Undo();
    }

    void Restore(Snapshot s)
    {
        inventory = s.inventory;
        deliveryPoints = s.deliveryPoints;
        comboPoints = s.comboPoints;
        teleportPoints = s.teleportPoints;
        unusedPoints = s.unusedPoints;
        deliveriesSoFar = new List<DeliveryRecord>(s.deliveries); // for the hints (the stages themselves stay)
        arrowsSoFar = s.arrows.ToDictionary(kv => kv.Key, kv => new List<ArrowRecord>(kv.Value));

        board.RestoreTiles(s.tiles);
        board.RestoreLocks(s.locked);
        for (int i = 0; i < objects.Count; i++)
        {
            var o = objects[i];
            var state = s.objects[i];
            o.pos = state.pos;
            o.heading = state.heading;
            o.completed = state.completed;
            o.blocked = state.blocked;
            o.viaDoor = state.viaDoor;
            o.teleports = state.teleports;
            o.doorsUsed = new List<DoorPass>(state.doorsUsed);
            o.path = state.path;
            board.SyncObject(o);
        }
        board.LayoutWaitingObjects(objects);

        hud.SetScoreLog(s.scoreLog);
        RefreshAll();
        HintsChanged?.Invoke(); // ghosts of undone tiles come back
    }
}
