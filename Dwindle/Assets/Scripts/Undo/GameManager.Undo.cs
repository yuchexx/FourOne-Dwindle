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
        for (int x = 0; x < tiles.GetLength(0); x++)
        for (int y = 0; y < tiles.GetLength(1); y++)
            tiles[x, y] = Cells[x, y]?.tile;

        return new Snapshot
        {
            inventory = new Dictionary<TileType, int>(inventory),
            tiles = tiles,
            objects = objects.Select(o => new ObjectState
            {
                pos = o.pos, heading = o.heading,
                completed = o.completed, blocked = o.blocked, viaDoor = o.viaDoor,
                teleports = o.teleports, path = new List<Vector2Int>(o.path),
            }).ToArray(),
            removed = new List<RemovedEntry>(removed),
            deliveryPoints = deliveryPoints, comboPoints = comboPoints,
            teleportPoints = teleportPoints, unusedPoints = unusedPoints,
            comboObjects = comboObjects, teleports = teleports, unusedTiles = unusedTiles,
            finalComboNote = finalComboNote,
            log = hud.GetLog(),
        };
    }

    // for the Undo button - works mid-round too (moving, a junction or a recovery choice), not once the level is over
    public void Undo()
    {
        if (levelOver || !history.CanUndo) return;

        StopAllCoroutines(); // the round, with the slides, teleports and choices it is waiting on
        board.ClearChoices();
        board.StopFlashes();
        hud.ResetSlotFlashes();
        hud.SetChoiceMode(false);
        busy = false;
        pendingRecovery = null;
        selected = null; // undo leaves no tile selected

        Restore(history.Pop());
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

        hud.SetLog(s.log);
        RefreshAll();
    }
}
