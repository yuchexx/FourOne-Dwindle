using UnityEngine;

// what the player did
public enum GameActionKind { SelectTile, PlaceTile, TakeBack, ChooseDirection, Undo, ClickDoor, Restart, LevelEnd, Delivered }

// a player action, reported by GameManager.ActionDone once it has taken effect in the game.
// for the tutorial: TutorialController waits on these, so a click that missed (and changed nothing) never counts.
// for analytics: Restart, LevelEnd, Delivered and what each Undo took back
public struct GameAction
{
    public GameActionKind kind;
    public TileType? tile;    // SelectTile: the selection after the click (null = deselected); PlaceTile, TakeBack: the tile
    public int board;         // PlaceTile, TakeBack: the board, and the square in that board's own coordinates
    public Vector2Int square; //   (the same coordinates as Levels.cs)
    public string objectName; // ChooseDirection: who chose, e.g. "Red"
    public Dir direction;     // ChooseDirection: the way it faces now
    public string door;       // ClickDoor: the door's letter
    public GameActionKind undid; // Undo: what it took back (PlaceTile or TakeBack), with its tile, board and square above
    public int score;         // Restart, LevelEnd, Delivered: the score at that point
    public int stars;         // LevelEnd: 0 to 3
    public int tilesUsed;     // Restart, LevelEnd: tiles placed and kept so far (placements minus take-backs and undos)
    public int delivered;     // Restart, LevelEnd: objects delivered
    public string reason;     // LevelEnd: why it ended, e.g. "Every object was delivered!" or "You ended the level."
    public Arrival[] arrivals; // Delivered: everyone the last placement delivered (2 or more = together, which earns a +)
}

// one object delivered, and the tile its delivery broke
public struct Arrival
{
    public string objectName;
    public int doors;         // doors it went through on the way
    public bool broke;        // a tile broke (false when none of its path was left)
    public int board;         // the broken tile's board and square
    public Vector2Int square;
    public TileType brokenTile;
    public string neededBy;   // an object still waiting that needed that tile, e.g. "Green"; null when nobody did
}
