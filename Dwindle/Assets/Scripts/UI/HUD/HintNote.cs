using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the note above Hide / Show: what stage 2 and 3 draw on the board, each line led by small copies of its marks
public class HintNote : MonoBehaviour
{
    [Tooltip("Optional: the labels' font (empty: TextMeshPro's default)")]
    public TMP_FontAsset font;
    public float fontSize = 18f;
    public float iconSize = 26f;
    [Min(1f)] public float frameWidth = 3f;
    public string orderText = "order", junctionText = "junctions";
    public string pressText = "Each press: next step", doneText = "placed correct";

    readonly List<Graphic> pulsing = new();
    int shownStage = -1;
    BoardView board;

    void Awake()
    {
        if (!TryGetComponent(out VerticalLayoutGroup layout)) layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 12);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        if (!TryGetComponent(out ContentSizeFitter fit)) fit = gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UIPalette.Changed += Rebuild;
    }

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
        var p = UIPalette.Current;
        if (stage == 2)
        {
            var order = Row(orderText);
            Square(order, p.hint, true);
            Round(order, p.hint);
            Framed(Row(junctionText));
        }
        else if (stage >= 3)
        {
            Row(pressText);
            Square(Row(doneText), p.accent, false); // still, like the board's
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

    // a line: its icons (added after) then the label
    RectTransform Row(string label)
    {
        var row = UIBuilder.NewRow("Line", transform, 6f);
        var text = Text(row, label, fontSize, UIPalette.Current.mutedText);
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

    // a destination's circle with a white #
    void Round(RectTransform row, Color color)
    {
        var box = Box(row, "Ring");
        var circle = UIBuilder.Circle(board.objectPrefab, box, color, iconSize);
        var rt = circle.Rect;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localEulerAngles = Vector3.zero;
        Hash(box, true);
        pulsing.Add(circle.body);
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
