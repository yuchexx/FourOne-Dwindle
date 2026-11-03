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
    [Range(0.1f, 0.6f)] public float hintBadgeSize = 0.3f;         // a number's square, as a share of a square
    [Min(1f)] public float hintFrameWidth = 4f;                    // the frame on a square where a group arrives
    [Range(0f, 1f)] public float hintGhostStrength = 0.6f;         // a ghost's color, from the square's (0) to Hint (1)
    [Tooltip("The hints slowly fade in and out (the stage 1 line too)")]
    public bool hintPulse = true;
    [Min(0.2f)] public float hintPulseSeconds = 1.6f;
    [Range(0f, 1f)] public float hintPulseDepth = 0.35f;           // how far it fades
    [Tooltip("Optional: says what a stage 2 mark means while the mouse is over it")]
    public HintTooltip hintTooltip;

    // stage 2's marks (a ring's number, a frame) and what each one's tooltip says
    readonly List<(RectTransform area, List<(string text, Color color)> tip)> hintAreas = new();

    RectTransform hintsRoot;
    enum Pulse { Ghost, Frame, Fade }
    readonly List<(Graphic g, Pulse kind)> pulsing = new List<(Graphic, Pulse)>();
    Graphic hintedArrow; // the plan's arrow while the game asks for a way (stage 3): it pulses in the Hint color

    static Color HintColor => UIPalette.Current.hint;

    // 1 at full, down to 1 - depth: for anything else that pulses with the hints (HintButton's lines)
    public float HintPulseLevel =>
        hintPulse ? 1f - hintPulseDepth * (0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * 2f * Mathf.PI / hintPulseSeconds)) : 1f;

    // replaces whatever hints were drawn
    public void ShowHints(List<(string objectName, int number)> badges, List<(Vector2Int square, int number)> junctions,
                          List<(Vector2Int square, TileType tile, int number, StepState state)> ghosts)
    {
        MakeHintsRoot();
        pulsing.Clear();
        hintAreas.Clear();
        if (hintTooltip != null) hintTooltip.Hide();
        foreach (var g in squareGhosts) if (g != null) Destroy(g);
        squareGhosts.Clear();
        if (level == null) return;
        float half = (CellSize - CellGap) / 2f, r = CellSize * hintBadgeSize / 2f;
        var upperLeft = new Vector2(-half + r + hintFrameWidth, half - r - hintFrameWidth);

        foreach (var (name, number) in badges)
        {
            var spec = level.objects.FirstOrDefault(o => o.name == name);
            if (spec != null) hintAreas.Add((RingBadge(OutsidePos(spec.dest), number), RingTip(badges, number)));
        }

        foreach (var (p, number) in junctions)
        {
            var box = Frame(p);
            var corner = Badge(CellPos(p) + upperLeft, number);
            hintAreas.Add((corner, RingTip(badges, number))); // its number says what the rings' do (checked first)
            var tip = Who(badges, number);
            tip.Add(("meet here", default));
            hintAreas.Add((box, tip));
        }

        // a step done right: just its number, in the accent color, still. the right tile at the wrong moment: its
        // number, pulsing. otherwise (empty, another tile): a ghost of its tile under the tiles, and its number.
        foreach (var (p, tile, number, state) in ghosts)
        {
            if (state == StepState.ToDo) GhostInSquare(p, tile);
            if (state == StepState.Done) Badge(CellPos(p) + upperLeft, number, UIPalette.Current.accent, pulse: false);
            else Badge(CellPos(p) + upperLeft, number);
        }
    }

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
        if (hintedArrow != null) hintedArrow.color = Color.Lerp(HintColor, Color.white, (1f - k) * 0.8f); // purple, fading lighter and back
        foreach (var (g, kind) in pulsing)
        {
            if (g == null) continue;
            if (kind == Pulse.Ghost) g.color = GhostColor(k);
            else if (kind == Pulse.Frame) g.color = Color.Lerp(cellColor, HintColor, k);
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

    // stage 2's number in a ring: a Hint-colored circle filling the ring's hole, pulsing, with a white number
    RectTransform RingBadge(Vector2 pos, int number)
    {
        var hole = destinationPrefab.hole.rectTransform;
        float size = CellSize * ringSize * (hole.anchorMax.x - hole.anchorMin.x);
        var circle = UIBuilder.Circle(objectPrefab, hintsRoot, HintColor, size);
        circle.Rect.anchoredPosition = pos;
        pulsing.Add((circle.body, Pulse.Fade));
        pulsing.Add((Number(circle.Rect, number, size * 0.62f), Pulse.Fade));
        return circle.Rect;
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
        return hits.Count == 0 || hits[0].gameObject.transform.IsChildOf(screen);
    }

    // a small square (Hint-colored unless color says otherwise) with a white number, centered at pos
    RectTransform Badge(Vector2 pos, int number, Color? color = null, bool pulse = true)
    {
        var back = UIBuilder.NewImage("Badge", hintsRoot, color ?? HintColor);
        var rt = back.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.one * CellSize * hintBadgeSize;
        rt.anchoredPosition = pos;
        var text = Number(rt, number, CellSize * hintBadgeSize * 0.7f);
        if (pulse)
        {
            pulsing.Add((back, Pulse.Fade));
            pulsing.Add((text, Pulse.Fade));
        }
        return rt;
    }

    // a white bold number filling box
    static TextMeshProUGUI Number(RectTransform box, int number, float size) => UIBuilder.BadgeLabel(box, number.ToString(), size);

    // four bars just inside a square's edge
    RectTransform Frame(Vector2Int p)
    {
        var box = Square("Frame", p);
        float w = hintFrameWidth;
        Bar(box, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, w));
        Bar(box, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -w), new Vector2(0, 0));
        Bar(box, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, w), new Vector2(w, -w));
        Bar(box, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-w, w), new Vector2(0, -w));
        return box;
    }

    void Bar(RectTransform box, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        var img = UIBuilder.NewImage("Bar", box, HintColor);
        var rt = img.rectTransform;
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        pulsing.Add((img, Pulse.Frame));
    }

}
