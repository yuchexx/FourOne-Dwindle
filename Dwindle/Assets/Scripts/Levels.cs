using System.Collections.Generic;
using UnityEngine;

// level layouts
// coordinates: (0,0) - bottom-left cell
// (x = -1, x = width, y = -1, y = height) - where objects and destinations sit
public static class Levels
{
    static readonly Color Red = new(0.92f, 0.30f, 0.30f);
    static readonly Color Green = new(0.25f, 0.62f, 0.35f);
    static readonly Color Cyan = new(0.25f, 0.80f, 0.85f);
    static readonly Color Blue = new(0.30f, 0.50f, 1.00f);
    static readonly Color Orange = new(1.00f, 0.60f, 0.15f);

    // 3 levels
    public static readonly List<LevelData> All = new() { Intro2x2(), Sketch3x3(), Shared4x4() };

    // 2x2, two separate routes, a tight supply and no + tiles
    static LevelData Intro2x2()
    {
        var l = new LevelData { title = "Level 1", width = 2, height = 2, required = 1, bonus = BonusGoal.ShortestRoute };
        l.inventory = Inv(cross: 0, ne: 1, es: 1, sw: 1, wn: 1, h: 1, v: 2);
        l.objects.Add(Obj("Red", Red, 0, -1, Dir.N, -1, 1));
        l.objects.Add(Obj("Blue", Blue, 2, 0, Dir.W, 1, 2));
        return l;
    }

    // 3x3, three objects, generous supply
    static LevelData Sketch3x3()
    {
        var l = new LevelData { title = "Level 2", width = 3, height = 3, required = 2, bonus = BonusGoal.UseEveryType };
        l.inventory = Inv(cross: 2, ne: 2, es: 1, sw: 2, wn: 2, h: 4, v: 2);
        l.objects.Add(Obj("Red", Red, 1, -1, Dir.N, 0, 3));
        l.objects.Add(Obj("Green", Green, 2, 3, Dir.S, -1, 1));
        l.objects.Add(Obj("Cyan", Cyan, -1, 0, Dir.E, 3, 0));
        return l;
    }

    // 4x4 with an obstacle - paths share cross tiles for the rewarding rule
    static LevelData Shared4x4()
    {
        var l = new LevelData { title = "Level 3", width = 4, height = 4, required = 2, bonus = BonusGoal.ComboDelivery };
        l.inventory = Inv(cross: 3, ne: 2, es: 2, sw: 2, wn: 2, h: 3, v: 3);
        l.obstacles.Add(new Vector2Int(0, 2));
        l.objects.Add(Obj("Red", Red, -1, 3, Dir.E, 4, 0));
        l.objects.Add(Obj("Blue", Blue, 0, -1, Dir.N, 4, 3));
        l.objects.Add(Obj("Green", Green, 1, 4, Dir.S, 1, -1));
        return l;
    }

    static ObjectSpec Obj(string name, Color color, int sx, int sy, Dir heading, int dx, int dy) =>
        new() { name = name, color = color, start = new Vector2Int(sx, sy), heading = heading, dest = new Vector2Int(dx, dy) };

    static Dictionary<TileType, int> Inv(int cross, int ne, int es, int sw, int wn, int h, int v) => new()
    {
        { TileType.Cross, cross },
        { TileType.CornerNE, ne }, { TileType.CornerES, es }, { TileType.CornerSW, sw }, { TileType.CornerWN, wn },
        { TileType.StraightH, h }, { TileType.StraightV, v },
    };
}
