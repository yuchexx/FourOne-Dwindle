using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// for the tutorial (a coach in Tutorials.cs, through GameManager.Tutorial.cs): guide lines along the roads it wants
// built, and squares closed to tiles. all of it goes when the board is built again (Build). part of BoardView
public partial class BoardView
{
    [Header("Tutorial guides")]
    public Color closedCellColor = new Color(0.55f, 0.56f, 0.60f); // a closed square, dimmed
    public Color closedMarkColor = new Color(0.22f, 0.22f, 0.25f); // the big X on it
    [Range(0.2f, 1f)] public float closedMarkSize = 0.75f;     // the X's arms, as a share of a square
    [Range(0.02f, 0.3f)] public float closedMarkWidth = 0.1f;  // their thickness, as a share of a square

    readonly List<(ObjectSpec owner, GameObject line)> guideLines = new();
    // a guide line's piece in one square: hidden while a tile is there or previewed there, so the tile covers it
    readonly List<(ObjectSpec owner, Vector2Int square, Cell cell, GameObject piece)> guidePieces = new();
    readonly Dictionary<Vector2Int, GameObject> closedMarks = new();

    // each door's bar and its letter's badge, in the level's order (MakeDoor), for Target.Door
    readonly List<(DoorSpec door, RectTransform bar, RectTransform badge)> doorViews = new();

    // a door's bar, or the badge with its letter (which reaches past the bar's sides), by its letter and which end of
    // the pair (0 = the first in Levels.cs); null when there's no such door
    public RectTransform DoorPart(string label, int which, bool badge)
    {
        foreach (var (door, bar, letterBadge) in doorViews)
            if (door.label == label && which-- == 0) return badge ? letterBadge : bar;
        return null;
    }

    // the middle of the edge between two squares next to each other (a start or a ring and a board's square, say)
    public Vector2 EdgeBetween(Vector2Int a, Vector2Int b) => EdgePos(a, Toward(b - a));

    // a stretch of an object's road, in its color: from `from` (a board position: the board's edge at its start, or a
    // door it comes out of) through the given squares (each next to the one before) to `to` (the board's edge at its
    // ring, or a door it goes into), drawn like the lines from the start and to the ring (End Line Dot Size, Gap,
    // Opacity, Dotted), turning in the middle of a square. on the markers layer, under the rings, doors and objects.
    // it goes once the object is delivered (MarkDelivered), as its road's tile breaks then
    public void GuideLine(ObjectSpec owner, Vector2 from, IList<Vector2Int> squares, Vector2 to)
    {
        var objectColor = owner.color;
        if (squares == null || squares.Count == 0) return;
        for (int i = 0; i < squares.Count; i++) // every square on a board, every step one square across
        {
            if (!InGrid(squares[i])) return;
            if (i > 0 && Mathf.Abs(squares[i].x - squares[i - 1].x) + Mathf.Abs(squares[i].y - squares[i - 1].y) != 1) return;
        }

        var group = new GameObject("GuideLine", typeof(RectTransform));
        var root = (RectTransform)group.transform;
        root.SetParent(markersRoot, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.SetAsFirstSibling(); // under everything else on the markers layer
        guideLines.Add((owner, group));

        // one piece per square, so each can hide under its own tile
        var pieces = new Dictionary<Vector2Int, RectTransform>();
        foreach (var c in squares)
        {
            if (pieces.ContainsKey(c)) continue; // a square passed twice
            var piece = new GameObject("Square " + c, typeof(RectTransform));
            var prt = (RectTransform)piece.transform;
            prt.SetParent(root, false);
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero;
            pieces[c] = prt;
            guidePieces.Add((owner, c, Cells[c.x, c.y], piece));
        }
        // the square a point on the road is in: the nearest middle
        Vector2Int SquareAt(Vector2 at)
        {
            var best = squares[0];
            foreach (var c in squares)
                if (Vector2.Distance(CellPos(c), at) < Vector2.Distance(CellPos(best), at)) best = c;
            return best;
        }

        // the stretch's corners: where it starts, each square's middle, where it ends
        var points = new List<Vector2> { from };
        foreach (var c in squares) points.Add(CellPos(c));
        points.Add(to);

        var color = objectColor; // as the start and ring lines
        color.a *= endLineOpacity;
        float dot = CellSize * endLineDotSize, gap = CellSize * endLineGap;
        if (!endLineDotted)
        {
            // solid: a bar along each half of each stretch between corners, with the square it's in. a dot longer,
            // so the bars overlap at the corners
            for (int i = 0; i + 1 < points.Count; i++)
                foreach (var (a, b) in new[] { (points[i], (points[i] + points[i + 1]) / 2f), ((points[i] + points[i + 1]) / 2f, points[i + 1]) })
                {
                    var bar = UIBuilder.NewImage("Bar", pieces[SquareAt((a + b) / 2f)], color).rectTransform;
                    bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
                    bar.anchoredPosition = (a + b) / 2f;
                    bool across = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.y - a.y);
                    float len = Vector2.Distance(a, b) + dot;
                    bar.sizeDelta = across ? new Vector2(len, dot) : new Vector2(dot, len);
                }
            return;
        }
        // dotted: between the start's line and the ring's (both have a dot touching the board's edge), as many dots
        // as fit End Line Gap apart along the whole road, the gaps evened out so both ends match and nothing is left
        // over at the ring's end. each dot goes with the square it's in
        float length = 0;
        for (int i = 0; i + 1 < points.Count; i++) length += Vector2.Distance(points[i], points[i + 1]);
        int count = Mathf.Max(1, Mathf.RoundToInt((length - gap) / (dot + gap)));
        float spacing = (length - count * dot) / (count + 1); // the gap between dots, and at both ends
        for (int i = 0; i < count; i++)
        {
            var at = Along(points, spacing + dot / 2f + i * (dot + spacing));
            var img = UIBuilder.NewImage("Dot", pieces[SquareAt(at)], color);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            img.rectTransform.sizeDelta = Vector2.one * dot;
            img.rectTransform.anchoredPosition = at;
        }
    }

    // the point t along a line through these points
    static Vector2 Along(List<Vector2> points, float t)
    {
        for (int i = 0; i + 1 < points.Count; i++)
        {
            float d = Vector2.Distance(points[i], points[i + 1]);
            if (t <= d || i + 2 == points.Count) return Vector2.MoveTowards(points[i], points[i + 1], t);
            t -= d;
        }
        return points[0];
    }

    // the direction one step of d goes
    static Dir Toward(Vector2Int d)
    {
        foreach (var dir in DirUtil.All) if (DirUtil.Offset(dir) == d) return dir;
        return Dir.N;
    }

    // a guide line's piece shows only on a square with no tile and no tile previewed (the hover ghost)
    void LateUpdate()
    {
        foreach (var (_, square, cell, piece) in guidePieces)
            if (piece != null) piece.SetActive(cell.tile == null && !(ghost != null && ghostCell == square));
    }

    // closed: no tile can go on the square (GameManager checks Cell.closed), it's dimmed and has a big X.
    // only an empty square can be closed
    public void SetClosed(Vector2Int p, bool closed)
    {
        var cell = Cells != null && InGrid(p) ? Cells[p.x, p.y] : null;
        if (cell == null || cell.obstacle || (closed && cell.tile != null)) return;
        cell.closed = closed;
        HideGhost(p); // a preview shown before the change
        cell.view.background.color = CellColor(cell);

        if (closedMarks.TryGetValue(p, out var old)) { if (old != null) Destroy(old); closedMarks.Remove(p); }
        if (!closed) return;
        var mark = new GameObject("ClosedMark", typeof(RectTransform));
        var rt = (RectTransform)mark.transform;
        rt.SetParent(markersRoot, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = CellPos(p);
        foreach (float angle in new[] { 45f, -45f })
        {
            var arm = UIBuilder.NewImage("Arm", rt, closedMarkColor).rectTransform;
            arm.anchorMin = arm.anchorMax = arm.pivot = new Vector2(0.5f, 0.5f);
            arm.sizeDelta = new Vector2(CellSize * closedMarkSize, CellSize * closedMarkWidth);
            arm.localEulerAngles = new Vector3(0, 0, angle);
        }
        closedMarks[p] = mark;
    }

    // the square opens at once (tiles can go on it), and its X fades out and shrinks over `seconds` while the square's
    // dim fades back to its own color
    public void OpenAnimated(Vector2Int p, float seconds)
    {
        var cell = Cells != null && InGrid(p) ? Cells[p.x, p.y] : null;
        if (cell == null || !cell.closed) return;
        var from = cell.view.background.color;
        cell.closed = false;
        HideGhost(p);
        closedMarks.TryGetValue(p, out var mark);
        closedMarks.Remove(p);
        StartCoroutine(FadeOpen(cell, mark, from, seconds));
    }

    IEnumerator FadeOpen(Cell cell, GameObject mark, Color from, float seconds)
    {
        var fade = mark != null ? mark.AddComponent<CanvasGroup>() : null;
        for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            if (fade != null)
            {
                fade.alpha = 1f - k;
                mark.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, k);
            }
            if (cell.view != null && cell.tile == null) cell.view.background.color = Color.Lerp(from, CellColor(cell), k);
            yield return null;
        }
        if (mark != null) Destroy(mark);
        if (cell.view != null) cell.view.background.color = CellColor(cell);
    }

    // the object was delivered: its guide lines go
    void ClearGuideLines(ObjectSpec owner)
    {
        foreach (var (o, line) in guideLines) if (o == owner && line != null) Destroy(line);
        guideLines.RemoveAll(g => g.owner == owner);
        guidePieces.RemoveAll(p => p.owner == owner);
    }

    // every guide line goes and every closed square opens
    public void ClearGuides()
    {
        foreach (var (_, line) in guideLines) if (line != null) Destroy(line);
        guideLines.Clear();
        guidePieces.Clear();
        foreach (var p in new List<Vector2Int>(closedMarks.Keys)) SetClosed(p, false);
    }

    // Build: the markers layer is cleared, and the guides with it
    void ForgetGuides()
    {
        guideLines.Clear();
        guidePieces.Clear();
        closedMarks.Clear();
    }
}
