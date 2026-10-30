using System.Linq;
using UnityEngine;

// every level's best plans are generated into Solutions.Generated.cs. these are the short forms it uses
public static partial class Solutions
{
    static Square Sq(int board, int x, int y) => new Square { board = board, square = new Vector2Int(x, y) };

    static AllowedTiles Allow(int board, int x, int y, params TileType[] tiles) => new AllowedTiles { at = Sq(board, x, y), tiles = tiles };

    // squares as (board, x, y)
    static Square[] Squares(params (int board, int x, int y)[] s) => s.Select(c => Sq(c.board, c.x, c.y)).ToArray();

    static Choice Turn(int board, int x, int y, Dir way) => new Choice { at = Sq(board, x, y), way = way };
    static Choice OutOf(string door, Dir way) => new Choice { door = door, way = way };

    static Road RoadOf(string objectName, (int board, int x, int y)[] squares, params Choice[] choices) =>
        new Road { objectName = objectName, squares = Squares(squares), choices = choices };

    static Delivery Deliver(string[] objects, params Completion[] completedBy) => new Delivery { objects = objects, completedBy = completedBy };
    static Completion At(int board, int x, int y, params (int board, int x, int y)[] before) =>
        new Completion { at = Sq(board, x, y), before = Squares(before) };

    static Trick TrickOf(string name, TrickNeed need) => new Trick { name = name, need = need };
}
