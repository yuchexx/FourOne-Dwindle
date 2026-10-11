using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ObjectSpec
{
    public string name;
    public Color color;
    public Vector2Int start;
    public Dir heading;
    public Vector2Int dest;
}

public class FixedTile
{
    public Vector2Int pos;
    public TileType type;
}

// colored wall filter
public class FilterSpec
{
    public Vector2Int cell;
    public Dir side;
    public Color color;

    public bool Matches(Color c) => color == c;

    public Vector2Int Neighbor => cell + DirUtil.Offset(side);

    // is this one of the two squares the filter sits between?
    public bool Touches(Vector2Int c) => c == cell || c == Neighbor;

    // the filter's side, seen from one of its two squares
    public Dir SideFrom(Vector2Int c) => c == cell ? side : DirUtil.Opposite(side);
}

public class LevelData
{
    public string title;
    public int width, height;
    public int targetScore; // score needed for the 3rd star
    public Dictionary<TileType, int> inventory = new();
    public List<Vector2Int> obstacles = new();
    public List<FixedTile> fixedTiles = new(); // levels can pre-place tiles that are never removed
    public List<FilterSpec> filters = new();
    public List<ObjectSpec> objects = new();

    // 1st star: deliver all but one
    public int Required => Mathf.Max(1, objects.Count - 1);

    // the filter on this cell's side, if any
    public FilterSpec FilterAt(Vector2Int cell, Dir side) =>
        filters.FirstOrDefault(f => f.cell == cell && f.side == side);

    // the filter on the edge between `from` and its neighbor in direction d, whichever cell owns it
    public FilterSpec FilterOnEdge(Vector2Int from, Dir d) =>
        FilterAt(from, d) ?? FilterAt(from + DirUtil.Offset(d), DirUtil.Opposite(d));
}
