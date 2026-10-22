using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// the tutorial's look, set on TutorialController (in Play mode, changes show from the next step)
[Serializable]
public class TutorialStyle
{
    public Color dimColor = new Color(0f, 0f, 0f, 0.65f);
    public Color frameColor = new Color(0.95f, 0.78f, 0.25f); // the frame just outside each area
    public float frameWidth = 4f;                              // 0 = no frame
    public float shakeDistance = 8f;  // how far the frames swing when the dim is clicked; 0 = no shake
    public float shakeSeconds = 0.35f;
    public Color bubbleColor = new Color(0.13f, 0.14f, 0.17f);
    public Color textColor = Color.white;
    public Color buttonColor = new Color(0.95f, 0.78f, 0.25f);
    public Color buttonTextColor = new Color(0.08f, 0.08f, 0.10f);
    public float fontSize = 26f;
    public float bubbleWidth = 560f;
    public float padding = 20f;  // inside the bubble
    public Vector2 buttonSize = new Vector2(120f, 44f);
    public float gap = 16f;      // between the areas and the bubble, and between the bubble and the screen edge
}

// dims the screen except the step's areas, blocks clicks, and shows a text bubble with an optional Next button.
// made entirely from code, as the last child of the canvas
public class TutorialOverlay : MonoBehaviour
{
    Canvas canvas;
    RectTransform root;
    Vector2 design; // the canvas reference resolution areas are written in
    TutorialStyle style;

    RectTransform dimLayer, frameLayer;      // hold the pieces below, so the dim stays under the frames
    readonly List<Image> dims = new();      // the dim, in as many pieces as the areas cut the screen into
    readonly List<Image> frames = new();    // four edges per area
    Image blocker;                          // see-through, over everything but the bubble, for look-only steps
    Image bubble, button;
    TMP_Text text, buttonLabel;

    TutorialStep step;
    Vector2 bubbleSize;
    bool nextClicked;
    float shakeStart = -1; // when the frames started shaking; -1 = still

    public static TutorialOverlay Create(Canvas canvas, TutorialStyle style)
    {
        var go = new GameObject("TutorialOverlay", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        Stretch(rt);

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

        // drawn in this order: dim, frames, blocker, bubble
        dimLayer = MakeLayer("Dim", root);
        frameLayer = MakeLayer("Frames", root);

        blocker = MakeImage("Blocker", root, raycast: true);
        blocker.color = Color.clear; // still takes clicks
        Stretch(blocker.rectTransform);

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
        Stretch(buttonLabel.rectTransform);
    }

    public void Show(TutorialStep s)
    {
        step = s;
        nextClicked = false;
        StopShake();
        bool hasNext = s.advance == Advance.NextButton;

        // the style again each step, so Inspector changes in Play mode show up (the dim and frames take theirs in Layout)
        bubble.color = style.bubbleColor;
        button.color = style.buttonColor;
        text.color = style.textColor;
        buttonLabel.color = style.buttonTextColor;
        text.fontSize = style.fontSize;
        buttonLabel.fontSize = style.fontSize * 0.85f;

        SetDimAndBubble(true); // Block may have hidden them
        blocker.gameObject.SetActive(!s.clickable && !s.hoverOnly); // hovering needs the pointer to reach the game too
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
        StopShake();
    }

    // for the wait before a step: nothing is dimmed or shown, but nothing can be clicked either
    public void Block()
    {
        step = null;
        StopShake();
        SetDimAndBubble(false);
        blocker.gameObject.SetActive(true);
        root.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    void SetDimAndBubble(bool visible)
    {
        dimLayer.gameObject.SetActive(visible);
        frameLayer.gameObject.SetActive(visible);
        bubble.gameObject.SetActive(visible);
    }

    // true once per click on Next
    public bool ConsumeNext()
    {
        bool clicked = nextClicked;
        nextClicked = false;
        return clicked;
    }

    // a click (press and release) just ended inside one of the areas. checked after the UI has handled that click,
    // so the click still reaches whatever is under the area
    public bool ReleasedInArea()
    {
        var pointer = Pointer.current; // mouse or touch
        if (step == null || pointer == null || !pointer.press.wasReleasedThisFrame) return false;
        return PointerAt(pointer, out var p) && Holes().Any(h => h.Contains(p));
    }

    // where the pointer is, in this overlay's units from the bottom-left corner
    bool PointerAt(Pointer pointer, out Vector2 p)
    {
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        bool inside = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            root, pointer.position.ReadValue(), cam, out var local);
        p = local - root.rect.min;
        return inside;
    }

    // a press on the dim is checked in Update, before the controller's coroutine can move on to the next step this
    // frame: a quick tap (a trackpad's tap to click) presses and releases in one frame, and by LateUpdate the Next it
    // pressed would already have brought up the next step's bubble somewhere else, making the press look like a miss
    void Update()
    {
        if (DimPressed()) Shake();
    }

    // the screen can be resized, so the pieces are placed again every frame
    void LateUpdate()
    {
        Layout();
        UpdateShake();
    }

    // the frames swing to point the player back at the areas; a new shake starts over
    public void Shake() => shakeStart = Time.unscaledTime;

    // a press just went down on the dim: outside every area and outside the bubble
    bool DimPressed()
    {
        var pointer = Pointer.current;
        if (step == null || pointer == null || !pointer.press.wasPressedThisFrame || !PointerAt(pointer, out var p))
            return false;
        var b = bubble.rectTransform;
        return !Holes().Any(h => h.Contains(p)) && !new Rect(b.anchoredPosition, b.sizeDelta).Contains(p);
    }

    // the frames swing side to side three times, dying down, to point the player back at the areas
    void UpdateShake()
    {
        if (shakeStart < 0) return;
        float t = style.shakeSeconds > 0 ? (Time.unscaledTime - shakeStart) / style.shakeSeconds : 1f;
        if (t >= 1f) { StopShake(); return; }
        float x = style.shakeDistance * Mathf.Sin(t * Mathf.PI * 2f * 3f) * (1f - t);
        frameLayer.anchoredPosition = new Vector2(x, 0);
    }

    void StopShake()
    {
        shakeStart = -1;
        if (frameLayer != null) frameLayer.anchoredPosition = Vector2.zero;
    }

    // the step's areas in this overlay's units, from the bottom-left corner: areas if it has them, otherwise area.
    // the same as the areas on a 16:9 screen; on other screens they're stretched to keep their place roughly
    List<Rect> Holes()
    {
        var areas = step.areas ?? (step.area != null ? new[] { step.area.Value } : Array.Empty<Rect>());
        return areas.Select(ToOverlay).Where(h => h.width > 0 && h.height > 0).ToList();
    }

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
        var holes = Holes();

        // the dim: the screen cut along every hole's edges into a grid, and each row's run of cells outside
        // every hole dimmed as one piece. with no holes that's one piece over the whole screen
        var xs = Cuts(holes.SelectMany(h => new[] { h.xMin, h.xMax }), size.x);
        var ys = Cuts(holes.SelectMany(h => new[] { h.yMin, h.yMax }), size.y);
        bool Dimmed(int i, int j) =>
            !holes.Any(h => h.Contains(new Vector2((xs[i] + xs[i + 1]) / 2, (ys[j] + ys[j + 1]) / 2)));
        int d = 0;
        for (int j = 0; j < ys.Count - 1; j++)
        {
            int i = 0;
            while (i < xs.Count - 1)
            {
                if (!Dimmed(i, j)) { i++; continue; }
                int start = i;
                while (i < xs.Count - 1 && Dimmed(i, j)) i++;
                var piece = Pooled(dims, d++, dimLayer, "Dim", raycast: true);
                piece.color = style.dimColor;
                Place(piece.rectTransform, Rect.MinMaxRect(xs[start], ys[j], xs[i], ys[j + 1]));
            }
        }
        HideFrom(dims, d);

        // each hole's frame sits on the dim, just outside the hole, so it covers nothing in the hole
        float w = Mathf.Max(0f, style.frameWidth);
        int f = 0;
        if (w > 0)
            foreach (var h in holes)
                foreach (var edge in new[]
                {
                    Rect.MinMaxRect(h.xMin - w, h.yMax, h.xMax + w, h.yMax + w), // top
                    Rect.MinMaxRect(h.xMin - w, h.yMin - w, h.xMax + w, h.yMin), // bottom
                    Rect.MinMaxRect(h.xMin - w, h.yMin, h.xMin, h.yMax),         // left
                    Rect.MinMaxRect(h.xMax, h.yMin, h.xMax + w, h.yMax),         // right
                })
                {
                    var piece = Pooled(frames, f++, frameLayer, "Frame", raycast: false);
                    piece.color = style.frameColor;
                    Place(piece.rectTransform, edge);
                }
        HideFrom(frames, f);

        if (holes.Count == 0)
        {
            PlaceBubble(new Rect(size / 2, Vector2.zero), BubbleSide.Center);
            return;
        }
        // next to all the holes together, keeping its gap from the frames, not from the holes
        var all = Rect.MinMaxRect(holes.Min(h => h.xMin) - w, holes.Min(h => h.yMin) - w,
                                  holes.Max(h => h.xMax) + w, holes.Max(h => h.yMax) + w);
        PlaceBubble(all, step.bubble);
    }

    // 0, the screen's length and every cut between them, in order
    static List<float> Cuts(IEnumerable<float> cuts, float length) =>
        cuts.Append(0f).Append(length).Distinct().OrderBy(c => c).ToList();

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

    // the pool's i-th image, shown, made first if the pool is that short
    static Image Pooled(List<Image> pool, int i, RectTransform parent, string name, bool raycast)
    {
        while (pool.Count <= i)
        {
            var img = MakeImage(name, parent, raycast);
            FromBottomLeft(img.rectTransform);
            pool.Add(img);
        }
        pool[i].gameObject.SetActive(true);
        return pool[i];
    }

    // hides the pool's images from i on, the ones this layout didn't use
    static void HideFrom(List<Image> pool, int i)
    {
        for (; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
    }

    // r is from the overlay's bottom-left corner, for a rect set up by FromBottomLeft
    static void Place(RectTransform rt, Rect r)
    {
        rt.anchoredPosition = r.position;
        rt.sizeDelta = r.size;
    }

    static void FromBottomLeft(RectTransform rt) => rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // an empty full-screen layer, to keep a group of pieces drawn under the ones made after it
    static RectTransform MakeLayer(string name, RectTransform parent)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent, false);
        Stretch(rt);
        return rt;
    }

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
