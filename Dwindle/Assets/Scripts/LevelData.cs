using System.Collections.Generic;
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

public class LevelData
{
    public string title;
    public int width, height;
    public int targetScore; // score needed for the 3rd star
    public Dictionary<TileType, int> inventory = new();
    public List<Vector2Int> obstacles = new();
    public List<FixedTile> fixedTiles = new(); // levels can pre-place tiles that are never removed
    public List<ObjectSpec> objects = new();

    // 1st star: deliver all but one
    public int Required => Mathf.Max(1, objects.Count - 1);
}
