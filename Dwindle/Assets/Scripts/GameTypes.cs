using System.Collections.Generic;
using UnityEngine;

// one square of the grid.
public class Cell
{
    public CellView view;
    public TileType? tile; // null = empty
    public bool obstacle;
    public TileView tileView;
}

// one object being guided to its ring.
public class MovingObject
{
    public ObjectSpec spec;
    public Vector2Int pos;  // last cell it passed through (or its start marker)
    public Dir heading;     // the way it faces
    public bool completed;
    public List<Vector2Int> path = new List<Vector2Int>();
    public ObjectView view;
    public DestinationView dest;
    public bool blocked;
}

// one tile lost this level, and where it was lost from.
public class RemovedEntry
{
    public TileType type;
    public string source;
}
