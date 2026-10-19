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
        Level1(), Level2(), Level3(), Level4(), Level5(), Level6(),
        Level7(), Level8(), Level9(), Level10(), Level11(), Level12(),
    };

    // Two separate routes: pick a tile, click a square.
    // Score 350 with 4 tiles (target).
    static LevelData Level1()
    {
        var l = new LevelData { title = "Level 1", targetScore = 350, bestTiles = 4,
            bestPlanHint = "The best plan uses fewer tiles." };
        l.SetBoards((2, 2));
        l.inventory = Inv(ne: 1, sw: 1, h: 1, v: 3);
        Obj(l, "Red", Red, 0, 0, -1, Dir.N, 0, -1, 1);
        Obj(l, "Blue", Blue, 0, 2, 0, Dir.W, 0, 1, 2);
        return l;
    }

    // The bridge: Red and Green cross over each other on it and arrive together, and the + you earn is the straight Blue is missing.
    // Score 550 with 8 tiles (target).
    static LevelData Level2()
    {
        var l = new LevelData { title = "Level 2", targetScore = 550, bestTiles = 8,
            bestPlanHint = "The best plan delivers some objects together." };
        l.SetBoards((3, 3));
        l.inventory = Inv(bridge: 1, es: 2, sw: 1, wn: 2, v: 3);
        Obj(l, "Red", Red, 0, 2, 3, Dir.S, 0, 2, -1);
        Obj(l, "Blue", Blue, 0, 1, 3, Dir.S, 0, 0, -1);
        Obj(l, "Green", Green, 0, 3, 0, Dir.W, 0, 1, -1);
        return l;
    }

    // A 4x3 board: Blue and Orange arrive together first, but Red and Green both still have to cross their tiles. Build Red's road and Green's road ahead so both pass first. Fixing only one isn't enough.
    // Score 900 with 9 tiles (target).
    static LevelData Level3()
    {
        var l = new LevelData { title = "Level 3", targetScore = 900, bestTiles = 9,
            bestPlanHint = "The best plan builds parts of two routes early, so both pass shared tiles before anyone is delivered." };
        l.SetBoards((4, 3));
        l.inventory = Inv(cross: 1, sw: 1, wn: 1, h: 4, tNES: 1, tESW: 1, tSWN: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, -1, 0, Dir.E, 0, 4, 0);
        Obj(l, "Blue", Blue, 0, -1, 1, Dir.E, 0, 1, 3);
        Obj(l, "Green", Green, 0, 3, 3, Dir.S, 0, 2, -1);
        Obj(l, "Orange", Orange, 0, 1, -1, Dir.N, 0, 2, 3);
        return l;
    }

    // The first pairs trap: the starts are spread out and two pairs look best on paper (900), but each pair's delivery breaks a junction someone still needs. All four have to arrive together on the last T.
    // Score 825 with 10 tiles (target).
    static LevelData Level4()
    {
        var l = new LevelData { title = "Level 4", targetScore = 825, bestTiles = 10,
            bestPlanHint = "Splitting into pairs breaks a junction someone still needs. The best plan brings more of them in together." };
        l.SetBoards((4, 4));
        l.inventory = Inv(cross: 1, ne: 1, sw: 1, wn: 1, h: 1, v: 3, tNES: 1, tESW: 1, tSWN: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 2, 4, Dir.S, 0, 3, -1);
        Obj(l, "Blue", Blue, 0, -1, 0, Dir.E, 0, 3, 4);
        Obj(l, "Green", Green, 0, 4, 0, Dir.W, 0, -1, 2);
        Obj(l, "Orange", Orange, 0, 4, 3, Dir.W, 0, -1, 3);
        return l;
    }

    // Two pairs: the first pair's + becomes the corner Green is missing. No single extra tile would let all four arrive together, so the pairs plan is the way.
    // Score 825 with 10 tiles (target).
    static LevelData Level5()
    {
        var l = new LevelData { title = "Level 5", targetScore = 825, bestTiles = 10,
            bestPlanHint = "The best plan delivers some objects together." };
        l.SetBoards((4, 4));
        l.inventory = Inv(ne: 3, es: 1, wn: 1, tNES: 1, tESW: 2, tSWN: 1, tWNE: 2);
        l.obstacles.Add(l.At(0, 1, 2));
        Obj(l, "Red", Red, 0, 3, 4, Dir.S, 0, -1, 2);
        Obj(l, "Blue", Blue, 0, 1, 4, Dir.S, 0, 4, 1);
        Obj(l, "Green", Green, 0, 4, 2, Dir.W, 0, 2, -1);
        Obj(l, "Orange", Orange, 0, 4, 3, Dir.W, 0, 3, -1);
        return l;
    }

    // Doors: every door sits between two squares, so each time an object comes out it picks which way to face. All three meet on one T and arrive together.
    // Score 825 with 6 tiles (target).
    static LevelData Level6()
    {
        var l = new LevelData { title = "Level 6", targetScore = 825, bestTiles = 6,
            bestPlanHint = "The best plan delivers some objects together." };
        l.SetBoards((2, 2), (2, 2));
        l.inventory = Inv(cross: 1, ne: 2, wn: 1, h: 1, tSWN: 3);
        Door(l, "A", 1, 1, 1, Dir.S);
        Door(l, "A", 0, 0, 0, Dir.E);
        Door(l, "B", 1, 1, 1, Dir.W);
        Door(l, "B", 0, 1, 1, Dir.S);
        Obj(l, "Red", Red, 1, 1, -1, Dir.N, 0, 0, 2);
        Obj(l, "Blue", Blue, 0, 1, -1, Dir.N, 1, 1, 2);
        Obj(l, "Green", Green, 0, -1, 1, Dir.E, 1, -1, 0);
        return l;
    }

    // The bridge again, on two boards: Blue goes north and Green goes west through the same square without meeting. Red's ring is on its own board, but its road goes out through a door and back.
    // Score 675 with 9 tiles (target).
    static LevelData Level7()
    {
        var l = new LevelData { title = "Level 7", targetScore = 675, bestTiles = 9,
            bestPlanHint = "The best plan delivers some objects together." };
        l.SetBoards((3, 3), (3, 3));
        l.inventory = Inv(bridge: 1, ne: 2, es: 2, wn: 2, h: 1, v: 1, tNES: 1, tESW: 1);
        Door(l, "A", 0, 2, 0, Dir.S);
        Door(l, "A", 1, 2, 2, Dir.W);
        Door(l, "B", 0, 0, 2, Dir.N);
        Door(l, "B", 1, 0, 2, Dir.S);
        Obj(l, "Red", Red, 0, 1, 3, Dir.S, 0, 3, 0);
        Obj(l, "Blue", Blue, 1, 1, -1, Dir.N, 1, 0, 3);
        Obj(l, "Green", Green, 1, 3, 0, Dir.W, 1, 0, -1);
        return l;
    }

    // Four small boards, a pairs trap: pairs look best on paper but break junctions someone still needs. Build Blue's road first (through doors A and C), then Red, Green and Orange arrive together, and Blue finishes.
    // Score 975 with 9 tiles (target).
    static LevelData Level8()
    {
        var l = new LevelData { title = "Level 8", targetScore = 975, bestTiles = 9,
            bestPlanHint = "Splitting into pairs breaks a junction someone still needs. The best plan brings more of them in together." };
        l.SetBoards((2, 2), (2, 2), (2, 2), (2, 2));
        l.inventory = Inv(cross: 1, ne: 3, wn: 1, v: 3, tNES: 1, tESW: 1, tWNE: 1);
        Door(l, "A", 0, 0, 1, Dir.E);
        Door(l, "A", 3, 1, 0, Dir.W);
        Door(l, "B", 2, 1, 0, Dir.W);
        Door(l, "B", 0, 1, 1, Dir.S);
        Door(l, "C", 1, 1, 1, Dir.S);
        Door(l, "C", 0, 0, 0, Dir.S);
        Obj(l, "Red", Red, 0, -1, 1, Dir.E, 2, 1, -1);
        Obj(l, "Blue", Blue, 3, 0, 2, Dir.S, 1, 2, 0);
        Obj(l, "Green", Green, 0, 2, 0, Dir.W, 2, 2, 0);
        Obj(l, "Orange", Orange, 2, 1, 2, Dir.S, 0, 1, 2);
        return l;
    }

    // An advanced detour on two 4x3 boards: everyone crosses boards, and Blue and Green share one long road out through the doors and back, which saves a tile. Without the detour the best is 1075, short of the 3rd star.
    // Score 1175 with 12 tiles (target).
    static LevelData Level9()
    {
        var l = new LevelData { title = "Level 9", targetScore = 1175, bestTiles = 12,
            bestPlanHint = "The best plan uses a door as a shortcut back to an object's own board." };
        l.SetBoards((4, 3), (4, 3));
        l.inventory = Inv(cross: 1, ne: 1, es: 2, sw: 1, wn: 1, h: 1, v: 3, tNES: 2, tESW: 1, tSWN: 1, tWNE: 1);
        Door(l, "A", 0, 1, 0, Dir.W);
        Door(l, "A", 1, 3, 2, Dir.W);
        Door(l, "B", 0, 2, 2, Dir.S);
        Door(l, "B", 1, 0, 0, Dir.W);
        Door(l, "C", 0, 0, 0, Dir.S);
        Door(l, "C", 1, 1, 2, Dir.W);
        Obj(l, "Red", Red, 1, 2, -1, Dir.N, 0, 2, 3);
        Obj(l, "Blue", Blue, 1, 0, 3, Dir.S, 1, 1, -1);
        Obj(l, "Green", Green, 1, -1, 2, Dir.E, 0, 2, -1);
        Obj(l, "Orange", Orange, 1, 4, 1, Dir.W, 1, 1, 3);
        return l;
    }

    // Two 4x4 boards: Blue and Orange arrive together first, but Red and Green both still have to cross their tiles. Build both roads ahead (Red's goes through three doors), and the + the pair earns is the corner Red needs at the end. Green's ring is on its own board, so its road goes out through a door and back.
    // Score 1025 with 11 tiles (target).
    static LevelData Level10()
    {
        var l = new LevelData { title = "Level 10", targetScore = 1025, bestTiles = 11,
            bestPlanHint = "The best plan builds parts of two routes early, so both pass shared tiles before anyone is delivered." };
        l.SetBoards((4, 4), (4, 4));
        l.inventory = Inv(es: 1, sw: 1, wn: 2, h: 1, v: 1, tESW: 3, tSWN: 1, tWNE: 2);
        Door(l, "A", 1, 1, 2, Dir.W);
        Door(l, "A", 0, 3, 0, Dir.E);
        Door(l, "B", 0, 0, 3, Dir.W);
        Door(l, "B", 1, 1, 1, Dir.N);
        Door(l, "C", 0, 1, 3, Dir.N);
        Door(l, "C", 1, 0, 1, Dir.E);
        Obj(l, "Red", Red, 0, 2, -1, Dir.N, 1, -1, 0);
        Obj(l, "Blue", Blue, 1, -1, 3, Dir.E, 0, 2, 4);
        Obj(l, "Green", Green, 1, 2, 4, Dir.S, 1, -1, 1);
        Obj(l, "Orange", Orange, 1, 0, 4, Dir.S, 0, 3, -1);
        return l;
    }

    // Four 3x3 boards, every door inside a board: two pairs, a detour, a route to build ahead, and the + the first pair earns is the corner Red needs.
    // Score 1025 with 15 tiles (target).
    static LevelData Level11()
    {
        var l = new LevelData { title = "Level 11", targetScore = 1025, bestTiles = 15,
            bestPlanHint = "The best plan builds part of one route early, so it passes a shared tile before anyone is delivered." };
        l.SetBoards((3, 3), (3, 3), (3, 3), (3, 3));
        l.inventory = Inv(ne: 3, sw: 1, h: 2, v: 4, tNES: 3, tESW: 1, tSWN: 2);
        Door(l, "A", 0, 1, 0, Dir.W);
        Door(l, "A", 2, 0, 1, Dir.N);
        Door(l, "B", 2, 1, 1, Dir.E);
        Door(l, "B", 3, 0, 1, Dir.E);
        Door(l, "C", 2, 2, 2, Dir.W);
        Door(l, "C", 3, 1, 0, Dir.N);
        Door(l, "D", 2, 2, 0, Dir.N);
        Door(l, "D", 1, 2, 2, Dir.W);
        Obj(l, "Red", Red, 3, 3, 0, Dir.W, 1, 3, 0);
        Obj(l, "Blue", Blue, 3, 1, 3, Dir.S, 0, -1, 1);
        Obj(l, "Green", Green, 2, 0, 3, Dir.S, 2, 2, -1);
        Obj(l, "Orange", Orange, 2, 3, 2, Dir.W, 2, 1, -1);
        return l;
    }

    // The finale on two 5x5 boards: the bridge, a detour Red and Orange can't do without, a road to build ahead, and an earned + at the junction where Red and Orange meet. Door B is a decoy.
    // Score 975 with 14 tiles (target).
    static LevelData Level12()
    {
        var l = new LevelData { title = "Level 12", targetScore = 975, bestTiles = 14,
            bestPlanHint = "The best plan builds part of one route early, so it passes a shared tile before anyone is delivered." };
        l.SetBoards((5, 5), (5, 5));
        l.inventory = Inv(cross: 1, bridge: 1, ne: 1, es: 1, sw: 1, wn: 2, h: 1, v: 3, tNES: 2, tESW: 1, tWNE: 1);
        l.obstacles.Add(l.At(1, 1, 3));
        l.obstacles.Add(l.At(1, 1, 1));
        Door(l, "A", 0, 4, 1, Dir.S);
        Door(l, "A", 1, 0, 1, Dir.N);
        Door(l, "B", 1, 3, 0, Dir.N);
        Door(l, "B", 0, 2, 2, Dir.N);
        Door(l, "C", 1, 3, 4, Dir.E);
        Door(l, "C", 0, 3, 3, Dir.W);
        Obj(l, "Red", Red, 0, 5, 3, Dir.W, 1, 0, -1);
        Obj(l, "Blue", Blue, 0, 5, 2, Dir.W, 0, 4, 5);
        Obj(l, "Green", Green, 1, 3, 5, Dir.S, 0, 1, -1);
        Obj(l, "Orange", Orange, 1, 2, 5, Dir.S, 1, -1, 1);
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
