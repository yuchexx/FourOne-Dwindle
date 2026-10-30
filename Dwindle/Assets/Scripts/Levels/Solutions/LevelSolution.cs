using UnityEngine;

// a level's best plans: every way to reach its target score (the 3rd star) that the Level Maker found.
// every level's data is generated into Solutions.Generated.cs, a section each (Editor/LevelTools/SolutionExport.cs) and linked from
// Levels.cs with l.solution = Solutions.LevelN(). for analytics (how a player differs from the best plans) and hints.
// squares are given as GameAction gives them: the board (from 0) and the square in that board's own coordinates
public class LevelSolution
{
    public int target, bestTiles;
    public Trick[] tricks;
    public RoadFamily[] families; // the sets of roads the best plans use
    // the plan the hints start from on an empty board: the best plan with the fewest steps (tiles and arrows)
    public int answerFamily, answerOrder;
}

public struct Square
{
    public int board;
    public Vector2Int square;
    public override string ToString() => $"b{board}({square.x},{square.y})";
}

// one set of roads, one per object, with every tile and delivery order that reaches the target on them
public class RoadFamily
{
    public Road[] roads;           // in the level's object order
    public AllowedTiles[] squares; // every square these roads use and the tiles a best plan can put there. any mix
                                   // the game lets the player place is a best plan: the supply (earned + included) decides the mix
    public DeliveryOrder[] orders; // the delivery orders that reach the target on these roads
}

public class Road
{
    public string objectName;
    public Square[] squares;  // in the order the object passes them (a square it crosses twice is listed twice)
    public Choice[] choices;  // the arrows it needs, in the order it meets them
}

// a junction (door null): the way out of `at`, asked only when the tile placed there gives a choice.
// coming out of a door (door = its letter): the way the object faces
public struct Choice
{
    public Square at;
    public string door;
    public Dir way;
}

public class AllowedTiles
{
    public Square at;
    public TileType[] tiles;
}

public class DeliveryOrder
{
    public Delivery[] deliveries; // in order
}

// objects arriving on the same placement (which of them reaches its ring first doesn't matter)
public class Delivery
{
    public string[] objects;
    public Completion[] completedBy; // the squares whose tile can deliver them
}

// before: tiles that must already be placed when `at` delivers, because a later object has to pass them
// first (the delivery can break one of them)
public class Completion
{
    public Square at;
    public Square[] before;
}

public enum TrickNeed { NotUsed, Optional, RequiredForTarget, RequiredToFinish }

public struct Trick
{
    public string name; // "Detour", "Earned +", "Bridge", "Build ahead", "Delivered together"
    public TrickNeed need;
}
