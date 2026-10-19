using System; using System.Collections.Generic; using System.IO; using System.Linq; using UnityEngine;

// writes each level's verified solution, target, tile hint and end-window nudge into Levels.cs
static class Export
{
    public static readonly string[] Ideas =
    {
        "Two separate routes: pick a tile, click a square.",
        "The bridge: Red and Green cross over each other on it and arrive together, and the + you earn is the straight Blue is missing.",
        "A 4x3 board: Red and Orange arrive together first, but Blue and Green both still have to cross their tiles. Build Blue's road and Green's road ahead so both pass first. Fixing only one isn't enough.",
        "The first pairs trap: the starts are spread out and two pairs look best on paper (900), but each pair's delivery breaks a junction someone still needs. All four have to arrive together on the last T.",
        "Two pairs: the first pair's + becomes the corner Green is missing. No single extra tile would let all four arrive together, so the pairs plan is the way.",
        "Doors: every door sits between two squares, so each time an object comes out it picks which way to face. All three meet on one T and arrive together.",
        "The bridge again, on two boards: Blue goes north and Green goes west through the same square without meeting. Red's ring is on its own board, but its road goes out through a door and back.",
        "Four small boards, a pairs trap: pairs look best on paper but break junctions someone still needs. Build Blue's road first (through doors A and C), then Red, Green and Orange arrive together, and Blue finishes.",
        "An advanced detour on two 4x3 boards: everyone crosses boards, and Blue and Green share one long road out through the doors and back, which saves a tile. Without the detour the best is 1075, short of the 3rd star.",
        "Two 4x4 boards: Blue and Orange arrive together first, but Red and Green both still have to cross their T tiles on board 1. Build both roads ahead, and the + the pair earns is the straight Red needs at the end.",
        "Four 3x3 boards, every door inside a board: two pairs, a detour, a route to build ahead, and the + the first pair earns is the corner Red needs.",
        "The finale on two 5x5 boards: the bridge, a detour Red and Orange can't do without, a road to build ahead, and an earned + at the junction where Red and Orange meet. Door B is a decoy.",
    };

    public static string TileText(TileType t) => t switch
    {
        TileType.Cross => "+", TileType.Bridge => "bridge", TileType.StraightH => "I-h", TileType.StraightV => "I-v",
        TileType.CornerNE => "L N-E", TileType.CornerES => "L E-S", TileType.CornerSW => "L S-W", TileType.CornerWN => "L W-N",
        TileType.TeeNES => "T N-E-S", TileType.TeeESW => "T E-S-W", TileType.TeeSWN => "T S-W-N", _ => "T W-N-E",
    };

    public static string Hint(Props p) =>
        p.pairTrap ? "Splitting into pairs breaks a junction someone still needs. The best plan brings more of them in together."
        : p.detour ? "The best plan uses a door as a shortcut back to an object's own board."
        : p.buildsAhead ? "The best plan builds part of one route early, so it passes a shared tile before anyone is delivered."
        : p.earnedCrossUsed ? "The best plan delivers two objects together early to earn a + tile."
        : p.groups.Any(g => g > 1) ? "The best plan delivers some objects together."
        : "The best plan uses fewer tiles.";

    public static void Run(string levelsPath)
    {
        var text = File.ReadAllText(levelsPath).Replace("\r\n", "\n");
        for (int i = 0; i < Levels.All.Count; i++)
        {
            var l = Levels.All[i];
            var p = Gen.Analyze(l);
            var rp = new Replay2(l).Run(p.best);
            if (!rp.ok) throw new Exception($"{l.title}: {rp.problem}");
            var steps = new List<string>();
            for (int k = 0; k < rp.placements.Count; k++)
            {
                var (c, t) = rp.placements[k];
                var ev = rp.stepEvents[k];
                steps.Add($"{LB.Local(l, c)} {TileText(t)}" + (ev.Count > 0 ? " -> " + string.Join("; ", ev) : ""));
            }
            var comment = new List<string> { Ideas[i], "Solution:" };
            comment.AddRange(steps.Select((s, k) => $"  {k + 1}. {s}"));
            comment.Add($"Score {rp.score} with {p.tiles} tiles (target).");
            string block = string.Join("\n", comment.Select(c => "    // " + c)) + "\n";

            // replace the comment above LevelN(), and set the target, hint and nudge
            int n = i + 1;
            var sig = $"    static LevelData Level{n}()\n";
            int at = text.IndexOf(sig);
            int lineStart = at;
            while (lineStart > 0 && text.Substring(text.LastIndexOf('\n', lineStart - 2) + 1, lineStart - text.LastIndexOf('\n', lineStart - 2) - 1).TrimStart().StartsWith("//"))
                lineStart = text.LastIndexOf('\n', lineStart - 2) + 1;
            text = text.Substring(0, lineStart) + block + text.Substring(at);
            at = text.IndexOf(sig);
            int newLevel = text.IndexOf("        var l = new LevelData {", at);
            // the header runs to the "};" that closes it (one line, or two once it has a bestPlanHint line)
            int lineEnd = text.IndexOf('\n', text.IndexOf("};", newLevel));
            text = text.Substring(0, newLevel) +
                $"        var l = new LevelData {{ title = \"Level {n}\", targetScore = {p.score}, bestTiles = {p.tiles},\n" +
                $"            bestPlanHint = \"{Hint(p)}\" }};" + text.Substring(lineEnd);
            Console.WriteLine($"Level {n}: {p.score}, {p.tiles} tiles, {Hint(p)}");
        }
        File.WriteAllText(levelsPath, text.Replace("\n", "\r\n"));
    }
}
