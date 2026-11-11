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

    // parts (BuildGuide/LEVEL_PLAN.md): teach + practice 1-12, hard 13-19, combine 20-24, finale 25-28
    public static readonly List<LevelData> All = new()
    {
        Level1(), Level2(), Level3(), Level4(), Level5(), Level6(), Level7(), Level8(), Level9(),
        Level10(), Level11(), Level12(), Level13(), Level14(), Level15(), Level16(), Level17(), Level18(), Level19(),
        Level20(), Level21(), Level22(), Level23(), Level24(), Level25(), Level26(), Level27(), Level28(),
    };

    // The first level, with the Basics tutorial (Tutorials.Basics). Red goes straight up and Blue straight left, and
    // both need the top-left square: a + there, placed last, delivers them together. Green then turns right on the +
    // that earns, in the bottom-right square. the + given goes to Red and Blue: given to Green, they'd block each other.
    // the two L's (the supply's 5th and 6th slots, open to the west) are spare: no object can use them.
    // Score 550 with 4 tiles (target): 3 x 100 + combo 2 x 50 + 2 unused x 75 (the two L's left over).
    // worked out by hand; check it with the Level Maker's solver (Solve), then Rebuild all solutions
    static LevelData Level1()
    {
        var l = new LevelData { title = "Level 1", targetScore = 550, bestTiles = 4,
            finishHint = Tips.EarnedPlus, bestPlanHint = Tips.FewerTiles };
        l.section = "Learn"; // the Levels page starts a part here
        l.tutorial = "Basics";
        l.teach = true;
        l.SetBoards((2, 2));
        l.inventory = Inv(cross: 1, sw: 1, wn: 1, h: 1, v: 1);
        Obj(l, "Red", Red, 0, 0, -1, Dir.N, 0, 0, 2);    // ring above the top-left square
        Obj(l, "Blue", Blue, 0, 2, 1, Dir.W, 0, -1, 1);  // ring left of the top-left square, across Red's way
        Obj(l, "Green", Green, 0, 1, -1, Dir.N, 0, 2, 0); // starts right of Red, ring below Blue
        l.solution = Solutions.Level1();
        return l;
    }

    // Red and Blue meet on a T and arrive together, and the + they earn is the straight Green's road is missing.
    // Score 475 with 7 tiles (target).
    static LevelData Level2()
    {
        var l = new LevelData { title = "Level 2", targetScore = 475, bestTiles = 7,
            finishHint = Tips.EarnedPlus, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((3, 3));
        l.inventory = Inv(ne: 2, es: 1, h: 1, v: 1, tNES: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 2, 3, Dir.S, 0, 3, 1);
        Obj(l, "Blue", Blue, 0, 1, 3, Dir.S, 0, 3, 2);
        Obj(l, "Green", Green, 0, 0, -1, Dir.N, 0, 3, 0);
        l.solution = Solutions.Level2();
        return l;
    }

    // Routing teach: corners and straights, just enough. Blue's straight way along the bottom would take the sideways
    // straights Red needs, so Blue bends up through the middle and back.
    // Score 300 with 12 tiles (target).
    static LevelData Level3()
    {
        var l = new LevelData { title = "Level 3", targetScore = 300, bestTiles = 11,
            bestPlanHint = Tips.Routing };
        l.teach = true;
        l.SetBoards((4, 3));
        l.inventory = Inv(ne: 2, es: 2, sw: 1, wn: 2, h: 3, v: 1);
        Obj(l, "Red", Red, 0, 2, 3, Dir.S, 0, -1, 2);
        Obj(l, "Blue", Blue, 0, 4, 0, Dir.W, 0, 0, -1);
        Obj(l, "Green", Green, 0, 4, 1, Dir.W, 0, 3, 3);
        l.solution = Solutions.Level3();
        return l;
    }

    // Routing with few tiles: mostly corners, roads around the edge, the middle unusable.
    // Score 200 with 8 tiles (target).
    static LevelData Level4()
    {
        var l = new LevelData { title = "Level 4", targetScore = 200, bestTiles = 8,
            bestPlanHint = Tips.FewerTiles };
        l.SetBoards((3, 3));
        l.inventory = Inv(es: 3, wn: 3, h: 1, v: 1);
        Obj(l, "Red", Red, 0, -1, 0, Dir.E, 0, 2, 3);
        Obj(l, "Blue", Blue, 0, 3, 1, Dir.W, 0, 1, -1);
        l.solution = Solutions.Level4();
        return l;
    }

    // The bridge: Red and Blue cross over each other on it and arrive together.
    // Score 450 with 6 tiles (target).
    static LevelData Level5()
    {
        var l = new LevelData { title = "Level 5", targetScore = 450, bestTiles = 6,
            finishHint = Tips.Bridge, bestPlanHint = Tips.TogetherForScore };
        l.teach = true;
        l.starsToPass = 2; // the trick is needed to deliver everyone
        l.SetBoards((3, 3));
        l.inventory = Inv(bridge: 1, ne: 2, es: 1, sw: 1, wn: 1, v: 1);
        Obj(l, "Red", Red, 0, 3, 1, Dir.W, 0, 2, 3);
        Obj(l, "Blue", Blue, 0, 3, 2, Dir.W, 0, 2, -1);
        l.solution = Solutions.Level5();
        return l;
    }

    // The bridge: Red and Green cross over each other on it and arrive together, and the + you earn is the straight Blue is missing.
    // Score 550 with 8 tiles (target).
    static LevelData Level6()
    {
        var l = new LevelData { title = "Level 6", targetScore = 550, bestTiles = 8,
            finishHint = Tips.Bridge, bestPlanHint = Tips.FewerTiles };
        // delivering everyone needs the + that delivering together earns, so the hint shows at 0 or 1 star instead
        l.SetBoards((3, 3));
        l.inventory = Inv(bridge: 1, es: 2, sw: 1, wn: 2, v: 3);
        Obj(l, "Red", Red, 0, 2, 3, Dir.S, 0, 2, -1);
        Obj(l, "Blue", Blue, 0, 1, 3, Dir.S, 0, 0, -1);
        Obj(l, "Green", Green, 0, 3, 0, Dir.W, 0, 1, -1);
        l.solution = Solutions.Level6();
        return l;
    }

    // Build ahead: Red's road through the shared column has to be built before Green's delivery breaks it. Without it, not everyone gets delivered. The + in the supply is a wildcard.
    // Score 550 with 7 tiles (target).
    static LevelData Level7()
    {
        var l = new LevelData { title = "Level 7", targetScore = 550, bestTiles = 7,
            finishHint = Tips.OneRoute, bestPlanHint = Tips.FewerTiles };
        l.teach = true;
        l.starsToPass = 2; // the trick is needed to deliver everyone
        l.SetBoards((4, 3));
        l.inventory = Inv(cross: 1, es: 1, sw: 1, v: 1, tNES: 1, tESW: 1, tSWN: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 0, 3, Dir.S, 0, 4, 0);
        Obj(l, "Blue", Blue, 0, 4, 1, Dir.W, 0, 2, -1);
        Obj(l, "Green", Green, 0, 1, 3, Dir.S, 0, 1, -1);
        l.solution = Solutions.Level7();
        return l;
    }

    // Blue's road through the top-left is built ahead before Green arrives, then Red and Blue arrive together. The + in the supply is a wildcard.
    // Score 550 with 8 tiles (target).
    static LevelData Level8()
    {
        var l = new LevelData { title = "Level 8", targetScore = 550, bestTiles = 8,
            bestPlanHint = Tips.OneRoute };
        l.SetBoards((4, 3));
        l.inventory = Inv(cross: 1, wn: 1, h: 1, v: 2, tNES: 1, tESW: 2, tWNE: 1);
        Obj(l, "Red", Red, 0, 3, 3, Dir.S, 0, 4, 0);
        Obj(l, "Blue", Blue, 0, -1, 1, Dir.E, 0, 2, -1);
        Obj(l, "Green", Green, 0, 1, 3, Dir.S, 0, -1, 2);
        l.solution = Solutions.Level8();
        return l;
    }

    // The pairs trap, small: each pair's delivery breaks a junction someone still needs, so all four arrive together on the last T. Pairs can't deliver everyone.
    // Score 825 with 7 tiles (target).
    static LevelData Level9()
    {
        var l = new LevelData { title = "Level 9", targetScore = 825, bestTiles = 7,
            finishHint = Tips.Pairs, bestPlanHint = Tips.FewerTiles };
        l.teach = true;
        l.starsToPass = 2; // the trick is needed to deliver everyone
        l.SetBoards((4, 3));
        l.inventory = Inv(cross: 1, es: 1, wn: 1, v: 1, tNES: 1, tESW: 2, tSWN: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 4, 0, Dir.W, 0, -1, 1);
        Obj(l, "Blue", Blue, 0, -1, 0, Dir.E, 0, 2, -1);
        Obj(l, "Green", Green, 0, 0, 3, Dir.S, 0, 3, -1);
        Obj(l, "Orange", Orange, 0, 1, -1, Dir.N, 0, 4, 1);
        l.solution = Solutions.Level9();
        return l;
    }

    // The starts are spread out and two pairs look best on paper (900), but each pair's delivery breaks a junction someone still needs. All four have to arrive together on the last T.
    // Score 825 with 10 tiles (target).
    static LevelData Level10()
    {
        var l = new LevelData { title = "Level 10", targetScore = 825, bestTiles = 10,
            finishHint = Tips.Pairs, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((4, 4));
        l.inventory = Inv(cross: 1, ne: 1, sw: 1, wn: 1, h: 1, v: 3, tNES: 1, tESW: 1, tSWN: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 2, 4, Dir.S, 0, 3, -1);
        Obj(l, "Blue", Blue, 0, -1, 0, Dir.E, 0, 3, 4);
        Obj(l, "Green", Green, 0, 4, 0, Dir.W, 0, -1, 2);
        Obj(l, "Orange", Orange, 0, 4, 3, Dir.W, 0, -1, 3);
        l.solution = Solutions.Level10();
        return l;
    }

    // Laid out for the Doors tutorial (Tutorials.Doors), the doors lesson: two 2x1 boards, each with door A on the edge between its
    // two squares. Red walks right into the left board's door, comes out of the right board's door, faces right
    // (facing left leads off the board) and walks on to its ring. The door is a wall, so the left board's right square
    // and the right board's left square are never used.
    // Score 150 with 2 tiles (target): 1 x 100 + used a door 1 x 50, no tiles left over. every delivery scores exactly
    // this, so there's no 2-star case and no best plan hint
    static LevelData Level11()
    {
        var l = new LevelData { title = "Level 11", targetScore = 150, bestTiles = 2 };
        l.tutorial = "Doors";
        l.teach = true;
        l.SetBoards((2, 1), (2, 1));
        l.inventory = Inv(h: 2);
        Door(l, "A", 0, 0, 0, Dir.E); // between the left board's two squares
        Door(l, "A", 1, 0, 0, Dir.E); // between the right board's two squares
        Obj(l, "Red", Red, 0, -1, 0, Dir.E, 1, 2, 0);
        l.solution = Solutions.Level11();
        return l;
    }

    // Blue's ring is on its own board, but its road goes out through door B and back. Door A is a decoy.
    // Score 500 with 6 tiles (target).
    static LevelData Level12()
    {
        var l = new LevelData { title = "Level 12", targetScore = 500, bestTiles = 6,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((3, 3), (3, 3));
        l.inventory = Inv(ne: 1, es: 1, sw: 1, h: 2, tESW: 1, tSWN: 1);
        Door(l, "A", 1, 2, 2, Dir.N);
        Door(l, "A", 0, 0, 0, Dir.S);
        Door(l, "B", 0, 2, 0, Dir.W);
        Door(l, "B", 1, 0, 1, Dir.S);
        Obj(l, "Red", Red, 1, 1, -1, Dir.N, 1, -1, 1);
        Obj(l, "Blue", Blue, 0, 3, 0, Dir.W, 0, 1, -1);
        l.solution = Solutions.Level12();
        return l;
    }

    // Blue goes down through the bridge while Orange crosses it along the top. Two pairs.
    // Score 825 with 10 tiles (target).
    static LevelData Level13()
    {
        var l = new LevelData { title = "Level 13", targetScore = 825, bestTiles = 10,
            finishHint = Tips.Bridge, bestPlanHint = Tips.TogetherForScore };
        l.section = "Challenge"; // the Levels page starts a part here
        l.SetBoards((4, 3));
        l.inventory = Inv(bridge: 1, sw: 1, wn: 1, h: 3, v: 1, tNES: 2, tSWN: 2);
        Obj(l, "Red", Red, 0, 3, 3, Dir.S, 0, 4, 0);
        Obj(l, "Blue", Blue, 0, 1, 3, Dir.S, 0, 1, -1);
        Obj(l, "Green", Green, 0, -1, 1, Dir.E, 0, 2, -1);
        Obj(l, "Orange", Orange, 0, 3, -1, Dir.N, 0, -1, 2);
        l.solution = Solutions.Level13();
        return l;
    }

    // Blue and Green arrive together on the left, and the + they earn is Red's last piece at the top.
    // Score 550 with 8 tiles (target).
    static LevelData Level14()
    {
        var l = new LevelData { title = "Level 14", targetScore = 550, bestTiles = 8,
            finishHint = Tips.EarnedPlus, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((4, 3));
        l.inventory = Inv(sw: 1, h: 2, v: 1, tNES: 1, tESW: 1, tSWN: 2, tWNE: 1);
        Obj(l, "Red", Red, 0, -1, 2, Dir.E, 0, 2, 3);
        Obj(l, "Blue", Blue, 0, 4, 1, Dir.W, 0, -1, 0);
        Obj(l, "Green", Green, 0, 2, -1, Dir.N, 0, 0, 3);
        l.solution = Solutions.Level14();
        return l;
    }

    // A 4x3 board: Blue and Orange arrive together first, but Red and Green both still have to cross their tiles. Build Red's road and Green's road ahead so both pass first. Fixing only one isn't enough.
    // Score 900 with 9 tiles (target).
    static LevelData Level15()
    {
        var l = new LevelData { title = "Level 15", targetScore = 900, bestTiles = 9,
            finishHint = Tips.TogetherToFinish, bestPlanHint = Tips.TwoRoutes };
        l.SetBoards((4, 3));
        l.inventory = Inv(cross: 1, sw: 1, wn: 1, h: 4, tNES: 1, tESW: 1, tSWN: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, -1, 0, Dir.E, 0, 4, 0);
        Obj(l, "Blue", Blue, 0, -1, 1, Dir.E, 0, 1, 3);
        Obj(l, "Green", Green, 0, 3, 3, Dir.S, 0, 2, -1);
        Obj(l, "Orange", Orange, 0, 1, -1, Dir.N, 0, 2, 3);
        l.solution = Solutions.Level15();
        return l;
    }

    // Red and Green arrive together on the given +, so Blue's and Orange's roads through it are built ahead first. Then Blue and Orange arrive together.
    // Score 825 with 9 tiles (target).
    static LevelData Level16()
    {
        var l = new LevelData { title = "Level 16", targetScore = 825, bestTiles = 9,
            bestPlanHint = Tips.TwoRoutes };
        l.SetBoards((4, 4));
        l.inventory = Inv(cross: 1, es: 1, sw: 1, h: 1, v: 2, tNES: 2, tESW: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 1, -1, Dir.N, 0, 4, 0);
        Obj(l, "Blue", Blue, 0, 3, 4, Dir.S, 0, 0, -1);
        Obj(l, "Green", Green, 0, 2, 4, Dir.S, 0, 2, -1);
        Obj(l, "Orange", Orange, 0, 4, 3, Dir.W, 0, -1, 1);
        l.solution = Solutions.Level16();
        return l;
    }

    // Two pairs: the first pair's + becomes the corner Green is missing. No single extra tile would let all four arrive together, so the pairs plan is the way. The + in the supply is a wildcard.
    // Score 825 with 10 tiles (target).
    static LevelData Level17()
    {
        var l = new LevelData { title = "Level 17", targetScore = 825, bestTiles = 10,
            finishHint = Tips.EarnedPlus, bestPlanHint = Tips.FewerTiles };
        // delivering everyone needs the + that delivering together earns, so the hint shows at 0 or 1 star instead
        l.SetBoards((4, 4));
        l.inventory = Inv(cross: 1, ne: 3, wn: 1, tNES: 1, tESW: 2, tSWN: 1, tWNE: 2);
        l.obstacles.Add(l.At(0, 1, 2));
        Obj(l, "Red", Red, 0, 3, 4, Dir.S, 0, -1, 2);
        Obj(l, "Blue", Blue, 0, 1, 4, Dir.S, 0, 4, 1);
        Obj(l, "Green", Green, 0, 4, 2, Dir.W, 0, 2, -1);
        Obj(l, "Orange", Orange, 0, 4, 3, Dir.W, 0, 3, -1);
        l.solution = Solutions.Level17();
        return l;
    }

    // An advanced detour on two 4x3 boards: everyone crosses boards, and Blue and Green share one long road out through the doors and back, which saves a tile. Without the detour the best is 1075, short of the 3rd star. Door D is a decoy.
    // Score 1175 with 12 tiles (target).
    static LevelData Level18()
    {
        var l = new LevelData { title = "Level 18", targetScore = 1175, bestTiles = 12,
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
        l.solution = Solutions.Level18();
        return l;
    }

    // A bigger pairs trap: all four arrive together on the top-right T. The + in the supply is a lure; the best plan doesn't place it.
    // Score 825 with 12 tiles (target).
    static LevelData Level19()
    {
        var l = new LevelData { title = "Level 19", targetScore = 825, bestTiles = 12,
            bestPlanHint = Tips.Pairs };
        l.SetBoards((5, 4));
        l.inventory = Inv(cross: 1, ne: 1, wn: 1, h: 3, v: 1, tNES: 1, tESW: 3, tSWN: 2, tWNE: 1);
        Obj(l, "Red", Red, 0, -1, 0, Dir.E, 0, 4, 4);
        Obj(l, "Blue", Blue, 0, 5, 2, Dir.W, 0, 4, -1);
        Obj(l, "Green", Green, 0, 1, -1, Dir.N, 0, 3, 4);
        Obj(l, "Orange", Orange, 0, 0, 4, Dir.S, 0, 3, -1);
        l.solution = Solutions.Level19();
        return l;
    }

    // The bridge again, on two boards: Blue goes north and Green goes west through the same square without meeting. Red's ring is on its own board, but its road goes out through a door and back.
    // Score 675 with 9 tiles (target).
    static LevelData Level20()
    {
        var l = new LevelData { title = "Level 20", targetScore = 675, bestTiles = 9,
            finishHint = Tips.BridgeAndDetour, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((3, 3), (3, 3));
        l.inventory = Inv(bridge: 1, ne: 2, es: 2, wn: 2, h: 1, v: 1, tNES: 1, tESW: 1);
        Door(l, "A", 0, 2, 0, Dir.S);
        Door(l, "A", 1, 2, 2, Dir.W);
        Door(l, "B", 0, 0, 2, Dir.N);
        Door(l, "B", 1, 0, 2, Dir.S);
        Obj(l, "Red", Red, 0, 1, 3, Dir.S, 0, 3, 0);
        Obj(l, "Blue", Blue, 1, 1, -1, Dir.N, 1, 0, 3);
        Obj(l, "Green", Green, 1, 3, 0, Dir.W, 1, 0, -1);
        l.solution = Solutions.Level20();
        return l;
    }

    // Four small boards, a pairs trap: pairs look best on paper but break junctions someone still needs. Build Blue's road first (through doors A and C), then Red, Green and Orange arrive together, and Blue finishes.
    // Score 975 with 9 tiles (target).
    static LevelData Level21()
    {
        var l = new LevelData { title = "Level 21", targetScore = 975, bestTiles = 9,
            finishHint = Tips.Pairs, bestPlanHint = Tips.FewerTiles };
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
        l.solution = Solutions.Level21();
        return l;
    }

    // Here pairs win: Red and Blue arrive together on the given +, and the + they earn lets Green and Orange arrive together.
    // Score 825 with 10 tiles (target).
    static LevelData Level22()
    {
        var l = new LevelData { title = "Level 22", targetScore = 825, bestTiles = 10,
            finishHint = Tips.EarnedPlus, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((4, 4));
        l.inventory = Inv(cross: 1, ne: 1, sw: 2, h: 2, v: 1, tNES: 1, tESW: 1, tSWN: 2);
        Obj(l, "Red", Red, 0, -1, 1, Dir.E, 0, 3, -1);
        Obj(l, "Blue", Blue, 0, -1, 2, Dir.E, 0, 0, -1);
        Obj(l, "Green", Green, 0, 4, 0, Dir.W, 0, 2, 4);
        Obj(l, "Orange", Orange, 0, 2, -1, Dir.N, 0, -1, 3);
        l.solution = Solutions.Level22();
        return l;
    }

    // Blue's and Green's roads are built ahead before Red and Orange arrive on the given +, and the + they earn is the piece Blue needs.
    // Score 750 with 10 tiles (target).
    static LevelData Level23()
    {
        var l = new LevelData { title = "Level 23", targetScore = 750, bestTiles = 10,
            finishHint = Tips.EarnedPlus, bestPlanHint = Tips.TwoRoutes };
        l.SetBoards((4, 4));
        l.inventory = Inv(cross: 1, es: 1, sw: 1, h: 1, v: 2, tNES: 1, tESW: 1, tSWN: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 1, -1, Dir.N, 0, -1, 0);
        Obj(l, "Blue", Blue, 0, 4, 1, Dir.W, 0, -1, 3);
        Obj(l, "Green", Green, 0, 3, 4, Dir.S, 0, -1, 1);
        Obj(l, "Orange", Orange, 0, 0, -1, Dir.N, 0, 4, 0);
        l.solution = Solutions.Level23();
        return l;
    }

    // Pairs trap and build ahead: Red goes first, Blue's road is built ahead, then Blue, Green and Orange arrive together.
    // Score 850 with 12 tiles (target).
    static LevelData Level24()
    {
        var l = new LevelData { title = "Level 24", targetScore = 850, bestTiles = 12,
            bestPlanHint = Tips.Pairs };
        l.SetBoards((5, 4));
        l.inventory = Inv(cross: 1, ne: 1, wn: 2, h: 3, v: 1, tNES: 1, tESW: 3, tSWN: 1, tWNE: 2);
        Obj(l, "Red", Red, 0, 1, 4, Dir.S, 0, -1, 3);
        Obj(l, "Blue", Blue, 0, 2, -1, Dir.N, 0, 3, 4);
        Obj(l, "Green", Green, 0, 5, 2, Dir.W, 0, 2, 4);
        Obj(l, "Orange", Orange, 0, -1, 1, Dir.E, 0, 5, 0);
        l.solution = Solutions.Level24();
        return l;
    }

    // Opens the finale on two 5x4 boards, a pairs trap: all four arrive together on the given +, and a detour reaches the target (775 without). Door B is a decoy.
    // Score 1025 with 13 tiles (target).
    static LevelData Level25()
    {
        var l = new LevelData { title = "Level 25", targetScore = 1025, bestTiles = 13,
            finishHint = Tips.Pairs, bestPlanHint = Tips.DetourForScore };
        l.section = "Finale"; // the Levels page starts a part here
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
        l.solution = Solutions.Level25();
        return l;
    }

    // Two 4x4 boards: Blue and Orange arrive together first, but Red and Green both still have to cross their tiles. Build both roads ahead (Red's goes through three doors), and the + the pair earns is the corner Red needs at the end. Green's ring is on its own board, so its road goes out through a door and back.
    // Score 1025 with 11 tiles (target).
    static LevelData Level26()
    {
        var l = new LevelData { title = "Level 26", targetScore = 1025, bestTiles = 11,
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
        l.solution = Solutions.Level26();
        return l;
    }

    // Four 3x3 boards, every door inside a board: two pairs, a detour, a route to build ahead, and the + the first pair earns is the corner Red needs. Door E is a decoy.
    // Score 1025 with 15 tiles (target).
    static LevelData Level27()
    {
        var l = new LevelData { title = "Level 27", targetScore = 1025, bestTiles = 15,
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
        l.solution = Solutions.Level27();
        return l;
    }

    // The finale on two 5x5 boards: the bridge, a detour Red and Orange can't do without, a road to build ahead, and an earned + at the junction where Red and Orange meet. Door B is a decoy.
    // Score 975 with 14 tiles (target).
    static LevelData Level28()
    {
        var l = new LevelData { title = "Level 28", targetScore = 975, bestTiles = 14,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.OneRoute };
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
        l.solution = Solutions.Level28();
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
