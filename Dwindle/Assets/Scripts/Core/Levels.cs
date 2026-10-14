using System.Collections.Generic;
using UnityEngine;

// level layouts
public static class Levels
{
    static readonly Color Red = new(0.92f, 0.30f, 0.30f);
    static readonly Color Green = new(0.25f, 0.62f, 0.35f);
    static readonly Color Cyan = new(0.25f, 0.80f, 0.85f);
    static readonly Color Blue = new(0.30f, 0.50f, 1.00f);
    static readonly Color Orange = new(1.00f, 0.60f, 0.15f);

    public static readonly List<LevelData> All = new()
    {
        Level1(), Level2(), Level3(), Level4(), Level5(), Level6(), Level7(), Level8(),
    };

    // 1 board, 2x2
    static LevelData Level1()
    {
        var l = new LevelData { title = "Level 1", targetScore = 350 };
        l.SetBoards((2, 2));
        l.inventory = Inv(ne: 1, es: 1, sw: 1, wn: 1, h: 1, v: 2);
        Obj(l, "Red", Red, 0, 0, -1, Dir.N, 0, -1, 1);
        Obj(l, "Blue", Blue, 0, 2, 0, Dir.W, 0, 1, 2);
        return l;
    }

    // 1 board, 3x3
    static LevelData Level2()
    {
        var l = new LevelData { title = "Level 2", targetScore = 900 };
        l.SetBoards((3, 3));
        l.inventory = Inv(cross: 2, ne: 2, es: 1, sw: 2, wn: 2, h: 4, v: 2);
        Obj(l, "Red", Red, 0, 1, -1, Dir.N, 0, 0, 3);
        Obj(l, "Green", Green, 0, 2, 3, Dir.S, 0, -1, 1);
        Obj(l, "Cyan", Cyan, 0, -1, 0, Dir.E, 0, 3, 0);
        return l;
    }

    // 1 board, 4x4 with an obstacle
    static LevelData Level3()
    {
        var l = new LevelData { title = "Level 3", targetScore = 825 };
        l.SetBoards((4, 4));
        l.inventory = Inv(cross: 3, ne: 2, es: 2, sw: 2, wn: 2, h: 3, v: 3);
        l.obstacles.Add(l.At(0, 0, 2));
        Obj(l, "Red", Red, 0, -1, 3, Dir.E, 0, 4, 0);
        Obj(l, "Blue", Blue, 0, 0, -1, Dir.N, 0, 4, 3);
        Obj(l, "Green", Green, 0, 1, 4, Dir.S, 0, 1, -1);
        return l;
    }

    // 2 boards, 2x2, one pair of doors on the edges
    static LevelData Level4()
    {
        var l = new LevelData { title = "Level 4", targetScore = 475 };
        l.SetBoards((2, 2), (2, 2));
        l.inventory = Inv(ne: 1, es: 1, sw: 1, wn: 1, h: 5, v: 1);
        Door(l, "A", 0, 1, 0, Dir.E);
        Door(l, "A", 1, 0, 1, Dir.W);
        Obj(l, "Red", Red, 0, -1, 0, Dir.E, 1, 1, 2);
        Obj(l, "Blue", Blue, 1, 2, 0, Dir.W, 1, 0, -1);
        return l;
    }

    // 2 boards, 2x2, two door pairs
    static LevelData Level5()
    {
        var l = new LevelData { title = "Level 5", targetScore = 725 };
        l.SetBoards((2, 2), (2, 2));
        l.inventory = Inv(cross: 1, tESW: 1, tSWN: 1, ne: 1, es: 1, wn: 1, h: 4, v: 1);
        Door(l, "A", 0, 0, 1, Dir.E); // inside board 1
        Door(l, "A", 1, 0, 0, Dir.W);
        Door(l, "B", 0, 1, 1, Dir.N);
        Door(l, "B", 1, 1, 0, Dir.N); // inside board 2
        Obj(l, "Red", Red, 0, -1, 1, Dir.E, 1, 2, 0);
        Obj(l, "Blue", Blue, 1, 2, 1, Dir.W, 0, 2, 0);
        Obj(l, "Green", Green, 0, 1, -1, Dir.N, 0, -1, 0);
        return l;
    }

    // 2 boards, 3x3
    static LevelData Level6()
    {
        var l = new LevelData { title = "Level 6", targetScore = 1050 };
        l.SetBoards((3, 3), (3, 3));
        l.inventory = Inv(bridge: 1, tNES: 1, tESW: 1, tSWN: 1, tWNE: 1, ne: 1, es: 1, sw: 2, wn: 1, h: 3, v: 1);
        Door(l, "A", 0, 1, 1, Dir.E); // inside board 1
        Door(l, "A", 1, 0, 1, Dir.W);
        Door(l, "B", 0, 0, 2, Dir.W);
        Door(l, "B", 1, 1, 1, Dir.N); // inside board 2
        Obj(l, "Red", Red, 0, -1, 1, Dir.E, 1, 3, 1);
        Obj(l, "Blue", Blue, 1, 1, 3, Dir.S, 0, 1, -1);
        Obj(l, "Green", Green, 0, 0, 3, Dir.S, 1, 3, 2);
        Obj(l, "Orange", Orange, 0, -1, 0, Dir.E, 0, 3, 0);
        return l;
    }

    // 4 boards, 2x2 (1 2 on top, 3 4 below)
    static LevelData Level7()
    {
        var l = new LevelData { title = "Level 7", targetScore = 1050 };
        l.SetBoards((2, 2), (2, 2), (2, 2), (2, 2));
        l.inventory = Inv(tNES: 1, tESW: 1, tWNE: 1, ne: 1, es: 1, sw: 1, wn: 1, h: 2, v: 3);
        Door(l, "A", 0, 0, 1, Dir.E); // inside board 1
        Door(l, "A", 1, 0, 0, Dir.W);
        Door(l, "B", 0, 0, 0, Dir.S);
        Door(l, "B", 2, 1, 0, Dir.N); // inside board 3
        Door(l, "C", 1, 1, 1, Dir.S); // inside board 2
        Door(l, "C", 3, 0, 1, Dir.N);
        Door(l, "D", 2, 0, 0, Dir.E); // inside board 3
        Door(l, "D", 3, 0, 0, Dir.W);
        Obj(l, "Red", Red, 0, -1, 1, Dir.E, 3, 2, 0);
        Obj(l, "Blue", Blue, 1, 2, 1, Dir.W, 2, -1, 0);
        Obj(l, "Green", Green, 2, 0, -1, Dir.N, 1, 1, 2);
        return l;
    }

    // 2 boards, 5x5, two obstacles
    static LevelData Level8()
    {
        var l = new LevelData { title = "Level 8", targetScore = 1325 };
        l.SetBoards((5, 5), (5, 5));
        l.inventory = Inv(cross: 1, bridge: 1, tNES: 1, tESW: 1, tSWN: 1, tWNE: 1, ne: 1, es: 3, sw: 2, wn: 3, h: 8, v: 3);
        l.obstacles.Add(l.At(0, 1, 1));
        l.obstacles.Add(l.At(1, 3, 3));
        Door(l, "A", 0, 3, 3, Dir.E); // inside board 1
        Door(l, "A", 1, 0, 1, Dir.W);
        Door(l, "B", 0, 2, 0, Dir.S);
        Door(l, "B", 1, 2, 3, Dir.N); // inside board 2
        Door(l, "C", 0, 1, 3, Dir.N); // inside board 1
        Door(l, "C", 1, 4, 2, Dir.E);
        Door(l, "D", 0, 4, 1, Dir.E);
        Door(l, "D", 1, 1, 1, Dir.S); // inside board 2
        Obj(l, "Red", Red, 0, -1, 0, Dir.E, 1, 5, 4);
        Obj(l, "Blue", Blue, 1, -1, 3, Dir.E, 0, 3, -1);
        Obj(l, "Green", Green, 0, 2, 5, Dir.S, 1, 2, -1);
        Obj(l, "Orange", Orange, 1, 5, 0, Dir.W, 0, -1, 3);
        Obj(l, "Cyan", Cyan, 1, 4, 5, Dir.S, 1, 4, -1);
        return l;
    }

    // an object starting at (sx,sy) on one board, with its ring at (dx,dy) on another (or the same) board
    static void Obj(LevelData l, string name, Color color, int startBoard, int sx, int sy, Dir heading,
                    int destBoard, int dx, int dy) =>
        l.objects.Add(new ObjectSpec
        {
            name = name, color = color, heading = heading,
            start = l.At(startBoard, sx, sy), dest = l.At(destBoard, dx, dy),
            startBoard = startBoard, destBoard = destBoard,
        });

    // one door of a pair: on the `side` edge of (x,y) on that board
    static void Door(LevelData l, string label, int board, int x, int y, Dir side) =>
        l.doors.Add(new DoorSpec { label = label, board = board, cell = l.At(board, x, y), side = side });

    // every count defaults to 0, so a level only lists the tiles it hands out
    static Dictionary<TileType, int> Inv(int cross = 0, int bridge = 0,
        int tNES = 0, int tESW = 0, int tSWN = 0, int tWNE = 0,
        int ne = 0, int es = 0, int sw = 0, int wn = 0, int h = 0, int v = 0) => new()
    {
        { TileType.Cross, cross }, { TileType.Bridge, bridge },
        { TileType.TeeNES, tNES }, { TileType.TeeESW, tESW }, { TileType.TeeSWN, tSWN }, { TileType.TeeWNE, tWNE },
        { TileType.CornerNE, ne }, { TileType.CornerES, es }, { TileType.CornerSW, sw }, { TileType.CornerWN, wn },
        { TileType.StraightH, h }, { TileType.StraightV, v },
    };
}
