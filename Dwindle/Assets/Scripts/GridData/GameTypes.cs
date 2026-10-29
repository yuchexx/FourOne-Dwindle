using System.Collections.Generic;
using UnityEngine;

// one square of the grid
public class Cell
{
    public CellView view;
    public TileType? tile; // null = empty
    public bool obstacle;
    public TileView tileView;
    public bool locked;    // an object has gone through this tile, so it can no longer be returned to the supply
}

// one object being guided to its ring
public class MovingObject
{
    public ObjectSpec spec;
    public Vector2Int pos;  // last cell it passed through / its start marker
    public Dir heading;     // the way it faces
    public bool completed;
    public List<Vector2Int> path = new List<Vector2Int>();
    public ObjectView view;
    public DestinationView dest;
    public bool blocked;
    public bool viaDoor; // just came out of a door
    public int teleports; // doors gone through
    public List<DoorPass> doorsUsed = new List<DoorPass>(); // which ones, in order
}

// one tile lost this level, and where it was lost from
public class RemovedEntry
{
    public TileType type;
    public string source;
}
