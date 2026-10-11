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
        Intro2x2(), FilterIntro3x3(), Junctions3x3(), Sorting4x4(), Finale4x4(),
    };

    // 2x2, two separate routes, no + tiles
    static LevelData Intro2x2()
    {
        var l = new LevelData { title = "Level 1", width = 2, height = 2, targetScore = 275 };
        l.inventory = Inv(ne: 1, es: 1, sw: 2, wn: 1, h: 1, v: 3);
        l.objects.Add(Obj("Red", Red, 0, -1, Dir.N, -1, 1));
        l.objects.Add(Obj("Blue", Blue, 2, 0, Dir.W, 1, 2));
        return l;
    }

    // 3x3, introduces the colored filter
    static LevelData FilterIntro3x3()
    {
        var l = new LevelData { title = "Level 2", width = 3, height = 3, targetScore = 400 };
        l.inventory = Inv(cross: 1, ne: 1, es: 1, sw: 1, wn: 1, h: 3, v: 2);
        l.filters.Add(Filter(1, 1, Dir.E, Blue));
        l.objects.Add(Obj("Red", Red, 1, -1, Dir.N, -1, 2));
        l.objects.Add(Obj("Blue", Blue, -1, 1, Dir.E, 3, 1));
        return l;
    }

    // 3x3, introduces T tiles and the bridge. No + in the supply, so the T and the bridge are needed.
    static LevelData Junctions3x3()
    {
        var l = new LevelData { title = "Level 3", width = 3, height = 3, targetScore = 550 };
        l.inventory = Inv(bridge: 1, tNES: 1, tESW: 1, tSWN: 1, ne: 1, wn: 1, h: 2, v: 5);
        l.filters.Add(Filter(2, 1, Dir.S, Blue));
        l.objects.Add(Obj("Red", Red, -1, 1, Dir.E, 2, 3));
        l.objects.Add(Obj("Blue", Blue, 0, -1, Dir.N, 2, -1));
        l.objects.Add(Obj("Green", Green, 1, 3, Dir.S, 1, -1));
        return l;
    }

    // 4x4 with two obstacles. Three objects join one road along row 2, and two filters on the last cell
    static LevelData Sorting4x4()
    {
        var l = new LevelData { title = "Level 4", width = 4, height = 4, targetScore = 600 };
        l.inventory = Inv(cross: 1, bridge: 1, tESW: 2, tSWN: 1, ne: 1, sw: 1, h: 2, v: 7);
        l.obstacles.Add(new Vector2Int(0, 0));
        l.obstacles.Add(new Vector2Int(2, 3));
        l.filters.Add(Filter(3, 2, Dir.N, Orange));
        l.filters.Add(Filter(3, 2, Dir.S, Green));
        l.objects.Add(Obj("Orange", Orange, -1, 2, Dir.E, 3, 4));
        l.objects.Add(Obj("Green", Green, 1, -1, Dir.N, 3, -1));
        l.objects.Add(Obj("Cyan", Cyan, 2, -1, Dir.N, 4, 2));
        return l;
    }

    // 4x4 finale with two obstacles, four objects, two filters
    static LevelData Finale4x4()
    {
        var l = new LevelData { title = "Level 5", width = 4, height = 4, targetScore = 725 };
        l.inventory = Inv(cross: 1, bridge: 1, tESW: 1, tSWN: 1, tWNE: 1, ne: 1, wn: 1, h: 5, v: 5);
        l.obstacles.Add(new Vector2Int(0, 2));
        l.obstacles.Add(new Vector2Int(3, 0));
        l.filters.Add(Filter(2, 1, Dir.S, Blue));
        l.filters.Add(Filter(1, 3, Dir.S, Orange));
        l.objects.Add(Obj("Red", Red, -1, 1, Dir.E, 4, 1));
        l.objects.Add(Obj("Blue", Blue, 0, -1, Dir.N, 2, -1));
        l.objects.Add(Obj("Orange", Orange, 1, 4, Dir.S, 1, -1));
        l.objects.Add(Obj("Green", Green, -1, 3, Dir.E, 4, 3));
        return l;
    }

    static ObjectSpec Obj(string name, Color color, int sx, int sy, Dir heading, int dx, int dy) =>
        new() { name = name, color = color, start = new Vector2Int(sx, sy), heading = heading, dest = new Vector2Int(dx, dy) };

    static FilterSpec Filter(int x, int y, Dir side, Color color) =>
        new() { cell = new Vector2Int(x, y), side = side, color = color };

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
