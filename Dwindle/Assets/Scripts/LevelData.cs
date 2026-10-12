using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// positions are in one shared grid
public class ObjectSpec
{
    public string name;
    public Color color;
    public Vector2Int start;
    public Dir heading;
    public Vector2Int dest;
    public int startBoard, destBoard;
}

public class FixedTile
{
    public Vector2Int pos;
    public TileType type;
}

// one board
public class BoardSpec
{
    public int width, height;
    public Vector2Int origin;

    public bool Contains(Vector2Int c) =>
        c.x >= origin.x && c.y >= origin.y && c.x < origin.x + width && c.y < origin.y + height;
}

// a teleport door on the edge between cell and its neighbor on side
public class DoorSpec
{
    public string label;
    public int board;
    public Vector2Int cell;
    public Dir side;

    public Vector2Int Neighbor => cell + DirUtil.Offset(side);

    // is the door on this edge, seen from either side?
    public bool IsOn(Vector2Int from, Dir d) =>
        (from == cell && d == side) || (from == Neighbor && d == DirUtil.Opposite(side));
}

public class LevelData
{
    public string title;
    public int width, height; // the shared grid that holds every board
    public int targetScore; // score needed for the 3rd star
    public Dictionary<TileType, int> inventory = new();
    public List<BoardSpec> boards = new();
    public List<DoorSpec> doors = new();
    public List<Vector2Int> obstacles = new();
    public List<FixedTile> fixedTiles = new(); // levels can pre-place tiles that are never removed
    public List<ObjectSpec> objects = new();

    const int BoardGap = 3;  // cells between boards: a ring line for each, and one empty line
    const int BoardsPerRow = 2; // a third board goes on a second row, so the cells stay big

    // 1st star deliver all but one
    public int Required => Mathf.Max(1, objects.Count - 1);

    // lays the boards out in rows of two, first row on top, each row and each board centered
    public void SetBoards(params (int width, int height)[] sizes)
    {
        boards.Clear();
        var rows = new List<List<(int w, int h)>>();
        for (int i = 0; i < sizes.Length; i += BoardsPerRow)
            rows.Add(sizes.Skip(i).Take(BoardsPerRow).ToList());

        int RowWidth(List<(int w, int h)> row) => row.Sum(s => s.w) + BoardGap * (row.Count - 1);
        width = rows.Max(RowWidth);
        height = rows.Sum(row => row.Max(s => s.h)) + BoardGap * (rows.Count - 1);

        int top = height;
        foreach (var row in rows)
        {
            int rowHeight = row.Max(s => s.h);
            int bottom = top - rowHeight;
            int x = (width - RowWidth(row)) / 2;
            foreach (var (w, h) in row)
            {
                boards.Add(new BoardSpec { width = w, height = h, origin = new Vector2Int(x, bottom + (rowHeight - h) / 2) });
                x += w + BoardGap;
            }
            top = bottom - BoardGap;
        }
    }

    // a board's own coordinate in the shared grid - (0,0) = that board's bottom-left cell
    public Vector2Int At(int board, int x, int y) => boards[board].origin + new Vector2Int(x, y);

    // the board a cell belongs to, or -1 outside every board
    public int BoardOf(Vector2Int c)
    {
        for (int i = 0; i < boards.Count; i++)
            if (boards[i].Contains(c)) return i;
        return -1;
    }

    public DoorSpec DoorOnEdge(Vector2Int from, Dir d) => doors.FirstOrDefault(door => door.IsOn(from, d));

    public DoorSpec Partner(DoorSpec door) => doors.First(other => other != door && other.label == door.label);
}
