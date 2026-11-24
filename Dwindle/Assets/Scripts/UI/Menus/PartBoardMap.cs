using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the part map as a small 3x3 board: a road through every square (the game's tile art), an object at how far the
// player has come, and a ring outside the board after the last part. a part can take more than one square
public class PartBoardMap : PartMapView
{
    [Header("Board")]
    public TileView tilePrefab;          // the game's tile (BoardView's)
    public ObjectView objectPrefab;      // the game's object (BoardView's)
    public DestinationView ringPrefab;   // the game's ring (BoardView's)
    public TMP_Text textTemplate;        // copied for the part names and stars
    public Button buttonStyle;           // its hover and press colors are copied (the level card's button)
    public float cellSize = 170f;
    public float cellGap = 8f;
    public float boardY = -40f;          // the board's middle, from the screen's middle
    [Tooltip("The whole board bigger or smaller: squares, texts, roads, object, ring, dots (in Play mode, changes show right away)")]
    [Range(0.3f, 2f)] public float boardScale = 1f;
    [Tooltip("Moves the whole board, in pixels (in Play mode, changes show right away)")]
    public Vector2 boardOffset;
    public Color cellColor = new Color(0.32f, 0.33f, 0.37f);
    [Tooltip("The road not traveled yet")]
    public Color roadColor = new Color(0.62f, 0.64f, 0.70f);
    [Tooltip("The road the object has traveled (the parts finished); the accent color when made in code")]
    public Color traveledColor = new Color(0.49f, 0.51f, 0.97f);
    [Range(0f, 1f)] public float roadAheadAlpha = 0.25f; // how much a locked part's road shows on its square
    [Range(0f, 1f)] public float lockedAlpha = 0.45f;    // a locked part's squares
    public Color ballColor = new Color(0.49f, 0.51f, 0.97f);
    [Tooltip("The ring's inner circle (the page background when made in code)")]
    public Color ringHoleColor = new Color(0.125f, 0.133f, 0.153f);
    [Tooltip("The road through the squares, the object and the ring")]
    public bool showRoads = true, showBall = true;
    [Tooltip("Off: the object just stands still. On: it walks to new progress, and to a tapped part with Unlock all")]
    public bool walk = true;
    public float secondsPerSquare = 0.22f;
    [Tooltip("Farther than this many squares, the object jumps instead of walking")]
    [Min(0)] public int maxWalkSquares = 2;

    [Header("Object and ring (in Play mode, changes show right away)")]
    [Tooltip("The object's size, as a share of a square")]
    [Range(0.1f, 1f)] public float ballSize = 0.4f;
    [Tooltip("The ring's size, as a share of a square")]
    [Range(0.1f, 1f)] public float ringSize = 0.5f;
    [Tooltip("How far outside the board the start and the ring sit, in squares (1 = a whole square out, 0.5 = half)")]
    [Range(0f, 1.5f)] public float outsideDistance = 1f;
    [Tooltip("The dotted lines from the start into the board and from the board out to the ring, like a level's")]
    [Header("Part names and stars (in Play mode, changes show right away)")]
    [Range(0.05f, 0.4f)] public float nameSize = 0.17f;   // as a share of a square
    [Range(0.05f, 0.4f)] public float starsSize = 0.13f;
    public Color nameColor = Color.white;
    public Color starsColor = Color.white;
    [Tooltip("How far locked parts' texts fade toward the background (0 = gone, 1 = like open ones)")]
    [Range(0f, 1f)] public float lockedTextAlpha = 0.55f;

    public bool showDots = true;
    [Range(0.02f, 0.3f)] public float dotSize = 0.08f;   // as a share of a square
    [Range(0.02f, 0.3f)] public float dotGap = 0.06f;    // between dots, as a share of a square
    [Range(0f, 1f)] public float dotOpacity = 0.75f;
    public string saveKey = "Dwindle.mapBall";

    [Header("Layout")]
    [Tooltip("The board's squares in road order (x, y from the bottom left)")]
    public Vector2Int[] order =
    {
        new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2),
        new Vector2Int(2, 1), new Vector2Int(1, 1), new Vector2Int(0, 1),
        new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
    };
    [Tooltip("Which part each square in Order belongs to (0 = the first part). A part with two squares shows its name " +
             "on the last one; the other only shares its look and lock")]
    public int[] owners = { 0, 1, 2, 3, 4, 5, 6, 6, 7 };

    class Square { public RectTransform rect; public Image bg; public Button button; public TileView road; public TMP_Text name, stars; public int part; public bool main, open = true; }
    readonly List<Square> squares = new List<Square>(); // one per square in Order, in road order
    Vector2Int startCell, ringCell;
    ObjectView ball;
    DestinationView ringView;
    RectTransform root;
    int ballAt = -1, target = -1; // a square in road order; -1 = the start outside, squares.Count = the ring
    Action<int> pick;
    Coroutine walking;

    Vector2 Pos(Vector2Int c) => new Vector2((c.x - 1) * (cellSize + cellGap), (c.y - 1) * (cellSize + cellGap) + boardY);

    public override void Build(IList<string> parts, Action<int> onPick)
    {
        pick = onPick;
        if (root != null) Destroy(root.gameObject);
        squares.Clear();
        root = new GameObject("Board", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(transform, false);
        ApplyScale();

        int count = Mathf.Min(order.Length, owners.Length);
        for (int i = 0; i < count; i++)
        {
            int part = owners[i];
            if (part < 0 || part >= parts.Count) continue;
            bool main = true;
            for (int k = i + 1; k < count; k++) if (owners[k] == part) main = false; // the name goes on its last square
            squares.Add(MakeSquare(order[i], part, main ? parts[part] : null));
        }
        if (squares.Count == 0) return;
        startCell = Cell(0) + Vector2Int.left;                         // just left of the first square
        ringCell = Cell(squares.Count - 1) + Step(squares.Count - 1);  // just past the last square, on its way out
        for (int i = 0; i < squares.Count; i++)
            if (squares[i].road != null) squares[i].road.Show(RoadType(i), roadColor);

        DrawDots();
        if (!showBall) return;
        if (ringPrefab != null)
        {
            var ring = ringView = Instantiate(ringPrefab, root);
            ring.name = "Ring";
            ring.Setup(ballColor, cellSize * ringSize);
            ring.hole.color = ringHoleColor;
            ((RectTransform)ring.transform).anchoredPosition = RingPos();
            foreach (var g in ring.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
        }
        ball = Instantiate(objectPrefab, root);
        ball.name = "Ball";
        ball.Setup(ballColor, cellSize * ballSize);
        foreach (var g in ball.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
        PlaceBall(walk ? PlayerPrefs.GetInt(Session.SaveKey(saveKey), -1) : -1);
    }

    Vector2Int Cell(int i) => Vector2Int.RoundToInt(new Vector2((squares[i].rect.anchoredPosition.x / (cellSize + cellGap)) + 1,
                                                                (squares[i].rect.anchoredPosition.y - boardY) / (cellSize + cellGap) + 1));

    Square MakeSquare(Vector2Int cell, int part, string title)
    {
        var s = new Square { part = part, main = title != null };
        var bg = MakeImage(title != null ? "Part " + title : "Part " + part + " (more)", root, cellColor);
        s.rect = bg.rectTransform;
        s.rect.sizeDelta = Vector2.one * cellSize;
        s.rect.anchoredPosition = Pos(cell);
        s.bg = bg;
        if (s.main)
        {
            s.button = bg.gameObject.AddComponent<Button>();
            s.button.targetGraphic = bg;
            if (buttonStyle != null) { s.button.transition = buttonStyle.transition; s.button.colors = buttonStyle.colors; s.button.spriteState = buttonStyle.spriteState; }
            var cb = s.button.colors; cb.disabledColor = cb.normalColor; s.button.colors = cb; // locked squares get their own solid color instead
            var hoverStyle = buttonStyle != null ? buttonStyle.GetComponent<ButtonHover>() : null; // the same hover as the level cards
            var hover = bg.gameObject.AddComponent<ButtonHover>();
            if (hoverStyle != null) { hover.lighten = hoverStyle.lighten; hover.fadeSeconds = hoverStyle.fadeSeconds; }
            s.button.onClick.AddListener(() => Tapped(part));
        }
        else bg.raycastTarget = false; // a part's extra square: no click, no hover

        if (showRoads)
        {
            s.road = Instantiate(tilePrefab, s.rect);
            var rr = (RectTransform)s.road.transform;
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;
            foreach (var g in s.road.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
        }
        if (s.main)
        {
            // with a road the texts keep clear of its middle; without one they sit in the middle
            s.name = showRoads ? MakeText("Name", s.rect, title, 0.62f, 1f, nameSize) : MakeText("Name", s.rect, title, 0.48f, 0.85f, nameSize);
            s.stars = showRoads ? MakeText("Stars", s.rect, "", 0f, 0.36f, starsSize) : MakeText("Stars", s.rect, "", 0.15f, 0.45f, starsSize);
        }
        return s;
    }

    // the road through a square: in from the one before (the start for the first), out to the next (the ring)
    TileType RoadType(int i)
    {
        Vector2Int from = i == 0 ? startCell : Cell(i - 1), to = i == squares.Count - 1 ? ringCell : Cell(i + 1);
        var a = DirTo(Cell(i), from); var b = DirTo(Cell(i), to);
        foreach (TileType t in Enum.GetValues(typeof(TileType)))
            if (t != TileType.Bridge && Tiles.Has(t, a) && Tiles.Has(t, b) && Arms(t) == 2) return t;
        return TileType.StraightH;
    }
    static int Arms(TileType t) { int k = 0; foreach (var d in DirUtil.All) if (Tiles.Has(t, d)) k++; return k; }
    Vector2Int Step(int i) => i + 1 < squares.Count ? Cell(i + 1) - Cell(i) : (i > 0 ? Cell(i) - Cell(i - 1) : Vector2Int.right);
    static Dir DirTo(Vector2Int from, Vector2Int to)
    {
        var d = to - from;
        return d.x > 0 ? Dir.E : d.x < 0 ? Dir.W : d.y > 0 ? Dir.N : Dir.S;
    }

    // a part's stars and lock; its extra squares fade with it
    public override void Set(int part, int got, int max, bool open)
    {
        foreach (var s in squares)
        {
            if (s.part != part) continue;
            s.open = open;
            s.bg.color = SquareColor(open);
            var border = s.bg.GetComponent<Outline>();
            if (border == null) border = s.bg.gameObject.AddComponent<Outline>();
            border.enabled = UIPalette.LightMode || UIPalette.HighContrast;
            border.effectColor = UIPalette.Current.outline;
            border.effectDistance = Vector2.one * (open ? 2f : 1f);
            if (s.name != null) s.name.color = TextColor(nameColor, open);
            if (s.stars != null) s.stars.color = TextColor(starsColor, open);
            if (!s.main) continue;
            s.button.interactable = open;
            s.stars.text = open ? starsText.Replace("{got}", got.ToString()).Replace("{max}", max.ToString()) : lockedText;
        }
    }

    // part: the part being played (parts count once all are done). roads are full up to its last square; the object
    // stands at the end of the parts already finished: the start before any, the ring after the last
    public override void SetCurrent(int part)
    {
        int done = -1; // the last square of the parts already finished
        for (int i = 0; i < squares.Count; i++)
            if (squares[i].part < part) done = i;
        target = done >= squares.Count - 1 ? squares.Count : done;
        ballColor = traveledColor = UIPalette.Current.accent;
        if (ball != null) ball.body.color = ballColor;
        if (ringView != null) { ringView.ring.color = ballColor; ringView.hole.color = UIPalette.Current.background; }
        if (ringView != null) ringView.SetDelivered(target == squares.Count); // filled once every part is done
        if (ball == null) { Recolor(done); return; }
        if (!walk) PlaceBall(target);
        else if (!Session.unlockAll) GoToTarget(); // with Unlock all it stays where it was tapped to
        Recolor(ballAt);
    }

    // the road up to square upTo (the object's square) in the traveled color, the rest open or dim
    void Recolor(int upTo)
    {
        for (int i = 0; i < squares.Count; i++)
        {
            // solid colors (see-through pieces would show where they overlap): locked roads blend into the square
            var c = i <= upTo ? traveledColor : Color.Lerp(SquareColor(squares[i].open), roadColor, squares[i].open ? 1f : roadAheadAlpha);
            c.a = 1f;
            if (squares[i].road != null) squares[i].road.Show(RoadType(i), c);
        }
    }

    void OnEnable() { if (walk && ball != null && !Session.unlockAll) GoToTarget(); }

    // new progress past it: it walks forward (once on screen). on a part that is locked now (Unlock all just turned off):
    // it is simply back at the progress. otherwise it stays on the part last opened
    void GoToTarget()
    {
        bool onLocked = ballAt >= 0 && ballAt < squares.Count && !squares[ballAt].open || ballAt == squares.Count && target < ballAt;
        if (onLocked) { if (walking != null) { StopCoroutine(walking); walking = null; } PlaceBall(target); PlayerPrefs.SetInt(Session.SaveKey(saveKey), ballAt); }
        else if (target > ballAt && isActiveAndEnabled) WalkTo(target, null);
    }

    // a tap: the object walks into the part (forward or back along the road), then the part opens
    void Tapped(int part)
    {
        int square = squares.FindLastIndex(s => s.part == part);
        if (walk && ball != null && square != ballAt) WalkTo(square, () => pick?.Invoke(part)); // it goes into the part first
        else pick?.Invoke(part);
    }

    // a short way (Max Walk Squares or fewer) it walks; farther it jumps straight there
    void WalkTo(int to, Action then)
    {
        if (walking != null) { StopCoroutine(walking); walking = null; }
        if (Mathf.Abs(to - ballAt) > maxWalkSquares)
        {
            PlaceBall(to);
            PlayerPrefs.SetInt(Session.SaveKey(saveKey), ballAt);
            then?.Invoke();
            return;
        }
        walking = StartCoroutine(Walk(to, then));
    }

    IEnumerator Walk(int to, Action then)
    {
        int step = to > ballAt ? 1 : -1;
        while (ballAt != to)
        {
            Vector2 a = Spot(ballAt), b = Spot(ballAt + step);
            ball.Face(DirTo(Vector2Int.zero, Vector2Int.RoundToInt((b - a).normalized)));
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / secondsPerSquare)
            {
                ball.Rect.anchoredPosition = Vector2.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            PlaceBall(ballAt + step);
        }
        PlayerPrefs.SetInt(Session.SaveKey(saveKey), ballAt);
        walking = null;
        then?.Invoke();
    }

    Vector2 Spot(int i) => i < 0 ? StartPos() : i >= squares.Count ? RingPos() : Pos(Cell(i));

    // the start and the ring: outsideDistance squares out from the first and last squares, plus their nudges
    Vector2 StartPos() => Vector2.Lerp(Pos(Cell(0)), Pos(startCell), outsideDistance);
    Vector2 RingPos() => Vector2.Lerp(Pos(Cell(squares.Count - 1)), Pos(ringCell), outsideDistance);

    // dots from the start to the first square's edge, and from the last square's edge to the ring (like a level's)
    RectTransform dots;
    void DrawDots()
    {
        if (dots != null) Destroy(dots.gameObject);
        dots = null;
        if (!showDots || squares.Count == 0) return;
        dots = new GameObject("Dots", typeof(RectTransform)).GetComponent<RectTransform>();
        dots.SetParent(root, false);
        dots.SetSiblingIndex(0);
        Vector2 first = Pos(Cell(0)), last = Pos(Cell(squares.Count - 1));
        Dotted(StartPos(), first + (StartPos() - first).normalized * cellSize / 2f);
        Dotted(RingPos(), last + (RingPos() - last).normalized * cellSize / 2f);
    }
    void Dotted(Vector2 end, Vector2 edge)
    {
        float dot = cellSize * dotSize, step = dot + cellSize * dotGap, length = Vector2.Distance(end, edge);
        var dir = (end - edge) / Mathf.Max(length, 0.01f);
        var color = ballColor; color.a *= dotOpacity;
        for (float t = dot / 2f; t <= length - dot / 2f; t += step) // from the board's edge out
        {
            var d = MakeImage("Dot", dots, color);
            d.raycastTarget = false;
            d.rectTransform.sizeDelta = Vector2.one * dot;
            d.rectTransform.anchoredPosition = edge + dir * t;
        }
    }

    // Reset progress: back to the start, nothing traveled
    public override void Forget()
    {
        PlayerPrefs.DeleteKey(Session.SaveKey(saveKey));
        if (walking != null) { StopCoroutine(walking); walking = null; }
        target = -1;
        if (ball != null) PlaceBall(-1); else Recolor(-1);
        if (ringView != null) ringView.SetDelivered(false);
    }

    // Inspector changes in Play mode: resize and move the object and the ring at once
    void OnValidate()
    {
        if (!Application.isPlaying || squares.Count == 0) return;
        ApplyScale();
        foreach (var s in squares)
        {
            if (s.name != null) { s.name.fontSize = cellSize * nameSize; s.name.color = TextColor(nameColor, s.open); }
            if (s.stars != null) { s.stars.fontSize = cellSize * starsSize; s.stars.color = TextColor(starsColor, s.open); }
        }
        if (ringView != null) { ringView.Setup(ballColor, cellSize * ringSize); ringView.hole.color = ringHoleColor; ((RectTransform)ringView.transform).anchoredPosition = RingPos(); ringView.SetDelivered(target == squares.Count); }
        if (ball != null && walking == null) { ball.Setup(ballColor, cellSize * ballSize); PlaceBall(ballAt); }
        if (root != null) DrawDots();
    }

    // stands on square i, facing along the road
    void PlaceBall(int i)
    {
        i = Mathf.Clamp(i, -1, squares.Count);
        ballAt = i;
        ball.Rect.anchoredPosition = Spot(i);
        var ahead = i < squares.Count ? Spot(i + 1) : Spot(i) + (Vector2)Step(squares.Count - 1);
        ball.Face(DirTo(Vector2Int.zero, Vector2Int.RoundToInt((ahead - Spot(i)).normalized)));
        ball.transform.SetAsLastSibling();
        Recolor(ballAt); // each square turns the traveled color as the object reaches it
    }

    // a locked square: its color mixed toward the background, solid
    Color SquareColor(bool open)
    {
        var p = UIPalette.Current;
        var c = UIPalette.LightMode ? (open ? p.button : Color.Lerp(p.panel, p.button, .65f))
            : open ? cellColor : Color.Lerp(p.background, cellColor, lockedAlpha);
        c.a = 1f; return c;
    }
    Color TextColor(Color c, bool open)
    {
        var ink = UIPalette.LightMode ? (open ? UIPalette.Current.text : UIPalette.Current.mutedText)
            : open ? c : Color.Lerp(UIPalette.Current.background, c, lockedTextAlpha);
        return UIPalette.EnsureContrast(ink, SquareColor(open), UIPalette.HighContrast ? 7f : 4.5f);
    }

    // the whole board scaled around its middle, and moved
    void ApplyScale()
    {
        if (root == null) return;
        root.localScale = Vector3.one * boardScale;
        root.anchoredPosition = new Vector2(0f, boardY) * (1f - boardScale) + boardOffset;
    }

    // new part names (the Part Text format changed)
    public override void Rename(IList<string> names)
    {
        foreach (var s in squares) if (s.name != null && s.part < names.Count) s.name.text = names[s.part];
    }

    static Image MakeImage(string name, Transform parent, Color color)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = color;
        return img;
    }

    // a copy of the text template in a band of the square (fractions of its height), sized to the square
    TMP_Text MakeText(string name, RectTransform parent, string text, float bottom, float top, float size)
    {
        var t = Instantiate(textTemplate, parent);
        t.name = name;
        t.gameObject.SetActive(true);
        t.text = text;
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = false;
        t.fontSize = cellSize * size;
        t.raycastTarget = false;
        var r = t.rectTransform;
        r.anchorMin = new Vector2(0f, bottom); r.anchorMax = new Vector2(1f, top);
        r.offsetMin = new Vector2(6f, 0f); r.offsetMax = new Vector2(-6f, 0f);
        return t;
    }
}
