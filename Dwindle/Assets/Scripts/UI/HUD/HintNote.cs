using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the note above Hide / Show: a header, then where the tile count is and what stage 1 (the order) draws on the board,
// or what stage 2 (the answer) draws
// (each line led by small copies of its marks)
public class HintNote : MonoBehaviour
{
    [Tooltip("Optional: the labels' font (empty: TextMeshPro's default)")]
    public TMP_FontAsset font;
     [System.NonSerialized] public float fontSize = 20f;
     [System.NonSerialized] public float iconSize = 26f;
     [System.NonSerialized] public float frameWidth = 3f;
     [System.NonSerialized] public string junctionText = "meeting junctions";
     [System.NonSerialized] public string pressText = "Each press: next step", doneText = "placed correct";
    [Tooltip("The note's first line; {s} is the stage, {total} the number of stages, {name} the stage's name")]
     [System.NonSerialized] public string headerText = "Hint {s}/{total} · {name}";
     [System.NonSerialized] public string[] stageTitles = { "Order", "Answer" }; // one per stage (was stageNames, with a "Tiles" stage first)
     [System.NonSerialized] public float headerSize = 20f;

    readonly List<Graphic> pulsing = new();
    int shownStage = -1;
    BoardView board;

    // its Vertical Layout Group and Content Size Fitter are set in the Inspector
    void Awake() => UIPalette.Changed += Rebuild;

    void OnDestroy() => UIPalette.Changed -= Rebuild;

    void Rebuild()
    {
        int stage = shownStage;
        shownStage = -1;
        if (stage >= 0 && board != null) Show(stage, board);
    }

    // rebuilds the lines when the stage changes; the round badge is the board's own circle
    public void Show(int stage, BoardView board)
    {
        if (stage == shownStage) return;
        shownStage = stage;
        this.board = board;
        pulsing.Clear();
        UIBuilder.Clear(transform);
        orderRow = null; // cleared with the rest (Destroy waits for the frame's end)
        var p = UIPalette.Current;
        if (stage >= 1) Header(stage, p.text);
        if (stage == 1) // what the board shows: the junction frames, where the order is, hovering for more
        {
            Framed(Row(junctionNote));
            Row(orderNote);
            Row(hoverNote);
        }
        if (stage >= 2)
        {
            Row(pressText);
            Square(Row(doneText), p.hintAccent, false); // still, like the board's
        }
        if (stage >= 1) GuideButton(p); // last: how the hints work (the guide comes later)
    }

    // "How hints work": a small text button under the lines; GuideClicked says it was pressed (nothing listens yet:
    // the hint guide is still to be designed)
     [System.NonSerialized] public string guideLinkText = "Hint guide";
    [Tooltip("Stage 1's lines")]
     [System.NonSerialized] public string junctionNote = "Junction: where objects meet", orderNote = "Order: below the supply dock", hoverNote = "Hover a ring or junction for more";
    public event System.Action GuideClicked;
    void GuideButton(UIPalette p)
    {
        var row = UIBuilder.NewRow("GuideLine", transform, 6f);
        var label = Text(row, "<u>" + guideLinkText + "</u>", fontSize, p.accent);
        label.raycastTarget = true;
        var b = label.gameObject.AddComponent<Button>();
        b.targetGraphic = label;
        b.onClick.AddListener(() => GuideClicked?.Invoke());
    }

    // stage 1's delivery order as one line under the header: "1 ● ●   2 ●", each object's colour, faded once it's delivered.
    // rebuilt on every hint redraw (the plan can change as the board does, and objects arrive)
    RectTransform orderRow;
     [System.NonSerialized] public float deliveredAlpha = 0.25f;
    public void ShowOrder(List<List<(Color color, bool delivered)>> groups)
    {
        if (orderRow == null)
        {
            orderRow = UIBuilder.NewRow("OrderLine", transform, 4f);
            orderRow.SetSiblingIndex(Mathf.Min(1, transform.childCount - 1)); // right under the header
        }
        UIBuilder.Clear(orderRow);
        for (int i = 0; i < groups.Count; i++)
        {
            var n = Text(orderRow, (i > 0 ? "  " : "") + (i + 1), fontSize, UIPalette.Current.text);
            n.fontStyle = FontStyles.Bold;
            foreach (var (color, delivered) in groups[i])
            {
                var box = (RectTransform)new GameObject("Dot", typeof(RectTransform)).transform;
                box.SetParent(orderRow, false);
                UIBuilder.FixedSize(box.gameObject, iconSize * 0.8f);
                var c = color; if (delivered) c.a = deliveredAlpha;
                var dot = UIBuilder.Circle(board.objectPrefab, box, c, iconSize * 0.8f);
                var rt = dot.Rect;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero; rt.localEulerAngles = Vector3.zero;
            }
        }
    }

    // fades with the board's hints
    public void Pulse(float k)
    {
        foreach (var g in pulsing)
        {
            var c = g.color;
            c.a = k;
            g.color = c;
        }
    }

    // the first line, e.g. "Hint 1/2 · Order": brighter and bold, doesn't pulse
    void Header(int stage, Color color)
    {
        string name = stage - 1 < stageTitles.Length ? stageTitles[stage - 1] : "";
        string s = headerText.Replace("{s}", stage.ToString()).Replace("{total}", Hints.Stages.ToString()).Replace("{name}", name);
        Text(transform, s, headerSize, color).fontStyle = FontStyles.Bold;
    }

    // a line: its icons (added after) then the label
    RectTransform Row(string label)
    {
        var row = UIBuilder.NewRow("Line", transform, 6f);
        var text = Text(row, label, fontSize, UIPalette.Current.text);
        text.gameObject.AddComponent<LayoutElement>().minHeight = iconSize;
        pulsing.Add(text);
        return row;
    }

    // an icon-sized box placed before the row's label
    RectTransform Box(RectTransform row, string name)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(row, false);
        rt.SetSiblingIndex(row.childCount - 2);
        UIBuilder.FixedSize(rt.gameObject, iconSize);
        return rt;
    }

    Image Fill(RectTransform box, Color color)
    {
        var img = UIBuilder.NewImage("Fill", box, color);
        UIBuilder.Fill(img.rectTransform);
        return img;
    }

    // a square with a white #
    void Square(RectTransform row, Color color, bool pulse)
    {
        var box = Box(row, "Badge");
        var back = Fill(box, color);
        Hash(box, pulse);
        if (pulse) pulsing.Add(back);
    }

    // four Hint bars, like a junction's frame
    void Framed(RectTransform row)
    {
        var box = Box(row, "Frame");
        float w = frameWidth;
        Bar(box, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, w));
        Bar(box, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -w), new Vector2(0, 0));
        Bar(box, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, w), new Vector2(w, -w));
        Bar(box, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-w, w), new Vector2(0, -w));
    }

    void Bar(RectTransform box, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var bar = Fill(box, UIPalette.Current.hint);
        var rt = bar.rectTransform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
        pulsing.Add(bar);
    }

    void Hash(RectTransform box, bool pulse)
    {
        var t = UIBuilder.BadgeLabel(box, "#", iconSize * 0.62f, font);
        if (pulse) pulsing.Add(t);
    }

    TextMeshProUGUI Text(Transform parent, string s, float size, Color color)
    {
        var t = UIBuilder.NewText("Text", parent, s, size, color, font);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        return t;
    }
}
