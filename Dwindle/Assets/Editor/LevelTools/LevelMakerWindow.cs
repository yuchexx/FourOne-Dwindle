using System.Collections.Generic; using System.IO; using System.Linq; using System.Threading.Tasks; using UnityEditor; using UnityEngine;

// Tools > Dwindle Level Maker: pick a layout and the traps a level should have, generate one,
// see its best solution step by step, and copy it (or write it) into Levels.cs
public class LevelMakerWindow : EditorWindow
{
    const float SettingsWidth = 340f;
    static string LevelsPath => Path.Combine(Application.dataPath, "Scripts/Levels/Levels.cs");
    static string SolutionsDir => Path.Combine(Application.dataPath, "Scripts/Levels/Solutions");

    static readonly Color CellColor = new Color(0.25f, 0.27f, 0.32f), TileColor = new Color(0.38f, 0.41f, 0.48f),
        LineColor = new Color(0.93f, 0.93f, 0.96f), ObstacleColor = new Color(0.10f, 0.10f, 0.12f),
        BrokenColor = new Color(0.85f, 0.30f, 0.30f, 0.55f), DoorColor = new Color(0.72f, 0.45f, 0.98f);

    [SerializeField] MakerSettings settings = new MakerSettings();
    [SerializeField] int levelNumber = 13;
    [SerializeField] int insertAfter = 1;
    [SerializeField] string comment = "";
    [SerializeField] int existing;
    [SerializeField] bool fullReport;

    Vector2 settingsScroll, resultScroll, codeScroll;
    MakerProgress progress; double startTime; bool solvingExisting;
    MakerResult result; int step; string status;
    // writing solutions (every best plan) takes a few seconds a level, so it runs in the background
    Task<string> writing; volatile string writingWhat;

    [MenuItem("Tools/Dwindle Level Maker")]
    static void Open()
    {
        var w = GetWindow<LevelMakerWindow>("Level Maker");
        w.minSize = new Vector2(820, 540);
    }

    void OnDisable()
    {
        if ((progress != null && !progress.done) || writing != null) Solver2.Abort = true; // stop the background search if the window closes or scripts reload
    }

    void Update()
    {
        if (writing != null)
        {
            if (!writing.IsCompleted) { Repaint(); return; }
            var job = writing; writing = null;
            status = job.IsFaulted ? job.Exception.InnerException.Message : job.Result;
            AssetDatabase.Refresh();
            Repaint();
        }
        if (progress == null) return;
        if (!progress.done && !solvingExisting && EditorApplication.timeSinceStartup - startTime > settings.timeLimit) Solver2.Abort = true;
        if (progress.done)
        {
            var pr = progress; progress = null;
            if (pr.result != null)
            {
                result = pr.result; step = result.steps.Count;
                if (solvingExisting)
                {
                    var numbers = SolutionExport.Numbers(LevelsPath); // its LevelN(), even if the All list was reordered
                    levelNumber = existing < numbers.Count ? numbers[existing] : existing + 1;
                    comment = LevelMaker.ExistingComment(LevelsPath, levelNumber); // its note from Levels.cs
                    status = $"Solved {result.level.title} in {result.seconds:0.0}s.";
                }
                else
                {
                    levelNumber = Levels.All.Count + 1;
                    comment = LevelMaker.Comment(result);
                    status = $"Found a level after {result.tries} tries in {result.seconds:0.0}s.";
                }
            }
            else if (pr.error != null) status = pr.error;
            else status = solvingExisting ? "Stopped." : $"No level found in {pr.tries} tries ({pr.solved} were solvable but missed a check). Try a bigger board, more objects, fewer Yes checks, or a longer time limit.";
        }
        Repaint();
    }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll, GUILayout.Width(SettingsWidth));
        DrawSettings();
        EditorGUILayout.EndScrollView();
        resultScroll = EditorGUILayout.BeginScrollView(resultScroll);
        DrawResult();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndHorizontal();
    }

    // ---------- settings ----------

    static void Section(string title)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    static Want WantField(string label, string tip, Want value) => (Want)EditorGUILayout.EnumPopup(new GUIContent(label, tip), value);

    const string NeedTip = "\nRequired: no plan without it reaches the target." +
        "\nUsed by best plan: the best plan uses it, but skipping it may still reach the target." +
        "\nNo: the best plan doesn't use it.";
    static Need NeedField(string label, string tip, Need value) => (Need)EditorGUILayout.EnumPopup(new GUIContent(label, tip + NeedTip), value);

    void DrawSettings()
    {
        var s = settings;
        bool running = progress != null;
        EditorGUI.BeginDisabledGroup(running);

        Section("Boards");
        s.boards = EditorGUILayout.IntPopup("Boards", s.boards, new[] { "1", "2", "4" }, new[] { 1, 2, 4 });
        s.width = EditorGUILayout.IntSlider("Width", s.width, 2, 5);
        s.height = EditorGUILayout.IntSlider("Height", s.height, 2, 5);
        s.obstacles = EditorGUILayout.IntSlider(new GUIContent("Blocked squares", "Squares no tile can go on"), s.obstacles, 0, 3);

        Section("Objects");
        s.objects = EditorGUILayout.IntSlider("Objects", s.objects, 1, 5);
        s.starts = (StartPlacement)EditorGUILayout.EnumPopup(new GUIContent("Starts", "Spread: starts at least 3 squares apart. Cluster: starts and rings close together."), s.starts);
        if (s.boards > 1) s.ringsOnOtherBoards = EditorGUILayout.Toggle(new GUIContent("Rings on other boards", "Most rings are on a different board from their object's start"), s.ringsOnOtherBoards);
        s.minRoute = EditorGUILayout.IntSlider(new GUIContent("Min tiles per object", "Every object needs at least this many tiles, so none is filler"), s.minRoute, 1, 4);

        Section("Doors");
        s.doorPairs = EditorGUILayout.IntSlider(new GUIContent("Door pairs", "Each pair is two door ends with the same letter"), s.doorPairs, 0, 6);
        if (s.doorPairs > 0)
        {
            s.randomDoorMix = EditorGUILayout.Toggle(new GUIContent("Random inside/edge mix"), s.randomDoorMix);
            if (!s.randomDoorMix)
            {
                s.insideEnds = EditorGUILayout.IntSlider(new GUIContent("Ends inside a board", "Between two squares, so an object coming out picks which way to face"), Mathf.Min(s.insideEnds, 2 * s.doorPairs), 0, 2 * s.doorPairs);
                EditorGUILayout.LabelField(" ", $"{2 * s.doorPairs - s.insideEnds} on a board's outer edge", EditorStyles.miniLabel);
            }
        }

        Section("Supply");
        s.startCross = EditorGUILayout.IntSlider("Starting + tiles", s.startCross, 0, 3);
        EditorGUI.BeginDisabledGroup(s.bridge == Need.No);
        s.startBridge = EditorGUILayout.IntSlider("Bridge tiles", s.bridge == Need.Required || s.bridge == Need.UsedByBestPlan ? Mathf.Max(1, s.startBridge) : s.startBridge, 0, 2);
        EditorGUI.EndDisabledGroup();
        s.noTees = EditorGUILayout.Toggle(new GUIContent("No T tiles", "Junctions have to be + tiles"), s.noTees);
        s.spareTiles = EditorGUILayout.IntSlider(new GUIContent("Spare tiles", "Plain tiles added beyond what the best plan places"), s.spareTiles, 0, 4);

        Section("Traps and checks (for the best plan)");
        s.groupDelivery = NeedField("Group delivery", "Two or more objects arrive on the same placement", s.groupDelivery);
        s.sharedTiles = NeedField("Objects share tiles", "Two routes go through the same square, so one tile serves both", s.sharedTiles);
        s.buildAhead = NeedField("Build ahead", "A later object's road is built (and passed) before an earlier delivery breaks a tile it needs", s.buildAhead);
        s.doubleBuildAhead = NeedField("Double build ahead", "One delivery has TWO later objects built ahead", s.doubleBuildAhead);
        s.earnedPlus = NeedField("Earned +", "The + earned by a group delivery fills a tile the supply doesn't have", s.earnedPlus);
        s.pairsTrap = WantField("Pairs trap", "Pairs look best on paper, but their deliveries break tiles; the safe plan groups differently and stays better even if you re-place broken tiles", s.pairsTrap);
        s.breakTrap = WantField("Break trap", "The best plan on paper scores more, but its deliveries break tiles someone still needs", s.breakTrap);
        s.detour = (DetourWant)EditorGUILayout.EnumPopup(new GUIContent("Detour", "An object whose ring is on its own board goes out through a door and back.\nUsed by best plan: the best plan has one.\nRequired for target: it can finish without one, but not at the target.\nRequired to finish: it can't finish without one.\nNo: the best plan has none."), s.detour);
        s.decoyDoor = WantField("Decoy door", "Yes: a door the best plan doesn't use. No: every door is used.", s.decoyDoor);
        s.bridge = NeedField("Bridge", "The best plan puts a bridge down", s.bridge);
        s.facingChoice = EditorGUILayout.Toggle(new GUIContent("Door facing choice", "An object comes out of a door between two squares, so it picks a direction"), s.facingChoice);
        s.everyBoardUsed = EditorGUILayout.Toggle("Every board used", s.everyBoardUsed);
        s.noNearMiss = EditorGUILayout.Toggle(new GUIContent("No near miss", "One extra tile of any type wouldn't let everyone arrive together (slow check)"), s.noNearMiss);
        s.luckProof = EditorGUILayout.Toggle(new GUIContent("Luck-proof", "Undo re-rolls which tile breaks, so no plan may beat the target by undoing until the break misses"), s.luckProof);
        s.minWays = EditorGUILayout.IntSlider(new GUIContent("Min ways to finish", "At least this many layouts finish the level, so weaker plans still get 2 stars"), s.minWays, 0, 6);

        Section("Search");
        s.timeLimit = EditorGUILayout.IntSlider(new GUIContent("Time limit (s)"), s.timeLimit, 10, 600);
        s.seed = EditorGUILayout.IntField(new GUIContent("Seed (0 = random)", "The same seed and settings give the same level"), s.seed);
        EditorGUI.EndDisabledGroup();

        var problems = LevelMaker.Problems(s);
        foreach (var p in problems) EditorGUILayout.HelpBox(p, MessageType.Error);

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(running || problems.Count > 0);
        if (GUILayout.Button("Generate", GUILayout.Height(28))) StartGenerate();
        EditorGUI.EndDisabledGroup();
        EditorGUI.BeginDisabledGroup(!running);
        if (GUILayout.Button("Cancel", GUILayout.Height(28), GUILayout.Width(80))) Solver2.Abort = true;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        if (writing != null) EditorGUILayout.HelpBox(writingWhat, MessageType.Info);
        else if (running)
            EditorGUILayout.HelpBox(solvingExisting ? "Solving..." :
                $"Searching... {EditorApplication.timeSinceStartup - startTime:0}s / {s.timeLimit}s, {progress.tries} layouts tried, {progress.solved} solvable", MessageType.Info);
        else if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);

        Section("Check an existing level");
        var titles = Levels.All.Select(l => l.title).ToArray();
        EditorGUILayout.BeginHorizontal();
        existing = EditorGUILayout.Popup(Mathf.Clamp(existing, 0, titles.Length - 1), titles);
        EditorGUI.BeginDisabledGroup(running);
        if (GUILayout.Button("Solve", GUILayout.Width(80))) StartSolve(Levels.All[existing]);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        EditorGUI.BeginDisabledGroup(running);
        fullReport = EditorGUILayout.Toggle(new GUIContent("Full report (slow)",
            "Also check which tricks are required (not just used by the best plan), whether a detour is required, " +
            "the luck check and the ways to finish. Each is another whole search."), fullReport);
        EditorGUI.EndDisabledGroup();

        Section("Solutions (best plans, for analytics and hints)");
        EditorGUI.BeginDisabledGroup(running || writing != null);
        if (GUILayout.Button(new GUIContent("Rebuild all solutions",
            "Writes every level's best plans into Scripts/Levels/Solutions/Solutions.Generated.cs again, dropping levels that are gone. " +
            "Needed after a change to the game's rules or the solver, or after editing Levels.cs by hand. About a minute."))) RebuildSolutions();
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.Space(10);
    }

    void StartGenerate()
    {
        var copy = JsonUtility.FromJson<MakerSettings>(JsonUtility.ToJson(settings)); // the search keeps its own settings
        Begin(false, pr => LevelMaker.Run(copy, pr));
    }

    void StartSolve(LevelData level)
    {
        bool full = fullReport;
        Begin(true, pr => LevelMaker.Solve(level, pr, full));
    }

    void Begin(bool existingLevel, System.Action<MakerProgress> work)
    {
        Solver2.Abort = false;
        solvingExisting = existingLevel; status = null;
        startTime = EditorApplication.timeSinceStartup;
        var pr = progress = new MakerProgress();
        Task.Run(() => work(pr));
    }

    // ---------- result ----------

    void DrawResult()
    {
        if (result == null)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("Pick the boards, objects, doors and traps on the left, then Generate. " +
                "Every Yes must hold in the level's best plan, every No must not, Any doesn't matter. " +
                "Or solve one of the levels already in Levels.cs.", MessageType.Info);
            return;
        }
        var r = result; var l = r.level; var p = r.props; var b = p.best;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(solvingExisting || r.tries == 0 ? l.title : "Generated level", EditorStyles.largeLabel);
        int unused = (p.score - Solver2.Delivery * l.objects.Count - Solver2.ComboPer * b.comboObjects - Solver2.DoorPer * b.doorObjects) / Solver2.Unused;
        EditorGUILayout.LabelField($"Best: {p.score} with {p.tiles} tiles  =  {l.objects.Count} deliveries {Solver2.Delivery * l.objects.Count}" +
            (b.comboObjects > 0 ? $" + together {Solver2.ComboPer * b.comboObjects}" : "") +
            (b.doorObjects > 0 ? $" + doors {Solver2.DoorPer * b.doorObjects}" : "") +
            $" + {unused} unused tiles {Solver2.Unused * unused}", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("Supply: " + string.Join(", ", Tiles.All.Where(t => l.inventory.GetValueOrDefault(t) > 0).Select(t => $"{Export.TileText(t)} x{l.inventory[t]}")), EditorStyles.wordWrappedLabel);
        if (!r.full) EditorGUILayout.LabelField("Ways to finish: not counted (turn on Full report).");
        else if (r.wayScores == null) EditorGUILayout.LabelField("Ways to finish: counting ran out of time.");
        else EditorGUILayout.LabelField($"Ways to finish: {r.wayScores.Count} ({r.wayScores.Count(x => x >= p.score)} reach the target)  " +
            string.Join(" ", r.wayScores.GroupBy(x => x).Select(g => g.Count() > 1 ? $"{g.Key}x{g.Count()}" : $"{g.Key}")), EditorStyles.wordWrappedLabel);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(r.full ? "Has" : "Has (used by the best plan; turn on Full report to see which are required)", EditorStyles.boldLabel);
        if (r.found.Count == 0) EditorGUILayout.LabelField("  nothing special: separate routes");
        foreach (var f in r.found) EditorGUILayout.LabelField("  • " + f, EditorStyles.wordWrappedLabel);

        // the board, at the chosen step
        EditorGUILayout.Space(6);
        int n = r.replay.placements.Count;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", GUILayout.Width(28))) step = Mathf.Max(0, step - 1);
        step = EditorGUILayout.IntSlider("Step", step, 0, n);
        if (GUILayout.Button("▶", GUILayout.Width(28))) step = Mathf.Min(n, step + 1);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(step == 0 ? "Empty board (faint tiles show the plan)" : $"{step}. {r.steps[step - 1]}", EditorStyles.wordWrappedLabel);
        DrawBoard(r, step);
        EditorGUILayout.LabelField("Red squares: a tile a delivery broke (one of the tiles the game could pick).", EditorStyles.miniLabel);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Solution", EditorStyles.boldLabel);
        for (int k = 0; k < r.steps.Count; k++)
            EditorGUILayout.LabelField($"{k + 1}. {r.steps[k]}", k == step - 1 ? new GUIStyle(EditorStyles.wordWrappedLabel) { fontStyle = FontStyle.Bold } : EditorStyles.wordWrappedLabel);
        if (!r.replay.ok) EditorGUILayout.HelpBox("Replay problem: " + r.replay.problem, MessageType.Warning);

        // the code
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("For Levels.cs", EditorStyles.boldLabel);
        levelNumber = Mathf.Max(1, EditorGUILayout.IntField("Level number", levelNumber));
        EditorGUILayout.LabelField("Idea (comment above it)");
        comment = EditorGUILayout.TextArea(comment, EditorStyles.textArea, GUILayout.MinHeight(36));
        var code = LevelMaker.Code(r, levelNumber, comment);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copy C#", GUILayout.Height(24)))
        {
            EditorGUIUtility.systemCopyBuffer = code;
            ShowNotification(new GUIContent("Copied"));
        }
        EditorGUI.BeginDisabledGroup(writing != null);
        if (GUILayout.Button(new GUIContent($"Write Level{levelNumber}() to Levels.cs", "Also writes its best plans into Solutions.Generated.cs"), GUILayout.Height(24))) WriteLevel(code);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        // or between two levels: the later ones move up one
        EditorGUILayout.BeginHorizontal();
        insertAfter = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Insert after level", "The new level becomes the next one; every later level (and its solution) moves up one"), insertAfter), 1, Levels.All.Count);
        EditorGUI.BeginDisabledGroup(writing != null);
        if (GUILayout.Button($"Insert as Level{insertAfter + 1}()", GUILayout.Height(20))) InsertLevel();
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        codeScroll = EditorGUILayout.BeginScrollView(codeScroll, GUILayout.Height(220));
        EditorGUILayout.TextArea(code, new GUIStyle(EditorStyles.textArea) { wordWrap = true }, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space(10);
    }

    // its solution first (Levels.cs links to it), then the level
    void WriteLevel(string code)
    {
        bool exists = File.ReadAllText(LevelsPath).Contains($"static LevelData Level{levelNumber}()");
        if (exists && !EditorUtility.DisplayDialog($"Replace Level {levelNumber}?",
                $"Levels.cs already has Level{levelNumber}(). Replace it and its comment with this level?", "Replace", "Cancel")) return;
        var level = result.level; int number = levelNumber;
        StartWriting($"Writing Level {number}'s solution...", () =>
        {
            Directory.CreateDirectory(SolutionsDir);
            SolutionExport.Write(SolutionsDir, level, number);
            return LevelMaker.WriteToLevels(LevelsPath, number, code) + $" Wrote its best plans into Solutions.Generated.cs.";
        });
    }

    void InsertLevel()
    {
        int after = insertAfter, count = Levels.All.Count;
        string moves = after < count ? $"Levels {after + 1}-{count} become {after + 2}-{count + 1} (with their solutions; tutorials stay with their levels). " +
            "Analytics rows already collected keep the old numbers." : "It goes at the end.";
        if (!EditorUtility.DisplayDialog($"Insert as Level {after + 1}?", $"The new level goes in after Level {after}. {moves}", "Insert", "Cancel")) return;
        var r = result; var text = comment;
        StartWriting($"Inserting as Level {after + 1} (working out its solution)...", () => LevelMaker.InsertAfter(LevelsPath, SolutionsDir, after, r, text));
    }

    void RebuildSolutions()
    {
        var levels = Levels.All.ToList();
        StartWriting("Rebuilding solutions...", () =>
            SolutionExport.RebuildAll(LevelsPath, SolutionsDir, levels, title => writingWhat = $"Rebuilding solutions: {title}..."));
    }

    void StartWriting(string what, System.Func<string> work)
    {
        Solver2.Abort = false;
        writingWhat = what; status = null;
        writing = Task.Run(work);
    }

    // ---------- board drawing ----------

    void DrawBoard(MakerResult r, int upTo)
    {
        var l = r.level; var plan = r.props.best;
        int cols = l.width + 2, rows = l.height + 2;
        float cs = Mathf.Clamp(Mathf.Floor((position.width - SettingsWidth - 40) / cols), 18, 56);
        var area = GUILayoutUtility.GetRect(cols * cs, rows * cs, GUILayout.ExpandWidth(false));
        if (Event.current.type != EventType.Repaint) return;
        Rect At(Vector2Int c) => new Rect(area.x + (c.x + 1) * cs, area.y + (l.height - c.y) * cs, cs, cs);

        // the board after `upTo` placements
        var placed = new Dictionary<Vector2Int, (TileType t, int step)>();
        var broken = new HashSet<Vector2Int>(); var arrived = new HashSet<int>();
        for (int i = 0; i < upTo && i < r.replay.stepBreaks.Count; i++)
        {
            var (c, t) = r.replay.placements[i];
            placed[c] = (t, i + 1);
            foreach (var x in r.replay.stepBreaks[i]) { placed.Remove(x); broken.Add(x); }
            foreach (var o in r.replay.stepArrivals[i]) arrived.Add(o);
        }
        var order = r.replay.placements.Select((x, i) => (x.cell, i + 1)).ToDictionary(x => x.cell, x => x.Item2);
        var small = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1, 1, 1, 0.7f) }, fontSize = Mathf.Max(8, (int)(cs * 0.22f)) };

        for (int x = 0; x < l.width; x++)
            for (int y = 0; y < l.height; y++)
            {
                var c = new Vector2Int(x, y);
                if (l.BoardOf(c) < 0) continue;
                var cr = At(c);
                EditorGUI.DrawRect(Shrink(cr, 1), CellColor);
                if (l.obstacles.Contains(c)) { EditorGUI.DrawRect(Shrink(cr, cs * 0.12f), ObstacleColor); continue; }
                if (placed.TryGetValue(c, out var pt)) DrawTile(cr, pt.t, TileColor, LineColor);
                else if (broken.Contains(c)) { DrawTile(cr, plan.tiles[c], new Color(0, 0, 0, 0), new Color(1, 1, 1, 0.12f)); EditorGUI.DrawRect(Shrink(cr, 2), BrokenColor); }
                else if (plan.tiles.TryGetValue(c, out var ghost)) DrawTile(cr, ghost, new Color(0, 0, 0, 0), new Color(1, 1, 1, 0.18f));
                if (order.TryGetValue(c, out int num)) GUI.Label(new Rect(cr.x + 2, cr.y, cr.width, cs * 0.4f), num.ToString(), small);
            }

        // board names in the empty corner above each board's top-left square
        if (l.boards.Count > 1)
            for (int bi = 0; bi < l.boards.Count; bi++)
                GUI.Label(At(l.boards[bi].origin + new Vector2Int(-1, l.boards[bi].height)), $"b{bi + 1}", EditorStyles.centeredGreyMiniLabel);

        // doors: a bar on their edge, with the letter just inside the square
        var doorStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = DoorColor }, fontSize = Mathf.Max(9, (int)(cs * 0.28f)) };
        foreach (var d in l.doors)
        {
            var cr = At(d.cell); float t = Mathf.Max(3, cs * 0.14f), len = cs * 0.8f, off = (cs - len) / 2;
            Rect bar = d.side switch
            {
                Dir.N => new Rect(cr.x + off, cr.y - t / 2, len, t),
                Dir.S => new Rect(cr.x + off, cr.yMax - t / 2, len, t),
                Dir.E => new Rect(cr.xMax - t / 2, cr.y + off, t, len),
                _ => new Rect(cr.x - t / 2, cr.y + off, t, len),
            };
            EditorGUI.DrawRect(bar, DoorColor);
            var lab = d.side switch
            {
                Dir.N => new Rect(cr.x + cs * 0.6f, cr.y + 1, cs * 0.4f, cs * 0.35f),
                Dir.S => new Rect(cr.x + cs * 0.6f, cr.yMax - cs * 0.36f, cs * 0.4f, cs * 0.35f),
                Dir.E => new Rect(cr.xMax - cs * 0.38f, cr.y + cs * 0.6f, cs * 0.35f, cs * 0.4f),
                _ => new Rect(cr.x + 1, cr.y + cs * 0.6f, cs * 0.35f, cs * 0.4f),
            };
            GUI.Label(lab, d.label, doorStyle);
        }

        // objects: a dot with an arrow where they start, a ring where they go (filled once delivered)
        var initial = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }, fontSize = Mathf.Max(8, (int)(cs * 0.26f)) };
        for (int i = 0; i < l.objects.Count; i++)
        {
            var o = l.objects[i]; bool done = arrived.Contains(i);
            var sr = Shrink(At(o.start), cs * 0.22f);
            var col = o.color; if (done) col.a = 0.3f;
            var c = sr.center; float w = Mathf.Max(2, cs * 0.08f), a = cs * 0.42f;
            var arrow = o.heading switch
            {
                Dir.N => new Rect(c.x - w / 2, c.y - a, w, a),
                Dir.S => new Rect(c.x - w / 2, c.y, w, a),
                Dir.E => new Rect(c.x, c.y - w / 2, a, w),
                _ => new Rect(c.x - a, c.y - w / 2, a, w),
            };
            EditorGUI.DrawRect(arrow, col);
            GUI.DrawTexture(sr, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, col, 0, sr.width / 2);
            GUI.Label(sr, o.name.Substring(0, 1), initial);
            var dr = Shrink(At(o.dest), cs * 0.2f);
            GUI.DrawTexture(dr, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, o.color, done ? 0 : Mathf.Max(2, cs * 0.09f), dr.width / 2);
            var ringLetter = new GUIStyle(initial) { normal = { textColor = done ? Color.white : o.color } };
            GUI.Label(dr, o.name.Substring(0, 1), ringLetter);
        }
    }

    static Rect Shrink(Rect r, float by) => new Rect(r.x + by, r.y + by, r.width - 2 * by, r.height - 2 * by);

    // a tile: its connections as bars from the middle to the sides; the bridge's north-south bar passes over the east-west one
    static void DrawTile(Rect cr, TileType t, Color fill, Color line)
    {
        if (fill.a > 0) EditorGUI.DrawRect(Shrink(cr, 2), fill);
        float w = Mathf.Max(2, cr.width * 0.16f); var c = cr.center;
        Rect Bar(Dir d) => d switch
        {
            Dir.N => new Rect(c.x - w / 2, cr.y + 2, w, c.y - cr.y - 2 + w / 2),
            Dir.S => new Rect(c.x - w / 2, c.y - w / 2, w, cr.yMax - 2 - c.y + w / 2),
            Dir.E => new Rect(c.x - w / 2, c.y - w / 2, cr.xMax - 2 - c.x + w / 2, w),
            _ => new Rect(cr.x + 2, c.y - w / 2, c.x - cr.x - 2 + w / 2, w),
        };
        if (t == TileType.Bridge)
        {
            EditorGUI.DrawRect(Bar(Dir.E), line); EditorGUI.DrawRect(Bar(Dir.W), line);
            var over = new Rect(c.x - w * 1.1f, cr.y + 2, w * 2.2f, cr.height - 4);
            EditorGUI.DrawRect(over, fill.a > 0 ? fill : new Color(0.25f, 0.27f, 0.32f));
            EditorGUI.DrawRect(Bar(Dir.N), line); EditorGUI.DrawRect(Bar(Dir.S), line);
            return;
        }
        foreach (var d in DirUtil.All) if (Tiles.Has(t, d)) EditorGUI.DrawRect(Bar(d), line);
    }
}
