using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// draws the boards and animates what happens on them. hints in BoardView.Hints.cs, dragging in BoardView.Drag.cs
public partial class BoardView : MonoBehaviour
{
    [Header("Prefabs")]
    public TileView tilePrefab;
    public CellView cellPrefab;
    public ObjectView objectPrefab;
    public DestinationView destinationPrefab;
    public Button arrowButtonPrefab;

    [Header("Layers")]
    public RectTransform cellsRoot;
    public RectTransform markersRoot;
    public RectTransform objectsRoot;
    public RectTransform choicesRoot;

    [Header("Board")]
    [Tooltip("A single board is at most this tall, with its rings. Two or more boards fill the free space instead")]
    public float boardSize = 700f;
    [Tooltip("A single board is at most this wide, with its rings (wider than Board Size, so wide boards aren't small)")]
    public float boardMaxWidth = 820f;
    [Tooltip("The space kept between the boards' frames and the screen's left and right sides")]
    public float boardSideMargin = 80f;
    [Tooltip("The space kept between the boards' frames and the title and score above")]
    public float boardHudMargin = 24f;
    [Tooltip("The boards' lowest frame sits exactly this far above the dock, on every level")]
    public float boardDockGap = 40f;
    [Tooltip("The space kept between the boards' frames and the top of the screen")]
    public float boardTopMargin = 60f;
    [Tooltip("With several boards: the space between two boards' frames")]
    public float boardGap = 40f;
    // set by GameManager: the top bar's parts (the boards stay below them) and the dock (they stay above it)
    [HideInInspector] public RectTransform[] keepBelow, keepAbove;
    [Range(0.2f, 1f)] public float objectSize = 0.55f; // an object's circle, as a fraction of a cell
    [Range(0.2f, 1f)] public float ringSize = 0.62f;   // a ring, as a fraction of a cell
    [Tooltip("How far an object's start and its ring sit from the board's edge, in squares (0.5 = the middle of the " +
             "square next to the board). Tutorial areas around starts and rings are measured for 0.5")]
    [Range(0.3f, 1f)] public float endDistance = 0.5f; // above about 0.65 they start to stick out of the dark board frame
    public float moveSecondsPerCell = 0.2f;
    public float teleportSeconds = 0.25f; // shrinking into one door then growing out of the other

    [Header("Colors")]
    public Color cellColor = new Color(0.86f, 0.87f, 0.89f);
    public Color obstacleColor = new Color(0.22f, 0.22f, 0.25f);
    public Color tileColor = new Color(0.28f, 0.31f, 0.38f);
    public Color crossHighlightColor = new Color(0.98f, 0.88f, 0.55f);
    public Color lockedCellColor = new Color(0.62f, 0.64f, 0.69f); // a tile an object has gone through: it can no longer be returned
    [Range(0f, 1f)] public float ghostFade = 0.55f; // how far the hover preview fades toward the cell color
    public Color boardFrameColor = new Color(0.19f, 0.20f, 0.24f); // behind each board, so boards read as separate
    [Tooltip("How far the dark plate reaches past the start and ring squares, as a fraction of a square. In Play mode, changes show right away")]
    [Range(0f, 1f)] public float framePadding = 0.45f;
    public Color doorColor = new Color(0.62f, 0.64f, 0.68f); // every door, so none looks tied to one object
    public Color doorLabelColor = new Color(0.08f, 0.08f, 0.10f);
    public Color doorHoverColor = new Color(0.98f, 0.82f, 0.35f); // both ends of a door pair while the mouse is over one
    public Color flashColor = new Color(1f, 0.45f, 0.45f);
    public float flashHoldSeconds = 0.4f;
    public float flashFadeSeconds = 1.2f;

    const float CellGap = 4f; // px between neighboring squares (each is drawn this much smaller than the grid)
    public Cell[,] Cells { get; private set; } // the shared grid, null outside every board
    public float CellSize { get; private set; }

    LevelData level;
    Dir? pickedDirection;

    // for the tutorial (GameManager.LockInput): while locked, a click on a junction's or a door's arrow is ignored
    bool inputLocked;
    public void LockInput(bool locked) => inputLocked = locked;
    Action<DoorSpec> onDoorClicked;

    // builds the boards, the doors, the destination rings and the objects, and hands back the objects.
    public List<MovingObject> Build(LevelData levelData, Action<Vector2Int> onCellClicked,
                                    Action<DoorSpec> onDoorClicked = null, Action<Vector2Int> onCellRightClicked = null,
                                    Func<TileType?> previewTile = null)
    {
        level = levelData;
        this.onDoorClicked = onDoorClicked;
        this.previewTile = previewTile;
        ghost = null;
        EndDrag(); // a drag cut short by a new level
        picked = null;
        FinishUndo(); // an undo rewind cut short by a new level
        undoing = false;
        StopRecording();
        StopAllCoroutines(); // a Flash left over from the last level would touch a destroyed cell
        UIBuilder.Clear(cellsRoot);
        frames.Clear();
        UIBuilder.Clear(markersRoot);
        UIBuilder.Clear(objectsRoot);
        UIBuilder.Clear(choicesRoot);

        // leave room for one ring of cells around each board, where objects and destinations sit
        int cols = level.width + 2, rows = level.height + 2;
        var free = FreeSpace();
        var board = cellsRoot.parent;
        LayOutGrid(free); // again: FreeSpace tried several
        // one board: at most Board Size tall and Board Max Width wide, rings included (so a wide board grows to about a
        // square one's height). several: as big as the free space allows
        if (level.boards.Count == 1) CellSize = Mathf.Min(CellSize, Mathf.Floor(Mathf.Min(boardMaxWidth / cols, boardSize / rows)));
        SetLines();
        // the lowest frame Board Dock Gap above the dock, on every level; centered across; the space left goes above
        board.localPosition = new Vector2(free.center.x, free.yMin + LayoutSize().y / 2f);

        foreach (var b in level.boards) MakeFrame(b);

        Cells = new Cell[level.width, level.height];
        foreach (var b in level.boards)
            for (int x = 0; x < b.width; x++)
            for (int y = 0; y < b.height; y++)
            {
                var p = b.origin + new Vector2Int(x, y);
                var view = Instantiate(cellPrefab, cellsRoot);
                var rt = (RectTransform)view.transform;
                rt.anchoredPosition = CellPos(p);
                rt.sizeDelta = Vector2.one * (CellSize - CellGap); // a thin gap between cells
                view.background.color = cellColor;
                view.Init(p, onCellClicked, onCellRightClicked, ShowGhost, HideGhost);
                view.board = this; // dragging a tile off the board (BoardView.Drag.cs)
                // no hover or click tint on the squares: the faded preview of the selected tile is the only hover
                var colors = view.button.colors;
                colors.highlightedColor = colors.normalColor;
                colors.selectedColor = colors.normalColor;
                colors.pressedColor = colors.normalColor;
                view.button.colors = colors;
                Cells[p.x, p.y] = new Cell { view = view };
            }

        foreach (var o in level.obstacles)
        {
            Cells[o.x, o.y].obstacle = true;
            Cells[o.x, o.y].view.background.color = obstacleColor;
        }

        doorHighlights.Clear();
        MakeDoorHighlightRoot();
        foreach (var door in level.doors) MakeDoor(door);

        // dotted lines from each start into the board and from the board out to each ring, drawn first so the
        // objects and rings sit on top of them
        startLines.Clear();
        ringLines.Clear();
        if (showEndLines)
            foreach (var spec in level.objects)
            {
                startLines[spec] = DottedLine(OutsidePos(spec.start, spec.heading), EdgePos(spec.start, spec.heading), spec.color, start: true);
                foreach (var d in DirUtil.All) // the ring's side that faces the board
                    if (InGrid(spec.dest + DirUtil.Offset(d))) ringLines[spec] = DottedLine(OutsidePos(spec.dest), EdgePos(spec.dest, d), spec.color, start: false);
            }

        var objects = new List<MovingObject>();
        foreach (var spec in level.objects)
        {
            var dest = Instantiate(destinationPrefab, markersRoot);
            dest.Setup(spec.color, CellSize * ringSize);
            ((RectTransform)dest.transform).anchoredPosition = OutsidePos(spec.dest);

            var view = Instantiate(objectPrefab, objectsRoot);
            view.Setup(spec.color, CellSize * objectSize);
            view.Face(spec.heading);

            // objects and rings don't take the mouse, so a door under one can still be hovered and clicked
            foreach (var g in view.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            foreach (var g in dest.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

            objects.Add(new MovingObject { spec = spec, pos = spec.start, heading = spec.heading, view = view, dest = dest });
        }
        LayoutWaitingObjects(objects);
        return objects;
    }

    // the space the boards can use, in Board Area's own coordinates: between the screen's sides (less Board Side
    // Margin), Board Dock Gap above the dock, and as high as it can go without the boards coming within Board HUD
    // Margin of the title or the score. those count as reaching their screen corner (the "<" sits left of the title)
    Rect FreeSpace()
    {
        var area = cellsRoot.parent.parent;
        Canvas.ForceUpdateCanvases(); // the HUD laid out for this level first
        var screen = Bounds((RectTransform)cellsRoot.GetComponentInParent<Canvas>().rootCanvas.transform, area);
        float bottom = screen.yMin;
        foreach (var t in keepAbove ?? Array.Empty<RectTransform>())
            if (t != null && t.gameObject.activeInHierarchy) bottom = Mathf.Max(bottom, Bounds(t, area).yMax);
        var corners = new List<Rect>();
        foreach (var t in keepBelow ?? Array.Empty<RectTransform>())
        {
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            var r = Bounds(t, area);
            if (r.center.x < screen.center.x) r.xMin = screen.xMin; else r.xMax = screen.xMax;
            corners.Add(Rect.MinMaxRect(r.xMin - boardHudMargin, r.yMin - boardHudMargin, r.xMax + boardHudMargin, r.yMax + boardHudMargin));
        }
        float left = screen.xMin + boardSideMargin, right = screen.xMax - boardSideMargin, low = bottom + boardDockGap;
        // the boards' top at the screen's top, or just below one of the corners: the biggest fit that clears them all
        var tops = new List<float> { screen.yMax - boardTopMargin };
        foreach (var c in corners) tops.Add(c.yMin);
        Rect best = Rect.MinMaxRect(left, low, right, tops.Min());
        float bestCell = -1f;
        foreach (var top in tops)
        {
            if (top <= low) continue;
            var free = Rect.MinMaxRect(left, low, right, top);
            LayOutGrid(free);
            var size = LayoutSize();
            var box = new Rect(free.center.x - size.x / 2f, free.yMin, size.x, size.y); // on the dock gap, as Build puts it
            if (corners.Any(c => c.Overlaps(box))) continue;
            if (CellSize > bestCell) { bestCell = CellSize; best = free; }
        }
        return best;
    }

    static Rect Bounds(RectTransform t, Transform space)
    {
        var corners = new Vector3[4];
        t.GetWorldCorners(corners);
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        foreach (var c in corners)
        {
            Vector2 p = space.InverseTransformPoint(c);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // a dark plate behind a board and its ring, so side-by-side boards read as separate
    void MakeFrame(BoardSpec b)
    {
        var frame = UIBuilder.NewImage("BoardFrame", cellsRoot, Color.white);
        frames.Add((frame, b));
        LayOutFrame(frame, b);
    }

    readonly List<(Image frame, BoardSpec board)> frames = new List<(Image, BoardSpec)>();

    void LayOutFrame(Image frame, BoardSpec b)
    {
        var low = CellPos(b.origin + new Vector2Int(-1, -1));
        var high = CellPos(b.origin + new Vector2Int(b.width, b.height));
        var rt = frame.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = (low + high) / 2f;
        rt.sizeDelta = high - low + Vector2.one * CellSize * 2f * framePadding;
        frame.color = boardFrameColor;
    }

    void OnValidate()
    {
        if (!Application.isPlaying || level == null) return;
        frames.RemoveAll(f => f.frame == null);
        foreach (var (frame, b) in frames) LayOutFrame(frame, b);
    }

    // a door: a gray bar along the edge, and a badge with its letter in the middle
    void MakeDoor(DoorSpec door)
    {
        var mid = EdgePos(door.cell, door.side);
        bool horizontalEdge = door.side == Dir.N || door.side == Dir.S;

        var bar = UIBuilder.NewImage("Door " + door.label, markersRoot, doorColor);
        bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        bar.rectTransform.anchoredPosition = mid;
        float length = CellSize * 0.7f, thickness = Mathf.Max(6f, CellSize * 0.1f);
        bar.rectTransform.sizeDelta = horizontalEdge ? new Vector2(length, thickness) : new Vector2(thickness, length);

        var badge = UIBuilder.NewImage("Badge", bar.rectTransform, doorColor);
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        badge.rectTransform.anchoredPosition = Vector2.zero;
        badge.rectTransform.sizeDelta = Vector2.one * CellSize * 0.3f;

        if (onDoorClicked != null)
        {
            // the bar and the badge both take clicks; the button on the bar gets the badge's too
            bar.raycastTarget = badge.raycastTarget = true;
            var button = bar.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onDoorClicked(door));
            // hovering either end lights up both ends of the pair, so it's clear which doors are linked
            var hover = bar.gameObject.AddComponent<HoverRelay>();
            hover.onEnter = () => SetDoorHighlight(door.label, true);
            hover.onExit = () => SetDoorHighlight(door.label, false);
        }

        var letter = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var lrt = (RectTransform)letter.transform;
        lrt.SetParent(badge.rectTransform, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var text = letter.GetComponent<TextMeshProUGUI>();
        text.text = door.label;
        text.fontSize = CellSize * 0.22f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = doorLabelColor;
        text.raycastTarget = false;

        // the hover highlight: a copy of this door in the highlight color, on a layer above the objects so an
        // object standing at the door doesn't hide it. it never takes clicks, and shows only while hovered
        var copy = Instantiate(bar.gameObject, doorHighlightRoot);
        foreach (var c in copy.GetComponents<Button>()) Destroy(c);
        foreach (var c in copy.GetComponents<HoverRelay>()) Destroy(c);
        foreach (var g in copy.GetComponentsInChildren<Graphic>(true))
        {
            g.raycastTarget = false;
            if (g is Image) g.color = doorHoverColor;
        }
        copy.transform.position = bar.transform.position;
        copy.SetActive(false);
        if (!doorHighlights.TryGetValue(door.label, out var copies)) doorHighlights[door.label] = copies = new List<GameObject>();
        copies.Add(copy);
    }

    // the highlight copies of both ends of each door pair, by letter, on a layer drawn after the objects
    readonly Dictionary<string, List<GameObject>> doorHighlights = new Dictionary<string, List<GameObject>>();
    RectTransform doorHighlightRoot;

    void SetDoorHighlight(string label, bool on)
    {
        if (!doorHighlights.TryGetValue(label, out var copies)) return;
        foreach (var c in copies) if (c != null) c.SetActive(on);
    }

    // a layer just above the objects, lined up with them
    void MakeDoorHighlightRoot()
    {
        if (doorHighlightRoot != null) { UIBuilder.Clear(doorHighlightRoot); return; }
        var go = new GameObject("DoorHighlights", typeof(RectTransform));
        doorHighlightRoot = (RectTransform)go.transform;
        doorHighlightRoot.SetParent(objectsRoot.parent, false);
        doorHighlightRoot.anchorMin = objectsRoot.anchorMin;
        doorHighlightRoot.anchorMax = objectsRoot.anchorMax;
        doorHighlightRoot.pivot = objectsRoot.pivot;
        doorHighlightRoot.anchoredPosition = objectsRoot.anchoredPosition;
        doorHighlightRoot.sizeDelta = objectsRoot.sizeDelta;
        doorHighlightRoot.localScale = objectsRoot.localScale;
        doorHighlightRoot.SetSiblingIndex(objectsRoot.GetSiblingIndex() + 1);
    }

    // the dotted lines at the starts and rings
    [Header("Start and ring lines")]
    public bool showEndLines = true;
    [Tooltip("On: a row of dots. Off: one solid line, End Line Dot Size thick (End Line Gap isn't used)")]
    public bool endLineDotted = true;
    [Range(0.02f, 0.3f)] public float endLineDotSize = 0.08f; // each dot (or the solid line's thickness), as a share of a square
    [Range(0.02f, 0.3f)] public float endLineGap = 0.06f;     // between dots, as a share of a square
    [Range(0f, 1f)] public float endLineOpacity = 0.75f;

    [Tooltip("Keep an object's start line once it has moved onto the board (off: it goes when the object leaves)")]
    public bool keepStartLine = true;
    [Tooltip("Keep a ring's line once its object is delivered (off: it goes when the ring fills)")]
    public bool keepRingLine = true;

    // one start or ring line: its dots (or its bar) and where it runs, so the part an object has passed can be erased
    class EndLine
    {
        public GameObject root;
        public Vector2 from, dir;  // from the start (or the ring) toward the board's edge
        public float length, thick;
        public float reach;        // how far from the line an object still counts as on it (half a square)
        public bool start;         // a start line: the object moves along dir. a ring line: against it
        public readonly List<(GameObject dot, float t)> dots = new List<(GameObject, float)>(); // t: distance along dir
        public RectTransform bar;
        float passed;              // how much of the line the object has passed (from the start, or from the board's edge)

        // the object counts only while it's on the line itself: anywhere else on the screen (another board, through a
        // door) says nothing about this line
        bool OnLine(Vector2 p, out float along)
        {
            var d = p - from;
            along = Vector2.Dot(d, dir);
            float off = Mathf.Abs(Vector2.Dot(d, new Vector2(-dir.y, dir.x)));
            return off <= reach && along >= -reach && along <= length + reach;
        }

        float PassedAt(float along) => Mathf.Clamp(start ? along : length - along, 0f, length);

        // during play: what the object has passed goes, and stays gone (erasing only moves forward)
        public void Advance(Vector2 objectPos)
        {
            if (!OnLine(objectPos, out var along)) return;
            float p = PassedAt(along);
            if (p <= passed) return;
            passed = p;
            Apply();
        }

        // during undo's rewind: the line follows the object back, filling in behind it
        public void Follow(Vector2 objectPos)
        {
            if (!OnLine(objectPos, out var along)) return;
            passed = PassedAt(along);
            Apply();
        }

        // as it looks with the object at objectPos; off the line, a start line counts as passed (the object has left
        // it for the board) and a ring line as not reached yet
        public void SetFor(Vector2 objectPos)
        {
            passed = OnLine(objectPos, out var along) ? PassedAt(along) : start ? length : 0f;
            Apply();
        }

        public void Restore()
        {
            passed = 0f;
            Apply();
        }

        // the part not passed yet shows: past the object on a start line, between the ring and the object on a ring line
        void Apply()
        {
            foreach (var (dot, t) in dots) dot.SetActive(start ? t > passed : t < length - passed);
            if (bar != null) SetBar(start ? passed : 0f, start ? length : length - passed);
        }

        void SetBar(float a, float b)
        {
            bar.gameObject.SetActive(b - a > 0.5f);
            bar.anchoredPosition = from + dir * ((a + b) / 2f);
            bar.sizeDelta = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? new Vector2(b - a, thick) : new Vector2(thick, b - a);
        }
    }

    readonly Dictionary<ObjectSpec, EndLine> startLines = new Dictionary<ObjectSpec, EndLine>();
    readonly Dictionary<ObjectSpec, EndLine> ringLines = new Dictionary<ObjectSpec, EndLine>();

    // each object's lines for where it is now: a start line while the object waits at its start, a ring line until
    // it's delivered (whole again when shown: undo brings them back). with Keep Start Line / Keep Ring Line on, always
    void ShowEndLines(List<MovingObject> objects)
    {
        foreach (var o in objects)
        {
            bool atStart = !o.completed && o.pos == o.spec.start;
            Show(startLines, o.spec, keepStartLine || atStart);
            Show(ringLines, o.spec, keepRingLine || !o.completed);
        }
    }

    static void Show(Dictionary<ObjectSpec, EndLine> lines, ObjectSpec spec, bool on)
    {
        if (!lines.TryGetValue(spec, out var line) || line == null) return;
        if (on) line.Restore();
        line.root.SetActive(on);
    }

    // while an object slides: the lines it doesn't keep are erased as it passes over them
    void EraseUnder(MovingObject o) => ForLines(o, line => line.Advance(o.view.Rect.anchoredPosition));

    // the lines of o that get erased (Keep Start Line / Keep Ring Line off) and are showing
    void ForLines(MovingObject o, Action<EndLine> act)
    {
        if (!keepStartLine && startLines.TryGetValue(o.spec, out var s) && s != null && s.root.activeSelf) act(s);
        if (!keepRingLine && ringLines.TryGetValue(o.spec, out var r) && r != null && r.root.activeSelf) act(r);
    }

    // dots (or one bar) from `from` to `to` (board positions), the last one touching `to`, grouped under one object;
    // null when it would be too short to draw. start: a start line (see EndLine)
    EndLine DottedLine(Vector2 from, Vector2 to, Color objectColor, bool start)
    {
        float dot = CellSize * endLineDotSize, step = dot + CellSize * endLineGap;
        float length = Vector2.Distance(from, to);
        if (length < dot) return null;
        var group = new GameObject("EndLine", typeof(RectTransform));
        var root = (RectTransform)group.transform;
        root.SetParent(markersRoot, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        var dir = (to - from) / length;
        // a solid line reaches the square's visible edge (the squares are drawn CellGap smaller than the grid, so
        // that's half of it further in), so it joins the square. dots stop at the grid line, keeping that small gap
        if (!endLineDotted) length += CellGap / 2f;
        var line = new EndLine { root = group, from = from, dir = dir, length = length, thick = dot, reach = CellSize * 0.5f, start = start };
        var color = objectColor; // the object's own color
        color.a *= endLineOpacity;
        if (!endLineDotted) // one bar from the start or ring to the board's edge
        {
            var bar = UIBuilder.NewImage("Bar", root, color);
            bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            line.bar = bar.rectTransform;
            line.Restore();
            return line;
        }
        for (float t = length - dot / 2f; t >= dot / 2f; t -= step) // from the board's edge back toward the start or ring
        {
            var img = UIBuilder.NewImage("Dot", root, color);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            img.rectTransform.sizeDelta = Vector2.one * dot;
            img.rectTransform.anchoredPosition = from + dir * t;
            line.dots.Add((img.gameObject, t));
        }
        return line;
    }

    // board position relative to the board's center of a grid coordinate (columns and rows from LayOutGrid)
    public Vector2 CellPos(Vector2Int c) => new Vector2(Line(colX, c.x), Line(rowY, c.y)) * CellSize;

    // each column's and row's center, in squares from the layout's middle, for -1 (the ring before the first board)
    // to width / height (the ring after the last). the empty line between two boards' rings is squeezed so their frames
    // are Board Gap apart
    float[] colX = new float[0], rowY = new float[0];

    static float Line(float[] at, int i) =>
        at.Length == 0 ? i : i < -1 ? at[0] + (i + 1) : i + 1 >= at.Length ? at[at.Length - 1] + (i + 2 - at.Length) : at[i + 1];

    // the square size that fits the whole layout (boards, rings, frames, Board Gap between frames) into free, and
    // where each column and row goes
    void LayOutGrid(Rect free)
    {
        float pad = Mathf.Max(framePadding, ringSize / 2f); // each frame past its ring (or the ring's circle, if bigger)
        bool[] emptyCol = EmptyLines(level.width, b => (b.origin.x, b.width));
        bool[] emptyRow = EmptyLines(level.height, b => (b.origin.y, b.height));
        int ex = emptyCol.Count(e => e), ey = emptyRow.Count(e => e);
        // across: the rings' centers span width + 1 squares, each frame adds pad; an empty line between two rings takes
        // what's left of Board Gap once the two frames' pads past their rings are counted
        float Fit(float room, int lines, int empties) => (room - empties * boardGap) / (lines + 1 + 2f * pad - empties * (2f - 2f * pad));
        CellSize = Mathf.Floor(Mathf.Min(Fit(free.width, level.width, ex), Fit(free.height, level.height, ey)));
        SetLines();
    }

    // where each column and row goes at the current square size (an empty line between boards keeps Board Gap)
    void SetLines()
    {
        float pad = Mathf.Max(framePadding, ringSize / 2f);
        float squeezed = Mathf.Max(0f, boardGap / Mathf.Max(1f, CellSize) - (1f - 2f * pad)); // an empty line's width, in squares
        colX = Centers(EmptyLines(level.width, b => (b.origin.x, b.width)), squeezed);
        rowY = Centers(EmptyLines(level.height, b => (b.origin.y, b.height)), squeezed);
    }

    // the whole layout's size at the current square size: ring to ring across, plus each frame past its ring
    Vector2 LayoutSize()
    {
        float pad = Mathf.Max(framePadding, ringSize / 2f);
        return new Vector2(colX[colX.Length - 1] - colX[0] + 2f * pad, rowY[rowY.Length - 1] - rowY[0] + 2f * pad) * CellSize;
    }

    // lines (0 to count - 1) no board or ring touches: the empty line between two boards
    bool[] EmptyLines(int count, Func<BoardSpec, (int start, int size)> span)
    {
        var res = new bool[count];
        for (int i = 0; i < count; i++)
            res[i] = level.boards.All(b => { var (s, n) = span(b); return i < s - 1 || i > s + n; });
        return res;
    }

    static float[] Centers(bool[] empty, float squeezed)
    {
        int count = empty.Length;
        float W(int i) => i >= 0 && i < count && empty[i] ? squeezed : 1f;
        var at = new float[count + 2]; // index i + 1 for line i, from -1 to count
        for (int i = 0; i <= count; i++) at[i + 1] = at[i] + W(i - 1) / 2f + W(i) / 2f;
        float middle = (at[0] + at[count + 1]) / 2f;
        for (int i = 0; i < at.Length; i++) at[i] -= middle;
        return at;
    }

    // the middle of a cell's edge
    public Vector2 EdgePos(Vector2Int c, Dir side) => CellPos(c) + (Vector2)DirUtil.Offset(side) * CellSize * 0.5f;

    // where a start or a ring sits: just outside the board, End Distance from the edge it faces. toward: the way it
    // faces the board (a start's heading); left out, the side of c that touches the board (a ring's)
    public Vector2 OutsidePos(Vector2Int c, Dir? toward = null)
    {
        Dir? side = toward;
        if (side == null)
            foreach (var d in DirUtil.All)
                if (InGrid(c + DirUtil.Offset(d))) { side = d; break; }
        if (side == null) return CellPos(c);
        return EdgePos(c, side.Value) - (Vector2)DirUtil.Offset(side.Value) * CellSize * endDistance;
    }

    // inside one of the boards
    public bool InGrid(Vector2Int c) => level.BoardOf(c) >= 0;

    public void SetTile(Vector2Int p, TileType t)
    {
        HideGhost();
        var cell = Cells[p.x, p.y];
        cell.tile = t;
        if (cell.tileView != null) Destroy(cell.tileView.gameObject);
        cell.tileView = Instantiate(tilePrefab, cell.view.transform);
        cell.tileView.Show(t, tileColor);
    }

    public void ClearTile(Vector2Int p)
    {
        var cell = Cells[p.x, p.y];
        RemoveTile(cell);
        StartCoroutine(Flash(cell.view.background));
    }

    // a cell's own color: dark for an obstacle, darker for a tile an object has used
    Color CellColor(Cell cell) => cell.obstacle ? obstacleColor : cell.locked ? lockedCellColor : cellColor;

    // an object went through this tile: from now on it can't be returned to the supply
    public void SetLocked(Vector2Int p, bool locked)
    {
        var cell = Cells[p.x, p.y];
        if (locked && !cell.locked) recording?.steps.Add(new RewindStep { kind = StepKind.Used, cell = p }); // for undo's rewind
        cell.locked = locked;
        cell.view.background.color = CellColor(cell);
    }

    // for undo: which tiles were locked, after RestoreTiles has put the tiles back
    public void RestoreLocks(bool[,] locked)
    {
        for (int x = 0; x < Cells.GetLength(0); x++)
        for (int y = 0; y < Cells.GetLength(1); y++)
        {
            var cell = Cells[x, y];
            if (cell == null) continue;
            cell.locked = locked[x, y];
            cell.view.background.color = CellColor(cell);
        }
    }

    // right-click returned the tile to the supply: it just goes, without the red flash of a lost tile
    public void TakeBackTile(Vector2Int p)
    {
        var cell = Cells[p.x, p.y];
        RemoveTile(cell);
        cell.view.background.color = CellColor(cell);
    }

    // the hover preview: a faded copy of the selected tile on an empty square.
    // with no tile selected (or on a square that's taken) nothing happens, since clicking wouldn't place anything
    Func<TileType?> previewTile;
    TileView ghost;
    Vector2Int ghostCell;

    void ShowGhost(Vector2Int p)
    {
        HideGhost(p);
        var cell = Cells[p.x, p.y];
        var t = draggedFrom != null ? draggedTile : previewTile?.Invoke(); // a board tile being dragged previews itself
        if (t == null || cell == null || cell.obstacle || cell.tile != null) return;
        ghost = Instantiate(tilePrefab, cell.view.transform);
        ghost.Show(t.Value, Color.Lerp(tileColor, cellColor, ghostFade));
        ghostCell = p;
    }

    void HideGhost(Vector2Int p)
    {
        if (ghost == null || p != ghostCell) return;
        HideGhost();
    }

    public void HideGhost()
    {
        if (ghost == null) return;
        Destroy(ghost.gameObject);
        ghost = null;
        var cell = Cells != null && ghostCell.x < Cells.GetLength(0) && ghostCell.y < Cells.GetLength(1) ? Cells[ghostCell.x, ghostCell.y] : null;
        if (cell != null && cell.view != null) cell.view.background.color = CellColor(cell);
    }

    static void RemoveTile(Cell cell)
    {
        cell.locked = false; // a new tile here would start free
        cell.tile = null;
        if (cell.tileView != null) Destroy(cell.tileView.gameObject);
        cell.tileView = null;
    }

    // for undo: puts every cell's tile back without the removal flash, and gives every cell its own color again
    public void RestoreTiles(TileType?[,] tiles)
    {
        for (int x = 0; x < Cells.GetLength(0); x++)
        for (int y = 0; y < Cells.GetLength(1); y++)
        {
            var cell = Cells[x, y];
            if (cell == null) continue;
            cell.view.background.color = CellColor(cell); // a flash or junction highlight cut short
            if (cell.tile == tiles[x, y]) continue;
            if (tiles[x, y] is TileType t)
            {
                SetTile(new Vector2Int(x, y), t);
                // undoing a take-back: the tile travels back from the supply bar. a tile a delivery broke is just back
                if (undoing && pendingRewind == null) TileComesFromSupply(cell, t);
            }
            else
            {
                // the placed tile travels back to the supply: PlayUndo shows it from the recording, broken or not
                if (undoing && pendingRewind == null && cell.tile is TileType placed) TileGoes(cell, placed);
                RemoveTile(cell);
            }
        }
    }

    public void StopFlashes()
    {
        FinishUndo();
        StopAllCoroutines();
    }

    // where an object rests
    public Vector2 WaitPos(MovingObject o) =>
        InGrid(o.pos) || o.viaDoor ? EdgePos(o.pos, o.heading) : OutsidePos(o.pos, o.heading); // at its start, outside the board

    // several spread each group out along its edge
    public void LayoutWaitingObjects(List<MovingObject> objects)
    {
        ShowEndLines(objects);
        var groups = objects.Where(o => !o.completed)
            .GroupBy(o => Vector2Int.RoundToInt(WaitPos(o) * 2f / CellSize));
        foreach (var group in groups)
        {
            var list = group.ToList();
            Vector2 center = WaitPos(list[0]);
            // East/West spread vertically and vice versa
            bool movesHorizontally = list[0].heading == Dir.E || list[0].heading == Dir.W;
            Vector2 along = movesHorizontally ? Vector2.up : Vector2.right;
            float spacing = Mathf.Min(0.36f, 0.9f / list.Count) * CellSize;
            for (int i = 0; i < list.Count; i++)
            {
                var spot = center + along * ((i - (list.Count - 1) / 2f) * spacing);
                // a shuffle side by side is placed at once; recorded as a short slide, so undo's rewind slides it back
                if ((list[i].view.Rect.anchoredPosition - spot).sqrMagnitude > 0.25f) Record(list[i], teleport: false);
                list[i].view.Rect.anchoredPosition = spot;
            }
        }
    }

    // animates the object to a board position
    public IEnumerator Slide(MovingObject o, Vector2 target)
    {
        Record(o, teleport: false); // for undo's rewind
        Vector2 start = o.view.Rect.anchoredPosition;
        float seconds = moveSecondsPerCell * Vector2.Distance(start, target) / CellSize;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            o.view.Rect.anchoredPosition = Vector2.Lerp(start, target, t / seconds);
            EraseUnder(o);
            yield return null;
        }
        o.view.Rect.anchoredPosition = target;
        EraseUnder(o);
    }

    // the object shrinks into one door and grows out of its partner
    public IEnumerator Teleport(MovingObject o, Vector2 to)
    {
        Record(o, teleport: true); // for undo's rewind
        float half = teleportSeconds / 2f;
        for (float t = 0; t < half; t += Time.deltaTime)
        {
            o.view.Rect.localScale = Vector3.one * (1f - t / half);
            yield return null;
        }
        o.view.Rect.anchoredPosition = to;
        for (float t = 0; t < half; t += Time.deltaTime)
        {
            o.view.Rect.localScale = Vector3.one * (t / half);
            yield return null;
        }
        o.view.Rect.localScale = Vector3.one;
    }

    public void MarkDelivered(MovingObject o)
    {
        recording?.steps.Add(new RewindStep { kind = StepKind.Delivered, obj = o }); // for undo's rewind
        o.view.gameObject.SetActive(false);
        o.dest.SetDelivered(true);
    }

    // for undo: matches an object's view to its state - shown or delivered, facing, full size, crossed out if blocked.
    // where it stands is set by LayoutWaitingObjects
    public void SyncObject(MovingObject o)
    {
        o.view.gameObject.SetActive(!o.completed);
        o.dest.SetDelivered(o.completed);
        o.view.Face(o.heading);
        o.view.Rect.localScale = Vector3.one; // a teleport cut short
        o.view.SetBlocked(o.blocked);
    }

    // the object waits at the center of a + or T tile or a door, with one arrow per way it may go
    public IEnumerator AskDirection(MovingObject o, List<Dir> options, Dir? hinted = null)
    {
        var cellBg = Cells[o.pos.x, o.pos.y].view.background;
        cellBg.color = crossHighlightColor;
        yield return AskDirectionAt(o, CellPos(o.pos), options, hinted);
        cellBg.color = CellColor(Cells[o.pos.x, o.pos.y]);
    }

    // arrows around a point. hinted: the stage 3 hint's way, whose arrow pulses in the Hint color
    public IEnumerator AskDirectionAt(MovingObject o, Vector2 center, List<Dir> options, Dir? hinted = null)
    {
        UIBuilder.Clear(choicesRoot);
        pickedDirection = null;
        hintedArrow = null;
        foreach (var d in options)
        {
            var dir = d; // copy for the click handler below
            var button = Instantiate(arrowButtonPrefab, choicesRoot);
            var rt = (RectTransform)button.transform;
            rt.sizeDelta = Vector2.one * CellSize * 0.25f;
            rt.anchoredPosition = center + (Vector2)DirUtil.Offset(d) * CellSize * 0.39f; // that way
            rt.localEulerAngles = new Vector3(0, 0, DirUtil.Angle(d)); // the prefab's arrow points up
            button.GetComponent<Graphic>().color = o.spec.color;
            if (hinted == d) hintedArrow = button.GetComponent<Graphic>();
            button.onClick.AddListener(() => { if (!inputLocked) pickedDirection = dir; }); // for the tutorial: see LockInput
        }

        while (pickedDirection == null) yield return null; // wait for a click
        hintedArrow = null;

        UIBuilder.Clear(choicesRoot);
        o.heading = pickedDirection.Value;
    }

    public void ClearChoices() => UIBuilder.Clear(choicesRoot);

    // briefly tints a cell red so the player sees where a tile was removed
    IEnumerator Flash(Image img)
    {
        img.color = flashColor;
        yield return new WaitForSeconds(flashHoldSeconds);
        for (float t = 0; t < flashFadeSeconds; t += Time.deltaTime)
        {
            img.color = Color.Lerp(flashColor, cellColor, t / flashFadeSeconds);
            yield return null;
        }
        img.color = cellColor;
    }

    // undo: the last round plays backwards, like a rewound video. every slide and teleport of a round is recorded (StartRecording, with the undo snapshot)
    [Header("Undo")]
    [Tooltip("Off: undo puts everything back at once")]
    public bool animateUndo = true;
    [Tooltip("How much faster than the real moves the rewind plays")]
    [Min(1f)] public float rewindSpeed = 3f;
    [Tooltip("How long a tile takes to travel between its square and the supply bar")]
    [Min(0f)] public float undoSeconds = 0.3f;

    // one round: the tile placed to start it, and what it caused, in order
    public class Rewind
    {
        public Vector2Int placedAt;
        public TileType placed;
        public readonly List<RewindStep> steps = new List<RewindStep>();
    }
    public enum StepKind { Move, Teleport, Delivered, Blocked, Used }
    public class RewindStep
    {
        public StepKind kind;
        public MovingObject obj;  // Move, Teleport, Delivered, Blocked
        public Vector2 from;      // Move, Teleport: where the object was when it started
        public float angle;       // Move, Teleport: the way it faced
        public bool was;          // Blocked: whether it was crossed out before
        public Vector2Int cell;   // Used: the square whose tile turned grey
    }

    Rewind recording; // the round being played, or null

    // a placement's round starts: the tile and what follows go in the returned recording (kept with the undo snapshot).
    // the tile is remembered even if a delivery breaks it later, so the rewind can still show it being walked over
    public Rewind StartRecording(Vector2Int placedAt, TileType placed) =>
        recording = new Rewind { placedAt = placedAt, placed = placed };
    public void StopRecording() => recording = null;

    void Record(MovingObject o, bool teleport)
    {
        recording?.steps.Add(new RewindStep
        {
            kind = teleport ? StepKind.Teleport : StepKind.Move, obj = o,
            from = o.view.Rect.anchoredPosition, angle = o.view.Rect.localEulerAngles.z,
        });
    }

    // GameManager.UpdateBlocked: an object's ✕ is about to change from `was`
    public void RecordBlocked(MovingObject o, bool was) =>
        recording?.steps.Add(new RewindStep { kind = StepKind.Blocked, obj = o, was = was });

    // a tile goes back to the supply (the placed tile; a stand-in shows on its square until it goes) or comes from it
    // (a taken-back tile; its real view stays hidden until it lands)
    class Trip { public RectTransform square; public TileType type; public bool toSupply; public GameObject standIn, landing; }
    readonly List<Trip> trips = new List<Trip>();
    readonly List<GameObject> flyingCopies = new List<GameObject>(); // the tiles on their way, above everything
    readonly List<(MovingObject o, Vector2 pos, float angle)> undoFinals = new List<(MovingObject, Vector2, float)>();
    readonly List<Vector2Int> greyedForRewind = new List<Vector2Int>(); // squares shown grey (used) until the rewind passes
    Dictionary<MovingObject, (Vector2 pos, float angle, bool shown, bool blocked)> beforeUndo;
    Rewind pendingRewind;
    Coroutine undoAnim;
    Func<TileType, RectTransform> supplySlot; // where a tile type's slot is in the supply bar
    Action supplyDone;                        // the supply bar's counts catch up, once the tiles have traveled
    bool undoing; // between BeginUndo and PlayUndo: RestoreTiles sends tiles traveling instead of swapping them

    // before the game state goes back: how everything looks now, and what to play backwards
    public void BeginUndo(List<MovingObject> objects, Rewind rewind)
    {
        FinishUndo();
        StopRecording();
        undoing = animateUndo && objects != null;
        pendingRewind = undoing ? rewind : null; // null: undoing a take-back
        beforeUndo = undoing
            ? objects.ToDictionary(o => o, o => (o.view.Rect.anchoredPosition, o.view.Rect.localEulerAngles.z, o.view.gameObject.activeSelf, o.blocked))
            : null;
    }

    // RestoreTiles, while undoing: a tile coming back from a delivery's break just reappears; the placed tile and a
    // taken-back tile travel (see Trip)
    void TileGoes(Cell cell, TileType type)
    {
        var standIn = Instantiate(tilePrefab, cell.view.transform);
        standIn.Show(type, tileColor);
        trips.Add(new Trip { square = (RectTransform)cell.view.transform, type = type, toSupply = true, standIn = standIn.gameObject });
    }

    void TileComesFromSupply(Cell cell, TileType type)
    {
        cell.tileView.gameObject.SetActive(false);
        trips.Add(new Trip { square = (RectTransform)cell.view.transform, type = type, toSupply = false, landing = cell.tileView.gameObject });
    }

    // after the state went back (everything already as it was): show it as it looked, then play it backwards.
    // slot: where a tile type's slot is in the supply bar; done: runs once the supply bar can show its new counts
    public void PlayUndo(List<MovingObject> objects, Func<TileType, RectTransform> slot, Action done)
    {
        supplySlot = slot;
        supplyDone = done;
        if (!undoing) { FinishUndo(); return; }
        undoing = false;
        rewinding.Clear();
        undoFinals.Clear();
        greyedForRewind.Clear();
        var rewind = pendingRewind;
        pendingRewind = null;
        foreach (var o in objects)
        {
            if (!beforeUndo.TryGetValue(o, out var before)) continue;
            var rt = o.view.Rect;
            undoFinals.Add((o, rt.anchoredPosition, rt.localEulerAngles.z));
            if (rewind == null) continue;
            // as it looked before the undo: where it stood, delivered (hidden, its ring filled) or not, crossed out or not
            rt.anchoredPosition = before.pos;
            rt.localEulerAngles = new Vector3(0, 0, before.angle);
            o.view.gameObject.SetActive(before.shown);
            o.dest.SetDelivered(!before.shown);
            o.view.SetBlocked(before.blocked);
            rewinding[rt] = o;
            ForLines(o, line => line.SetFor(before.pos)); // its lines as they looked
        }
        beforeUndo = null;
        if (rewind != null)
        {
            // the squares the round used are grey again until the rewind passes back over them
            foreach (var step in rewind.steps)
                if (step.kind == StepKind.Used && Cells[step.cell.x, step.cell.y] is Cell used)
                {
                    used.view.background.color = lockedCellColor;
                    greyedForRewind.Add(step.cell);
                }
            // the placed tile is back on its square for the whole rewind, even if a delivery broke it, and leaves at the end
            if (Cells[rewind.placedAt.x, rewind.placedAt.y] is Cell placedCell) TileGoes(placedCell, rewind.placed);
        }
        undoAnim = StartCoroutine(UndoRoutine(rewind));
    }

    // the objects being rewound, by their view, so their lines can be redrawn as they move back
    readonly Dictionary<RectTransform, MovingObject> rewinding = new Dictionary<RectTransform, MovingObject>();

    // the eraser in reverse: the lines show again behind the object as it retraces its moves
    void Redraw(MovingObject o)
    {
        if (o != null) ForLines(o, line => line.Follow(o.view.Rect.anchoredPosition));
    }

    // the steps to play back, with laps repeated back to back kept once: an object looping round the same way (the
    // same moves from the same spots, facing the same ways) rewinds that lap a single time. laps that differ, and
    // everything that isn't a move (tiles turning grey, deliveries, ✕ marks), are all kept
    static List<RewindStep> FoldRepeats(List<RewindStep> steps)
    {
        var moves = new List<int>(); // where the slides and teleports are in steps
        for (int i = 0; i < steps.Count; i++)
            if (steps[i].kind == StepKind.Move || steps[i].kind == StepKind.Teleport) moves.Add(i);

        bool SameStep(RewindStep a, RewindStep b) =>
            a.obj == b.obj && a.kind == b.kind && (a.from - b.from).sqrMagnitude < 1f && Mathf.Abs(Mathf.DeltaAngle(a.angle, b.angle)) < 1f;
        bool SameLap(int a, int b, int length)
        {
            for (int t = 0; t < length; t++)
                if (!SameStep(steps[moves[a + t]], steps[moves[b + t]])) return false;
            return true;
        }

        var dropped = new HashSet<int>();
        for (int i = 0; i < moves.Count;)
        {
            int next = i + 1;
            for (int length = 1; i + 2 * length <= moves.Count; length++)
            {
                if (!SameLap(i, i + length, length)) continue;
                int k = i + length; // the first lap stays; every copy right after it goes
                while (k + length <= moves.Count && SameLap(i, k, length))
                {
                    for (int t = k; t < k + length; t++) dropped.Add(moves[t]);
                    k += length;
                }
                next = k;
                break;
            }
            i = next;
        }
        return dropped.Count == 0 ? steps : steps.Where((s, i) => !dropped.Contains(i)).ToList();
    }

    IEnumerator UndoRoutine(Rewind rewind)
    {
        var steps = rewind != null ? FoldRepeats(rewind.steps) : null;
        if (steps != null)
            for (int i = steps.Count - 1; i >= 0; i--)
            {
                var step = steps[i];
                var o = step.obj;
                switch (step.kind)
                {
                    case StepKind.Delivered: // it comes back out of its ring
                        if (o?.view == null) break;
                        o.view.gameObject.SetActive(true);
                        o.dest.SetDelivered(false);
                        break;
                    case StepKind.Blocked:
                        if (o?.view != null) o.view.SetBlocked(step.was);
                        break;
                    case StepKind.Used: // the object backs off the square: its tile isn't grey any more
                        if (Cells[step.cell.x, step.cell.y] is Cell cell) cell.view.background.color = CellColor(cell);
                        greyedForRewind.Remove(step.cell);
                        break;
                    case StepKind.Teleport:
                    {
                        if (o?.view == null) break;
                        var rt = o.view.Rect;
                        rt.localEulerAngles = new Vector3(0, 0, step.angle); // backwards, still facing forward
                        float half = teleportSeconds / 2f / rewindSpeed;
                        for (float t = 0; t < half; t += Time.unscaledDeltaTime) { rt.localScale = Vector3.one * (1f - t / half); yield return null; }
                        rt.anchoredPosition = step.from;
                        Redraw(o);
                        for (float t = 0; t < half; t += Time.unscaledDeltaTime) { rt.localScale = Vector3.one * (t / half); yield return null; }
                        rt.localScale = Vector3.one;
                        break;
                    }
                    default: // Move: a slide, or a shuffle side by side
                    {
                        if (o?.view == null) break;
                        var rt = o.view.Rect;
                        rt.localEulerAngles = new Vector3(0, 0, step.angle);
                        Vector2 start = rt.anchoredPosition;
                        float seconds = moveSecondsPerCell * Vector2.Distance(start, step.from) / CellSize / rewindSpeed;
                        for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
                        {
                            rt.anchoredPosition = Vector2.Lerp(start, step.from, t / seconds);
                            Redraw(o);
                            yield return null;
                        }
                        rt.anchoredPosition = step.from;
                        Redraw(o);
                        break;
                    }
                }
            }
        PlaceFinals(); // exactly how the undone state has them
        yield return Trips();
        undoAnim = null;
        FinishUndo();
    }

    // every traveling tile at once, at full size and fully solid, between its square and its supply slot
    IEnumerator Trips()
    {
        var canvasRoot = GetComponentInParent<Canvas>()?.rootCanvas.transform as RectTransform;
        if (trips.Count == 0 || canvasRoot == null || supplySlot == null || undoSeconds <= 0f) yield break;
        var flying = new List<(RectTransform rt, Vector3 from, Vector3 to, Vector2 fromSize, Vector2 toSize)>();
        foreach (var trip in trips)
        {
            var slot = supplySlot(trip.type);
            if (slot == null || trip.square == null) continue;
            if (trip.standIn != null) trip.standIn.SetActive(false); // the traveling copy takes over
            var view = Instantiate(tilePrefab, canvasRoot);
            view.Show(trip.type, tileColor);
            flyingCopies.Add(view.gameObject);
            var rt = (RectTransform)view.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            foreach (var g in view.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            Vector2 squareSize = WorldSize(trip.square, canvasRoot), slotSize = WorldSize(slot, canvasRoot);
            float slotSide = Mathf.Min(slotSize.x, slotSize.y);
            var (fromPos, toPos) = trip.toSupply ? (trip.square.position, slot.position) : (slot.position, trip.square.position);
            var (fromSize, toSize) = trip.toSupply ? (squareSize, Vector2.one * slotSide) : (Vector2.one * slotSide, squareSize);
            rt.position = fromPos;
            rt.sizeDelta = fromSize;
            flying.Add((rt, fromPos, toPos, fromSize, toSize));
        }
        for (float t = 0; t < undoSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / undoSeconds);
            foreach (var f in flying)
            {
                f.rt.position = Vector3.Lerp(f.from, f.to, k);
                f.rt.sizeDelta = Vector2.Lerp(f.fromSize, f.toSize, k);
            }
            yield return null;
        }
        foreach (var f in flying) Destroy(f.rt.gameObject);
        flyingCopies.Clear();
    }

    // a rect's size in another rect's units (both on the same canvas)
    static Vector2 WorldSize(RectTransform rt, RectTransform space)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        float scale = space.lossyScale.x > 0 ? space.lossyScale.x : 1f;
        return new Vector2(c[2].x - c[0].x, c[2].y - c[0].y) / scale;
    }

    // every object, ring and square exactly as the undone state has it
    void PlaceFinals()
    {
        foreach (var (o, pos, angle) in undoFinals)
        {
            if (o?.view == null) continue;
            var rt = o.view.Rect;
            rt.anchoredPosition = pos;
            rt.localEulerAngles = new Vector3(0, 0, angle);
            rt.localScale = Vector3.one;
            o.view.gameObject.SetActive(!o.completed);
            o.dest.SetDelivered(o.completed);
            o.view.SetBlocked(o.blocked);
        }
        foreach (var p in greyedForRewind)
            if (Cells != null && Cells[p.x, p.y] is Cell cell) cell.view.background.color = CellColor(cell);
        greyedForRewind.Clear();
    }

    // everything straight to where it ends up
    public void FinishUndo()
    {
        if (undoAnim != null) { StopCoroutine(undoAnim); undoAnim = null; }
        PlaceFinals();
        undoFinals.Clear();
        foreach (var o in rewinding.Values) ForLines(o, line => line.SetFor(o.view.Rect.anchoredPosition)); // as they are for where each ended up
        rewinding.Clear();
        foreach (var trip in trips)
        {
            if (trip.standIn != null) Destroy(trip.standIn);
            if (trip.landing != null) trip.landing.SetActive(true);
        }
        trips.Clear();
        foreach (var copy in flyingCopies) if (copy != null) Destroy(copy); // a trip cut short
        flyingCopies.Clear();
        var done = supplyDone;
        supplyDone = null;
        done?.Invoke();
        // (undoing stays as it is: Undo stops the board's effects between BeginUndo and PlayUndo)
    }
}
