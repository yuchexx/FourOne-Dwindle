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

    // parts: learn 1-12 (teach + practice), challenge 13-36, finale 37-44, each in order of difficulty
    public static readonly List<LevelData> All = new()
    {
        Level1(), Level2(), Level3(), Level4(), Level5(), Level6(), Level7(), Level8(), Level9(), Level10(),
        Level11(), Level12(), Level13(), Level14(), Level15(), Level16(), Level17(), Level18(), Level19(), Level20(),
        Level21(), Level22(), Level23(), Level24(), Level25(), Level26(), Level27(), Level28(), Level29(), Level30(),
        Level31(), Level32(), Level33(), Level34(), Level35(), Level36(), Level37(), Level38(), Level39(), Level40(),
        Level41(), Level42(), Level43(), Level44(),
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
        l.starsToPass = 3; // a teach level is passed when it's solved properly
        l.SetBoards((2, 2));
        l.inventory = Inv(cross: 1, sw: 1, wn: 1, h: 1, v: 1);
        Obj(l, "Red", Red, 0, 0, -1, Dir.N, 0, 0, 2);    // ring above the top-left square
        Obj(l, "Blue", Blue, 0, 2, 1, Dir.W, 0, -1, 1);  // ring left of the top-left square, across Red's way
        Obj(l, "Green", Green, 0, 1, -1, Dir.N, 0, 2, 0); // starts right of Red, ring below Blue
        l.solution = Solutions.Level1();
        return l;
    }

    // Together + earned + practice: Red and Blue arrive together at the bottom, and the + they earn finishes Green's road along the top.
    // Solution:
    //   1. (2,0) T W-N-E -> Blue picks W at (2,0)
    //   2. (2,1) L E-S
    //   3. (0,0) I-h
    //   4. (1,0) T E-S-W -> Red picks E at (1,0); Red picks N at (2,0); Blue picks W at (1,0); Red and Blue arrive together, +1 + tile
    //   5. (2,2) L W-N
    //   6. (1,2) + -> Green picks W at (1,2)
    //   7. (0,2) I-h -> Green arrives
    // Score 475 with 7 tiles (target).
    static LevelData Level2()
    {
        var l = new LevelData { title = "Level 2", targetScore = 475, bestTiles = 7,
            finishHint = Tips.EarnedPlus, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((3, 3));
        l.inventory = Inv(es: 2, wn: 1, h: 2, tESW: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 1, -1, Dir.N, 0, 3, 1);
        Obj(l, "Blue", Blue, 0, 3, 0, Dir.W, 0, -1, 0);
        Obj(l, "Green", Green, 0, 2, 3, Dir.S, 0, -1, 2);
        l.solution = Solutions.Level2();
        return l;
    }

    // Bridge teach on 2x2: Red and Blue cross over each other on the bridge and arrive together.
    // Solution:
    //   1. (0,0) L E-S
    //   2. (1,1) L E-S
    //   3. (1,0) bridge -> Red and Blue arrive together, +1 + tile
    // Score 450 with 3 tiles (target).
    static LevelData Level3()
    {
        var l = new LevelData { title = "Level 3", targetScore = 450, bestTiles = 3,
            finishHint = Tips.Bridge, bestPlanHint = Tips.TogetherForScore };
        l.tutorial = "Bridge"; // for the tutorial (Tutorials.Bridge.cs): one step framing the bridge's supply card
        l.teach = true;
        l.starsToPass = 3; // a teach level is passed when it's solved properly
        l.SetBoards((2, 2));
        l.inventory = Inv(bridge: 1, es: 2, sw: 1);
        Obj(l, "Red", Red, 0, 0, -1, Dir.N, 0, 2, 0);
        Obj(l, "Blue", Blue, 0, 1, -1, Dir.N, 0, 2, 1);
        l.solution = Solutions.Level3();
        return l;
    }

    // Bridge practice on 3x3 with three objects: two roads cross on the bridge, and Red and Blue arrive together.
    // Solution:
    //   1. (1,0) I-v
    //   2. (1,2) T N-E-S
    //   3. (0,1) I-h
    //   4. (2,2) bridge
    //   5. (1,1) T S-W-N -> Red picks N at (1,1); Red picks N at (1,2); Blue picks N at (1,1); Blue picks E at (1,2); Red and Blue arrive together, +1 + tile
    //   6. (2,1) L N-E -> Green arrives
    // Score 550 with 6 tiles (target).
    static LevelData Level4()
    {
        var l = new LevelData { title = "Level 4", targetScore = 550, bestTiles = 6,
            finishHint = Tips.Bridge, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((3, 3));
        l.inventory = Inv(bridge: 1, ne: 1, wn: 1, h: 1, v: 1, tNES: 1, tSWN: 1);
        Obj(l, "Red", Red, 0, 1, -1, Dir.N, 0, 1, 3);
        Obj(l, "Blue", Blue, 0, -1, 1, Dir.E, 0, 3, 2);
        Obj(l, "Green", Green, 0, 2, 3, Dir.S, 0, 3, 1);
        l.solution = Solutions.Level4();
        return l;
    }

    // Laid out for the Doors tutorial (Tutorials.Doors), the doors lesson: two 2x1 boards, each with door A on the edge between its
    // two squares. Red walks right into the left board's door, comes out of the right board's door, faces right
    // (facing left leads off the board) and walks on to its ring. The door is a wall, so the left board's right square
    // and the right board's left square are never used.
    // Score 150 with 2 tiles (target): 1 x 100 + used a door 1 x 50, no tiles left over. every delivery scores exactly
    // this, so there's no 2-star case and no best plan hint
    static LevelData Level5()
    {
        var l = new LevelData { title = "Level 5", targetScore = 150, bestTiles = 2 };
        l.tutorial = "Doors";
        l.teach = true;
        l.starsToPass = 3; // a teach level is passed when it's solved properly
        l.SetBoards((2, 1), (2, 1));
        l.inventory = Inv(h: 2);
        Door(l, "A", 0, 0, 0, Dir.E); // between the left board's two squares
        Door(l, "A", 1, 0, 0, Dir.E); // between the right board's two squares
        Obj(l, "Red", Red, 0, -1, 0, Dir.E, 1, 2, 0);
        l.solution = Solutions.Level5();
        return l;
    }

    // Detour practice on two 2x2 boards: Blue's ring is on its own board and a direct road there looks shorter, but the level can only be finished by Blue going out through door A and back through door B. Then Red and Blue arrive together.
    // Solution:
    //   1. b1(1,1) I-v -> Red goes through door B
    //   2. b2(1,1) T W-N-E
    //   3. b2(1,0) L E-S -> Blue goes through door A, comes out facing E
    //   4. b1(1,0) L W-N -> Blue goes through door B
    //   5. b2(0,1) L N-E -> Red picks E at b2(1,1); Blue picks N at b2(1,1); Red and Blue arrive together, +1 + tile
    // Score 550 with 5 tiles (target).
    static LevelData Level6()
    {
        var l = new LevelData { title = "Level 6", targetScore = 550, bestTiles = 5,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((2, 2), (2, 2));
        l.inventory = Inv(ne: 2, es: 1, wn: 1, v: 1, tWNE: 1);
        Door(l, "A", 0, 0, 0, Dir.E);
        Door(l, "A", 1, 1, 0, Dir.S);
        Door(l, "B", 0, 1, 1, Dir.S);
        Door(l, "B", 1, 0, 1, Dir.N);
        Obj(l, "Red", Red, 0, 1, 2, Dir.S, 1, 2, 1);
        Obj(l, "Blue", Blue, 1, 2, 0, Dir.W, 1, 1, 2);
        l.solution = Solutions.Level6();
        return l;
    }

    // Routing teach on 3x3: two objects, just enough corners and straights and no T or + tiles, so each road has to leave the other the pieces it needs.
    // Solution:
    //   1. (2,0) L W-N
    //   2. (2,1) L S-W
    //   3. (1,1) L N-E
    //   4. (1,2) L E-S
    //   5. (2,2) L W-N
    //   6. (1,0) L E-S -> Red arrives
    //   7. (0,2) I-v
    //   8. (0,1) I-v
    //   9. (0,0) L W-N -> Blue arrives
    // Score 200 with 9 tiles (target).
    static LevelData Level7()
    {
        var l = new LevelData { title = "Level 7", targetScore = 200, bestTiles = 9,
            bestPlanHint = Tips.Routing };
        l.tutorial = "Routing"; // for the tutorial (Tutorials.Routing.cs): one step saying tiles can be limited
        l.teach = true;
        l.starsToPass = 3; // a teach level is passed when it's solved properly
        l.SetBoards((3, 3));
        l.inventory = Inv(ne: 1, es: 2, sw: 1, wn: 3, v: 2);
        Obj(l, "Red", Red, 0, 1, -1, Dir.N, 0, 2, 3);
        Obj(l, "Blue", Blue, 0, 0, 3, Dir.S, 0, -1, 0);
        l.solution = Solutions.Level7();
        return l;
    }

    // Routing with few tiles: mostly corners, roads around the edge, the middle unusable.
    // Score 200 with 8 tiles (target).
    static LevelData Level8()
    {
        var l = new LevelData { title = "Level 8", targetScore = 200, bestTiles = 8,
            finishHint = "Try the edges. The supply is mostly corners.", bestPlanHint = Tips.FewerTiles };
        l.SetBoards((3, 3));
        l.inventory = Inv(es: 3, wn: 3, h: 1, v: 1);
        Obj(l, "Red", Red, 0, -1, 0, Dir.E, 0, 2, 3);
        Obj(l, "Blue", Blue, 0, 3, 1, Dir.W, 0, 1, -1);
        l.solution = Solutions.Level8();
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
    static LevelData Level9()
    {
        var l = new LevelData { title = "Level 9", targetScore = 750, bestTiles = 7,
            finishHint = Tips.TogetherToFinish, bestPlanHint = Tips.Pairs };
        l.tutorial = "Pairs"; // for the tutorial (Tutorials.Pairs.cs): one step saying to deliver all four at one junction
        l.teach = true;
        l.starsToPass = 3; // a teach level is passed when it's solved properly
        l.SetBoards((3, 3));
        l.inventory = Inv(wn: 1, h: 1, tNES: 1, tESW: 2, tSWN: 2, tWNE: 1);
        Obj(l, "Red", Red, 0, 3, 1, Dir.W, 0, 2, -1);
        Obj(l, "Blue", Blue, 0, -1, 2, Dir.E, 0, 1, -1);
        Obj(l, "Green", Green, 0, 2, 3, Dir.S, 0, 0, -1);
        Obj(l, "Orange", Orange, 0, -1, 0, Dir.E, 0, 1, 3);
        l.solution = Solutions.Level9();
        return l;
    }

    // Pairs trap practice on 3x3: again each pair's delivery would break a junction someone still needs, so all four arrive together on the bottom-right T.
    // Solution:
    //   1. (1,2) T W-N-E -> Red picks E at (1,2)
    //   2. (2,2) + -> Red picks S at (2,2); Orange picks S at (2,2)
    //   3. (2,1) I-v
    //   4. (1,0) T E-S-W -> Green picks E at (1,0)
    //   5. (0,0) T E-S-W -> Blue picks E at (0,0); Blue picks E at (1,0)
    //   6. (0,2) I-h
    //   7. (2,0) T S-W-N -> Red picks W at (2,0); Red picks W at (1,0); Red picks W at (0,0); Blue picks N at (2,0); Blue picks N at (2,2); Green picks N at (2,0); Green picks W at (2,2); Green picks W at (1,2); Orange picks S at (2,0); Red and Blue and Green and Orange arrive together, +1 + tile
    // Score 750 with 7 tiles (target).
    static LevelData Level10()
    {
        var l = new LevelData { title = "Level 10", targetScore = 750, bestTiles = 7,
            finishHint = Tips.Pairs, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((3, 3));
        l.inventory = Inv(cross: 1, sw: 1, h: 1, v: 1, tESW: 2, tSWN: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 1, 3, Dir.S, 0, -1, 0);
        Obj(l, "Blue", Blue, 0, 0, -1, Dir.N, 0, 2, 3);
        Obj(l, "Green", Green, 0, 1, -1, Dir.N, 0, -1, 2);
        Obj(l, "Orange", Orange, 0, 3, 2, Dir.W, 0, 2, -1);
        l.solution = Solutions.Level10();
        return l;
    }

    // Build ahead teach: Red's road through the left column is started before Green's delivery breaks a tile there, then Red and Blue arrive together. Delivering everyone works without the build ahead; the target doesn't, so it takes 3 stars to pass.
    // Solution:
    //   1. (2,0) L S-W
    //   2. (1,0) T E-S-W -> Red picks W at (1,0); Green picks W at (1,0)
    //   3. (0,0) T W-N-E -> Red picks N at (0,0); Green picks W at (0,0); Green arrives
    //   4. (0,2) T S-W-N -> Blue picks S at (0,2)
    //   5. (0,1) T S-W-N -> Red picks N at (0,1); Red picks W at (0,2); Blue picks W at (0,1); Red and Blue arrive together, +1 + tile
    // Score 550 with 5 tiles (target).
    static LevelData Level11()
    {
        var l = new LevelData { title = "Level 11", targetScore = 550, bestTiles = 5,
            finishHint = Tips.TogetherToFinish, bestPlanHint = Tips.OneRoute };
        l.tutorial = "BuildAhead"; // for the tutorial (Tutorials.BuildAhead.cs): one step saying not to always deliver everyone at once
        l.teach = true;
        l.starsToPass = 3; // a teach level is passed when it's solved properly
        l.SetBoards((3, 3));
        l.inventory = Inv(sw: 2, tESW: 1, tSWN: 2, tWNE: 1);
        Obj(l, "Red", Red, 0, 2, -1, Dir.N, 0, -1, 2);
        Obj(l, "Blue", Blue, 0, 0, 3, Dir.S, 0, -1, 1);
        Obj(l, "Green", Green, 0, 1, -1, Dir.N, 0, -1, 0);
        l.solution = Solutions.Level11();
        return l;
    }

    // Build ahead practice: Blue's road is built ahead through the middle before Green arrives, then Red and Blue arrive together.
    // Solution:
    //   1. (2,0) I-v
    //   2. (1,1) T N-E-S
    //   3. (2,1) T E-S-W -> Blue picks W at (2,1); Blue picks N at (1,1)
    //   4. (1,0) I-v -> Green picks E at (1,1); Green picks E at (2,1); Green arrives
    //   5. (1,2) T S-W-N -> Red picks W at (1,2); Blue picks W at (1,2)
    //   6. (0,2) T W-N-E -> Red picks W at (0,2); Blue picks N at (0,2); Red and Blue arrive together, +1 + tile
    // Score 550 with 6 tiles (target).
    static LevelData Level12()
    {
        var l = new LevelData { title = "Level 12", targetScore = 550, bestTiles = 6,
            finishHint = Tips.OneRoute, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((3, 3));
        l.inventory = Inv(sw: 1, v: 2, tNES: 1, tESW: 1, tSWN: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 1, 3, Dir.S, 0, -1, 2);
        Obj(l, "Blue", Blue, 0, 2, -1, Dir.N, 0, 0, 3);
        Obj(l, "Green", Green, 0, 1, -1, Dir.N, 0, 3, 1);
        l.solution = Solutions.Level12();
        return l;
    }

    // The bridge: Red and Blue cross over each other on it and arrive together.
    // Score 450 with 6 tiles (target).
    static LevelData Level13()
    {
        var l = new LevelData { title = "Level 13", targetScore = 450, bestTiles = 6,
            finishHint = Tips.Bridge, bestPlanHint = Tips.TogetherForScore };
        l.section = "Challenge"; // the Levels page starts a part here
        l.SetBoards((3, 3));
        l.inventory = Inv(bridge: 1, ne: 2, es: 1, sw: 1, wn: 1, v: 1);
        Obj(l, "Red", Red, 0, 3, 1, Dir.W, 0, 2, 3);
        Obj(l, "Blue", Blue, 0, 3, 2, Dir.W, 0, 2, -1);
        l.solution = Solutions.Level13();
        return l;
    }

    // Together + earned + (found with the Learn levels): Red and Blue arrive together on the right, and the + they earn is the junction Green needs.
    // Solution:
    //   1. (2,1) T N-E-S -> Red picks S at (2,1)
    //   2. (2,2) T S-W-N -> Green picks W at (2,2)
    //   3. (1,2) T N-E-S -> Green picks S at (1,2)
    //   4. (2,0) T N-E-S -> Red picks S at (2,0); Blue picks N at (2,0); Blue picks N at (2,1); Blue picks W at (2,2); Blue picks N at (1,2); Red and Blue arrive together, +1 + tile
    //   5. (1,1) + -> Green picks W at (1,1)
    //   6. (0,1) I-h -> Green arrives
    // Score 475 with 6 tiles (target).
    static LevelData Level14()
    {
        var l = new LevelData { title = "Level 14", targetScore = 475, bestTiles = 6,
            finishHint = Tips.EarnedPlus, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((3, 3));
        l.inventory = Inv(h: 1, v: 1, tNES: 3, tSWN: 1);
        Obj(l, "Red", Red, 0, 3, 1, Dir.W, 0, 2, -1);
        Obj(l, "Blue", Blue, 0, 3, 0, Dir.W, 0, 1, 3);
        Obj(l, "Green", Green, 0, 2, 3, Dir.S, 0, -1, 1);
        l.solution = Solutions.Level14();
        return l;
    }

    // Detour on two 2x2 boards: Red's ring is on its own board and a direct road there looks shorter (3 squares against the detour's 4), but Red has to go out through a door and back. Then Red and Blue arrive together.
    // Solution:
    //   1. b1(0,0) L E-S
    //   2. b1(1,0) I-h -> Red goes through door B, comes out facing W
    //   3. b2(0,0) T N-E-S -> Red picks N at b2(0,0); Red goes through door A, comes out facing N; Blue picks N at b2(0,0); Blue goes through door A, comes out facing N
    //   4. b1(1,1) T N-E-S -> Red picks E at b1(1,1); Blue picks N at b1(1,1); Red and Blue arrive together, +1 + tile
    // Score 550 with 4 tiles (target).
    static LevelData Level15()
    {
        var l = new LevelData { title = "Level 15", targetScore = 550, bestTiles = 4,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((2, 2), (2, 2));
        l.inventory = Inv(es: 1, sw: 1, h: 1, tNES: 2);
        Door(l, "A", 1, 0, 0, Dir.N);
        Door(l, "A", 0, 1, 1, Dir.S);
        Door(l, "B", 1, 1, 0, Dir.W);
        Door(l, "B", 0, 1, 0, Dir.E);
        Obj(l, "Red", Red, 0, 0, -1, Dir.N, 0, 2, 1);
        Obj(l, "Blue", Blue, 1, 0, -1, Dir.N, 0, 1, 2);
        l.solution = Solutions.Level15();
        return l;
    }

    // Red and Blue meet on a T and arrive together, and the + they earn is the straight Green's road is missing.
    // Score 475 with 7 tiles (target).
    static LevelData Level16()
    {
        var l = new LevelData { title = "Level 16", targetScore = 475, bestTiles = 7,
            finishHint = Tips.EarnedPlus, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((3, 3));
        l.inventory = Inv(ne: 2, es: 1, h: 1, v: 1, tNES: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 2, 3, Dir.S, 0, 3, 1);
        Obj(l, "Blue", Blue, 0, 1, 3, Dir.S, 0, 3, 2);
        Obj(l, "Green", Green, 0, 0, -1, Dir.N, 0, 3, 0);
        l.solution = Solutions.Level16();
        return l;
    }

    // The pairs trap, small: each pair's delivery breaks a junction someone still needs, so all four arrive together on the last T. Pairs can't deliver everyone.
    // Score 825 with 7 tiles (target).
    static LevelData Level17()
    {
        var l = new LevelData { title = "Level 17", targetScore = 825, bestTiles = 7,
            finishHint = Tips.Pairs, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((4, 3));
        l.inventory = Inv(cross: 1, es: 1, wn: 1, v: 1, tNES: 1, tESW: 2, tSWN: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 4, 0, Dir.W, 0, -1, 1);
        Obj(l, "Blue", Blue, 0, -1, 0, Dir.E, 0, 2, -1);
        Obj(l, "Green", Green, 0, 0, 3, Dir.S, 0, 3, -1);
        Obj(l, "Orange", Orange, 0, 1, -1, Dir.N, 0, 4, 1);
        l.solution = Solutions.Level17();
        return l;
    }

    // Build ahead: Red's road through the shared column has to be built before Green's delivery breaks it. Without it, not everyone gets delivered. The + in the supply is a wildcard.
    // Score 550 with 7 tiles (target).
    static LevelData Level18()
    {
        var l = new LevelData { title = "Level 18", targetScore = 550, bestTiles = 7,
            finishHint = Tips.OneRoute, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((4, 3));
        l.inventory = Inv(cross: 1, es: 1, sw: 1, v: 1, tNES: 1, tESW: 1, tSWN: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 0, 3, Dir.S, 0, 4, 0);
        Obj(l, "Blue", Blue, 0, 4, 1, Dir.W, 0, 2, -1);
        Obj(l, "Green", Green, 0, 1, 3, Dir.S, 0, 1, -1);
        l.solution = Solutions.Level18();
        return l;
    }

    // Detour on two 2x2 boards: a direct road to Red's ring looks much shorter (2 squares against 4), but only the trip out through a door and back works.
    // Solution:
    //   1. b2(1,1) T S-W-N -> Blue picks S at b2(1,1); Blue goes through door B
    //   2. b2(0,1) L E-S
    //   3. b1(1,1) L N-E
    //   4. b1(0,0) I-v
    //   5. b1(0,1) T S-W-N -> Red picks N at b1(0,1); Red goes through door B, comes out facing N; Red picks W at b2(1,1); Red goes through door A; Blue picks S at b1(0,1); Red and Blue arrive together, +1 + tile
    // Score 550 with 5 tiles (target).
    static LevelData Level19()
    {
        var l = new LevelData { title = "Level 19", targetScore = 550, bestTiles = 5,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((2, 2), (2, 2));
        l.inventory = Inv(ne: 1, es: 1, wn: 1, v: 1, tSWN: 2);
        Door(l, "A", 1, 0, 0, Dir.N);
        Door(l, "A", 0, 1, 1, Dir.E);
        Door(l, "B", 1, 1, 0, Dir.N);
        Door(l, "B", 0, 0, 1, Dir.N);
        Obj(l, "Red", Red, 0, -1, 1, Dir.E, 0, 1, 2);
        Obj(l, "Blue", Blue, 1, 1, 2, Dir.S, 0, 0, -1);
        l.solution = Solutions.Level19();
        return l;
    }

    // Blue and Green arrive together on the left, and the + they earn is Red's last piece at the top.
    // Score 550 with 8 tiles (target).
    static LevelData Level20()
    {
        var l = new LevelData { title = "Level 20", targetScore = 550, bestTiles = 8,
            finishHint = Tips.EarnedPlus, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((4, 3));
        l.inventory = Inv(sw: 1, h: 2, v: 1, tNES: 1, tESW: 1, tSWN: 2, tWNE: 1);
        Obj(l, "Red", Red, 0, -1, 2, Dir.E, 0, 2, 3);
        Obj(l, "Blue", Blue, 0, 4, 1, Dir.W, 0, -1, 0);
        Obj(l, "Green", Green, 0, 2, -1, Dir.N, 0, 0, 3);
        l.solution = Solutions.Level20();
        return l;
    }

    // Four together on one 4x3 board: the target only comes with all four arriving on one delivery; any other way scores 625 at best.
    // Solution:
    //   1. (3,0) + -> Red picks W at (3,0)
    //   2. (2,0) T E-S-W -> Red picks W at (2,0)
    //   3. (1,1) I-v
    //   4. (1,2) T S-W-N
    //   5. (3,1) L E-S -> Blue picks W at (3,0); Blue picks W at (2,0)
    //   6. (0,2) T W-N-E -> Orange picks E at (0,2); Orange picks S at (1,2)
    //   7. (0,0) I-h
    //   8. (1,0) T W-N-E -> Red picks N at (1,0); Red picks N at (1,2); Blue picks N at (1,0); Blue picks W at (1,2); Blue picks W at (0,2); Green picks E at (1,0); Green picks S at (2,0); Orange picks E at (1,0); Orange picks E at (2,0); Orange picks E at (3,0); Red and Blue and Green and Orange arrive together, +1 + tile
    // Score 750 with 8 tiles (target).
    static LevelData Level21()
    {
        var l = new LevelData { title = "Level 21", targetScore = 750, bestTiles = 8,
            finishHint = Tips.Pairs, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((4, 3));
        l.inventory = Inv(cross: 1, es: 1, h: 1, v: 2, tESW: 1, tSWN: 1, tWNE: 2);
        Obj(l, "Red", Red, 0, 3, -1, Dir.N, 0, 1, 3);
        Obj(l, "Blue", Blue, 0, 4, 1, Dir.W, 0, -1, 2);
        Obj(l, "Green", Green, 0, -1, 0, Dir.E, 0, 2, -1);
        Obj(l, "Orange", Orange, 0, 0, 3, Dir.S, 0, 4, 0);
        l.solution = Solutions.Level21();
        return l;
    }

    // Blue's ring is on its own board, but its road goes out through door B and back. Door A is a decoy.
    // Score 500 with 6 tiles (target).
    static LevelData Level22()
    {
        var l = new LevelData { title = "Level 22", targetScore = 500, bestTiles = 6,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((3, 3), (3, 3));
        l.inventory = Inv(ne: 1, es: 1, sw: 1, h: 2, tESW: 1, tSWN: 1);
        Door(l, "A", 1, 2, 2, Dir.N);
        Door(l, "A", 0, 0, 0, Dir.S);
        Door(l, "B", 0, 2, 0, Dir.W);
        Door(l, "B", 1, 0, 1, Dir.S);
        Obj(l, "Red", Red, 1, 1, -1, Dir.N, 1, -1, 1);
        Obj(l, "Blue", Blue, 0, 3, 0, Dir.W, 0, 1, -1);
        l.solution = Solutions.Level22();
        return l;
    }

    // The bridge: Red and Green cross over each other on it and arrive together, and the + you earn is the straight Blue is missing.
    // Score 550 with 8 tiles (target).
    static LevelData Level23()
    {
        var l = new LevelData { title = "Level 23", targetScore = 550, bestTiles = 8,
            finishHint = Tips.Bridge, bestPlanHint = Tips.FewerTiles };
        // delivering everyone needs the + that delivering together earns, so the hint shows at 0 or 1 star instead
        l.SetBoards((3, 3));
        l.inventory = Inv(bridge: 1, es: 2, sw: 1, wn: 2, v: 3);
        Obj(l, "Red", Red, 0, 2, 3, Dir.S, 0, 2, -1);
        Obj(l, "Blue", Blue, 0, 1, 3, Dir.S, 0, 0, -1);
        Obj(l, "Green", Green, 0, 3, 0, Dir.W, 0, 1, -1);
        l.solution = Solutions.Level23();
        return l;
    }

    // Detour on two 2x2 boards, one more tile: again Red's direct road looks shorter (2 squares against 4), and the detour is the only way.
    // Solution:
    //   1. b1(0,1) L N-E
    //   2. b1(1,1) L S-W -> Red goes through door B
    //   3. b1(0,0) I-h
    //   4. b1(1,0) I-v -> Blue goes through door B
    //   5. b2(1,1) L E-S
    //   6. b2(1,0) T N-E-S -> Red picks S at b2(1,0); Red goes through door A, comes out facing W; Blue picks N at b2(1,0); Red and Blue arrive together, +1 + tile
    // Score 550 with 6 tiles (target).
    static LevelData Level24()
    {
        var l = new LevelData { title = "Level 24", targetScore = 550, bestTiles = 6,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((2, 2), (2, 2));
        l.inventory = Inv(ne: 1, es: 2, sw: 1, h: 1, v: 1, tNES: 1);
        Door(l, "A", 1, 1, 0, Dir.S);
        Door(l, "A", 0, 0, 0, Dir.E);
        Door(l, "B", 1, 1, 0, Dir.E);
        Door(l, "B", 0, 1, 1, Dir.S);
        Obj(l, "Red", Red, 0, 0, 2, Dir.S, 0, -1, 0);
        Obj(l, "Blue", Blue, 0, 1, -1, Dir.N, 1, 2, 1);
        l.solution = Solutions.Level24();
        return l;
    }

    // Detour on two 3x3 boards: Red's direct road looks shorter (4 squares against 5), but Red has to go out to the other board and come back.
    // Solution:
    //   1. b1(2,2) L S-W
    //   2. b2(1,2) L E-S
    //   3. b2(2,2) T E-S-W
    //   4. b1(2,0) I-v
    //   5. b2(2,1) L N-E -> Blue picks W at b2(2,2); Blue goes through door A, comes out facing N
    //   6. b1(0,2) I-h
    //   7. b1(1,2) T W-N-E -> Red picks E at b1(1,2); Red goes through door A, comes out facing N; Red picks E at b2(2,2); Red goes through door B, comes out facing S; Blue picks W at b1(1,2); Red and Blue arrive together, +1 + tile
    // Score 550 with 7 tiles (target).
    static LevelData Level25()
    {
        var l = new LevelData { title = "Level 25", targetScore = 550, bestTiles = 7,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((3, 3), (3, 3));
        l.inventory = Inv(ne: 2, es: 1, sw: 1, h: 1, v: 1, tESW: 1, tWNE: 1);
        Door(l, "A", 0, 2, 1, Dir.N);
        Door(l, "A", 1, 1, 1, Dir.N);
        Door(l, "B", 0, 2, 0, Dir.N);
        Door(l, "B", 1, 2, 2, Dir.E);
        Obj(l, "Red", Red, 0, 1, 3, Dir.S, 0, 2, -1);
        Obj(l, "Blue", Blue, 1, 3, 1, Dir.W, 0, -1, 2);
        l.solution = Solutions.Level25();
        return l;
    }

    // The starts are spread out and two pairs look best on paper (900), but each pair's delivery breaks a junction someone still needs. All four have to arrive together on the last T.
    // Score 825 with 10 tiles (target).
    static LevelData Level26()
    {
        var l = new LevelData { title = "Level 26", targetScore = 825, bestTiles = 10,
            finishHint = Tips.TogetherToFinish, bestPlanHint = Tips.FewerTiles };
        l.SetBoards((4, 4));
        l.inventory = Inv(cross: 1, ne: 1, sw: 1, wn: 1, h: 1, v: 3, tNES: 1, tESW: 1, tSWN: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 2, 4, Dir.S, 0, 3, -1);
        Obj(l, "Blue", Blue, 0, -1, 0, Dir.E, 0, 3, 4);
        Obj(l, "Green", Green, 0, 4, 0, Dir.W, 0, -1, 2);
        Obj(l, "Orange", Orange, 0, 4, 3, Dir.W, 0, -1, 3);
        l.solution = Solutions.Level26();
        return l;
    }

    // Two pairs: the first pair's + becomes the corner Green is missing. No single extra tile would let all four arrive together, so the pairs plan is the way. The + in the supply is a wildcard.
    // Score 825 with 10 tiles (target).
    static LevelData Level27()
    {
        var l = new LevelData { title = "Level 27", targetScore = 825, bestTiles = 10,
            finishHint = Tips.EarnedPlus, bestPlanHint = Tips.FewerTiles };
        // delivering everyone needs the + that delivering together earns, so the hint shows at 0 or 1 star instead
        l.SetBoards((4, 4));
        l.inventory = Inv(cross: 1, ne: 3, wn: 1, tNES: 1, tESW: 2, tSWN: 1, tWNE: 2);
        l.obstacles.Add(l.At(0, 1, 2));
        Obj(l, "Red", Red, 0, 3, 4, Dir.S, 0, -1, 2);
        Obj(l, "Blue", Blue, 0, 1, 4, Dir.S, 0, 4, 1);
        Obj(l, "Green", Green, 0, 4, 2, Dir.W, 0, 2, -1);
        Obj(l, "Orange", Orange, 0, 4, 3, Dir.W, 0, 3, -1);
        l.solution = Solutions.Level27();
        return l;
    }

    // Detour on two 3x3 boards, a longer trip: Red's direct road is 4 squares, the detour 6, and only the detour finishes the level.
    // Solution:
    //   1. b1(1,0) I-v -> Red goes through door A, comes out facing S
    //   2. b2(1,0) T S-W-N -> Red picks W at b2(1,0)
    //   3. b2(0,1) I-v
    //   4. b2(0,2) T E-S-W
    //   5. b1(2,2) L N-E
    //   6. b2(2,2) L W-N
    //   7. b2(1,2) I-h -> Blue picks S at b2(0,2)
    //   8. b2(0,0) L N-E -> Red picks W at b2(0,2); Red goes through door B; Blue picks S at b2(1,0); Red and Blue arrive together, +1 + tile
    // Score 500 with 8 tiles (target).
    static LevelData Level28()
    {
        var l = new LevelData { title = "Level 28", targetScore = 500, bestTiles = 8,
            finishHint = Tips.DetourToFinish, bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((3, 3), (3, 3));
        l.inventory = Inv(ne: 2, sw: 1, wn: 1, h: 1, v: 2, tESW: 1, tSWN: 1);
        Door(l, "A", 1, 1, 1, Dir.S);
        Door(l, "A", 0, 1, 1, Dir.S);
        Door(l, "B", 0, 2, 2, Dir.N);
        Door(l, "B", 1, 0, 2, Dir.W);
        Obj(l, "Red", Red, 0, 1, -1, Dir.N, 0, 3, 2);
        Obj(l, "Blue", Blue, 1, 2, 3, Dir.S, 1, 1, -1);
        l.solution = Solutions.Level28();
        return l;
    }

    // A 4x3 board: Blue and Orange arrive together first, but Red and Green both still have to cross their tiles. Build Red's road and Green's road ahead so both pass first. Fixing only one isn't enough.
    // Score 900 with 9 tiles (target).
    static LevelData Level29()
    {
        var l = new LevelData { title = "Level 29", targetScore = 900, bestTiles = 9,
            finishHint = Tips.TogetherToFinish, bestPlanHint = Tips.TwoRoutes };
        l.SetBoards((4, 3));
        l.inventory = Inv(cross: 1, sw: 1, wn: 1, h: 4, tNES: 1, tESW: 1, tSWN: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, -1, 0, Dir.E, 0, 4, 0);
        Obj(l, "Blue", Blue, 0, -1, 1, Dir.E, 0, 1, 3);
        Obj(l, "Green", Green, 0, 3, 3, Dir.S, 0, 2, -1);
        Obj(l, "Orange", Orange, 0, 1, -1, Dir.N, 0, 2, 3);
        l.solution = Solutions.Level29();
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
    static LevelData Level30()
    {
        var l = new LevelData { title = "Level 30", targetScore = 700, bestTiles = 7,
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
        l.solution = Solutions.Level30();
        return l;
    }

    // A bigger pairs trap: all four arrive together on the top-right T. The + in the supply is a lure; the best plan doesn't place it.
    // Score 825 with 12 tiles (target).
    static LevelData Level31()
    {
        var l = new LevelData { title = "Level 31", targetScore = 825, bestTiles = 12,
            bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((5, 4));
        l.inventory = Inv(cross: 1, ne: 1, wn: 1, h: 3, v: 1, tNES: 1, tESW: 3, tSWN: 2, tWNE: 1);
        Obj(l, "Red", Red, 0, -1, 0, Dir.E, 0, 4, 4);
        Obj(l, "Blue", Blue, 0, 5, 2, Dir.W, 0, 4, -1);
        Obj(l, "Green", Green, 0, 1, -1, Dir.N, 0, 3, 4);
        Obj(l, "Orange", Orange, 0, 0, 4, Dir.S, 0, 3, -1);
        l.solution = Solutions.Level31();
        return l;
    }

    // Blue's and Green's roads are built ahead before Red and Orange arrive on the given +, and the + they earn is the piece Blue needs.
    // Score 750 with 10 tiles (target).
    static LevelData Level32()
    {
        var l = new LevelData { title = "Level 32", targetScore = 750, bestTiles = 10,
            finishHint = Tips.EarnedPlus, bestPlanHint = Tips.TwoRoutes };
        l.SetBoards((4, 4));
        l.inventory = Inv(cross: 1, es: 1, sw: 1, h: 1, v: 2, tNES: 1, tESW: 1, tSWN: 1, tWNE: 1);
        Obj(l, "Red", Red, 0, 1, -1, Dir.N, 0, -1, 0);
        Obj(l, "Blue", Blue, 0, 4, 1, Dir.W, 0, -1, 3);
        Obj(l, "Green", Green, 0, 3, 4, Dir.S, 0, -1, 1);
        Obj(l, "Orange", Orange, 0, 0, -1, Dir.N, 0, 4, 0);
        l.solution = Solutions.Level32();
        return l;
    }

    // Doors and the bridge on two 3x3 boards: two roads cross on the bridge, and the objects reach the other board through the doors.
    // Solution:
    //   1. b2(0,0) L W-N
    //   2. b2(0,1) I-v
    //   3. b2(0,2) L S-W -> Red goes through door A, comes out facing S
    //   4. b1(0,1) bridge
    //   5. b1(1,0) T E-S-W
    //   6. b1(2,0) I-h
    //   7. b1(0,0) T W-N-E -> Red picks E at b1(0,0); Red picks E at b1(1,0); Blue picks E at b1(0,0); Blue picks S at b1(1,0); Red and Blue arrive together, +1 + tile
    //   8. b2(2,2) I-v
    //   9. b1(1,1) L S-W -> Green goes through door B, comes out facing N; Green arrives
    // Score 650 with 9 tiles (target).
    static LevelData Level33()
    {
        var l = new LevelData { title = "Level 33", targetScore = 650, bestTiles = 9,
            bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((3, 3), (3, 3));
        l.inventory = Inv(bridge: 1, ne: 1, sw: 2, wn: 1, h: 1, v: 2, tESW: 1, tWNE: 1);
        Door(l, "A", 1, 0, 2, Dir.W);
        Door(l, "A", 0, 0, 2, Dir.S);
        Door(l, "B", 1, 2, 1, Dir.N);
        Door(l, "B", 0, 1, 0, Dir.N);
        Obj(l, "Red", Red, 1, -1, 0, Dir.E, 0, 3, 0);
        Obj(l, "Blue", Blue, 0, -1, 0, Dir.E, 0, 1, -1);
        Obj(l, "Green", Green, 0, -1, 1, Dir.E, 1, 2, 3);
        l.solution = Solutions.Level33();
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
    static LevelData Level34()
    {
        var l = new LevelData { title = "Level 34", targetScore = 950, bestTiles = 10,
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
        l.solution = Solutions.Level34();
        return l;
    }

    // Pairs trap and build ahead: Red goes first, Blue's road is built ahead, then Blue, Green and Orange arrive together.
    // Score 850 with 12 tiles (target).
    static LevelData Level35()
    {
        var l = new LevelData { title = "Level 35", targetScore = 850, bestTiles = 12,
            bestPlanHint = Tips.Pairs };
        l.SetBoards((5, 4));
        l.inventory = Inv(cross: 1, ne: 1, wn: 2, h: 3, v: 1, tNES: 1, tESW: 3, tSWN: 1, tWNE: 2);
        Obj(l, "Red", Red, 0, 1, 4, Dir.S, 0, -1, 3);
        Obj(l, "Blue", Blue, 0, 2, -1, Dir.N, 0, 3, 4);
        Obj(l, "Green", Green, 0, 5, 2, Dir.W, 0, 2, 4);
        Obj(l, "Orange", Orange, 0, -1, 1, Dir.E, 0, 5, 0);
        l.solution = Solutions.Level35();
        return l;
    }

    // Four together on two 3x3 boards, a longer meeting: again one delivery for all four, 900 otherwise.
    // Solution:
    //   1. b1(1,2) T N-E-S -> Red picks S at b1(1,2)
    //   2. b1(1,1) T S-W-N -> Red picks S at b1(1,1)
    //   3. b2(1,1) T N-E-S
    //   4. b2(1,2) + -> Orange picks S at b2(1,2); Orange picks E at b2(1,1); Orange goes through door B
    //   5. b2(2,2) L W-N
    //   6. b2(0,2) L N-E -> Blue picks S at b2(1,2); Blue picks E at b2(1,1); Blue goes through door B
    //   7. b1(0,0) L E-S
    //   8. b1(0,1) I-h -> Green picks S at b1(1,1)
    //   9. b2(1,0) I-v
    //   10. b1(2,2) I-h
    //   11. b1(1,0) T S-W-N -> Red picks S at b1(1,0); Red goes through door B, comes out facing W; Red picks N at b2(1,1); Red picks E at b2(1,2); Blue picks W at b1(1,0); Green picks S at b1(1,0); Green goes through door B, comes out facing W; Green picks S at b2(1,1); Orange picks N at b1(1,0); Orange picks N at b1(1,1); Orange picks E at b1(1,2); Red and Blue and Green and Orange arrive together, +1 + tile
    // Score 950 with 11 tiles (target).
    static LevelData Level36()
    {
        var l = new LevelData { title = "Level 36", targetScore = 950, bestTiles = 11,
            bestPlanHint = Tips.TogetherForScore };
        l.SetBoards((3, 3), (3, 3));
        l.inventory = Inv(cross: 1, ne: 1, es: 2, wn: 1, h: 2, v: 1, tNES: 2, tSWN: 2);
        Door(l, "A", 1, 0, 1, Dir.W);
        Door(l, "A", 0, 2, 1, Dir.E);
        Door(l, "B", 0, 1, 0, Dir.S);
        Door(l, "B", 1, 2, 1, Dir.W);
        Obj(l, "Red", Red, 0, 1, 3, Dir.S, 1, 2, 3);
        Obj(l, "Blue", Blue, 1, 0, 3, Dir.S, 0, 0, -1);
        Obj(l, "Green", Green, 0, -1, 1, Dir.E, 1, 1, -1);
        Obj(l, "Orange", Orange, 1, 1, 3, Dir.S, 0, 3, 2);
        l.solution = Solutions.Level36();
        return l;
    }

    // The bridge again, on two boards: Blue goes north and Green goes west through the same square without meeting. Red's ring is on its own board, but its road goes out through a door and back.
    // Score 675 with 9 tiles (target).
    static LevelData Level37()
    {
        var l = new LevelData { title = "Level 37", targetScore = 675, bestTiles = 9,
            finishHint = Tips.BridgeAndDetour, bestPlanHint = Tips.TogetherForScore };
        l.section = "Finale"; // the Levels page starts a part here
        l.SetBoards((3, 3), (3, 3));
        l.inventory = Inv(bridge: 1, ne: 2, es: 2, wn: 2, h: 1, v: 1, tNES: 1, tESW: 1);
        Door(l, "A", 0, 2, 0, Dir.S);
        Door(l, "A", 1, 2, 2, Dir.W);
        Door(l, "B", 0, 0, 2, Dir.N);
        Door(l, "B", 1, 0, 2, Dir.S);
        Obj(l, "Red", Red, 0, 1, 3, Dir.S, 0, 3, 0);
        Obj(l, "Blue", Blue, 1, 1, -1, Dir.N, 1, 0, 3);
        Obj(l, "Green", Green, 1, 3, 0, Dir.W, 1, 0, -1);
        l.solution = Solutions.Level37();
        return l;
    }

    // Four small boards, a pairs trap: pairs look best on paper but break junctions someone still needs. Build Blue's road first (through doors A and C), then Red, Green and Orange arrive together, and Blue finishes.
    // Score 975 with 9 tiles (target).
    static LevelData Level38()
    {
        var l = new LevelData { title = "Level 38", targetScore = 975, bestTiles = 9,
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
        l.solution = Solutions.Level38();
        return l;
    }

    // Opens the finale on two 5x4 boards, a pairs trap: all four arrive together on the given +, and a detour reaches the target (775 without). Door B is a decoy.
    // Score 1025 with 13 tiles (target).
    static LevelData Level39()
    {
        var l = new LevelData { title = "Level 39", targetScore = 1025, bestTiles = 13,
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
        l.solution = Solutions.Level39();
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
    static LevelData Level40()
    {
        var l = new LevelData { title = "Level 40", targetScore = 950, bestTiles = 11,
            finishHint = Tips.TogetherToFinish, bestPlanHint = Tips.TogetherForScore };
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
        l.solution = Solutions.Level40();
        return l;
    }

    // An advanced detour on two 4x3 boards: everyone crosses boards, and Blue and Green share one long road out through the doors and back, which saves a tile. Without the detour the best is 1075, short of the 3rd star. Door D is a decoy.
    // Score 1175 with 12 tiles (target).
    static LevelData Level41()
    {
        var l = new LevelData { title = "Level 41", targetScore = 1175, bestTiles = 12,
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
        l.solution = Solutions.Level41();
        return l;
    }

    // Two 4x4 boards: Blue and Orange arrive together first, but Red and Green both still have to cross their tiles. Build both roads ahead (Red's goes through three doors), and the + the pair earns is the corner Red needs at the end. Green's ring is on its own board, so its road goes out through a door and back.
    // Score 1025 with 11 tiles (target).
    static LevelData Level42()
    {
        var l = new LevelData { title = "Level 42", targetScore = 1025, bestTiles = 11,
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
        l.solution = Solutions.Level42();
        return l;
    }

    // The finale on two 5x5 boards: the bridge, a detour Red and Orange can't do without, a road to build ahead, and an earned + at the junction where Red and Orange meet. Door B is a decoy.
    // Score 975 with 14 tiles (target).
    static LevelData Level43()
    {
        var l = new LevelData { title = "Level 43", targetScore = 975, bestTiles = 14,
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
        l.solution = Solutions.Level43();
        return l;
    }

    // Four 3x3 boards, every door inside a board: two pairs, a detour, a route to build ahead, and the + the first pair earns is the corner Red needs. Door E is a decoy.
    // Score 1025 with 15 tiles (target).
    static LevelData Level44()
    {
        var l = new LevelData { title = "Level 44", targetScore = 1025, bestTiles = 15,
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
        l.solution = Solutions.Level44();
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
