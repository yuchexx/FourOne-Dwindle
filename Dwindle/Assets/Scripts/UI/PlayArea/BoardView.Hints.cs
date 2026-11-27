using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the hints on the board, in the palette's Hint color: stage 2's numbers by the rings and frames on the squares where
// groups arrive, stage 3's numbered ghost tiles. numbers sit in small squares; everything slowly pulses, so hints read
// apart from play. part of BoardView
public partial class BoardView
{
    [Header("Hints")]
     [System.NonSerialized] public float hintBadgeSize = 0.3f;         // a number's square, as a share of a square
     [System.NonSerialized] public float hintFrameWidth = 7f;                    // the frame on a square where a group arrives
     [System.NonSerialized] public float hintGhostStrength = 0.6f;         // a ghost's color, from the square's (0) to Hint (1)
    [Tooltip("The hints slowly fade in and out (the stage 1 line too)")]
     [System.NonSerialized] public bool hintPulse = true;
     [System.NonSerialized] public float hintPulseSeconds = 1.5f;
     [System.NonSerialized] public float hintPulseDepth = 0.35f;           // how far it fades
    [Tooltip("Optional: says what a stage 2 mark means while the mouse is over it")]
    public HintTooltip hintTooltip;

    // stage 2's marks (a ring's number, a frame) and what each one's tooltip says
    readonly List<(RectTransform area, List<(string text, Color color)> tip)> hintAreas = new();

    RectTransform hintsRoot;
    enum Pulse { Ghost, Frame, Fade, Junction }
    readonly List<(Graphic g, Pulse kind)> pulsing = new List<(Graphic, Pulse)>();
    Graphic hintedArrow; // the plan's arrow while the game asks for a way (stage 3): it pulses in the Hint color

    static Color HintColor => UIPalette.Current.hint;

    // 1 at full, down to 1 - depth: for anything else that pulses with the hints (HintButton's lines)
    public float HintPulseLevel =>
        hintPulse ? 1f - hintPulseDepth * (0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * 2f * Mathf.PI / hintPulseSeconds)) : 1f;

    // replaces whatever hints were drawn
    public void ShowHints(List<(string objectName, int number)> badges, List<(Vector2Int square, int number)> junctions,
                          List<(Vector2Int square, TileType tile, int number, StepState state)> ghosts, ICollection<string> delivered = null)
    {
        MakeHintsRoot();
        pulsing.Clear();
        hintAreas.Clear();
        if (hintTooltip != null) hintTooltip.Hide();
        foreach (var g in squareGhosts) if (g != null) Destroy(g);
        borderPieces.Clear(); // the old frames go with the hints root
        guideJunctions.Clear(); guideRings.Clear(); guideGhosts.Clear(); guideBadges.Clear(); guideGhostTiles.Clear();
        squareGhosts.Clear();
        if (level == null) return;
        float half = (CellSize - CellGap) / 2f, r = CellSize * hintBadgeSize / 2f;
        var upperLeft = new Vector2(-half + r + hintFrameWidth, half - r - hintFrameWidth);

        foreach (var (name, number) in badges)
        {
            var spec = level.objects.FirstOrDefault(o => o.name == name);
            if (spec == null) continue;
            var ringArea = RingArea(OutsidePos(spec.dest));
            hintAreas.Add((ringArea, RingTip(badges, number))); // no number on the ring (the order is under the dock); its tip stays
            guideRings.Add(ringArea);
        }

        foreach (var (p, number) in junctions)
        {
            // a meeting already delivered: no ghost, its frame faded (done)
            bool done = delivered != null && badges.Where(b => b.number == number).All(b => delivered.Contains(b.objectName));
            var box = Frame(p, JunctionColors(badges, number), done ? junctionDoneStrength : 1f);
            if (!done) JunctionCycle(p);
            if (!done) guideJunctions.Add((box, number));
            var tip = new List<(string text, Color color)> { ("Junction:", default) };
            tip.AddRange(Who(badges, number));
            tip.Add(("meet here", default));
            hintAreas.Add((box, tip));
        }

        // a step done right: just its number, in the accent color, still. the right tile at the wrong moment: its
        // number, pulsing. otherwise (empty, another tile): a ghost of its tile under the tiles, and its number.
        foreach (var (p, tile, number, state) in ghosts)
        {
            if (state == StepState.ToDo) GhostInSquare(p, tile);
            var badge = state == StepState.Done ? Badge(CellPos(p) + upperLeft, number, UIPalette.Current.hintAccent, pulse: false) : Badge(CellPos(p) + upperLeft, number);
            if (state == StepState.ToDo && Cells[p.x, p.y]?.view != null) guideGhosts.Add(((RectTransform)Cells[p.x, p.y].view.transform, badge, number, p)); if (state == StepState.ToDo) guideGhostTiles.Add((number, tile));
            guideBadges[p] = badge;
        }
    }

    // for the hint guide: where stage 1's junctions (still to deliver) and rings are, and stage 2's ghosts still to place
    readonly List<(RectTransform frame, int number)> guideJunctions = new();
    readonly List<RectTransform> guideRings = new();
    readonly List<(RectTransform square, RectTransform badge, int number, Vector2Int cell)> guideGhosts = new();
    readonly Dictionary<Vector2Int, RectTransform> guideBadges = new(); // every answer step's number, by its square
    public Vector2Int? GuideGhostCell => guideGhosts.Count > 0 ? guideGhosts.OrderBy(g => g.number).First().cell : (Vector2Int?)null;
    public TileType? GuideGhostTile => guideGhostTiles.Count > 0 ? guideGhostTiles.OrderBy(g => g.number).First().tile : (TileType?)null;
    readonly List<(int number, TileType tile)> guideGhostTiles = new();
    public RectTransform GuideBadgeAt(Vector2Int cell) => guideBadges.TryGetValue(cell, out var b) && b != null ? b : null;
    public RectTransform GuideJunction => guideJunctions.Count > 0 ? guideJunctions.OrderBy(j => j.number).First().frame : null;
    public int GuideJunctionNumber => guideJunctions.Count > 0 ? guideJunctions.OrderBy(j => j.number).First().number : -1;
    public RectTransform GuideRing => guideRings.Count > 0 ? guideRings[0] : null;
    public RectTransform GuideGhost => guideGhosts.Count > 0 ? guideGhosts.OrderBy(g => g.number).First().square : null;
    public RectTransform HintedArrowRect => hintedArrow != null ? hintedArrow.rectTransform : null;
    public Dir? HintedWay { get { if (hintedArrow == null) return null; foreach (var kv in arrowGraphics) if (kv.Value == hintedArrow) return kv.Key; return null; } } // the way the pulsing arrow points

    // stage 3's ghost tiles sit in their squares, under any tile placed there and the hover preview
    readonly List<GameObject> squareGhosts = new List<GameObject>();

    void GhostInSquare(Vector2Int p, TileType tile)
    {
        var cell = Cells[p.x, p.y];
        if (cell?.view == null) return;
        var t = Instantiate(tilePrefab, cell.view.transform);
        t.transform.SetAsFirstSibling(); // right above the square's own color
        var rt = (RectTransform)t.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        t.Show(tile, GhostColor(1f)); // solid, so crossing roads don't show darker overlaps
        foreach (var img in t.GetComponentsInChildren<Image>(true)) { img.raycastTarget = false; pulsing.Add((img, Pulse.Ghost)); }
        squareGhosts.Add(t.gameObject);
    }

    Color GhostColor(float k) => Color.Lerp(cellColor, HintColor, hintGhostStrength * k);

    void Update()
    {
        UpdateHintTooltip();
        float k = HintPulseLevel;
        if (hintedArrow != null) hintedArrow.color = Color.Lerp(arrowColor, UIPalette.Current.accent, 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * 2f * Mathf.PI / hintPulseSeconds)); // its own colour, pulsing to the accent and back
        TurnJunctions();
        UpdateBorders(k);
        foreach (var (g, kind) in pulsing)
        {
            if (g == null) continue;
            if (kind == Pulse.Ghost) g.color = GhostColor(k);
            else if (kind == Pulse.Frame) g.color = Color.Lerp(cellColor, HintColor, k);
            else if (kind == Pulse.Junction) g.color = Color.Lerp(cellColor, HintColor, junctionGhostStrength * k); // the cycling junction ghost
            else { var c = g.color; c.a = k; g.color = c; }
        }
    }

    // a layer above the tiles and rings, below the objects; it never takes the mouse
    void MakeHintsRoot()
    {
        if (hintsRoot != null) { UIBuilder.Clear(hintsRoot); return; }
        var go = new GameObject("Hints", typeof(RectTransform), typeof(CanvasGroup));
        hintsRoot = (RectTransform)go.transform;
        hintsRoot.SetParent(objectsRoot.parent, false);
        hintsRoot.anchorMin = objectsRoot.anchorMin;
        hintsRoot.anchorMax = objectsRoot.anchorMax;
        hintsRoot.pivot = objectsRoot.pivot;
        hintsRoot.anchoredPosition = objectsRoot.anchoredPosition;
        hintsRoot.sizeDelta = objectsRoot.sizeDelta;
        hintsRoot.localScale = objectsRoot.localScale;
        hintsRoot.SetSiblingIndex(objectsRoot.GetSiblingIndex());
        var group = go.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    // an empty box the size of a square, on it
    RectTransform Square(string name, Vector2Int p)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(hintsRoot, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = CellPos(p);
        rt.sizeDelta = Vector2.one * (CellSize - CellGap);
        return rt;
    }

    // the hover area of a ring (its tip), nothing drawn
    RectTransform RingArea(Vector2 pos)
    {
        var rt = (RectTransform)new GameObject("RingArea", typeof(RectTransform)).transform;
        rt.SetParent(hintsRoot, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = Vector2.one * CellSize * ringSize;
        return rt;
    }

    // the objects in a delivery as icons, "+" between them: [Red] + [Blue]
    List<(string text, Color color)> Who(List<(string objectName, int number)> badges, int number)
    {
        var res = new List<(string, Color)>();
        foreach (var (name, n) in badges)
        {
            var spec = n == number ? level.objects.FirstOrDefault(o => o.name == name) : null;
            if (spec == null) continue;
            if (res.Count > 0) res.Add(("+", default));
            res.Add((null, spec.color));
        }
        return res;
    }

    // "Deliver [Red] + [Blue] together first", "Deliver [Green], after [Red] + [Blue]"
    List<(string text, Color color)> RingTip(List<(string objectName, int number)> badges, int number)
    {
        bool together = badges.Count(b => b.number == number) >= 2;
        var tip = new List<(string, Color)> { ("Deliver", default) };
        tip.AddRange(Who(badges, number));
        if (number == 1) tip.Add((together ? "together first" : "first", default));
        else
        {
            tip.Add((together ? "together, after" : "after", default));
            tip.AddRange(Who(badges, number - 1));
        }
        return tip;
    }

    // the tooltip of the stage 2 mark under the mouse, if any (Settings: Hint tips)
    void UpdateHintTooltip()
    {
        if (hintTooltip == null) return;
        var pointer = UnityEngine.InputSystem.Pointer.current;
        if (pointer == null || hintAreas.Count == 0 || !Session.showHintTips) { hintTooltip.Hide(); return; }
        Vector2 at = pointer.position.ReadValue();
        var canvas = cellsRoot.GetComponentInParent<Canvas>().rootCanvas;
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!OnGameScreen(at, canvas.transform)) { hintTooltip.Hide(); return; }
        foreach (var (area, tip) in hintAreas)
            if (area != null && RectTransformUtility.RectangleContainsScreenPoint(area, at, cam))
            {
                hintTooltip.Show(tip, objectPrefab, at);
                return;
            }
        hintTooltip.Hide();
    }

    // nothing over the board at this point: the top thing under the mouse is in the board's own screen, not a window
    // above it (the answer's check, a menu, Settings)
    bool OnGameScreen(Vector2 at, Transform canvas)
    {
        var events = UnityEngine.EventSystems.EventSystem.current;
        if (events == null) return true;
        var screen = cellsRoot.transform;
        while (screen.parent != null && screen.parent != canvas) screen = screen.parent;
        var hits = new List<UnityEngine.EventSystems.RaycastResult>();
        events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events) { position = at }, hits);
        var guide = TutorialOverlay.HoverHints; // a guide step that lets tips through: look past its dim and blocker
        if (guide != null)
            hits.RemoveAll(h => h.gameObject.transform.IsChildOf(guide.transform) && !guide.IsBubble(h.gameObject.transform));
        return hits.Count == 0 || hits[0].gameObject.transform.IsChildOf(screen);
    }

    // a small square (Hint-colored unless color says otherwise) with a white number, centered at pos
    RectTransform Badge(Vector2 pos, int number, Color? color = null, bool pulse = true, float size = -1f)
    {
        float share = size > 0f ? size : hintBadgeSize;
        var back = UIBuilder.NewImage("Badge", hintsRoot, color ?? HintColor);
        var rt = back.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.one * CellSize * share;
        rt.anchoredPosition = pos;
        var text = Number(rt, number, CellSize * share * 0.7f);
        if (pulse)
        {
            pulsing.Add((back, Pulse.Fade));
            pulsing.Add((text, Pulse.Fade));
        }
        return rt;
    }

    // a white bold number filling box
    static TextMeshProUGUI Number(RectTransform box, int number, float size) => UIBuilder.BadgeLabel(box, number.ToString(), size);

    // the colours of the objects delivered at a junction's number, in level order (none found: the Hint colour)
    List<Color> JunctionColors(List<(string objectName, int number)> badges, int number)
    {
        var res = new List<Color>();
        foreach (var o in level.objects) if (badges.Any(b => b.objectName == o.name && b.number == number)) res.Add(o.color);
        if (res.Count == 0) res.Add(HintColor);
        return res;
    }

    // a frame just inside a square's edge in the colours of the objects meeting there: many short pieces around the
    // square, each coloured by its place along the edge (from the top middle, clockwise). still: one part per colour
    // (two: right half, left half; four: a quarter each, meeting at the side middles). UpdateBorders colours them
    [Header("Junction frames")]
    [Tooltip("How strong the frame's colours are: from the square's colour (0) to the objects' own (1). The ghost uses Hint Ghost Strength the same way")]
     [System.NonSerialized] public float junctionBorderStrength = 0.6f;
    [Tooltip("A junction whose objects are already delivered: its frame this much as strong (0 = gone, 1 = as before)")]
     [System.NonSerialized] public float junctionDoneStrength = 0.35f;
    readonly List<(Graphic g, float t, List<Color> colors, float strength)> borderPieces = new();

    RectTransform Frame(Vector2Int p, List<Color> colors, float strength = 1f)
    {
        var box = Square("Frame", p);
        float s = CellSize - CellGap, w = hintFrameWidth, total = 4f * s;
        int n = colors.Count;
        // the sides in walking order from the top middle, each with its stretch of the walk (a, b). the top and bottom bars
        // run the full width (corners included); the side bars fit between them, so no two pieces overlap (the hints
        // layer fades each piece on its own, and an overlap would show darker)
        var sides = new (Vector2 from, Vector2 dir, float a, float b, float trim)[]
        {
            (new Vector2(0f, s / 2 - w / 2), Vector2.right, 0f, s / 2, 0f),
            (new Vector2(s / 2 - w / 2, s / 2), Vector2.down, s / 2, 1.5f * s, w),
            (new Vector2(s / 2, -s / 2 + w / 2), Vector2.left, 1.5f * s, 2.5f * s, 0f),
            (new Vector2(-s / 2 + w / 2, -s / 2), Vector2.up, 2.5f * s, 3.5f * s, w),
            (new Vector2(-s / 2, s / 2 - w / 2), Vector2.right, 3.5f * s, 4f * s, 0f),
        };
        foreach (var (from, dir, a, b, trim) in sides)
        {
            // cut where the colour changes (each colour has an equal share of the walk)
            var cuts = new List<float> { a, b };
            for (int c = 1; c < n; c++) { float at = c * total / n; if (at > a && at < b) cuts.Add(at); }
            cuts.Sort();
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                float lo = cuts[i] - a, hi = cuts[i + 1] - a, len = b - a;
                if (trim > 0f) { lo = Mathf.Max(lo, trim); hi = Mathf.Min(hi, len - trim); } // the side bars stop at the top and bottom bars
                if (hi - lo <= 0.01f) continue;
                var img = UIBuilder.NewImage("Bar", box, colors[0]);
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = from + dir * ((lo + hi) / 2f);
                rt.sizeDelta = dir.y == 0f ? new Vector2(hi - lo, w) : new Vector2(w, hi - lo);
                img.raycastTarget = false;
                borderPieces.Add((img, (a + (cuts[i] - a + cuts[i + 1] - a) / 2f) / total, colors, strength));
            }
        }
        return box;
    }

    // each frame piece's colour now: its object's part of the frame, softened toward the square's colour and pulsing like
    // the ghosts
    void UpdateBorders(float k)
    {
        if (borderPieces.Count == 0) return;
        for (int i = borderPieces.Count - 1; i >= 0; i--)
        {
            var (g, t, colors, strength) = borderPieces[i];
            if (g == null) { borderPieces.RemoveAt(i); continue; }
            int n = colors.Count, idx = Mathf.Min(n - 1, (int)Mathf.Floor(t * n));
            g.color = Color.Lerp(cellColor, colors[idx], junctionBorderStrength * strength * (strength < 1f ? 1f : k)); // a done junction's frame stays still
        }
    }

    // a faint ghost on an empty junction square, cycling through every junction tile (+, the four T turns, the bridge),
    // all junctions together: "a junction goes here", without saying which (Update turns them)
    static readonly TileType[] JunctionTiles = { TileType.Cross, TileType.TeeNES, TileType.TeeESW, TileType.TeeSWN, TileType.TeeWNE, TileType.Bridge };
     [System.NonSerialized] public float junctionCycleSeconds = 1.5f;
     [System.NonSerialized] public float junctionGhostStrength = 0.3f; // its colour, from the square's (0) to Hint (1)
    readonly List<TileView> cycling = new List<TileView>();
    int cycleShown = -1;

    void JunctionCycle(Vector2Int p)
    {
        var cell = Cells[p.x, p.y];
        if (cell?.view == null || cell.tile != null) return; // a tile already there: nothing to show under it
        var t = Instantiate(tilePrefab, cell.view.transform);
        t.transform.SetAsFirstSibling();
        var rt = (RectTransform)t.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        foreach (var img in t.GetComponentsInChildren<Image>(true)) { img.raycastTarget = false; pulsing.Add((img, Pulse.Ghost)); } // the same look and pulse as stage 2's ghosts
        cycling.Add(t);
        squareGhosts.Add(t.gameObject); // goes with the other hints
        cycleShown = -1;
    }

    void TurnJunctions()
    {
        cycling.RemoveAll(t => t == null);
        if (cycling.Count == 0) return;
        int i = (int)(Time.unscaledTime / junctionCycleSeconds) % JunctionTiles.Length;
        if (i == cycleShown) return;
        cycleShown = i;
        var color = GhostColor(1f); // as stage 2's ghosts (the pulse takes over each frame)
        foreach (var t in cycling) t.Show(JunctionTiles[i], color);
    }

}
