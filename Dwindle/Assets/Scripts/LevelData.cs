using System.Collections.Generic;
using UnityEngine;

public enum BonusGoal { UseEveryType, NoCross, ComboDelivery, ShortestRoute }

// a moving object. starts just outside the grid facing into the grid
// completed when it steps onto its destination also just outside the grid
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
    public int required; // objects needed for the first star
    public BonusGoal bonus;
    public Dictionary<TileType, int> inventory = new();
    public List<Vector2Int> obstacles = new();
    public List<FixedTile> fixedTiles = new(); // levels can pre-place tiles that are never removed
    public List<ObjectSpec> objects = new();

    public string BonusText() => bonus switch
    {
        BonusGoal.UseEveryType => "Place at least one of every tile type (+, L, I).",
        BonusGoal.NoCross => "Deliver everything without placing a + tile.",
        BonusGoal.ShortestRoute => "Deliver every object along a shortest possible route.",
        _ => "Deliver 2+ objects with a single tile placement.",
    };
}
