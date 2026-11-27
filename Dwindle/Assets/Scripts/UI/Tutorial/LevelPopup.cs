using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the pop-ups before a colour group's first level (LevelData.popup): a title, a line or two, 1x1 demos drawn with the
// game's own tiles (a dot runs each tile's road), and a Continue icon. GameManager.StartGuide shows them
public class LevelPopup : MonoBehaviour
{
    // one demo: squares in a row (gap = a separate board), each with a tile and door bars; dots run paths over them
    class Demo
    {
        public string caption;
        public readonly List<(TileType? tile, Dir[] doors)> squares = new();
        public bool apart;                       // squares drawn as separate boards (a gap and a frame each)
        public TileType[] runTiles;              // the first square's tile for each run (a tile turning through its rotations)
        public TileView view;                    // the first square's tile view, to turn it
        public Color tileColor; public int shownRun = -1;
        public readonly List<Vector2[][]> runs = new(); // each run: one or more dot paths shown together; runs take turns
        public Dir[] choices; public Dir chosen; // arrows shown at the middle (a + or T), and the one picked
        public List<(Dir d, RectTransform rt, int run)> arrows = new(); // run: the turn it belongs to (-1: every run)
        public Image cellBg; public Color cellColor, highlight; // the square, highlighted while the arrows show
        public float captionY;
        public RectTransform captionRt; // placed by the pop-up's layout (Layouts)
        public bool oneBoard;                    // the squares as one board: one frame, empty squares shown
        public readonly List<(Vector2 at, bool across, string letter)> doorMarks = new(); // doors as in a level (bar + lettered badge), on edges, in square units
        public readonly List<(string letter, Image bar, Image badge)> doorViews = new();
        public Color doorColor, doorLit;
        public int jumpAt = -1;                  // the path segment (point i to i+1) crossed through a door: hidden a moment, no travel
        public int stopAt = 1;                   // the path point where it stops for the arrows
        public Vector2 choiceAt;                 // where the arrows centre, in square units ((0,0) = the first square's middle)
        public int gridW, gridH;                 // a square board (gridH > 1): its size; squares by (x, y) in gridTiles, (0,0) bottom left
        public readonly Dictionary<Vector2Int, TileType> gridTiles = new();
        public List<Turn> turns;                 // runs by different objects: each run's own settings, object and ring in its colour
        public int curRun = -1;
    }

    // one run of a demo with turns: its door crossing, its stop with arrows, and the door pair it uses (lit meanwhile)
    class Turn
    {
        public int jumpAt = -1, stopAt = 1;
        public Vector2 choiceAt;
        public Dir[] choices; public Dir chosen;
        public string door;
    }

    const float Cell = 90f, Gap = 46f, Run = 1.6f, Pause = 0.5f;
    readonly List<(Demo d, RectTransform root, List<ObjectView> objs, List<List<DestinationView>> rings)> demos = new();
    Action onClose;
    float started;

    public static LevelPopup Show(Canvas canvas, string key, TMP_FontAsset font, BoardView board, Action onClose)
    {
        var (title, body, list) = Content(key);
        var p = UIPalette.Current;
        var root = new GameObject("LevelPopup " + key, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)root.transform;
        rt.SetParent(canvas.rootCanvas.transform, false);
        UIBuilder.Fill(rt);
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f); // takes every click while it's up
        rt.SetAsLastSibling();

        bool textOnly = list.Count == 0;
        float panelW = textOnly ? 680f : 760f; float textW = textOnly ? 600f : 700f; // text only: a narrower card, the icon in its own row under the text
        int rowsTall = 1; foreach (var dm in list) rowsTall = Mathf.Max(rowsTall, dm.gridH);
        float demoH = textOnly ? 0f : Cell * (rowsTall + 1.7f) + 70f; // room for the start and ring outside the square
        var panelImg = Box("Panel", rt, Vector2.zero, new Vector2(panelW, 100f), p.panel); TutorialOverlay.Round(panelImg);
        var panel = panelImg.rectTransform;
        var titleText = Text("Title", panel, Vector2.zero, new Vector2(700f, 44f), $"<b>{title}</b>", 32, font, p.text);
        var bodyText = Text("Body", panel, Vector2.zero, new Vector2(textW, 76f), body, 23, font, p.text);
        if (textOnly) bodyText.paragraphSpacing = 10f; // a little air between the lines
        float textH = bodyText.GetPreferredValues(body, textW, 0f).y + 4f; // as tall as its lines
        float bodyH = textOnly ? textH : Mathf.Max(76f, textH);
        float blockH = 44f + 18f + bodyH; // text only: the title and the text together, centred in the card
        float h = textOnly ? blockH + 160f : 164f + bodyH + demoH; // text only: just under the text; demos: room under them for the icon
        panel.sizeDelta = new Vector2(panelW, h);
        titleText.rectTransform.anchoredPosition = new Vector2(0f, textOnly ? blockH / 2 - 22f : h / 2 - 46f);
        bodyText.rectTransform.anchoredPosition = new Vector2(0f, textOnly ? blockH / 2 - 62f - bodyH / 2 : h / 2 - 70f - bodyH / 2);
        bodyText.rectTransform.sizeDelta = new Vector2(textW, bodyH);

        var popup = root.AddComponent<LevelPopup>();
        popup.onClose = onClose;
        // every caption at one height, above: right over the squares, or over the highest start or ring if a road leaves a square's top
        bool anyTop = false;
        foreach (var dm in list) foreach (var run in dm.runs) foreach (var path in run) foreach (var pt in path) if (pt.y > 0.4f) anyTop = true;
        foreach (var dm in list) dm.captionY = anyTop ? Cell * 1.32f + 8f : Cell / 2f + 24f;
        if (list.Count > 0)
        {
            float span = 700f / list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                var g = Group("Demo" + i, panel, new Vector2(-350f + span * (i + 0.5f), h / 2 - 165f - (bodyH - 76f) - demoH / 2 - 10f), new Vector2(span, Cell));
                popup.Build(list[i], g, font, board);
            }
        }

        // Continue: the next icon
        var button = Box("Continue", panel, new Vector2(panelW / 2 - 56f, -h / 2 + 40f), new Vector2(64f, 52f), p.accent);
        TutorialOverlay.Round(button);
        button.raycastTarget = true;
        var b = button.gameObject.AddComponent<Button>();
        b.targetGraphic = button;
        var icon = Box("Icon", button.rectTransform, Vector2.zero, new Vector2(34f, 34f), Color.white);
        var tex = Resources.Load<Texture2D>("UIIcons/nextpage");
        if (tex != null) icon.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        icon.preserveAspect = true;
        icon.color = Color.white;
        b.onClick.AddListener(popup.Close);
        popup.started = Time.unscaledTime;

        // each pop-up's layout, set by hand: the card, title, text, the demos (middle, spacing), their captions, the icon
        if (Layouts.TryGetValue(key, out var lay))
        {
            panel.sizeDelta = lay.panel;
            titleText.rectTransform.anchoredPosition = lay.title;
            bodyText.rectTransform.anchoredPosition = lay.body;
            button.rectTransform.anchoredPosition = lay.button;
            var groups = new List<RectTransform>();
            foreach (Transform c in panel) if (c.name.StartsWith("Demo")) groups.Add((RectTransform)c);
            for (int i = 0; i < groups.Count; i++) groups[i].anchoredPosition = lay.demos + new Vector2((i - (groups.Count - 1) / 2f) * lay.spacing, 0f);
            foreach (var dm in list) if (dm.captionRt != null) dm.captionRt.anchoredPosition = lay.caption;
        }
        return popup;
    }

    // the layouts set by hand in Play mode (2026-11-26): card size, title and text places, the demos' middle and the gap
    // between them, each caption's place from its demo's middle (under the board), the Continue icon
    static readonly Dictionary<string, (Vector2 panel, Vector2 title, Vector2 body, Vector2 demos, float spacing, Vector2 caption, Vector2 button)> Layouts = new()
    {
        { "tiles",     (new Vector2(700f, 450f), new Vector2(0f, 170f), new Vector2(0f, 120f), new Vector2(0f, -50f),  300f, new Vector2(0f, -70f), new Vector2(300f, -180f)) }, // Level 1
        { "junctions", (new Vector2(700f, 450f), new Vector2(0f, 170f), new Vector2(0f, 120f), new Vector2(0f, -55f),  200f, new Vector2(0f, -70f), new Vector2(300f, -180f)) }, // Level 3
        { "realplay",  (new Vector2(600f, 350f), new Vector2(0f, 100f), new Vector2(0f, -30f), Vector2.zero,            0f,   Vector2.zero,          new Vector2(250f, -135f)) }, // Level 5
        { "groups",    (new Vector2(600f, 350f), new Vector2(0f, 70f),  new Vector2(0f, -30f), Vector2.zero,            0f,   Vector2.zero,          new Vector2(250f, -135f)) }, // Level 9
        { "doors",     (new Vector2(700f, 600f), new Vector2(0f, 250f), new Vector2(0f, 150f), new Vector2(0f, -120f), 0f,   Vector2.zero,          new Vector2(300f, -258f)) }, // Level 18
    };

    void Close()
    {
        var done = onClose;
        Destroy(gameObject);
        done?.Invoke();
    }

    // each pop-up's words and demos. paths are in square units: (0,0) the first square's centre, 0.5 its edge
    static (string title, string body, List<Demo> demos) Content(string key)
    {
        var list = new List<Demo>();
        Demo One(string caption, TileType t, params Vector2[][] runs)
        {
            var d = new Demo { caption = caption };
            d.squares.Add((t, null));
            foreach (var r in runs) d.runs.Add(new[] { r });
            list.Add(d);
            return d;
        }
        Vector2 V(float x, float y) => new Vector2(x, y);
        switch (key)
        {
            case "tiles":
            {
                One("I goes straight", TileType.StraightH, new[] { V(-0.5f, 0), V(0.5f, 0) }); // lying down: in from the left, out the right
                One("L turns", TileType.CornerWN, new[] { V(-0.5f, 0), V(0, 0), V(0, 0.5f) });
                return ("Tiles", "Place tiles to make the road.", list);
            }
            case "junctions":
                var plus = One("+", TileType.Cross, new[] { V(-0.5f, 0), V(0, 0), V(0, 0.5f) }); // in from the left, picks up
                plus.choices = new[] { Dir.N, Dir.E, Dir.S }; plus.chosen = Dir.N;
                var tee = One("T", TileType.TeeNES, new[] { V(0, -0.5f), V(0, 0), V(0, 0.5f) }); // in from the bottom, picks up (ring on top)
                tee.choices = new[] { Dir.N, Dir.E }; tee.chosen = Dir.N;
                var br = new Demo { caption = "Bridge" };
                br.squares.Add((TileType.Bridge, null));
                br.runs.Add(new[] { new[] { V(-0.5f, 0), V(0.5f, 0) }, new[] { V(0, -0.5f), V(0, 0.5f) } });
                list.Add(br);
                return ("Junctions", "+ and T: pick a way.\nBridge: both go straight.", list);
            case "realplay":
                return ("Now it's real play", "From here on, you plan the roads yourself.\nTry to deliver everyone with as few tiles as you can.\nEach combo earns a +.\nUse it, or keep it for +75 at the end.\nIf you get stuck, press Hint.", list);
            case "groups":
                return ("More than three objects", "Now there are more objects to deliver.\nYou choose which ones travel together,\nand which group arrives first.", list);
            case "doors":
            {
                // one 3x3 board, two door pairs, as in a level; Red then Blue take turns, each pair lit in its turn.
                // A (Red): both on the board's edge, one way out. B (Blue): the second B between two squares, arrows to pick a way
                var d = new Demo { gridW = 3, gridH = 3, turns = new List<Turn>() };
                d.gridTiles[new Vector2Int(0, 2)] = TileType.CornerWN;
                d.gridTiles[new Vector2Int(2, 0)] = TileType.CornerES;
                d.gridTiles[new Vector2Int(0, 0)] = TileType.StraightH;
                d.gridTiles[new Vector2Int(1, 2)] = TileType.StraightV;
                d.doorMarks.Add((V(0, 2.5f), true, "A"));
                d.doorMarks.Add((V(2, -0.5f), true, "A"));
                d.doorMarks.Add((V(0.5f, 0), false, "B"));
                d.doorMarks.Add((V(1, 1.5f), true, "B"));
                d.runs.Add(new[] { new[] { V(-0.5f, 2), V(0, 2), V(0, 2.5f), V(2, -0.5f), V(2, 0), V(2.5f, 0) } }); // Red: up into A, out of the bottom A, right
                d.turns.Add(new Turn { jumpAt = 2, door = "A" });
                d.runs.Add(new[] { new[] { V(-0.5f, 0), V(0, 0), V(0.5f, 0), V(1, 1.5f), V(1, 2), V(1, 2.5f) } }); // Blue: into B, out of the middle B, picks up
                d.turns.Add(new Turn { jumpAt = 2, stopAt = 3, choiceAt = V(1, 1.5f), choices = new[] { Dir.N, Dir.S }, chosen = Dir.N, door = "B" });
                list.Add(d);
                return ("Doors", "Go in a door, and come out of the one with the same letter.\nOn the board's edge, there's one way out.\nBetween two squares, pick a way.\n+50 for going through.", list);
            }
            case "teleport":
                return ("Doors across boards", "Doors with the same letter are linked, even on different boards:\ngo in one, and you come out of the other.\nNot every door helps. Trace the roads first, or try it and undo.", list);
        }
        return (key, "", list);
    }

    // the squares (the game's cell colour and tile), door bars, the caption; the game's own objects and rings, sized and
    // placed as on the board (objectSize, ringSize, endDistance), each run's rings at its paths' ends
    static readonly Color[] RunColors = { new(0.92f, 0.30f, 0.30f), new(0.30f, 0.50f, 1.00f) }; // Red, Blue (Levels.cs)
    void Build(Demo d, RectTransform g, TMP_FontAsset font, BoardView board)
    {
        Image firstCell = null;
        float step = Cell + (d.apart ? Gap : 0f);
        var root = Group("Squares", g, Vector2.zero, new Vector2(step * d.squares.Count, Cell));
        if (d.oneBoard) Box("Board", root, Vector2.zero, new Vector2(Cell * d.squares.Count + 14f, Cell + 14f), board != null ? board.boardFrameColor : Color.black);
        if (d.gridH > 1) // a square board: one frame, every square, the tiles placed on it
        {
            Box("Board", root, Vector2.zero, new Vector2(Cell * d.gridW + 14f, Cell * d.gridH + 14f), board != null ? board.boardFrameColor : Color.black);
            for (int x = 0; x < d.gridW; x++)
                for (int y = 0; y < d.gridH; y++)
                {
                    var cell = Box("Cell", root, Pos(d, new Vector2(x, y)), Vector2.one * (Cell - 4f), board != null ? board.cellColor : Color.gray);
                    if (d.gridTiles.TryGetValue(new Vector2Int(x, y), out var gt) && board != null && board.tilePrefab != null)
                    {
                        var view = Instantiate(board.tilePrefab, cell.rectTransform);
                        var vrt = (RectTransform)view.transform;
                        vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = vrt.offsetMax = Vector2.zero;
                        view.Show(gt, board.tileColor);
                    }
                }
        }
        for (int i = 0; i < d.squares.Count; i++)
        {
            var (tile, doors) = d.squares[i];
            var at = SquarePos(d, i);
            if (tile == null && !d.oneBoard) continue; // the gap between door ends (or between boards)
            if (!d.oneBoard) Box("Board", root, at, Vector2.one * (Cell + 14f), board != null ? board.boardFrameColor : Color.black);
            var cell = Box("Cell", root, at, Vector2.one * (Cell - 4f), board != null ? board.cellColor : Color.gray);
            if (i == 0) firstCell = cell;
            if (tile != null && board != null && board.tilePrefab != null)
            {
                var view = Instantiate(board.tilePrefab, cell.rectTransform);
                var vrt = (RectTransform)view.transform;
                vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = vrt.offsetMax = Vector2.zero;
                view.Show(tile.Value, board.tileColor);
                if (i == 0) { d.view = view; d.tileColor = board.tileColor; }
            }
            if (doors != null)
                foreach (var side in doors)
                {
                    var o = DirUtil.Offset(side);
                    bool across = side == Dir.N || side == Dir.S;
                    var bar = Box("Door", root, at + new Vector2(o.x, o.y) * (Cell / 2f - 4f), across ? new Vector2(Cell - 20f, 8f) : new Vector2(8f, Cell - 20f), board != null ? board.doorColor : Color.white);
                    Text("Letter", bar.rectTransform, Vector2.zero, new Vector2(30f, 30f), "<b>A</b>", 18, font, board != null ? board.doorLabelColor : Color.black);
                }
        }
        d.doorColor = board != null ? board.doorColor : Color.gray;
        d.doorLit = board != null ? board.doorHoverColor : Color.yellow;
        foreach (var (pt, across, letter) in d.doorMarks) { var (bar, badge) = DoorMark(root, Pos(d, pt), across, letter, font, board); d.doorViews.Add((letter, bar, badge)); }
        if (!string.IsNullOrEmpty(d.caption))
        {
            d.captionRt = Text("Caption", g, new Vector2(0f, d.captionY), new Vector2(g.sizeDelta.x, 30f), d.caption, 20, font, UIPalette.Current.mutedText).rectTransform;
        }

        float endOut = board != null ? board.endDistance : 0.5f;
        // each path: its ends pushed out past the square's edge, where the board puts starts and rings
        foreach (var run in d.runs)
            for (int j = 0; j < run.Length; j++) run[j] = Extend(run[j], endOut);
        var objs = new List<ObjectView>();
        var rings = new List<List<DestinationView>>();
        int most = 0;
        foreach (var r in d.runs) most = Mathf.Max(most, r.Length);
        if (d.turns != null) most = d.runs.Count; // an object per run, in its own colour
        for (int ri = 0; ri < d.runs.Count; ri++)
        {
            var run = d.runs[ri];
            var list = new List<DestinationView>();
            for (int j = 0; j < run.Length; j++)
            {
                if (board == null || board.destinationPrefab == null) continue;
                var ring = Instantiate(board.destinationPrefab, root);
                ring.Setup(RunColors[(d.turns != null ? ri : j) % RunColors.Length], Cell * board.ringSize);
                var rrt = (RectTransform)ring.transform;
                rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
                rrt.anchoredPosition = Pos(d, run[j][run[j].Length - 1]);
                list.Add(ring);
            }
            rings.Add(list);
        }
        for (int k = 0; k < most; k++)
        {
            if (board == null || board.objectPrefab == null) break;
            var o = Instantiate(board.objectPrefab, root);
            o.Setup(RunColors[k % RunColors.Length], Cell * board.objectSize);
            var ort = o.Rect;
            ort.anchorMin = ort.anchorMax = new Vector2(0.5f, 0.5f);
            objs.Add(o);
        }
        // the arrows a + or T shows when the object reaches its middle: the board's own arrow buttons, sized, placed and
        // coloured as in a level (a quarter of a square, 0.39 out, in the object's colour), the square highlighted meanwhile
        void Arrows(Dir[] choices, Vector2 center, int run)
        {
            if (choices == null || board == null || board.arrowButtonPrefab == null) return;
            foreach (var dir in choices)
            {
                var o = DirUtil.Offset(dir);
                var button = Instantiate(board.arrowButtonPrefab, root);
                button.enabled = false; // a picture only: nothing to click in the pop-up
                var rt = (RectTransform)button.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = Vector2.one * Cell * 0.25f;
                rt.anchoredPosition = Pos(d, center) + new Vector2(o.x, o.y) * Cell * 0.39f;
                rt.localEulerAngles = new Vector3(0, 0, DirUtil.Angle(dir)); // the prefab's arrow points up
                var arrowGraphic = button.GetComponent<Graphic>();
                arrowGraphic.color = RunColors[Mathf.Max(0, run) % RunColors.Length];
                foreach (var gr in button.GetComponentsInChildren<Graphic>(true)) gr.raycastTarget = false;
                button.gameObject.SetActive(false);
                d.arrows.Add((dir, rt, run));
            }
        }
        Arrows(d.choices, d.choiceAt, -1);
        if (d.turns != null) for (int ri = 0; ri < d.turns.Count; ri++) Arrows(d.turns[ri].choices, d.turns[ri].choiceAt, ri);
        if (d.choices != null && board != null && d.doorMarks.Count == 0) { d.cellBg = firstCell; d.cellColor = board.cellColor; d.highlight = board.crossHighlightColor; }
        demos.Add((d, root, objs, rings));
    }

    static Vector2[] Extend(Vector2[] path, float by)
    {
        var p = (Vector2[])path.Clone();
        if (p.Length < 2) return p;
        p[0] -= (p[1] - p[0]).normalized * by;
        int n = p.Length - 1;
        p[n] += (p[n] - p[n - 1]).normalized * by;
        return p;
    }

    static Vector2 SquarePos(Demo d, int i)
    {
        float step = Cell + (d.apart ? Gap : 0f);
        return new Vector2((i - (d.squares.Count - 1) / 2f) * step, 0f);
    }

    // a path point in square units to the demo's position
    static Vector2 Pos(Demo d, Vector2 c)
    {
        if (d.gridH > 1) return new Vector2((c.x - (d.gridW - 1) / 2f) * Cell, (c.y - (d.gridH - 1) / 2f) * Cell); // a square board, centred
        int i = Mathf.Clamp(Mathf.RoundToInt(c.x), 0, d.squares.Count - 1);
        float local = c.x - i;
        return SquarePos(d, i) + new Vector2(local * Cell, c.y * Cell);
    }

    void Update()
    {
        float t = Time.unscaledTime - started;
        foreach (var (d, _, objs, rings) in demos)
        {
            if (d.runs.Count == 0) continue;
            int run = (int)(t / (Run + Pause)) % d.runs.Count;
            float k = Mathf.Clamp01((t % (Run + Pause)) / Run);
            var paths = d.runs[run];
            if (d.runTiles != null && d.view != null && run != d.shownRun && run < d.runTiles.Length) { d.view.Show(d.runTiles[run], d.tileColor); d.shownRun = run; } // the tile turns with each run
            for (int r = 0; r < rings.Count; r++)
                for (int j = 0; j < rings[r].Count; j++)
                {
                    rings[r][j].gameObject.SetActive(r == run);
                    if (r == run) rings[r][j].SetDelivered(k >= 1f);
                }
            if (d.turns != null) // this run's own settings, and its door pair lit
            {
                var tu = d.turns[run];
                d.jumpAt = tu.jumpAt; d.stopAt = tu.stopAt; d.chosen = tu.chosen; d.curRun = run;
                foreach (var (letter, bar, badge) in d.doorViews) bar.color = badge.color = letter == tu.door ? d.doorLit : d.doorColor;
                foreach (var (_, rt, ar) in d.arrows) if (ar != run) rt.gameObject.SetActive(false);
            }
            bool stops = d.turns != null ? d.turns[run].choices != null : d.choices != null;
            for (int j = 0; j < objs.Count; j++)
            {
                int path = d.turns != null ? (j == run ? 0 : -1) : j; // turns: only this run's object, on its path
                if (path < 0 || path >= paths.Length) { objs[j].gameObject.SetActive(false); continue; }
                var (at, dir, hidden) = stops ? AlongWithStop(d, paths[path], k) : Along(d, paths[path], k); // a + or T (or a door): it stops for the arrows
                objs[j].Rect.anchoredPosition = Pos(d, at);
                objs[j].gameObject.SetActive(!hidden && k < 1f);
                objs[j].Face(dir);
            }
        }
    }

    // to the middle (40%), a stop with the arrows (30%: the picked one lights up halfway), then on (30%)
    (Vector2, Dir, bool) AlongWithStop(Demo d, Vector2[] path, float k)
    {
        float total = 0f; for (int i = 1; i < path.Length; i++) total += Seg(d, path, i);
        float toMiddle = 0f; for (int i = 1; i <= Mathf.Min(d.stopAt, path.Length - 1); i++) toMiddle += Seg(d, path, i); // to the stop (a + or T's middle, or a door)
        bool stopped = k >= 0.4f && k < 0.7f;
        foreach (var (dir, rt, ar) in d.arrows)
        {
            if (ar >= 0 && ar != d.curRun) continue; // another run's arrows
            rt.gameObject.SetActive(stopped);
            rt.localScale = Vector3.one * (dir == d.chosen && k >= 0.55f ? 1.25f : 1f); // the picked one, as if pressed
        }
        if (d.cellBg != null) d.cellBg.color = stopped ? d.highlight : d.cellColor;
        float go = k < 0.4f ? k / 0.4f * toMiddle : k < 0.7f ? toMiddle : toMiddle + (k - 0.7f) / 0.3f * (total - toMiddle);
        return Along(d, path, total > 0f ? go / total : 1f);
    }

    const float JumpLen = 0.35f; // the time a door crossing takes, as a length of road (hidden meanwhile)
    static float Seg(Demo d, Vector2[] path, int i) => i - 1 == d.jumpAt ? JumpLen : Vector2.Distance(path[i - 1], path[i]);

    // where along its path, facing which way, and whether it's inside a door (between its two ends)
    static (Vector2 at, Dir facing, bool hidden) Along(Demo d, Vector2[] path, float k)
    {
        float total = 0f;
        for (int i = 1; i < path.Length; i++) total += Seg(d, path, i);
        float go = k * total;
        Dir facing = Dir.E;
        for (int i = 1; i < path.Length; i++)
        {
            float seg = Seg(d, path, i);
            if (i - 1 == d.jumpAt) { if (go <= seg) return (path[i], facing, true); go -= seg; continue; }
            var v = path[i] - path[i - 1];
            facing = Mathf.Abs(v.x) > Mathf.Abs(v.y) ? (v.x > 0 ? Dir.E : Dir.W) : (v.y > 0 ? Dir.N : Dir.S);
            if (go <= seg) return (Vector2.Lerp(path[i - 1], path[i], seg > 0f ? go / seg : 1f), facing, false);
            go -= seg;
        }
        return (path[path.Length - 1], facing, false);
    }

    // a door as the board draws it (BoardView.MakeDoor): a bar along the edge, 0.7 of a square, and a badge with its letter
    static (Image bar, Image badge) DoorMark(RectTransform parent, Vector2 at, bool across, string letter, TMP_FontAsset font, BoardView board)
    {
        var color = board != null ? board.doorColor : Color.gray;
        float length = Cell * 0.7f, thickness = Mathf.Max(6f, Cell * 0.1f);
        var bar = Box("Door", parent, at, across ? new Vector2(length, thickness) : new Vector2(thickness, length), color);
        var badge = Box("Badge", bar.rectTransform, Vector2.zero, Vector2.one * Cell * 0.3f, color);
        var t = Text("Letter", badge.rectTransform, Vector2.zero, Vector2.one * Cell * 0.3f, $"<b>{letter}</b>", Cell * 0.22f, font, board != null ? board.doorLabelColor : Color.black);
        t.raycastTarget = false;
        return (bar, badge);
    }

    static Sprite circle;
    static Sprite CircleSprite()
    {
        if (circle != null) return circle;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x - n / 2f + 0.5f, dy = y - n / 2f + 0.5f;
                float a = Mathf.Clamp01(n / 2f - Mathf.Sqrt(dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        return circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    static RectTransform Group(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static Image Box(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var img = UIBuilder.NewImage(name, parent, color);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        img.raycastTarget = false;
        return img;
    }

    static TMP_Text Text(string name, Transform parent, Vector2 pos, Vector2 size, string text, float fontSize, TMP_FontAsset font, Color color)
    {
        var t = UIBuilder.NewText(name, parent, text, fontSize, color, font);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        t.alignment = TextAlignmentOptions.Center;
        return t;
    }
}
