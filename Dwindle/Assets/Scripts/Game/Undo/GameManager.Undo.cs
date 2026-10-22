using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// undo: a snapshot before every tile placement, and going back to the newest one
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
                path = new List<Vector2Int>(o.path),
            }).ToArray(),
            removed = new List<RemovedEntry>(removed),
            deliveryPoints = deliveryPoints,
            comboPoints = comboPoints,
            teleportPoints = teleportPoints,
            unusedPoints = unusedPoints,
            comboObjects = comboObjects,
            teleports = teleports,
            unusedTiles = unusedTiles,
            finalComboNote = finalComboNote,
            log = hud.GetLog(),
        };
    }

    // for the Undo button - works mid-round too (moving, a junction or a recovery choice), not once the level is over
    public void Undo()
    {
        if (levelOver || !history.CanUndo) return;
        if (inputLocked) return; // for the tutorial (see LockInput)

        StopAllCoroutines(); // the round, with the slides, teleports and choices it is waiting on
        board.ClearChoices();
        board.StopFlashes();
        hud.ResetSlotFlashes();
        hud.SetChoiceMode(false);
        busy = false;
        board.HideGhost();
        selected = null; // undo leaves no tile selected

        Restore(history.Pop());
        UpdateStuck(); // the banner goes if this made the level winnable again
        Report(new GameAction { kind = GameActionKind.Undo }); // for the tutorial
    }

    // the end window's Undo button: back into the level, as it was before its last placement. Undo itself refuses
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
        removed = s.removed;
        deliveryPoints = s.deliveryPoints;
        comboPoints = s.comboPoints;
        teleportPoints = s.teleportPoints;
        unusedPoints = s.unusedPoints;
        comboObjects = s.comboObjects;
        teleports = s.teleports;
        unusedTiles = s.unusedTiles;
        finalComboNote = s.finalComboNote;

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
            o.path = state.path;
            board.SyncObject(o);
        }
        board.LayoutWaitingObjects(objects);

        // undoing a placement takes its piece back off the count; undoing a take-back puts it back on
        if (s.takeBack) TelemetryManager.Instance.AddPiece();
        else TelemetryManager.Instance.RemovePiece();

        hud.SetLog(s.log);
        RefreshAll();
    }
}
