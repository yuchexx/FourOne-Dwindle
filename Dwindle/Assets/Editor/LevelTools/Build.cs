using System.Collections.Generic; using System.Linq; using UnityEngine;

// builds levels the same way new1's Levels.cs does
static class LB
{
    public static readonly Color Red = new(0.92f, 0.30f, 0.30f), Green = new(0.25f, 0.62f, 0.35f),
        Cyan = new(0.25f, 0.80f, 0.85f), Blue = new(0.30f, 0.50f, 1.00f), Orange = new(1.00f, 0.60f, 0.15f);
    public static readonly (string name, Color color)[] Palette =
        { ("Red", Red), ("Blue", Blue), ("Green", Green), ("Orange", Orange), ("Cyan", Cyan) };

    public static LevelData New(string title, params (int, int)[] boards)
    {
        var l = new LevelData { title = title };
        l.SetBoards(boards);
        return l;
    }

    public static void Obj(LevelData l, string name, Color color, int sb, int sx, int sy, Dir heading, int db, int dx, int dy) =>
        l.objects.Add(new ObjectSpec { name = name, color = color, heading = heading,
            start = l.At(sb, sx, sy), dest = l.At(db, dx, dy), startBoard = sb, destBoard = db });

    public static void Door(LevelData l, string label, int board, int x, int y, Dir side) =>
        l.doors.Add(new DoorSpec { label = label, board = board, cell = l.At(board, x, y), side = side });

    public static Dictionary<TileType, int> Inv(int cross = 0, int bridge = 0, int tNES = 0, int tESW = 0, int tSWN = 0, int tWNE = 0,
        int ne = 0, int es = 0, int sw = 0, int wn = 0, int h = 0, int v = 0) => new()
    {
        { TileType.Cross, cross }, { TileType.Bridge, bridge },
        { TileType.TeeNES, tNES }, { TileType.TeeESW, tESW }, { TileType.TeeSWN, tSWN }, { TileType.TeeWNE, tWNE },
        { TileType.CornerNE, ne }, { TileType.CornerES, es }, { TileType.CornerSW, sw }, { TileType.CornerWN, wn },
        { TileType.StraightH, h }, { TileType.StraightV, v },
    };

    public static string Short(TileType t) => t switch
    {
        TileType.Cross => "+", TileType.Bridge => "Br", TileType.StraightH => "H", TileType.StraightV => "V",
        TileType.CornerNE => "NE", TileType.CornerES => "ES", TileType.CornerSW => "SW", TileType.CornerWN => "WN",
        TileType.TeeNES => "Tnes", TileType.TeeESW => "Tesw", TileType.TeeSWN => "Tswn", _ => "Twne",
    };

    // "b2(1,0)" as players see it
    public static string Local(LevelData l, Vector2Int c)
    {
        int b = l.BoardOf(c);
        if (b < 0) return $"({c.x},{c.y})";
        var p = c - l.boards[b].origin;
        return l.boards.Count > 1 ? $"b{b + 1}({p.x},{p.y})" : $"({p.x},{p.y})";
    }
}
