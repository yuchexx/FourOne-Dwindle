using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// the tutorial's look, set on TutorialController (in Play mode, changes show from the next step)
[Serializable]
public class TutorialStyle
{
    public Color dimColor = new Color(0f, 0f, 0f, 0.65f);
    public Color frameColor = new Color(0.95f, 0.78f, 0.25f); // the frame just outside the area
    public float frameWidth = 4f;                              // 0 = no frame
    public Color bubbleColor = new Color(0.13f, 0.14f, 0.17f);
    public Color textColor = Color.white;
    public Color buttonColor = new Color(0.95f, 0.78f, 0.25f);
    public Color buttonTextColor = new Color(0.08f, 0.08f, 0.10f);
    public float fontSize = 26f;
    public float bubbleWidth = 560f;
    public float padding = 20f;  // inside the bubble
    public Vector2 buttonSize = new Vector2(120f, 44f);
    public float gap = 16f;      // between the area and the bubble, and between the bubble and the screen edge
}

// dims the screen except one area, blocks clicks, and shows a text bubble with an optional Next button.
// made entirely from code, as the last child of the canvas
public class TutorialOverlay : MonoBehaviour
{
    Canvas canvas;
    RectTransform root;
    Vector2 design; // the canvas reference resolution areas are written in
    TutorialStyle style;

    Image top, bottom, left, right; // the dim, in four pieces around the area
    Image[] frame;                  // the frame's top, bottom, left and right edges, around the area
    Image blocker;                 // see-through, over everything but the bubble, for look-only steps
    Image bubble, button;
    TMP_Text text, buttonLabel;

    TutorialStep step;
    Vector2 bubbleSize;
    bool nextClicked;

    public static TutorialOverlay Create(Canvas canvas, TutorialStyle style)
    {
        var go = new GameObject("TutorialOverlay", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var overlay = go.AddComponent<TutorialOverlay>();
        overlay.Build(canvas, style);
        go.SetActive(false);
        return overlay;
    }

    // the canvas reference resolution (1600x900), which areas are written in
    public static Vector2 DesignSize(Canvas canvas)
    {
        var scaler = canvas.GetComponent<CanvasScaler>();
        return scaler != null ? scaler.referenceResolution : new Vector2(1600, 900);
    }

    void Build(Canvas canvas, TutorialStyle style)
    {
        this.canvas = canvas;
        this.style = style;
        root = (RectTransform)transform;
        design = DesignSize(canvas);

        // drawn in this order: dim, frame, blocker, bubble
        top = MakeImage("DimTop", root, raycast: true);
        bottom = MakeImage("DimBottom", root, raycast: true);
        left = MakeImage("DimLeft", root, raycast: true);
        right = MakeImage("DimRight", root, raycast: true);
        foreach (var piece in new[] { top, bottom, left, right }) FromBottomLeft(piece.rectTransform);

        frame = new[]
        {
            MakeImage("FrameTop", root, raycast: false), MakeImage("FrameBottom", root, raycast: false),
            MakeImage("FrameLeft", root, raycast: false), MakeImage("FrameRight", root, raycast: false),
        };
        foreach (var edge in frame) FromBottomLeft(edge.rectTransform);

        blocker = MakeImage("Blocker", root, raycast: true);
        blocker.color = Color.clear; // still takes clicks
        var brt = blocker.rectTransform;
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = brt.offsetMax = Vector2.zero;

        bubble = MakeImage("Bubble", root, raycast: true); // clicks on the bubble don't reach the game
        FromBottomLeft(bubble.rectTransform);

        text = MakeText("Text", bubble.rectTransform);
        text.alignment = TextAlignmentOptions.TopLeft;
        var trt = text.rectTransform; // across the top of the bubble, inside the padding
        trt.anchorMin = new Vector2(0, 1);
        trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0.5f, 1);

        button = MakeImage("NextButton", bubble.rectTransform, raycast: true);
        var nrt = button.rectTransform; // bottom-right corner of the bubble
        nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(1, 0);
        var next = button.gameObject.AddComponent<Button>();
        next.targetGraphic = button;
        next.onClick.AddListener(() => nextClicked = true);

        buttonLabel = MakeText("Label", nrt); // the text is set per step in Show
        buttonLabel.fontStyle = FontStyles.Bold;
        buttonLabel.alignment = TextAlignmentOptions.Center;
        var lrt = buttonLabel.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
    }

    public void Show(TutorialStep s)
    {
        step = s;
        nextClicked = false;
        bool hasNext = s.advance == Advance.NextButton;

        // the style again each step, so Inspector changes in Play mode show up
        foreach (var piece in new[] { top, bottom, left, right }) piece.color = style.dimColor;
        foreach (var edge in frame) edge.color = style.frameColor;
        bubble.color = style.bubbleColor;
        button.color = style.buttonColor;
        text.color = style.textColor;
        buttonLabel.color = style.buttonTextColor;
        text.fontSize = style.fontSize;
        buttonLabel.fontSize = style.fontSize * 0.85f;

        SetDimAndBubble(true); // Block may have hidden them
        blocker.gameObject.SetActive(!s.clickable);
        button.gameObject.SetActive(hasNext);
        buttonLabel.text = string.IsNullOrEmpty(s.buttonText) ? "Next" : s.buttonText;

        // the bubble is as tall as its text, plus the button below it
        float width = s.bubbleWidth > 0 ? s.bubbleWidth : style.bubbleWidth;
        float textWidth = width - 2 * style.padding;
        text.text = s.text;
        float textHeight = text.GetPreferredValues(s.text, textWidth, 0).y;
        text.rectTransform.anchoredPosition = new Vector2(0, -style.padding);
        text.rectTransform.sizeDelta = new Vector2(-2 * style.padding, textHeight);
        button.rectTransform.anchoredPosition = new Vector2(-style.padding, style.padding);
        button.rectTransform.sizeDelta = style.buttonSize;
        bubbleSize = new Vector2(width,
            style.padding + textHeight + (hasNext ? style.gap + style.buttonSize.y : 0) + style.padding);

        root.SetAsLastSibling(); // over every screen and pop-up
        gameObject.SetActive(true);
        Layout();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        step = null;
    }

    // for the wait before a step: nothing is dimmed or shown, but nothing can be clicked either
    public void Block()
    {
        step = null;
        SetDimAndBubble(false);
        blocker.gameObject.SetActive(true);
        root.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    void SetDimAndBubble(bool visible)
    {
        foreach (var piece in new[] { top, bottom, left, right }) piece.enabled = visible;
        foreach (var edge in frame) edge.enabled = visible; // Layout turns them off again when there's no frame
        bubble.gameObject.SetActive(visible);
    }

    // true once per click on Next
    public bool ConsumeNext()
    {
        bool clicked = nextClicked;
        nextClicked = false;
        return clicked;
    }

    // a click (press and release) just ended inside the area. checked after the UI has handled that click,
    // so the click still reaches whatever is under the area
    public bool ReleasedInArea()
    {
        var pointer = Pointer.current; // mouse or touch
        if (step?.area == null || pointer == null || !pointer.press.wasReleasedThisFrame) return false;
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointer.position.ReadValue(), cam, out var local))
            return false;
        return Hole().Contains(local - root.rect.min);
    }

    // the screen can be resized, so the pieces are placed again every frame
    void LateUpdate() => Layout();

    // the step's area in this overlay's units, from the bottom-left corner.
    // the same as the area on a 16:9 screen; on other screens it's stretched to keep its place roughly
    Rect Hole() => ToOverlay(step.area.Value);

    // an area (design units, from the top-left) in this overlay's units, from the bottom-left corner, kept on screen
    Rect ToOverlay(Rect a)
    {
        var size = root.rect.size;
        float sx = size.x / design.x, sy = size.y / design.y;
        return Rect.MinMaxRect(
            Mathf.Clamp(a.xMin * sx, 0, size.x), Mathf.Clamp(size.y - a.yMax * sy, 0, size.y),
            Mathf.Clamp(a.xMax * sx, 0, size.x), Mathf.Clamp(size.y - a.yMin * sy, 0, size.y));
    }

    void Layout()
    {
        if (step == null) return;
        var size = root.rect.size;

        if (step.area == null)
        {
            Place(top.rectTransform, new Rect(Vector2.zero, size));
            Place(bottom.rectTransform, Rect.zero);
            Place(left.rectTransform, Rect.zero);
            Place(right.rectTransform, Rect.zero);
            foreach (var edge in frame) edge.enabled = false;
            PlaceBubble(new Rect(size / 2, Vector2.zero), BubbleSide.Center);
            return;
        }

        var hole = Hole();
        Place(top.rectTransform, Rect.MinMaxRect(0, hole.yMax, size.x, size.y));
        Place(bottom.rectTransform, Rect.MinMaxRect(0, 0, size.x, hole.yMin));
        Place(left.rectTransform, Rect.MinMaxRect(0, hole.yMin, hole.xMin, hole.yMax));
        Place(right.rectTransform, Rect.MinMaxRect(hole.xMax, hole.yMin, size.x, hole.yMax));

        // the frame sits on the dim, just outside the hole, so it covers nothing in the hole
        float w = Mathf.Max(0f, style.frameWidth);
        foreach (var edge in frame) edge.enabled = w > 0;
        Place(frame[0].rectTransform, Rect.MinMaxRect(hole.xMin - w, hole.yMax, hole.xMax + w, hole.yMax + w));
        Place(frame[1].rectTransform, Rect.MinMaxRect(hole.xMin - w, hole.yMin - w, hole.xMax + w, hole.yMin));
        Place(frame[2].rectTransform, Rect.MinMaxRect(hole.xMin - w, hole.yMin, hole.xMin, hole.yMax));
        Place(frame[3].rectTransform, Rect.MinMaxRect(hole.xMax, hole.yMin, hole.xMax + w, hole.yMax));

        // the bubble keeps its gap from the frame, not from the hole
        PlaceBubble(Rect.MinMaxRect(hole.xMin - w, hole.yMin - w, hole.xMax + w, hole.yMax + w), step.bubble);
    }

    // next to the hole on the given side, kept on screen
    void PlaceBubble(Rect hole, BubbleSide side)
    {
        var size = root.rect.size;
        var b = bubbleSize;
        float g = style.gap;

        if (side == BubbleSide.Auto)
            side = hole.yMin - g - b.y >= g ? BubbleSide.Below
                : size.y - hole.yMax - g - b.y >= g ? BubbleSide.Above
                : BubbleSide.Center;

        Vector2 pos = side switch // the bubble's bottom-left corner
        {
            BubbleSide.Below => new Vector2(hole.center.x - b.x / 2, hole.yMin - g - b.y),
            BubbleSide.Above => new Vector2(hole.center.x - b.x / 2, hole.yMax + g),
            BubbleSide.Left => new Vector2(hole.xMin - g - b.x, hole.center.y - b.y / 2),
            BubbleSide.Right => new Vector2(hole.xMax + g, hole.center.y - b.y / 2),
            _ => (size - b) / 2,
        };
        pos.x = Mathf.Clamp(pos.x, g, Mathf.Max(g, size.x - g - b.x));
        pos.y = Mathf.Clamp(pos.y, g, Mathf.Max(g, size.y - g - b.y));
        Place(bubble.rectTransform, new Rect(pos, b));
    }

    // r is from the overlay's bottom-left corner, for a rect set up by FromBottomLeft
    static void Place(RectTransform rt, Rect r)
    {
        rt.anchoredPosition = r.position;
        rt.sizeDelta = r.size;
    }

    static void FromBottomLeft(RectTransform rt) => rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;

    static Image MakeImage(string name, RectTransform parent, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        ((RectTransform)go.transform).SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = raycast;
        return img;
    }

    static TMP_Text MakeText(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        ((RectTransform)go.transform).SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.raycastTarget = false;
        return t;
    }
}
