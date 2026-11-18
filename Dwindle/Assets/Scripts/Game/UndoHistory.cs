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
    public List<DeliveryRecord> deliveries;                // for the hints: who arrived together so far,
    public Dictionary<string, List<ArrowRecord>> arrows;   //   and the arrows picked
    public List<ScoreEntry> scoreLog; // the score history
    public bool takeBack; // the action after this snapshot was a right-click take-back, not a placement
    public bool move;     // or a board tile dragged to another square (taken back and placed in one go)
    public GameAction action; // that placement or take-back, as ActionDone reported it (for analytics: what an undo takes back)
    public BoardView.Rewind rewind; // the moves the placement after this snapshot caused, for undo to play backwards
    public List<ChoicePoint> choices = new List<ChoicePoint>(); // the arrow questions its round asked, oldest first (undo by step)
}

// an arrow question in a round (a + or T, or a door with two ways out), saved just before the arrows showed, so undo
// by step can go back to it and ask again
public class ChoicePoint
{
    public Snapshot state;     // everything as it was then
    public int obj;            // the asking object (index in GameManager.objects)
    public List<Dir> ways;     // the arrows
    public DoorSpec door;      // the door it came through, or null at a + or T
    public int[] delivered;    // objects this round had already delivered
    public int rewindSteps;    // how much of the placement's recording came before it
    public bool answered;      // false while its arrows are showing
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
    // tiles on the board from the player's own hand: placements still in the history, minus take-backs (moves: 0)
    public int TilesUsed => snapshots.Sum(s => s.takeBack ? -1 : s.move ? 0 : 1);

    public void Record(Snapshot s) => snapshots.Push(s);
    public Snapshot Peek() => snapshots.Peek();
    public Snapshot Pop() => snapshots.Pop();
    public void Clear() => snapshots.Clear();
}
