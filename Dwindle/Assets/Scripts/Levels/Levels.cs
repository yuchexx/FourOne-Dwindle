using System.Collections.Generic;
using UnityEngine;

// the 36 levels in play order, in 6 colour groups; each group starts at a pop-up (LevelPopups). the Level Maker
// (Editor/LevelTools) writes LevelN() here and its best plans into Solutions.Generated.cs
public static partial class Levels
{
    static readonly Color Red = new(0.92f, 0.30f, 0.30f);
    static readonly Color Green = new(0.25f, 0.62f, 0.35f);
    static readonly Color Cyan = new(0.25f, 0.80f, 0.85f);
    static readonly Color Blue = new(0.30f, 0.50f, 1.00f);
    static readonly Color Orange = new(1.00f, 0.60f, 0.15f);
    static readonly Color Purple = new(0.62f, 0.42f, 0.90f);

    // colour group names, shown on the Levels page
    public static string[] GroupNames => new[]
    {
        "Tiles and controls", "Junctions and combos", "Real play", "More than three objects", "Doors on one board", "Doors across boards",
    };

    // the level list in play order
    public static readonly List<LevelData> All = new()
    {
        Level1(),
        Level2(),
        Level3(),
        Level4(),
        Level5(),
        Level6(),
        Level7(),
        Level8(),
        Level9(),
        Level10(),
        Level11(),
        Level12(),
        Level13(),
        Level14(),
        Level15(),
        Level16(),
        Level17(),
        Level18(),
        Level19(),
        Level20(),
        Level21(),
        Level22(),
        Level23(),
        Level24(),
        Level25(),
        Level26(),
        Level27(),
        Level28(),
        Level29(),
        Level30(),
        Level31(),
        Level32(),
        Level33(),
        Level34(),
        Level35(),
        Level36(),
    };

    // labels and titles are the play number; teach levels need all 3 stars to unlock the next level; each colour
    // group's first level names it on the Levels page
    static Levels()
    {
        for (int i = 0; i < All.Count; i++)
        {
            var l = All[i];
            l.label = (i + 1).ToString();
            l.title = "Level " + l.label;
            l.starsToPass = l.teach ? 3 : 1;
            if (i == 0 || l.colorGroup != All[i - 1].colorGroup) l.section = l.group = GroupNames[l.colorGroup];
        }
    }

    // The first level: two corners and two straights to Red's ring, one straight already on the board.
    static LevelData Level1()
    {
        var l = new LevelData { title = "Level 1", targetScore = 250, bestTiles = 3, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((2, 2));
        l.inventory = Inv(es: 1, wn: 1, v: 2);
        l.startTiles.Add((l.At(0, 0, 1), TileType.StraightH)); // on the board at the start, free (the guide moves it down)
        Obj(l, "Red", Red, 0, -1, 0, Dir.E, 0, 1, 2);
        l.colorGroup = 0;
        l.teach = true; // needs all 3 stars to unlock the next level
        l.trick = "tiles and controls";
        l.popup = "tiles"; // the pop-up before it (LevelPopups)
        l.tutorial = "FirstLevel";
        l.solution = Solutions.Level1();
        return l;
    }

    // Routing with few tiles: mostly corners, roads around the edge, the middle unusable.
    // Score 200 with 8 tiles (target).
    static LevelData Level2()
    {
        var l = new LevelData { title = "Level 2", targetScore = 200, bestTiles = 8,
            finishHint = "Try the edges. The supply is mostly corners.", bestPlanHint = Tips.FewerTiles };
        l.SetBoards((3, 3));
        l.inventory = Inv(es: 3, wn: 3, h: 1, v: 1);
        Obj(l, "Red", Red, 0, -1, 0, Dir.E, 0, 2, 3);
        Obj(l, "Blue", Blue, 0, 3, 1, Dir.W, 0, 1, -1);
        l.colorGroup = 0;
        l.solution = Solutions.Level2();
        return l;
    }

    static LevelData Level3()
    {
        var l = new LevelData { title = "Level 3", targetScore = 375, bestTiles = 3,
            finishHint = Tips.TogetherToFinish, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((2, 2));
        l.inventory = Inv(cross: 1, h: 1, v: 1);
        Obj(l, "Red", Red, 0, 0, -1, Dir.N, 0, -1, 1);
        Obj(l, "Blue", Blue, 0, 2, 1, Dir.W, 0, 0, 2);
        l.colorGroup = 1;
        l.teach = true; // needs all 3 stars to unlock the next level
        l.trick = "the + and combo";
        l.popup = "junctions"; // the pop-up before it (LevelPopups)
        l.tutorial = "PlusCombo";
        l.solution = Solutions.Level3();
        return l;
    }

    static LevelData Level4()
    {
        var l = new LevelData { title = "Level 4", targetScore = 375, bestTiles = 3,
            finishHint = Tips.Bridge, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((2, 2));
        l.inventory = Inv(bridge: 1, v: 1, tSWN: 1);
        Obj(l, "Red", Red, 0, -1, 1, Dir.E, 0, 1, 2);
        Obj(l, "Blue", Blue, 0, 0, -1, Dir.N, 0, 0, 2);
        l.colorGroup = 1;
        l.solution = Solutions.Level4();
        return l;
    }

    // Red and Blue meet on a T and arrive together, and the + they earn is the straight Green's road is missing.
    // Score 475 with 7 tiles (target).
    static LevelData Level5()
    {
        var l = new LevelData { title = "Level 5", targetScore = 475, bestTiles = 7,
            finishHint = Tips.EarnedPlus, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((3, 3));
        l.inventory = Inv(ne: 2, es: 1, h: 1, v: 1, tNES: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 2, 3, Dir.S, 0, 3, 1);
        Obj(l, "Blue", Blue, 0, 1, 3, Dir.S, 0, 3, 2);
        Obj(l, "Green", Green, 0, 0, -1, Dir.N, 0, 3, 0);
        l.colorGroup = 2;
        l.teach = true; // needs all 3 stars to unlock the next level
        l.trick = "the earned +";
        l.popup = "realplay"; // the pop-up before it (LevelPopups)
        l.solution = Solutions.Level5();
        return l;
    }

    // generated: baX2x33_9500_9500
    // Solution:
    //   1. (1,2) I-v
    //   2. (0,2) L S-W
    //   3. (0,0) I-v
    //   4. (2,0) L N-E
    //   5. (2,1) L S-W
    //   6. (1,1) + -> Blue picks S at (1,1); Green picks W at (1,1)
    //   7. (0,1) + -> Red picks S at (0,1); Green picks W at (0,1); Red and Green arrive together, +1 + tile
    //   8. (1,0) I-v -> Blue arrives
    // Score 550 with 8 tiles (target).
    static LevelData Level6()
    {
        var l = new LevelData { title = "Level 6", targetScore = 550, bestTiles = 8,
            finishHint = Tips.OneRoute, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((3, 3));
        l.inventory = Inv(cross: 2, ne: 1, es: 1, sw: 2, v: 3);
        Obj(l, "Red", Red, 0, -1, 2, Dir.E, 0, 0, -1);
        Obj(l, "Blue", Blue, 0, 1, 3, Dir.S, 0, 1, -1);
        Obj(l, "Green", Green, 0, 3, 0, Dir.W, 0, -1, 1);
        l.colorGroup = 2;
        l.teach = true; // needs all 3 stars to unlock the next level
        l.trick = "build ahead";
        l.solution = Solutions.Level6();
        return l;
    }

    // generated: x_410
    // Solution:
    //   1. (2,2) + -> Red picks S at (2,2); Green picks S at (2,2)
    //   2. (1,1) I-h
    //   3. (0,1) L N-E
    //   4. (0,2) I-v
    //   5. (2,0) I-v
    //   6. (1,2) L N-E
    //   7. (2,1) + -> Red picks W at (2,1); Blue picks N at (2,1); Blue picks W at (2,2); Green picks E at (2,1); Red and Blue and Green arrive together, +1 + tile
    // Score 600 with 7 tiles (target).
    static LevelData Level7()
    {
        var l = new LevelData { title = "Level 7", targetScore = 600, bestTiles = 7,
            bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((3, 3));
        l.inventory = Inv(cross: 2, ne: 2, es: 1, h: 1, v: 2);
        Obj(l, "Red", Red, 0, 3, 2, Dir.W, 0, 0, 3);
        Obj(l, "Blue", Blue, 0, 2, -1, Dir.N, 0, 1, 3);
        Obj(l, "Green", Green, 0, 2, 3, Dir.S, 0, 3, 1);
        l.colorGroup = 2;
        l.teach = true; // needs all 3 stars to unlock the next level
        l.trick = "deliver all";
        l.solution = Solutions.Level7();
        return l;
    }

    // generated: daXTB_1140
    // Solution:
    //   1. (0,1) T E-S-W -> Red picks S at (0,1)
    //   2. (1,0) T S-W-N -> Green picks W at (1,0)
    //   3. (1,1) bridge
    //   4. (1,2) I-v
    //   5. (2,1) I-h
    //   6. (0,0) + -> Red picks E at (0,0); Red picks N at (1,0); Blue picks N at (0,0); Blue picks E at (0,1); Green picks W at (0,0); Red and Blue and Green arrive together, +1 + tile
    // Score 600 with 6 tiles (target).
    static LevelData Level8()
    {
        var l = new LevelData { title = "Level 8", targetScore = 600, bestTiles = 6,
            bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((3, 3));
        l.inventory = Inv(cross: 1, bridge: 1, h: 1, v: 2, tESW: 1, tSWN: 1);
        Obj(l, "Red", Red, 0, -1, 1, Dir.E, 0, 1, 3);
        Obj(l, "Blue", Blue, 0, 0, -1, Dir.N, 0, 3, 1);
        Obj(l, "Green", Green, 0, 1, -1, Dir.N, 0, -1, 0);
        l.colorGroup = 2;
        l.solution = Solutions.Level8();
        return l;
    }

    // generated: dba33x4_32000_32001
    // Solution:
    //   1. (2,2) T W-N-E -> Red picks W at (2,2); Orange picks W at (2,2)
    //   2. (0,1) I-h
    //   3. (1,1) T S-W-N -> Blue picks S at (1,1)
    //   4. (1,2) T N-E-S -> Red picks S at (1,2); Red picks S at (1,1); Green picks S at (1,2); Green picks S at (1,1); Orange picks S at (1,2); Orange picks S at (1,1)
    //   5. (0,0) L E-S
    //   6. (1,0) + -> Red picks E at (1,0); Blue picks S at (1,0); Green picks W at (1,0); Orange picks E at (1,0); Blue and Green arrive together, +1 + tile
    //   7. (2,0) T E-S-W -> Red picks S at (2,0); Orange picks E at (2,0); Red and Orange arrive together, +1 + tile
    // Score 825 with 7 tiles (target).
    static LevelData Level9()
    {
        var l = new LevelData { title = "Level 9", targetScore = 825, bestTiles = 7,
            finishHint = Tips.TogetherToFinish, bestPlanHint = Tips.TwoRoutes };
        l.SetBoards((3, 3));
        l.inventory = Inv(cross: 1, es: 1, wn: 1, h: 1, tNES: 1, tESW: 1, tSWN: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 3, 2, Dir.W, 0, 2, -1);
        Obj(l, "Blue", Blue, 0, -1, 1, Dir.E, 0, 1, -1);
        Obj(l, "Green", Green, 0, 1, 3, Dir.S, 0, 0, -1);
        Obj(l, "Orange", Orange, 0, 2, 3, Dir.S, 0, 3, 0);
        l.colorGroup = 3;
        l.teach = true; // needs all 3 stars to unlock the next level
        l.trick = "double build ahead";
        l.popup = "groups"; // the pop-up before it (LevelPopups)
        l.solution = Solutions.Level9();
        return l;
    }

    static LevelData Level10()
    {
        var l = new LevelData { title = "Level 10", targetScore = 825, bestTiles = 8,
            finishHint = Tips.TwoRoutes, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((3, 3));
        l.inventory = Inv(cross: 1, bridge: 1, ne: 1, sw: 1, h: 1, v: 2, tESW: 2);
        Obj(l, "Red", Red, 0, 1, -1, Dir.N, 0, -1, 0);
        Obj(l, "Blue", Blue, 0, 3, 1, Dir.W, 0, 0, 3);
        Obj(l, "Green", Green, 0, 2, -1, Dir.N, 0, 1, 3);
        Obj(l, "Orange", Orange, 0, 3, 2, Dir.W, 0, -1, 2);
        l.colorGroup = 3;
        l.solution = Solutions.Level10();
        return l;
    }

    // Pairs trap teach on 3x3: pairs look best on paper, but each pair's delivery breaks a junction someone still needs, so all four arrive together on the last T.
    // Solution:
    //   1. (2,1) T N-E-S -> Red picks S at (2,1)
    //   2. (0,2) I-h
    //   3. (1,2) T W-N-E -> Blue picks E at (1,2)
    //   4. (2,2) T S-W-N -> Blue picks S at (2,2); Blue picks S at (2,1); Green picks S at (2,2); Green picks S at (2,1)
    //   5. (1,0) T E-S-W
    //   6. (0,0) T E-S-W -> Orange picks E at (0,0); Orange picks E at (1,0)
    //   7. (2,0) T S-W-N -> Red picks S at (2,0); Blue picks W at (2,0); Blue picks S at (1,0); Green picks W at (2,0); Green picks W at (1,0); Green picks S at (0,0); Orange picks N at (2,0); Orange picks N at (2,1); Orange picks W at (2,2); Orange picks N at (1,2); Red and Blue and Green and Orange arrive together, +1 + tile
    // Score 750 with 7 tiles (target).
    static LevelData Level11()
    {
        var l = new LevelData { title = "Level 11", targetScore = 750, bestTiles = 7,
            finishHint = Tips.TogetherToFinish, bestPlanHint = Tips.Pairs };
        l.SetBoards((3, 3));
        l.inventory = Inv(wn: 1, h: 1, tNES: 1, tESW: 2, tSWN: 2, tWNE: 1);
        Obj(l, "Red", Red, 0, 3, 1, Dir.W, 0, 2, -1);
        Obj(l, "Blue", Blue, 0, -1, 2, Dir.E, 0, 1, -1);
        Obj(l, "Green", Green, 0, 2, 3, Dir.S, 0, 0, -1);
        Obj(l, "Orange", Orange, 0, -1, 0, Dir.E, 0, 1, 3);
        l.colorGroup = 3;
        l.solution = Solutions.Level11();
        return l;
    }

    static LevelData Level12()
    {
        var l = new LevelData { title = "Level 12", targetScore = 700, bestTiles = 6,
            finishHint = Tips.Pairs, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((3, 3));
        l.inventory = Inv(cross: 1, es: 1, sw: 1, tNES: 1, tSWN: 1, tWNE: 2);
        Obj(l, "Red", Red, 0, -1, 1, Dir.E, 0, 1, 3);
        Obj(l, "Blue", Blue, 0, 3, 2, Dir.W, 0, 0, 3);
        Obj(l, "Green", Green, 0, 0, -1, Dir.N, 0, 2, 3);
        Obj(l, "Orange", Orange, 0, 1, -1, Dir.N, 0, -1, 0);
        l.colorGroup = 3;
        l.teach = true; // needs all 3 stars to unlock the next level
        l.trick = "pairs trap";
        l.solution = Solutions.Level12();
        return l;
    }

    // Two pairs: the first pair's + becomes the corner Green is missing. No single extra tile would let all four arrive together, so the pairs plan is the way. The + in the supply is a wildcard.
    // Score 825 with 10 tiles (target).
    static LevelData Level13()
    {
        var l = new LevelData { title = "Level 13", targetScore = 825, bestTiles = 10,
            finishHint = Tips.EarnedPlus, bestPlanHint = Tips.FewerTiles };
        // delivering everyone needs the + that delivering together earns, so the hint shows at 0 or 1 star instead
        l.SetBoards((4, 4));
        l.inventory = Inv(cross: 1, ne: 3, wn: 1, tNES: 1, tESW: 2, tSWN: 1, tWNE: 2);
        l.obstacles.Add(l.At(0, 1, 2));
        Obj(l, "Red", Red, 0, 3, 4, Dir.S, 0, -1, 2);
        Obj(l, "Blue", Blue, 0, 1, 4, Dir.S, 0, 4, 1);
        Obj(l, "Green", Green, 0, 4, 2, Dir.W, 0, 2, -1);
        Obj(l, "Orange", Orange, 0, 4, 3, Dir.W, 0, 3, -1);
        l.colorGroup = 3;
        l.solution = Solutions.Level13();
        return l;
    }

    // Two pairs: Blue and Orange arrive together on the right first, then Green loops around the top-left corner
    // (through the + at (1,3) twice) to arrive with Red on (0,3). Bringing 3 together and 1 alone only scores 850.
    // Score 900 with 13 tiles (target).
    static LevelData Level14()
    {
        var l = new LevelData { title = "Level 14", targetScore = 900, bestTiles = 13,
            bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((5, 4));
        l.inventory = Inv(cross: 1, ne: 1, wn: 2, h: 3, v: 1, tNES: 1, tESW: 3, tSWN: 1, tWNE: 2);
        Obj(l, "Red", Red, 0, 1, 4, Dir.S, 0, -1, 3);
        Obj(l, "Blue", Blue, 0, 2, -1, Dir.N, 0, 3, 4);
        Obj(l, "Green", Green, 0, 5, 2, Dir.W, 0, 2, 4);
        Obj(l, "Orange", Orange, 0, -1, 1, Dir.E, 0, 5, 0);
        l.colorGroup = 3;
        l.solution = Solutions.Level14();
        return l;
    }

    static LevelData Level15()
    {
        var l = new LevelData { title = "Level 15", targetScore = 1200, bestTiles = 12,
            bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((4, 4));
        l.inventory = Inv(cross: 1, es: 2, sw: 1, wn: 1, v: 2, tNES: 2, tSWN: 3, tWNE: 1);
        Obj(l, "Red", Red, 0, -1, 3, Dir.E, 0, 2, 4);
        Obj(l, "Blue", Blue, 0, 3, 4, Dir.S, 0, 3, -1);
        Obj(l, "Green", Green, 0, -1, 1, Dir.E, 0, 0, -1);
        Obj(l, "Orange", Orange, 0, 4, 2, Dir.W, 0, 2, -1);
        Obj(l, "Cyan", Cyan, 0, 0, 4, Dir.S, 0, 1, -1);
        Obj(l, "Purple", Purple, 0, 1, 4, Dir.S, 0, -1, 2);
        l.colorGroup = 3;
        l.solution = Solutions.Level15();
        return l;
    }

    // a group of 4 and a pair on a 5x5 core; 15,208 best plans (roads up to 1 square longer)
    static LevelData Level16()
    {
        var l = new LevelData { title = "Level 16", targetScore = 1425, bestTiles = 16, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((5, 5));
        l.inventory = Inv(cross: 3, es: 1, h: 5, v: 4, tNES: 1, tESW: 2, tSWN: 2, tWNE: 3);
        Obj(l, "Red", Red, 0, 2, 5, Dir.S, 0, -1, 1);
        Obj(l, "Blue", Blue, 0, 5, 2, Dir.W, 0, 5, 1);
        Obj(l, "Green", Green, 0, -1, 3, Dir.E, 0, 3, -1);
        Obj(l, "Orange", Orange, 0, 1, -1, Dir.N, 0, 3, 5);
        Obj(l, "Purple", Purple, 0, 2, -1, Dir.N, 0, 5, 3);
        Obj(l, "Cyan", Cyan, 0, -1, 2, Dir.E, 0, 1, 5);
        l.colorGroup = 3;
        l.solution = Solutions.Level16();
        return l;
    }

    // 3 + 3 on a 5x5 core (centre +, ring, roads through the middle); 18,772 best plans from a search stopped early (54 road families)
    static LevelData Level17()
    {
        var l = new LevelData { title = "Level 17", targetScore = 1350, bestTiles = 17, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((5, 5));
        l.inventory = Inv(cross: 3, h: 2, v: 4, tNES: 2, tESW: 4, tSWN: 2, tWNE: 4);
        Obj(l, "Red", Red, 0, 1, -1, Dir.N, 0, 3, 5);
        Obj(l, "Blue", Blue, 0, 2, 5, Dir.S, 0, 5, 2);
        Obj(l, "Green", Green, 0, -1, 1, Dir.E, 0, 2, -1);
        Obj(l, "Orange", Orange, 0, 3, -1, Dir.N, 0, -1, 2);
        Obj(l, "Purple", Purple, 0, 5, 1, Dir.W, 0, -1, 3);
        Obj(l, "Cyan", Cyan, 0, 5, 3, Dir.W, 0, 1, 5);
        l.colorGroup = 3;
        l.solution = Solutions.Level17();
        return l;
    }

    static LevelData Level18()
    {
        var l = new LevelData { title = "Level 18", targetScore = 375, bestTiles = 4,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((3, 3));
        l.inventory = Inv(ne: 3, sw: 2);
        Door(l, "A", 0, 2, 0, Dir.W);
        Door(l, "A", 0, 2, 2, Dir.N);
        Door(l, "B", 0, 0, 0, Dir.S);
        Door(l, "B", 0, 0, 2, Dir.E);
        Obj(l, "Red", Red, 0, -1, 0, Dir.E, 0, 0, 3);
        Obj(l, "Blue", Blue, 0, 2, -1, Dir.N, 0, 3, 2);
        l.colorGroup = 4;
        l.teach = true; // needs all 3 stars to unlock the next level
        l.trick = "doors";
        l.popup = "doors"; // the pop-up before it (LevelPopups)
        l.solution = Solutions.Level18();
        return l;
    }

    static LevelData Level19()
    {
        var l = new LevelData { title = "Level 19", targetScore = 750, bestTiles = 6,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((3, 3));
        l.inventory = Inv(ne: 1, es: 1, sw: 1, wn: 1, h: 1, tNES: 1, tESW: 1);
        Door(l, "A", 0, 2, 0, Dir.E);
        Door(l, "A", 0, 2, 2, Dir.S);
        Door(l, "B", 0, 0, 1, Dir.N);
        Door(l, "B", 0, 1, 0, Dir.S);
        Obj(l, "Red", Red, 0, 2, -1, Dir.N, 0, 3, 2);
        Obj(l, "Blue", Blue, 0, 3, 1, Dir.W, 0, -1, 1);
        Obj(l, "Green", Green, 0, -1, 2, Dir.E, 0, 2, 3);
        l.colorGroup = 4;
        l.solution = Solutions.Level19();
        return l;
    }

    static LevelData Level20()
    {
        var l = new LevelData { title = "Level 20", targetScore = 700, bestTiles = 7,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((4, 3));
        l.inventory = Inv(ne: 1, sw: 1, v: 2, tESW: 1, tSWN: 2, tWNE: 1);
        Door(l, "A", 0, 3, 2, Dir.E);
        Door(l, "A", 0, 0, 2, Dir.W);
        Door(l, "B", 0, 2, 0, Dir.N);
        Door(l, "B", 0, 3, 0, Dir.W);
        Door(l, "C", 0, 1, 0, Dir.S);
        Door(l, "C", 0, 3, 0, Dir.S);
        Obj(l, "Red", Red, 0, -1, 1, Dir.E, 0, 3, 3);
        Obj(l, "Blue", Blue, 0, 4, 0, Dir.W, 0, 0, 3);
        Obj(l, "Green", Green, 0, 2, -1, Dir.N, 0, -1, 0);
        l.colorGroup = 4;
        l.solution = Solutions.Level20();
        return l;
    }

    static LevelData Level21()
    {
        var l = new LevelData { title = "Level 21", targetScore = 1025, bestTiles = 8,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.OneRoute };
        l.SetBoards((4, 3));
        l.inventory = Inv(es: 1, sw: 1, h: 1, tNES: 3, tESW: 1, tSWN: 2);
        Door(l, "A", 0, 2, 1, Dir.E);
        Door(l, "A", 0, 3, 0, Dir.N);
        Door(l, "B", 0, 0, 0, Dir.S);
        Door(l, "B", 0, 3, 2, Dir.S);
        Obj(l, "Red", Red, 0, -1, 1, Dir.E, 0, 2, -1);
        Obj(l, "Blue", Blue, 0, 4, 0, Dir.W, 0, 1, -1);
        Obj(l, "Green", Green, 0, 2, 3, Dir.S, 0, -1, 0);
        Obj(l, "Orange", Orange, 0, 4, 2, Dir.W, 0, 3, -1);
        l.colorGroup = 4;
        l.solution = Solutions.Level21();
        return l;
    }

    static LevelData Level22()
    {
        var l = new LevelData { title = "Level 22", targetScore = 750, bestTiles = 9,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((4, 4));
        l.inventory = Inv(ne: 1, sw: 3, wn: 1, h: 1, tNES: 1, tESW: 2, tWNE: 1);
        Door(l, "A", 0, 0, 3, Dir.S);
        Door(l, "A", 0, 1, 1, Dir.W);
        Door(l, "B", 0, 2, 3, Dir.W);
        Door(l, "B", 0, 2, 2, Dir.N);
        Door(l, "C", 0, 3, 3, Dir.S);
        Door(l, "C", 0, 0, 3, Dir.N);
        Obj(l, "Red", Red, 0, 2, -1, Dir.N, 0, 1, 4);
        Obj(l, "Blue", Blue, 0, -1, 0, Dir.E, 0, 4, 3);
        Obj(l, "Green", Green, 0, 3, -1, Dir.N, 0, 2, 4);
        l.colorGroup = 4;
        l.solution = Solutions.Level22();
        return l;
    }

    static LevelData Level23()
    {
        var l = new LevelData { title = "Level 23", targetScore = 1025, bestTiles = 9,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((4, 4));
        l.inventory = Inv(es: 2, sw: 1, h: 1, v: 1, tNES: 3, tESW: 1, tWNE: 1);
        Door(l, "A", 0, 3, 0, Dir.S);
        Door(l, "A", 0, 3, 0, Dir.N);
        Door(l, "B", 0, 3, 3, Dir.S);
        Door(l, "B", 0, 2, 0, Dir.W);
        Door(l, "C", 0, 0, 2, Dir.S);
        Door(l, "C", 0, 0, 3, Dir.E);
        Obj(l, "Red", Red, 0, 4, 2, Dir.W, 0, 2, -1);
        Obj(l, "Blue", Blue, 0, -1, 3, Dir.E, 0, 4, 0);
        Obj(l, "Green", Green, 0, -1, 1, Dir.E, 0, 3, 4);
        Obj(l, "Orange", Orange, 0, 1, -1, Dir.N, 0, 4, 1);
        l.colorGroup = 4;
        l.solution = Solutions.Level23();
        return l;
    }

    // Doors and build ahead on two 3x3 boards: one road is built ahead past the others before a delivery breaks it, and one door pair is a decoy.
    // Solution:
    //   1. b1(2,0) L S-W
    //   2. b1(1,2) I-v
    //   3. b1(1,1) I-v -> Red goes through door B, comes out facing N
    //   4. b1(0,0) I-h
    //   5. b1(1,0) T W-N-E -> Blue picks N at b1(1,0); Blue goes through door B, comes out facing N; Green picks N at b1(1,0); Green goes through door B, comes out facing S
    //   6. b2(2,2) T N-E-S -> Red picks E at b2(2,2); Blue picks N at b2(2,2); Red and Blue arrive together, +1 + tile
    //   7. b2(2,1) L N-E -> Green arrives
    // Score 700 with 7 tiles (target).
    static LevelData Level24()
    {
        var l = new LevelData { title = "Level 24", targetScore = 700, bestTiles = 7,
            finishHint = Tips.OneRoute, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((3, 3), (3, 3));
        l.inventory = Inv(ne: 1, sw: 2, h: 1, v: 2, tNES: 1, tWNE: 1);
        Door(l, "A", 1, 2, 0, Dir.N);
        Door(l, "A", 0, 2, 2, Dir.E);
        Door(l, "B", 1, 2, 2, Dir.S);
        Door(l, "B", 0, 1, 0, Dir.N);
        Obj(l, "Red", Red, 0, 1, 3, Dir.S, 1, 3, 2);
        Obj(l, "Blue", Blue, 0, -1, 0, Dir.E, 1, 2, 3);
        Obj(l, "Green", Green, 0, 2, -1, Dir.N, 1, 3, 1);
        l.colorGroup = 5;
        l.teach = true; // needs all 3 stars to unlock the next level
        l.trick = "doors across boards";
        l.tutorial = "DoorsAcross";
        l.solution = Solutions.Level24();
        return l;
    }

    static LevelData Level25()
    {
        var l = new LevelData { title = "Level 25", targetScore = 975, bestTiles = 12,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((4, 3), (4, 3));
        l.inventory = Inv(cross: 1, ne: 2, sw: 2, wn: 1, h: 1, v: 2, tNES: 2, tWNE: 2);
        Door(l, "A", 0, 1, 0, Dir.N);
        Door(l, "A", 1, 0, 1, Dir.N);
        Door(l, "B", 0, 3, 0, Dir.S);
        Door(l, "B", 1, 2, 2, Dir.N);
        Door(l, "C", 0, 3, 0, Dir.W);
        Door(l, "C", 0, 2, 0, Dir.N);
        Obj(l, "Red", Red, 0, 3, 3, Dir.S, 1, 3, 3);
        Obj(l, "Blue", Blue, 1, -1, 1, Dir.E, 1, 1, 3);
        Obj(l, "Green", Green, 0, 4, 0, Dir.W, 0, 2, -1);
        Obj(l, "Orange", Orange, 0, 4, 1, Dir.W, 1, 3, -1);
        l.colorGroup = 5;
        l.teach = true; // needs all 3 stars to unlock the next level
        l.trick = "doors between and within";
        l.solution = Solutions.Level25();
        return l;
    }

    static LevelData Level26()
    {
        var l = new LevelData { title = "Level 26", targetScore = 950, bestTiles = 13,
            finishHint = Tips.TogetherToFinish, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((3, 3), (3, 3));
        l.inventory = Inv(cross: 1, ne: 1, es: 1, sw: 1, h: 3, v: 2, tNES: 1, tESW: 2, tWNE: 2);
        Door(l, "A", 0, 0, 0, Dir.S);
        Door(l, "A", 1, 1, 0, Dir.S);
        Door(l, "B", 1, 1, 1, Dir.N);
        Door(l, "B", 0, 0, 0, Dir.W);
        Door(l, "C", 1, 0, 2, Dir.N);
        Door(l, "C", 1, 2, 0, Dir.W);
        Obj(l, "Red", Red, 1, 3, 0, Dir.W, 0, 3, 1);
        Obj(l, "Blue", Blue, 1, 3, 2, Dir.W, 0, -1, 2);
        Obj(l, "Green", Green, 0, 2, 3, Dir.S, 1, -1, 0);
        Obj(l, "Orange", Orange, 0, 3, 0, Dir.W, 1, 1, 3);
        l.colorGroup = 5;
        l.solution = Solutions.Level26();
        return l;
    }

    // Four together on two 3x3 boards: the four meet across the doors and arrive on one delivery; in smaller groups the best is 900.
    // Solution:
    //   1. b2(0,0) T E-S-W -> Red picks E at b2(0,0); Orange picks E at b2(0,0)
    //   2. b2(1,0) T W-N-E -> Red picks N at b2(1,0); Orange picks N at b2(1,0)
    //   3. b2(1,1) L S-W
    //   4. b2(0,1) T W-N-E -> Red picks W at b2(0,1); Red goes through door A; Orange picks W at b2(0,1); Orange goes through door A
    //   5. b1(1,0) L S-W
    //   6. b2(2,0) T E-S-W
    //   7. b1(0,2) I-v
    //   8. b1(0,1) T S-W-N -> Green picks S at b1(0,1)
    //   9. b2(0,2) L S-W
    //   10. b1(0,0) + -> Red picks E at b1(0,0); Blue picks W at b1(0,0); Blue goes through door A; Blue picks E at b2(0,1); Blue picks E at b2(1,0); Blue picks S at b2(2,0); Green picks W at b1(0,0); Green goes through door A; Green picks N at b2(0,1); Orange picks N at b1(0,0); Orange picks W at b1(0,1); Red and Blue and Green and Orange arrive together, +1 + tile
    // Score 950 with 10 tiles (target).
    static LevelData Level27()
    {
        var l = new LevelData { title = "Level 27", targetScore = 950, bestTiles = 10,
            bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((3, 3), (3, 3));
        l.inventory = Inv(cross: 1, sw: 3, v: 2, tESW: 2, tSWN: 1, tWNE: 2);
        Door(l, "A", 1, 0, 1, Dir.W);
        Door(l, "A", 0, 0, 0, Dir.W);
        Door(l, "B", 0, 1, 0, Dir.N);
        Door(l, "B", 1, 2, 2, Dir.N);
        Obj(l, "Red", Red, 1, 0, -1, Dir.N, 0, 1, -1);
        Obj(l, "Blue", Blue, 0, 0, -1, Dir.N, 1, 2, -1);
        Obj(l, "Green", Green, 0, 0, 3, Dir.S, 1, -1, 2);
        Obj(l, "Orange", Orange, 1, -1, 0, Dir.E, 0, -1, 1);
        l.colorGroup = 5;
        l.solution = Solutions.Level27();
        return l;
    }

    // generated: oDBA_127005
    // Solution:
    //   1. b2(2,2) L W-N
    //   2. b2(1,2) T W-N-E -> Red picks W at b2(1,2); Orange picks W at b2(1,2)
    //   3. b2(0,2) + -> Red picks W at b2(0,2); Red goes through door B, comes out facing W; Orange picks W at b2(0,2); Orange goes through door B, comes out facing W
    //   4. b2(2,0) L S-W -> Green goes through door A
    //   5. b2(0,1) L W-N
    //   6. b1(2,0) T E-S-W -> Blue picks W at b1(2,0); Blue goes through door B; Blue picks N at b2(0,2); Green picks W at b1(2,0); Green goes through door B; Green picks S at b2(0,2); Blue and Green arrive together, +1 + tile
    //   7. b1(1,1) I-v
    //   8. b1(1,2) T N-E-S
    //   9. b1(2,2) L W-N
    //   10. b1(1,0) L N-E -> Red picks E at b1(1,2); Orange picks N at b1(1,2); Red and Orange arrive together, +1 + tile
    // Score 1025 with 10 tiles (target).
    static LevelData Level28()
    {
        var l = new LevelData { title = "Level 28", targetScore = 1025, bestTiles = 10,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.TwoRoutes };
        l.SetBoards((3, 3), (3, 3));
        l.inventory = Inv(cross: 1, ne: 1, sw: 1, wn: 3, h: 1, v: 1, tNES: 1, tESW: 1, tWNE: 1);
        Door(l, "A", 0, 2, 0, Dir.E);
        Door(l, "A", 1, 2, 0, Dir.W);
        Door(l, "B", 0, 1, 0, Dir.E);
        Door(l, "B", 1, 0, 2, Dir.W);
        Obj(l, "Red", Red, 1, 2, 3, Dir.S, 0, 2, 3);
        Obj(l, "Blue", Blue, 0, 2, -1, Dir.N, 1, 0, 3);
        Obj(l, "Green", Green, 1, 2, -1, Dir.N, 1, -1, 1);
        Obj(l, "Orange", Orange, 1, 1, 3, Dir.S, 0, 1, 3);
        l.colorGroup = 5;
        l.solution = Solutions.Level28();
        return l;
    }

    // Four small boards, a pairs trap: pairs look best on paper but break junctions someone still needs. Build Blue's road first (through doors A and C), then Red, Green and Orange arrive together, and Blue finishes.
    // Score 975 with 9 tiles (target).
    static LevelData Level29()
    {
        var l = new LevelData { title = "Level 29", targetScore = 975, bestTiles = 9,
            finishHint = Tips.TogetherToFinish, bestPlanHint = Tips.Pairs };
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
        l.colorGroup = 5;
        l.solution = Solutions.Level29();
        return l;
    }

    // Four together on four 2x2 boards: everyone travels through the doors to meet and arrive on one delivery; 900 otherwise.
    // Solution:
    //   1. b1(1,0) + -> Red picks N at b1(1,0); Blue picks N at b1(1,0)
    //   2. b1(1,1) L S-W
    //   3. b3(0,1) L S-W
    //   4. b1(0,0) T N-E-S
    //   5. b2(1,1) L W-N
    //   6. b2(0,1) T N-E-S
    //   7. b4(0,1) I-h
    //   8. b4(1,1) I-h -> Green goes through door C, comes out facing E; Green picks N at b1(1,0)
    //   9. b2(0,0) T S-W-N -> Orange picks N at b2(0,0); Orange picks E at b2(0,1); Orange goes through door A; Orange picks N at b1(0,0)
    //   10. b3(0,0) L W-N
    //   11. b1(0,1) T N-E-S -> Red picks N at b1(0,1); Red goes through door B, comes out facing N; Blue picks S at b1(0,1); Blue picks S at b1(0,0); Blue goes through door A; Blue picks N at b2(0,1); Green picks S at b1(0,1); Green picks S at b1(0,0); Green goes through door A; Green picks S at b2(0,1); Green picks S at b2(0,0); Orange picks N at b1(0,1); Orange goes through door B, comes out facing S; Red and Blue and Green and Orange arrive together, +1 + tile
    // Score 950 with 11 tiles (target).
    static LevelData Level30()
    {
        var l = new LevelData { title = "Level 30", targetScore = 950, bestTiles = 11,
            finishHint = Tips.TogetherToFinish, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((2, 2), (2, 2), (2, 2), (2, 2));
        l.inventory = Inv(cross: 1, sw: 3, wn: 2, h: 2, tNES: 3, tSWN: 1);
        Door(l, "A", 1, 1, 1, Dir.N);
        Door(l, "A", 0, 0, 0, Dir.S);
        Door(l, "B", 2, 0, 1, Dir.S);
        Door(l, "B", 0, 0, 1, Dir.N);
        Door(l, "C", 0, 0, 0, Dir.E);
        Door(l, "C", 3, 1, 1, Dir.E);
        Obj(l, "Red", Red, 0, 1, -1, Dir.N, 2, -1, 1);
        Obj(l, "Blue", Blue, 0, 2, 0, Dir.W, 1, 0, 2);
        Obj(l, "Green", Green, 3, -1, 1, Dir.E, 1, 0, -1);
        Obj(l, "Orange", Orange, 1, -1, 0, Dir.E, 2, -1, 0);
        l.colorGroup = 5;
        l.solution = Solutions.Level30();
        return l;
    }

    // Opens the finale on two 5x4 boards, a pairs trap: all four arrive together on the given +, and a detour reaches the target (775 without). Door B is a decoy.
    // Score 1025 with 13 tiles (target).
    static LevelData Level31()
    {
        var l = new LevelData { title = "Level 31", targetScore = 1025, bestTiles = 13,
            finishHint = Tips.TogetherToFinish, bestPlanHint = Tips.Pairs };
        l.SetBoards((5, 4), (5, 4));
        l.inventory = Inv(cross: 1, ne: 2, es: 1, sw: 3, wn: 1, v: 1, tNES: 1, tESW: 2, tSWN: 1, tWNE: 2);
        Door(l, "A", 1, 0, 3, Dir.W);
        Door(l, "A", 0, 0, 2, Dir.W);
        Door(l, "B", 0, 4, 0, Dir.S);
        Door(l, "B", 1, 1, 3, Dir.N);
        Door(l, "C", 1, 0, 1, Dir.N);
        Door(l, "C", 0, 4, 3, Dir.N);
        Obj(l, "Red", Red, 1, -1, 2, Dir.E, 0, 5, 3);
        Obj(l, "Blue", Blue, 0, -1, 1, Dir.E, 0, 3, 4);
        Obj(l, "Green", Green, 0, 2, 4, Dir.S, 1, 2, -1);
        Obj(l, "Orange", Orange, 1, 3, -1, Dir.N, 0, -1, 0);
        l.colorGroup = 5;
        l.solution = Solutions.Level31();
        return l;
    }

    // Two 4x4 boards: Blue and Orange arrive together first, but Red and Green both still have to cross their tiles. Build both roads ahead (Red's goes through three doors), and the + the pair earns is the corner Red needs at the end. Green's ring is on its own board, so its road goes out through a door and back.
    // Score 1025 with 11 tiles (target).
    static LevelData Level32()
    {
        var l = new LevelData { title = "Level 32", targetScore = 1025, bestTiles = 11,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.TwoRoutes };
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
        l.colorGroup = 5;
        l.solution = Solutions.Level32();
        return l;
    }

    // An advanced detour on two 4x3 boards: everyone crosses boards, and Blue and Green share one long road out through the doors and back, which saves a tile. Without the detour the best is 1075, short of the 3rd star. Door D is a decoy.
    // Score 1175 with 12 tiles (target).
    static LevelData Level33()
    {
        var l = new LevelData { title = "Level 33", targetScore = 1175, bestTiles = 12,
            bestPlanHint = Tips.DetourForScore };
        l.SetBoards((4, 3), (4, 3));
        l.inventory = Inv(cross: 1, ne: 1, es: 2, sw: 1, wn: 1, h: 1, v: 3, tNES: 2, tESW: 1, tSWN: 1, tWNE: 1);
        Door(l, "A", 0, 1, 0, Dir.W);
        Door(l, "A", 1, 3, 2, Dir.W);
        Door(l, "B", 0, 2, 2, Dir.S);
        Door(l, "B", 1, 0, 0, Dir.W);
        Door(l, "C", 0, 0, 0, Dir.S);
        Door(l, "C", 1, 1, 2, Dir.W);
        Door(l, "D", 0, 3, 0, Dir.N); // decoy
        Door(l, "D", 1, 2, 2, Dir.W);
        Obj(l, "Red", Red, 1, 2, -1, Dir.N, 0, 2, 3);
        Obj(l, "Blue", Blue, 1, 0, 3, Dir.S, 1, 1, -1);
        Obj(l, "Green", Green, 1, -1, 2, Dir.E, 0, 2, -1);
        Obj(l, "Orange", Orange, 1, 4, 1, Dir.W, 1, 1, 3);
        l.colorGroup = 5;
        l.solution = Solutions.Level33();
        return l;
    }

    // The finale on two 5x5 boards: the bridge, a detour Red and Orange can't do without, a road to build ahead, and an earned + at the junction where Red and Orange meet. Door B is a decoy.
    // Score 975 with 14 tiles (target).
    static LevelData Level34()
    {
        var l = new LevelData { title = "Level 34", targetScore = 975, bestTiles = 14,
            finishHint = Tips.BridgeAndDetour, bestPlanHint = Tips.OneRoute };
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
        l.colorGroup = 5;
        l.solution = Solutions.Level34();
        return l;
    }

    // Four 3x3 boards, every door inside a board: two pairs, a detour, a route to build ahead, and the + the first pair earns is the corner Red needs. Door E is a decoy.
    // Score 1025 with 15 tiles (target).
    static LevelData Level35()
    {
        var l = new LevelData { title = "Level 35", targetScore = 1025, bestTiles = 15,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.OneRoute };
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
        Door(l, "E", 0, 1, 2, Dir.W); // decoy
        Door(l, "E", 1, 2, 0, Dir.S);
        Obj(l, "Red", Red, 3, 3, 0, Dir.W, 1, 3, 0);
        Obj(l, "Blue", Blue, 3, 1, 3, Dir.S, 0, -1, 1);
        Obj(l, "Green", Green, 2, 0, 3, Dir.S, 2, 2, -1);
        Obj(l, "Orange", Orange, 2, 3, 2, Dir.W, 2, 1, -1);
        l.colorGroup = 5;
        l.solution = Solutions.Level35();
        return l;
    }

    static LevelData Level36()
    {
        var l = new LevelData { title = "Level 36", targetScore = 850, bestTiles = 13,
            bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2));
        l.inventory = Inv(es: 3, sw: 2, wn: 1, h: 2, v: 2, tNES: 1, tSWN: 1, tWNE: 2);
        Door(l, "A", 6, 1, 1, Dir.W);
        Door(l, "A", 4, 0, 1, Dir.N);
        Door(l, "B", 1, 0, 1, Dir.N);
        Door(l, "B", 6, 0, 1, Dir.W);
        Door(l, "C", 3, 0, 0, Dir.S);
        Door(l, "C", 8, 1, 1, Dir.E);
        Door(l, "D", 4, 1, 1, Dir.N);
        Door(l, "D", 7, 0, 0, Dir.S);
        Door(l, "E", 5, 0, 0, Dir.E);
        Door(l, "E", 0, 1, 0, Dir.N);
        Door(l, "F", 6, 1, 0, Dir.E);
        Door(l, "F", 5, 0, 0, Dir.N);
        Door(l, "G", 7, 0, 0, Dir.E);
        Door(l, "G", 8, 0, 0, Dir.E);
        Door(l, "H", 2, 1, 0, Dir.W);
        Door(l, "H", 5, 0, 1, Dir.N);
        Obj(l, "Red", Red, 6, 0, -1, Dir.N, 4, -1, 1);
        Obj(l, "Blue", Blue, 6, 1, 2, Dir.S, 8, 0, -1);
        Obj(l, "Green", Green, 7, 1, -1, Dir.N, 5, 0, -1);
        Obj(l, "Orange", Orange, 0, 2, 0, Dir.W, 0, -1, 0);
        l.colorGroup = 5;
        l.solution = Solutions.Level36();
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
