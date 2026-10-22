using System.Collections.Generic;
using UnityEngine;

// everything one tile placement can change, saved just before the placement
public class Snapshot
{
    public Dictionary<TileType, int> inventory;
    public TileType?[,] tiles; // each cell's tile in the shared grid, null = empty or outside every board
    public bool[,] locked;     // which tiles an object has gone through (they can't be put back)
    public ObjectState[] objects; // same order as GameManager's objects
    public List<RemovedEntry> removed;
    public int deliveryPoints, comboPoints, teleportPoints, unusedPoints;
    public int comboObjects, teleports, unusedTiles;
    public string finalComboNote;
    public List<string> log;
    public bool takeBack; // the action after this snapshot was a right-click take-back, not a placement
}

// the parts of a MovingObject that change while it moves
public class ObjectState
{
    public Vector2Int pos;
    public Dir heading;
    public bool completed, blocked, viaDoor;
    public int teleports;
    public List<Vector2Int> path;
}

// one snapshot per placement this level, newest on top
public class UndoHistory
{
    readonly Stack<Snapshot> snapshots = new Stack<Snapshot>();

    public bool CanUndo => snapshots.Count > 0;

    public void Record(Snapshot s) => snapshots.Push(s);
    public Snapshot Pop() => snapshots.Pop();
    public void Clear() => snapshots.Clear();
}
