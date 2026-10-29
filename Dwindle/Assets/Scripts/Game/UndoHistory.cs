using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// everything one tile placement can change, saved just before the placement
public class Snapshot
{
    public Dictionary<TileType, int> inventory;
    public TileType?[,] tiles; // each cell's tile in the shared grid, null = empty or outside every board
    public bool[,] locked;     // which tiles an object has gone through (they can't be put back)
    public ObjectState[] objects; // same order as GameManager's objects
    public int deliveryPoints, comboPoints, teleportPoints, unusedPoints;
    public int comboObjects, teleports, unusedTiles;
    public List<string> log;
    public List<string> scoreLog; // the lower-left score log
    public bool takeBack; // the action after this snapshot was a right-click take-back, not a placement
    public GameAction action; // that placement or take-back, as ActionDone reported it (for analytics: what an undo takes back)
    public BoardView.Rewind rewind; // the moves the placement after this snapshot caused, for undo to play backwards
}

// the parts of a MovingObject that change while it moves
public class ObjectState
{
    public Vector2Int pos;
    public Dir heading;
    public bool completed, blocked, viaDoor;
    public int teleports;
    public List<DoorPass> doorsUsed;
    public List<Vector2Int> path;
}

// one snapshot per placement this level, newest on top
public class UndoHistory
{
    readonly Stack<Snapshot> snapshots = new Stack<Snapshot>();

    public bool CanUndo => snapshots.Count > 0;
    // tiles on the board from the player's own hand: placements still in the history, minus take-backs
    public int TilesUsed => snapshots.Sum(s => s.takeBack ? -1 : 1);

    public void Record(Snapshot s) => snapshots.Push(s);
    public Snapshot Pop() => snapshots.Pop();
    public void Clear() => snapshots.Clear();
}
