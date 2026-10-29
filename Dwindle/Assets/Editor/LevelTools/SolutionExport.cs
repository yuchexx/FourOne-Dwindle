using System; using System.Collections.Generic; using System.IO; using System.Linq; using System.Text; using UnityEngine;

// writes a level's best plans (every plan reaching its target) into Scripts/Levels/Solutions/Solutions.Generated.cs (a section each),
// for analytics and hints. LevelSolution.cs says what each part means
static class SolutionExport
{
    // roads up to this much longer than an object's shortest. Level 9's best plans stop growing at 5 (checked up to 9)
    public const int Slack = 6;

    // every plan reaching the target: each tile assignment, grouping, order, delivering square and build-ahead
    public static List<Plan> BestPlans(LevelData l)
    {
        var sv = new Solver2(l, Slack) { every = new List<Plan>(), floor = l.targetScore, maxReplays = 5000000, maxLeaves = 100000000 };
        sv.Best();
        if (sv.limitHit) throw new Exception($"{l.title}: the solver hit its search limit, so the list of best plans could be incomplete.");
        if (sv.every.Count == 0) throw new Exception($"{l.title}: no plan reaches the target {l.targetScore}.");
        return sv.every.GroupBy(p => Tiles(p) + "#" + Roads(p) + "#" + Deliveries(p) + "#" + string.Join(",", p.lastCells) + "#" + string.Join("/", p.pre.Select(x => string.Join(",", x))))
            .Select(g => g.First()).ToList();
    }

    static string Tiles(Plan p) => string.Join(";", p.tiles.OrderBy(kv => kv.Key.x).ThenBy(kv => kv.Key.y).Select(kv => kv.Key + "=" + kv.Value));
    static string Roads(Plan p) => string.Join("|", p.routes.Select(r => string.Join(",", r.steps.Select(s => $"{s.cell}{s.entry}{s.exit}"))));
    static string Deliveries(Plan p) => string.Join("|", p.events.Select(g => string.Join(",", g.OrderBy(j => j))));

    // the placement order Replay2 uses: each delivery's new squares (build-ahead first), its delivering square last
    static List<Vector2Int> Order(Plan p, Vector2Int? moved = null, int after = -1)
    {
        var order = new List<Vector2Int>(); var placed = new HashSet<Vector2Int>();
        for (int k = 0; k < p.events.Count; k++)
        {
            var fresh = p.pre[k].Concat(p.events[k].SelectMany(j => p.routes[j].order)).Where(c => !placed.Contains(c)).Distinct().ToList();
            var last = p.lastCells[k];
            foreach (var c in fresh.Where(c => c != last && c != moved)) order.Add(c);
            order.Add(last);
            if (k == after && moved != null) order.Add(moved.Value);
            foreach (var c in fresh) placed.Add(c);
        }
        if (moved != null && !order.Contains(moved.Value)) order.Add(moved.Value);
        return order;
    }

    // the build-ahead squares of delivery k that really have to come first: placed right after the delivery
    // instead, the plan no longer reaches the target
    static List<Vector2Int> Before(LevelData l, Plan p, int k) =>
        p.pre[k].Where(c =>
        {
            var rp = new Replay2(l).Run(p, order: Order(p, c, k));
            return !(rp.ok && rp.score >= l.targetScore);
        }).ToList();

    static bool InGrid(LevelData l, Vector2Int c) => l.BoardOf(c) >= 0;
    static (int b, int x, int y) Loc(LevelData l, Vector2Int c) { int b = l.BoardOf(c); var p = c - l.boards[b].origin; return (b, p.x, p.y); }
    static string L(LevelData l, Vector2Int c) { var (b, x, y) = Loc(l, c); return $"{b}, {x}, {y}"; }
    static string Tuple(LevelData l, Vector2Int c) => $"({L(l, c)})";

    // tricks: what the best plans use, and whether the level needs it
    static bool Detour(LevelData l, Plan p) => Enumerable.Range(0, l.objects.Count).Any(i => l.objects[i].startBoard == l.objects[i].destBoard && p.routes[i].doors > 0);
    // placing the bridge at all, as LevelMaker.BridgeNeeded counts it (on some levels it's laid as a straight, so an I is free elsewhere)
    static bool Bridge(Plan p) => p.tiles.Values.Contains(TileType.Bridge);

    static TrickNeed Need(List<Plan> plans, Func<Plan, bool> uses, Func<bool> finishesWithout)
    {
        int n = plans.Count(uses);
        if (n == 0) return TrickNeed.NotUsed;
        if (n < plans.Count) return TrickNeed.Optional;
        return finishesWithout() ? TrickNeed.RequiredForTarget : TrickNeed.RequiredToFinish;
    }

    static List<(string name, TrickNeed need)> Tricks(LevelData l, List<Plan> plans, Func<Plan, bool> buildsAhead)
    {
        bool Finishes(Solver2 s) => s.Best() != null;
        return new List<(string, TrickNeed)>
        {
            ("Delivered together", Need(plans, p => p.events.Any(e => e.Count >= 2), () => Finishes(new Solver2(l) { allowCombos = false }))),
            ("Detour", Need(plans, p => Detour(l, p), () => Finishes(new Solver2(l) { noDetours = true }))),
            ("Build ahead", Need(plans, buildsAhead, () => Finishes(new Solver2(l) { filter = x => !x.BuildsAhead }))),
            ("Earned +", Need(plans, p => LevelMaker.UsesEarnedPlus(l, p), () => Finishes(new Solver2(l) { filter = x => !LevelMaker.UsesEarnedPlus(l, x) }))),
            ("Bridge", Need(plans, Bridge, () =>
            {
                var keep = l.inventory;
                l.inventory = new Dictionary<TileType, int>(keep) { [TileType.Bridge] = 0 };
                try { return Finishes(new Solver2(l)); } finally { l.inventory = keep; }
            })),
        };
    }

    // the generated file
    public static string Code(LevelData l, int number)
    {
        var plans = BestPlans(l);
        var befores = new Dictionary<(Plan, int), List<Vector2Int>>();
        List<Vector2Int> BeforeOf(Plan p, int k) => befores.TryGetValue((p, k), out var b) ? b : befores[(p, k)] = Before(l, p, k);
        var families = plans.GroupBy(Roads).ToList();
        var tricks = Tricks(l, plans, p => Enumerable.Range(0, p.events.Count).Any(k => BeforeOf(p, k).Count > 0));
        string Names(IEnumerable<int> objs) => "new[] { " + string.Join(", ", objs.OrderBy(j => j).Select(j => $"\"{l.objects[j].name}\"")) + " }";

        var sb = new StringBuilder();
        sb.Append($"    // ---- Level {number} ({plans.Count} best plans, {plans.Select(Tiles).Distinct().Count()} tile layouts, {families.Count} road famil{(families.Count == 1 ? "y" : "ies")}) ----\n");
        sb.Append($"    public static LevelSolution Level{number}() => new LevelSolution\n    {{\n");
        sb.Append($"        target = {l.targetScore}, bestTiles = {plans.Min(p => p.cells)},\n");
        sb.Append("        tricks = new[]\n        {\n");
        foreach (var (name, need) in tricks) sb.Append($"            TrickOf(\"{name}\", TrickNeed.{need}),\n");
        sb.Append("        },\n        families = new[]\n        {\n");
        for (int f = 0; f < families.Count; f++)
        {
            var ps = families[f].ToList(); var p0 = ps[0];
            var allowed = p0.tiles.Keys.ToDictionary(c => c, c => ps.Select(p => p.tiles[c]).Distinct().OrderBy(t => t).ToList());
            sb.Append($"            // family {(char)('A' + f)}: {ps.Select(Tiles).Distinct().Count()} tile layouts\n");
            sb.Append("            new RoadFamily\n            {\n                roads = new[]\n                {\n");
            for (int o = 0; o < l.objects.Count; o++)
            {
                var steps = p0.routes[o].steps;
                var choices = new List<string>();
                for (int s = 0; s < steps.Count; s++)
                {
                    var st = steps[s];
                    if (allowed[st.cell].Any(t => global::Tiles.Exits(t, st.entry).Count > 1)) choices.Add($"Turn({L(l, st.cell)}, Dir.{st.exit})");
                    var door = l.DoorOnEdge(st.cell, st.exit);
                    if (door == null || s + 1 >= steps.Count) continue;
                    var partner = l.Partner(door);
                    if (InGrid(l, partner.cell) && InGrid(l, partner.Neighbor)) choices.Add($"OutOf(\"{door.label}\", Dir.{DirUtil.Opposite(steps[s + 1].entry)})");
                }
                sb.Append($"                    RoadOf(\"{l.objects[o].name}\", new[] {{ {string.Join(", ", steps.Select(st => Tuple(l, st.cell)))} }}" +
                    (choices.Count > 0 ? ",\n                        " + string.Join(", ", choices) : "") + "),\n");
            }
            sb.Append("                },\n                squares = new[]\n                {\n");
            foreach (var c in allowed.Keys.OrderBy(c => Loc(l, c).b).ThenBy(c => Loc(l, c).x).ThenBy(c => Loc(l, c).y))
                sb.Append($"                    Allow({L(l, c)}, {string.Join(", ", allowed[c].Select(t => "TileType." + t))}),\n");
            sb.Append("                },\n                orders = new[]\n                {\n");
            foreach (var og in ps.GroupBy(p => string.Join("|", p.events.Select(g => string.Join(",", g.OrderBy(j => j))))))
            {
                var q = og.ToList();
                sb.Append("                    new DeliveryOrder\n                    {\n                        deliveries = new[]\n                        {\n");
                for (int k = 0; k < q[0].events.Count; k++)
                {
                    var completions = q.GroupBy(p => p.lastCells[k]).Select(g =>
                    {
                        // a square counts as "before" only when every plan delivering here needs it
                        var need = g.Select(p => (IEnumerable<Vector2Int>)BeforeOf(p, k)).Aggregate((a, b) => a.Intersect(b)).ToList();
                        return $"At({L(l, g.Key)}" + string.Concat(need.OrderBy(c => Loc(l, c).b).ThenBy(c => Loc(l, c).x).ThenBy(c => Loc(l, c).y).Select(c => ", " + Tuple(l, c))) + ")";
                    });
                    sb.Append($"                            Deliver({Names(q[0].events[k])}, {string.Join(", ", completions)}),\n");
                }
                sb.Append("                        },\n                    },\n");
            }
            sb.Append("                },\n            },\n");
        }
        sb.Append("        },\n");

        // the answer for the stage 3 hint: the best plan with the fewest steps (tiles and arrows), played out
        List<string> answer = null; Plan answerPlan = null;
        string OrderKey(Plan p) => string.Join("|", p.events.Select(g => string.Join(",", g.OrderBy(j => j))));
        foreach (var p in plans)
        {
            var rp = new Replay2(l).Run(p);
            if (!rp.ok || rp.score < l.targetScore) throw new Exception($"{l.title}: a best plan didn't replay to the target ({(rp.ok ? "score " + rp.score : rp.problem)}).");
            var actions = new List<string>();
            for (int s = 0; s < rp.placements.Count; s++)
            {
                var (c, t) = rp.placements[s];
                actions.Add($"Place({L(l, c)}, TileType.{t})");
                foreach (var (obj, way) in rp.stepChoices[s]) actions.Add($"Choose(\"{l.objects[obj].name}\", Dir.{way})");
            }
            if (answer == null || actions.Count < answer.Count) { answer = actions; answerPlan = p; }
        }
        // which family and order it follows, in the order they're written above
        int answerFamily = families.FindIndex(g => g.Key == Roads(answerPlan));
        int answerOrder = families[answerFamily].Select(OrderKey).Distinct().ToList().IndexOf(OrderKey(answerPlan));
        sb.Append($"        answerFamily = {answerFamily}, answerOrder = {answerOrder},\n");
        sb.Append("        answer = new[]\n        {\n");
        foreach (var a in answer) sb.Append($"            {a},\n");
        sb.Append("        },\n    };\n");
        sb.Append($"    // ---- end of Level {number} ----\n");
        return sb.ToString();
    }

    // ---- the file: every level's section in Solutions.Generated.cs, in level order
    public static string FilePath(string solutionsDir) => Path.Combine(solutionsDir, "Solutions.Generated.cs");

    static readonly System.Text.RegularExpressions.Regex SectionRx = new(
        @"    // ---- Level (\d+) .*?----\n.*?    // ---- end of Level \1 ----\n", System.Text.RegularExpressions.RegexOptions.Singleline);

    static SortedDictionary<int, string> ReadSections(string solutionsDir)
    {
        var res = new SortedDictionary<int, string>();
        var path = FilePath(solutionsDir);
        if (!File.Exists(path)) return res;
        foreach (System.Text.RegularExpressions.Match m in SectionRx.Matches(File.ReadAllText(path).Replace("\r\n", "\n")))
            res[int.Parse(m.Groups[1].Value)] = m.Value;
        return res;
    }

    // CRLF, like the other scripts
    static void WriteSections(string solutionsDir, SortedDictionary<int, string> sections)
    {
        Directory.CreateDirectory(solutionsDir);
        var sb = new StringBuilder();
        sb.Append("// generated by the Level Maker (Editor/LevelTools/SolutionExport.cs) from Levels.cs. don't edit by hand:\n");
        sb.Append("// save a level in the Level Maker again, or press Rebuild all solutions. one section per level, in level order\n");
        sb.Append("public static partial class Solutions\n{\n");
        sb.Append(string.Join("\n", sections.Values));
        sb.Append("}\n");
        File.WriteAllText(FilePath(solutionsDir), sb.ToString().Replace("\n", "\r\n"));
    }

    // a section renamed from level `from` to `to` (its markers and method)
    static string Renumber(string section, int from, int to) =>
        System.Text.RegularExpressions.Regex.Replace(section, $@"\bLevel( ?){from}\b", m => $"Level{m.Groups[1].Value}{to}");

    // one level's section, written or replaced
    public static void Write(string solutionsDir, LevelData l, int number)
    {
        var sections = ReadSections(solutionsDir);
        sections[number] = Code(l, number);
        WriteSections(solutionsDir, sections);
    }

    // for inserting a level after `after`: every later section moves up one, and `added` (worked out for after + 1) goes in
    public static void InsertSection(string solutionsDir, int after, string added)
    {
        var sections = ReadSections(solutionsDir);
        var moved = new SortedDictionary<int, string>();
        foreach (var (n, s) in sections) moved[n > after ? n + 1 : n] = n > after ? Renumber(s, n, n + 1) : s;
        moved[after + 1] = added;
        WriteSections(solutionsDir, moved);
    }

    // the one-file-per-level files from before, removed (with their .meta) once everything is in Solutions.Generated.cs
    static int RemoveOldFiles(string solutionsDir)
    {
        int n = 0;
        foreach (var f in Directory.GetFiles(solutionsDir, "Solutions.Level*.cs"))
        {
            File.Delete(f); if (File.Exists(f + ".meta")) File.Delete(f + ".meta");
            n++;
        }
        return n;
    }

    // the line in LevelN() that links its solution
    public static string LinkLine(int number) => $"        l.solution = Solutions.Level{number}();\n";

    // the LevelN() methods in Levels.All's order, so a solution goes with its level even after the list is reordered
    public static List<int> Numbers(string levelsPath)
    {
        var text = File.ReadAllText(levelsPath);
        int list = text.IndexOf("public static readonly List<LevelData> All");
        int close = text.IndexOf("};", list);
        return System.Text.RegularExpressions.Regex.Matches(text.Substring(list, close - list), @"Level(\d+)\(\)").Select(m => int.Parse(m.Groups[1].Value)).ToList();
    }

    // adds the link line to LevelN() when it's missing
    static void Link(string levelsPath, int number)
    {
        var raw = File.ReadAllText(levelsPath);
        bool crlf = raw.Contains("\r\n");
        var text = raw.Replace("\r\n", "\n");
        int at = text.IndexOf($"    static LevelData Level{number}()\n");
        if (at < 0) throw new Exception($"Levels.cs has no Level{number}().");
        int end = text.IndexOf("\n    }\n", at);
        if (text.Substring(at, end - at).Contains("l.solution =")) return;
        int ret = text.LastIndexOf("        return l;\n", end);
        text = text.Substring(0, ret) + LinkLine(number) + text.Substring(ret);
        File.WriteAllText(levelsPath, crlf ? text.Replace("\n", "\r\n") : text);
    }

    // every level's solution again (after a rules or solver change, or levels edited by hand); sections of levels that
    // are gone drop out. all of them are worked out before the file is written. levels: Levels.All.
    // progress: called with each level's title
    public static string RebuildAll(string levelsPath, string solutionsDir, IList<LevelData> levels, Action<string> progress = null)
    {
        var numbers = Numbers(levelsPath);
        if (numbers.Count != levels.Count) throw new Exception($"Levels.All lists {numbers.Count} levels in Levels.cs but has {levels.Count} loaded. Let Unity recompile first.");
        var before = ReadSections(solutionsDir).Keys.ToList();
        var sections = new SortedDictionary<int, string>();
        for (int i = 0; i < levels.Count; i++)
        {
            progress?.Invoke(levels[i].title);
            sections[numbers[i]] = Code(levels[i], numbers[i]);
        }
        WriteSections(solutionsDir, sections);
        foreach (var n in numbers) Link(levelsPath, n);
        int gone = before.Count(n => !numbers.Contains(n));
        RemoveOldFiles(solutionsDir);
        return $"Rebuilt the solutions of {levels.Count} levels" + (gone > 0 ? $", dropped {gone} for levels that are gone." : ".");
    }
}
