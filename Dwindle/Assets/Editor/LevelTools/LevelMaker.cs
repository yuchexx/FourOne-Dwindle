using System; using System.Collections.Generic; using System.IO; using System.Linq; using System.Text; using UnityEngine;

public enum Want { Any, Yes, No }
public enum DetourWant { Any, None, Used, Optional, Required }
public enum StartPlacement { Any, Spread, Cluster }

// what the Level Maker window asks for
[Serializable]
public class MakerSettings
{
    // layout
    public int width = 3, height = 3, boards = 1, objects = 3;
    public int doorPairs, insideEnds; public bool randomDoorMix;
    public int obstacles; public bool ringsOnOtherBoards = true; public StartPlacement starts; public int minRoute = 2;
    // supply
    public int startCross = 1, startBridge; public bool noTees; public int spareTiles = 2;
    // what the best plan must (or must not) have
    public Want groupDelivery = Want.Yes, sharedTiles = Want.Yes, buildAhead, doubleBuildAhead, earnedPlus, pairsTrap, breakTrap, decoyDoor, bridge;
    public DetourWant detour;
    public bool facingChoice, everyBoardUsed, noNearMiss; public int minWays;
    public bool luckProof = true; // undo re-rolls the break, so no plan may beat the target by undoing until the break misses
    // search
    public int timeLimit = 60, seed;
}

// a level the maker found (or one from Levels.cs), with its best plan played out
class MakerResult
{
    public LevelData level; public Props props; public Replay2 replay;
    public List<string> steps = new(), found = new();
    public List<int> wayScores; // best score of every finishing layout, null if counting ran out of time
    public int tries; public float seconds;
}

class MakerProgress
{
    public volatile int tries, solved; public volatile bool done;
    public MakerResult result; public string error;
}

static class LevelMaker
{
    static bool Is(Want w, bool v) => w == Want.Any || (w == Want.Yes) == v;

    public static Spec ToSpec(MakerSettings s) => new Spec
    {
        boards = Enumerable.Repeat((s.width, s.height), s.boards).ToArray(),
        objects = s.objects, doorPairs = s.doorPairs, obstacles = s.obstacles,
        startCross = s.startCross, startBridge = s.bridge == Want.Yes ? Math.Max(1, s.startBridge) : s.bridge == Want.No ? 0 : s.startBridge,
        slack = s.spareTiles, crossBoard = s.ringsOnOtherBoards, noTees = s.noTees,
        earnedNeeded = s.earnedPlus == Want.Yes ? 1 : 0, minRoute = s.minRoute, requireBridge = s.bridge == Want.Yes,
        spread = s.starts == StartPlacement.Spread, cluster = s.starts == StartPlacement.Cluster,
        insideEnds = s.randomDoorMix ? -1 : s.insideEnds, type = "any",
    };

    // settings that can never be met, so the search doesn't run for nothing
    public static List<string> Problems(MakerSettings s)
    {
        var res = new List<string>();
        bool doors = s.doorPairs > 0;
        if (!doors && (s.detour is DetourWant.Used or DetourWant.Optional or DetourWant.Required)) res.Add("A detour needs at least one door pair.");
        if (!doors && s.decoyDoor == Want.Yes) res.Add("A decoy door needs at least one door pair.");
        if (!doors && s.facingChoice) res.Add("A facing choice needs at least one door pair.");
        if (s.facingChoice && !s.randomDoorMix && s.insideEnds == 0) res.Add("A facing choice needs a door end inside a board.");
        if (s.groupDelivery == Want.No && (s.earnedPlus == Want.Yes || s.pairsTrap == Want.Yes)) res.Add("Earned + and the pairs trap need group deliveries.");
        if (s.doubleBuildAhead == Want.Yes && s.buildAhead == Want.No) res.Add("Double build ahead is a kind of build ahead.");
        if (s.doubleBuildAhead == Want.Yes && s.objects < 3) res.Add("Double build ahead needs at least 3 objects.");
        if (s.pairsTrap == Want.Yes && s.objects < 4) res.Add("A pairs trap needs at least 4 objects (two pairs on paper).");
        return res;
    }

    // does the level have everything the settings ask for? cheap checks first
    public static bool Check(MakerSettings s, LevelData l, Props p)
    {
        if (p == null || !p.replayOk || l.doors.Count != 2 * s.doorPairs) return false;
        var b = p.best;
        if (s.groupDelivery == Want.Yes && !p.comboNeeded) return false;
        if (s.groupDelivery == Want.No && p.groups.Any(g => g > 1)) return false;
        if (!Is(s.sharedTiles, p.tiles < p.fewestSum)) return false;
        if (!Is(s.buildAhead, p.buildsAhead)) return false;
        if (!Is(s.earnedPlus, p.earnedCrossUsed)) return false;
        if (!Is(s.pairsTrap, p.pairTrap)) return false;
        if (!Is(s.breakTrap, p.trap)) return false;
        if (s.bridge == Want.No && b.tiles.Values.Contains(TileType.Bridge)) return false;
        if (s.bridge == Want.Yes && !BridgeNeeded(l, p)) return false;
        if (s.doorPairs > 0 && !Is(s.decoyDoor, UnusedDoors(l, b).Count > 0)) return false;
        if (s.everyBoardUsed && !Gen.AllBoardsUsed(l, b)) return false;
        if (s.facingChoice && !Gen.Facing(l, b)) return false;
        if (s.doubleBuildAhead != Want.Any && Gen.Abcd(l, b) != (s.doubleBuildAhead == Want.Yes)) return false;
        switch (s.detour)
        {
            case DetourWant.None: if (p.detour) return false; break;
            case DetourWant.Used: if (!p.detour) return false; break;
            case DetourWant.Optional:
            case DetourWant.Required:
                if (!p.detour) return false;
                var without = new Solver2(l) { noDetours = true }.Best();
                if (s.detour == DetourWant.Required ? without != null : without == null || without.score >= p.score) return false;
                break;
        }
        if (s.minWays > 0 && Ways(l).Count < s.minWays) return false;
        if (s.noNearMiss && !Gen.NoNearMiss(l, p.score)) return false;
        if (s.luckProof && LuckyBest(l) > p.score) return false;
        return true;
    }

    // runs on a background thread until it finds a level, Solver2.Abort is set, or something throws
    public static void Run(MakerSettings s, MakerProgress pr)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var sp = ToSpec(s);
            var rnd = new System.Random(s.seed != 0 ? s.seed : Environment.TickCount);
            while (!Solver2.Abort)
            {
                pr.tries++;
                var (l, p) = Gen.Candidate(sp, rnd, "Level");
                if (l == null) continue;
                pr.solved++;
                var checkClock = System.Diagnostics.Stopwatch.StartNew();
                bool fits = Check(s, l, p);
                Gen.Trace?.Invoke($"  checks {(fits ? "passed" : "failed")} in {checkClock.ElapsedMilliseconds} ms");
                if (!fits) continue;
                l.targetScore = p.score; l.bestTiles = p.tiles; l.bestPlanHint = Export.Hint(p);
                pr.result = Finish(l, p);
                pr.result.tries = pr.tries; pr.result.seconds = (float)clock.Elapsed.TotalSeconds;
                return;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { pr.error = e.ToString(); }
        finally { pr.done = true; }
    }

    // solves a level that already exists (from Levels.cs), without changing it
    public static void Solve(LevelData l, MakerProgress pr)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var p = Gen.Analyze(l);
            if (p == null) { pr.error = $"{l.title} has no solution within the solver's limits."; return; }
            pr.result = Finish(l, p);
            pr.result.seconds = (float)clock.Elapsed.TotalSeconds;
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { pr.error = e.ToString(); }
        finally { pr.done = true; }
    }

    static MakerResult Finish(LevelData l, Props p)
    {
        var r = new MakerResult { level = l, props = p, replay = new Replay2(l).Run(p.best) };
        for (int k = 0; k < r.replay.placements.Count; k++)
        {
            var (c, t) = r.replay.placements[k];
            var ev = r.replay.stepEvents[k];
            r.steps.Add($"{LB.Local(l, c)} {Export.TileText(t)}" + (ev.Count > 0 ? " -> " + string.Join("; ", ev) : ""));
        }
        var b = p.best;
        if (p.comboNeeded) r.found.Add("Group delivery pays off");
        if (p.tiles < p.fewestSum) r.found.Add("Objects share tiles");
        if (p.buildsAhead) r.found.Add(Gen.Abcd(l, b) ? "Double build ahead (two later objects)" : "Build ahead");
        if (p.earnedCrossUsed) r.found.Add("Earned + needed (as " + string.Join(", ", EarnedUse.Roles(l, b).Distinct()) + ")");
        if (p.pairTrap) r.found.Add("Pairs trap");
        else if (p.trap) r.found.Add($"Break trap (the best plan on paper scores {p.unsafeRaw}, but its deliveries break tiles)");
        if (p.detour)
        {
            var without = new Solver2(l) { noDetours = true }.Best();
            r.found.Add(without == null ? "Required detour (can't finish without it)" : $"Optional detour ({without.score} without it)");
        }
        if (b.tiles.Values.Contains(TileType.Bridge)) r.found.Add("Bridge");
        int lucky = LuckyBest(l);
        if (lucky > p.score) r.found.Add($"NOT luck-proof: undoing until the break misses reaches {lucky}");
        var unused = UnusedDoors(l, b);
        if (unused.Count > 0) r.found.Add($"Decoy door (the best plan doesn't use door {string.Join(", ", unused)})");
        if (l.doors.Count > 0 && Gen.Facing(l, b)) r.found.Add("Door facing choice");
        try { r.wayScores = Ways(l); } catch (OperationCanceledException) { r.wayScores = null; }
        return r;
    }

    // the best score a player can reach by undoing until each break misses (undo re-rolls it)
    public static int LuckyBest(LevelData l) => new Solver2(l) { requireSafe = false, penalize = false, lucky = true }.Best()?.score ?? 0;

    // the best plan uses the bridge, and without it the level scores less (or can't be finished)
    public static bool BridgeNeeded(LevelData l, Props p)
    {
        if (!p.best.tiles.Values.Contains(TileType.Bridge)) return false;
        var keep = l.inventory;
        l.inventory = new Dictionary<TileType, int>(keep) { [TileType.Bridge] = 0 };
        try { var without = new Solver2(l).Best(); return without == null || without.score < p.score; }
        finally { l.inventory = keep; }
    }

    // door pairs no object goes through, in either direction
    public static List<string> UnusedDoors(LevelData l, Plan b) =>
        l.doors.Select(d => d.label).Distinct()
            .Where(lb => !l.doors.Where(d => d.label == lb).Any(d => b.routes.Any(rt => rt.steps.Any(st => d.IsOn(st.cell, st.exit))))).ToList();

    // the best score of every layout that finishes the level
    public static List<int> Ways(LevelData l)
    {
        var sv = Solver2.CountWays(l);
        return sv.collect.GroupBy(c => c.layout).Select(g => g.Max(x => x.score)).OrderByDescending(x => x).ToList();
    }

    public static string Comment(MakerResult r) =>
        r.found.Count == 0 ? "Generated level." : "Generated: " + string.Join(", ", r.found.Select(f => f.Split(" (")[0].ToLower())) + ".";

    static readonly Dictionary<TileType, string> InvNames = new()
    {
        [TileType.Cross] = "cross", [TileType.Bridge] = "bridge", [TileType.TeeNES] = "tNES", [TileType.TeeESW] = "tESW",
        [TileType.TeeSWN] = "tSWN", [TileType.TeeWNE] = "tWNE", [TileType.CornerNE] = "ne", [TileType.CornerES] = "es", [TileType.CornerSW] = "sw", [TileType.CornerWN] = "wn",
        [TileType.StraightH] = "h", [TileType.StraightV] = "v",
    };

    // the level as Levels.cs writes it: the idea, the solution, then LevelN()
    public static string Code(MakerResult r, int number, string comment)
    {
        var l = r.level; var sb = new StringBuilder();
        foreach (var line in (comment ?? "").Split('\n').Where(x => x.Trim().Length > 0)) sb.Append($"    // {line.Trim()}\n");
        sb.Append("    // Solution:\n");
        for (int k = 0; k < r.steps.Count; k++) sb.Append($"    //   {k + 1}. {r.steps[k]}\n");
        sb.Append($"    // Score {r.props.score} with {r.props.tiles} tiles (target).\n");
        sb.Append($"    static LevelData Level{number}()\n    {{\n");
        sb.Append($"        var l = new LevelData {{ title = \"Level {number}\", targetScore = {r.props.score}, bestTiles = {r.props.tiles},\n");
        sb.Append($"            bestPlanHint = \"{Export.Hint(r.props)}\" }};\n");
        sb.Append($"        l.SetBoards({string.Join(", ", l.boards.Select(b => $"({b.width}, {b.height})"))});\n");
        sb.Append("        l.inventory = Inv(" + string.Join(", ", Tiles.All.Where(t => l.inventory.GetValueOrDefault(t) > 0).Select(t => $"{InvNames[t]}: {l.inventory[t]}")) + ");\n");
        foreach (var o in l.obstacles) { int b = l.BoardOf(o); var p = o - l.boards[b].origin; sb.Append($"        l.obstacles.Add(l.At({b}, {p.x}, {p.y}));\n"); }
        foreach (var d in l.doors) { var p = d.cell - l.boards[d.board].origin; sb.Append($"        Door(l, \"{d.label}\", {d.board}, {p.x}, {p.y}, Dir.{d.side});\n"); }
        foreach (var o in l.objects)
        {
            var s = o.start - l.boards[o.startBoard].origin; var t = o.dest - l.boards[o.destBoard].origin;
            sb.Append($"        Obj(l, \"{o.name}\", {o.name}, {o.startBoard}, {s.x}, {s.y}, Dir.{o.heading}, {o.destBoard}, {t.x}, {t.y});\n");
        }
        sb.Append("        return l;\n    }\n");
        return sb.ToString();
    }

    // adds LevelN() to Levels.cs (and to the All list), or replaces it if it's already there
    public static string WriteToLevels(string path, int number, string code)
    {
        var raw = File.ReadAllText(path);
        bool crlf = raw.Contains("\r\n");
        var text = raw.Replace("\r\n", "\n");
        var sig = $"    static LevelData Level{number}()\n";
        int at = text.IndexOf(sig);
        string what;
        if (at >= 0)
        {
            // replace the comment block above it and the method itself
            int start = at;
            while (start > 0)
            {
                int prev = text.LastIndexOf('\n', start - 2) + 1;
                if (!text.Substring(prev, start - prev).TrimStart().StartsWith("//")) break;
                start = prev;
            }
            int end = text.IndexOf("\n    }\n", at) + "\n    }\n".Length;
            text = text.Substring(0, start) + code + text.Substring(end);
            what = $"Replaced Level{number}() in Levels.cs.";
        }
        else
        {
            int helpers = text.IndexOf("    // an object starting at");
            if (helpers < 0) helpers = text.IndexOf("    static void Obj(");
            if (helpers < 0) throw new Exception("Couldn't find where the levels end in Levels.cs.");
            text = text.Substring(0, helpers) + code + "\n" + text.Substring(helpers);
            int list = text.IndexOf("public static readonly List<LevelData> All");
            int close = text.IndexOf("\n    };", list);
            text = text.Substring(0, close) + $"\n        Level{number}()," + text.Substring(close);
            what = $"Added Level{number}() to Levels.cs and to Levels.All.";
        }
        File.WriteAllText(path, crlf ? text.Replace("\n", "\r\n") : text);
        return what;
    }
}
