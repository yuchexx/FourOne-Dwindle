using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// the tutorial's look, set on TutorialController (in Play mode, changes show from the next step). the frames and the
// Next button are in the UI Palette's Tutorial color (a yellow), Skip a fixed grey (SkipColor), the dim fixed too
// (Dim); the notes and the floating points are in the UI Palette's Accent (the theme color)
[Serializable]
public class TutorialStyle
{
    public float frameWidth = 4f;                              // 0 = no frame
    public float targetPadding = 6f;  // around a target's element (TutorialStep.targets), so the frame doesn't touch it
    public float shakeDistance = 8f;  // how far the frames swing when the dim is clicked; 0 = no shake
    public float shakeSeconds = 0.35f;
    public Color bubbleColor = new Color(0.13f, 0.14f, 0.17f);
    public Color textColor = Color.white;
    public float fontSize = 26f;
    public float bubbleWidth = 560f;
    public float padding = 20f;  // inside the bubble
    public Vector2 buttonSize = new Vector2(120f, 44f);
    public float gap = 16f;      // between the areas and the bubble, and between the bubble and the screen edge
    [Header("Notes (a coach's, in the bottom-right corner, in the UI Palette's Accent)")]
    public float noteWidth = 400f;
    // from the screen's right and bottom edges (lined up with Restart's right edge). the notes also keep Gap above
    // whatever their coach names (TutorialCoach.noteAbove: the supply bar and the buttons), wherever they are; several
    // stack up, the newest at the bottom, Gap apart
    public Vector2 noteMargin = new Vector2(22f, 24f);
    public float noteCloseSize = 32f;                  // its X button, in the top-right corner
}

// dims the screen except the step's areas, blocks clicks, and shows a text bubble with an optional Next button
// (and a Skip button left of it). also notes on their own (ShowNote): no dim, nothing blocked, in the bottom-right
// corner. made entirely from code, as the last child of the canvas
public class TutorialOverlay : MonoBehaviour
{
    Canvas canvas;
    RectTransform root;
    TutorialStyle style;

    RectTransform dimLayer, frameLayer;      // hold the pieces below, so the dim stays under the frames
    readonly List<Image> dims = new();      // the dim, in as many pieces as the areas cut the screen into
    readonly List<Image> frames = new();    // four edges per area
    Image blocker;                          // see-through, over everything but the bubble, for look-only steps
    Image bubble, button, skipButton;
    TMP_Text text, buttonLabel, skipLabel;

    TutorialStep step;
    Vector2 bubbleSize;
    bool nextClicked, skipClicked;
    float shakeStart = -1; // when the frames started shaking; -1 = still
    readonly Vector3[] corners = new Vector3[4]; // a target's world corners, reused every frame

    // the notes (ShowNote), oldest first; the newest sits at the bottom
    class Note
    {
        public Image bubble;
        public string message;
        public float until = -1;    // when it closes by itself; -1 = only with its X
        public float y = float.NaN; // where it is now (it slides up as newer ones come in); NaN = not placed yet
    }
    readonly List<Note> notes = new();
    Func<RectTransform>[] noteAbove; // what the notes keep above (ShowNote)
    const float NoteSlide = 14f;     // how fast a note slides to its place (the higher, the quicker)

    // floating points (ShowPop): bold text in the UI Palette's Accent, over the elements it's about, rising a little
    // as it fades in
    const float PopRise = 10f, PopSeconds = 0.3f;
    public class Pop
    {
        public RectTransform badge;        // the text's
        public CanvasGroup fade;
        public Func<RectTransform>[] near; // what it floats over
        public float above;                // how far over their top it ends up
        public float shownAt;
    }
    readonly List<Pop> pops = new();

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

    void Build(Canvas canvas, TutorialStyle style)
    {
        this.canvas = canvas;
        this.style = style;
        root = (RectTransform)transform;

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

        skipButton = MakeImage("SkipButton", bubble.rectTransform, raycast: true);
        var srt = skipButton.rectTransform; // the bottom-right corner too, moved left of Next in Show
        srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(1, 0);
        var skip = skipButton.gameObject.AddComponent<Button>();
        skip.targetGraphic = skipButton;
        skip.onClick.AddListener(() => skipClicked = true);

        skipLabel = MakeText("Label", srt);
        skipLabel.text = "Skip";
        skipLabel.fontStyle = FontStyles.Bold;
        skipLabel.alignment = TextAlignmentOptions.Center;
        Stretch(skipLabel.rectTransform);

    }

    // a note in the bottom-right corner: nothing dimmed or blocked. it closes with its X, or by itself after `seconds`
    // (0 = only with the X). a step showing, or Hide, closes it too. one already showing slides up over it; one with
    // the same message goes, so it isn't there twice. above: elements the notes keep Gap above (missing or hidden ones
    // don't count), found again every frame
    public void ShowNote(string message, float seconds, Func<RectTransform>[] above = null)
    {
        noteAbove = above;
        if (step == null) // not over a step: only the notes show
        {
            SetDimAndBubble(false);
            blocker.gameObject.SetActive(false);
        }
        foreach (var old in notes.FindAll(n => n.message == message)) CloseNote(old);

        // made over everything else in the overlay, the blocker too. as tall as its text, which leaves room on the
        // right for the X
        var palette = UIPalette.Current; // the theme color, and its text color
        var bubble = MakeImage("Note", root, raycast: true); // clicks on it don't reach the game
        bubble.color = palette.accent;
        var ort = bubble.rectTransform; // from the bottom-right corner of the screen
        ort.anchorMin = ort.anchorMax = ort.pivot = new Vector2(1, 0);
        var note = new Note { bubble = bubble, message = message, until = seconds > 0 ? Time.unscaledTime + seconds : -1 };

        float closeSize = style.noteCloseSize;
        float textWidth = style.noteWidth - 2 * style.padding - closeSize;
        var text = MakeText("Text", ort);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = palette.text;
        text.fontSize = style.fontSize * 0.85f;
        text.text = message;
        float textHeight = text.GetPreferredValues(message, textWidth, 0).y;
        var trt = text.rectTransform; // the top-left corner, inside the padding
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0, 1);
        trt.anchoredPosition = new Vector2(style.padding, -style.padding);
        trt.sizeDelta = new Vector2(textWidth, textHeight);

        var close = MakeImage("CloseButton", ort, raycast: true);
        close.color = Color.clear; // just the X shows, the square still takes the click
        var crt = close.rectTransform; // the top-right corner
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1, 1);
        crt.anchoredPosition = new Vector2(-style.padding / 2, -style.padding / 2);
        crt.sizeDelta = Vector2.one * closeSize;
        var button = close.gameObject.AddComponent<Button>();
        button.targetGraphic = close;
        button.onClick.AddListener(() => CloseNote(note));
        var x = MakeText("Label", crt);
        x.text = "\u00D7"; // the multiplication sign, as an X
        x.alignment = TextAlignmentOptions.Center;
        x.color = palette.text;
        x.fontSize = style.fontSize;
        Stretch(x.rectTransform);

        ort.sizeDelta = new Vector2(style.noteWidth, Mathf.Max(textHeight, closeSize) + 2 * style.padding);
        notes.Add(note);
        root.SetAsLastSibling(); // over every screen and pop-up
        gameObject.SetActive(true);
        PlaceNotes();
    }

    // Note Margin from the bottom-right corner, raised to Gap above the highest of the elements they keep above; the
    // newest there, each older one Gap above the one after it. every frame, as those can move (a resized screen), and
    // as notes come and go: the others slide to their new places
    void PlaceNotes()
    {
        if (notes.Count == 0) return;
        float bottom = style.noteMargin.y;
        if (noteAbove != null)
            foreach (var find in noteAbove)
                if (find != null && Around(find(), out var r)) bottom = Mathf.Max(bottom, r.yMax + style.gap);
        float slide = 1f - Mathf.Exp(-NoteSlide * Time.unscaledDeltaTime);
        for (int i = notes.Count - 1; i >= 0; i--)
        {
            var n = notes[i];
            n.y = float.IsNaN(n.y) ? bottom : Mathf.Lerp(n.y, bottom, slide);
            n.bubble.rectTransform.anchoredPosition = new Vector2(-style.noteMargin.x, n.y);
            bottom += n.bubble.rectTransform.sizeDelta.y + style.gap;
        }
    }

    void CloseNote(Note note)
    {
        if (note.bubble != null) Destroy(note.bubble.gameObject);
        notes.Remove(note);
        HideIfEmpty();
    }

    // every note closes
    public void HideNotes()
    {
        foreach (var n in notes) if (n.bubble != null) Destroy(n.bubble.gameObject);
        notes.Clear();
        HideIfEmpty();
    }

    // nothing left showing (no step, no blocker, no notes, no floating points): the overlay goes
    void HideIfEmpty()
    {
        if (step == null && !blocker.gameObject.activeSelf && notes.Count == 0 && pops.Count == 0) gameObject.SetActive(false);
    }

    // the end of a Block: things can be clicked again; any notes and floating points stay
    public void Unblock()
    {
        blocker.gameObject.SetActive(false);
        HideIfEmpty();
    }

    // floating points, e.g. "+100": centered over all of `near` together (missing or hidden ones don't count;
    // none found: not shown), its bottom `above` over their top once it has risen. nothing blocked by it, and over
    // the dim. stays until RemovePop, ClearPops or Hide
    public Pop ShowPop(string message, float above, params Func<RectTransform>[] near)
    {
        var t = MakeText("Pop", root);
        t.text = message;
        t.color = UIPalette.Current.accent; // the theme color
        t.fontSize = style.fontSize;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        var brt = t.rectTransform;
        brt.anchorMin = brt.anchorMax = Vector2.zero;
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = t.GetPreferredValues(message) + new Vector2(4, 0);

        var pop = new Pop { badge = brt, fade = t.gameObject.AddComponent<CanvasGroup>(), near = near, above = above,
            shownAt = Time.unscaledTime };
        pops.Add(pop);
        root.SetAsLastSibling(); // over every screen and pop-up
        gameObject.SetActive(true);
        PlacePops();
        return pop;
    }

    public void RemovePop(Pop pop)
    {
        if (pop == null) return;
        if (pop.badge != null) Destroy(pop.badge.gameObject);
        pops.Remove(pop);
    }

    public void ClearPops()
    {
        foreach (var p in pops) if (p.badge != null) Destroy(p.badge.gameObject);
        pops.Clear();
    }

    // each frame, as what they float over can move: over their elements (their own box, without Target Padding),
    // rising and fading in at first. made last, they're over the dim and the frames
    void PlacePops()
    {
        foreach (var pop in pops)
        {
            if (pop.badge == null) continue;
            bool found = false;
            var box = new Rect();
            if (pop.near != null)
                foreach (var find in pop.near)
                    if (find != null && Around(find(), out var r))
                    {
                        box = found ? Rect.MinMaxRect(Mathf.Min(box.xMin, r.xMin), Mathf.Min(box.yMin, r.yMin),
                                                      Mathf.Max(box.xMax, r.xMax), Mathf.Max(box.yMax, r.yMax)) : r;
                        found = true;
                    }
            pop.badge.gameObject.SetActive(found);
            if (!found) continue;
            pop.badge.SetAsLastSibling();
            float k = Mathf.Clamp01((Time.unscaledTime - pop.shownAt) / PopSeconds);
            float ease = 1f - (1f - k) * (1f - k);
            pop.fade.alpha = ease;
            float top = box.yMax - style.targetPadding;
            pop.badge.anchoredPosition = new Vector2(box.center.x,
                top + pop.above - PopRise * (1f - ease) + pop.badge.sizeDelta.y / 2f);
        }
    }

    // canSkip: Skip shows next to Next (TutorialController decides which steps get it)
    public void Show(TutorialStep s, bool canSkip = false)
    {
        step = s;
        nextClicked = skipClicked = false;
        StopShake();
        HideNotes(); // a step takes over the screen

        // the style again each step, so Inspector changes in Play mode show up (the dim and frames take theirs in Layout)
        bubble.color = style.bubbleColor;
        button.color = UIPalette.Current.tutorial; // like the frames; Skip a quiet dark grey, so Next stays the main one
        buttonLabel.color = NextTextColor;
        skipButton.color = SkipColor;
        skipLabel.color = Color.white;
        text.color = style.textColor;
        text.fontSize = style.fontSize;
        buttonLabel.fontSize = skipLabel.fontSize = style.fontSize * 0.85f;

        SetDimAndBubble(true); // Block may have hidden them
        blocker.gameObject.SetActive(true); // only the bubble's buttons take clicks
        button.gameObject.SetActive(true);
        skipButton.gameObject.SetActive(canSkip);
        buttonLabel.text = string.IsNullOrEmpty(s.buttonText) ? "Next" : s.buttonText;

        // the bubble is as tall as its text, plus the buttons below it. a step in the middle of the screen (nothing
        // framed) with text is the Bubble Width in the Style; any other is as wide as its longest line or its buttons,
        // whichever is wider: up to that Bubble Width (longer lines wrap there), or with no limit for a step whose width
        // is below 0. a step's own width wins over both. no text: just the buttons
        bool hasText = !string.IsNullOrEmpty(s.text);
        bool centered = s.targets == null || s.targets.Length == 0;
        float buttonsWidth = style.buttonSize.x + (canSkip ? style.gap + style.buttonSize.x : 0);
        float snug = Mathf.Max(hasText ? text.GetPreferredValues(s.text).x + 1f : 0, buttonsWidth) + 2 * style.padding;
        float width = s.bubbleWidth > 0 ? s.bubbleWidth
            : s.bubbleWidth < 0 ? snug
            : centered && hasText ? style.bubbleWidth
            : Mathf.Min(snug, style.bubbleWidth);
        float textWidth = width - 2 * style.padding;
        text.text = hasText ? s.text : "";
        float textHeight = hasText ? text.GetPreferredValues(s.text, textWidth, 0).y : 0;
        text.rectTransform.anchoredPosition = new Vector2(0, -style.padding);
        text.rectTransform.sizeDelta = new Vector2(-2 * style.padding, textHeight);
        button.rectTransform.anchoredPosition = new Vector2(-style.padding, style.padding);
        button.rectTransform.sizeDelta = style.buttonSize;
        skipButton.rectTransform.anchoredPosition = new Vector2(-style.padding - style.buttonSize.x - style.gap, style.padding);
        skipButton.rectTransform.sizeDelta = style.buttonSize;
        bubbleSize = new Vector2(width,
            style.padding + textHeight + (hasText ? style.gap : 0) + style.buttonSize.y + style.padding);

        root.SetAsLastSibling(); // over every screen and pop-up
        gameObject.SetActive(true);
        Layout();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        foreach (var n in notes) if (n.bubble != null) Destroy(n.bubble.gameObject); // the notes too
        notes.Clear();
        ClearPops(); // and the floating points
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

    // true once per click on Skip
    public bool ConsumeSkip()
    {
        bool clicked = skipClicked;
        skipClicked = false;
        return clicked;
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
        foreach (var n in notes.FindAll(n => n.until >= 0 && Time.unscaledTime >= n.until)) CloseNote(n); // their time is up
    }

    // the screen can be resized, so the pieces are placed again every frame
    void LateUpdate()
    {
        PlaceNotes();
        PlacePops();
        Layout();
        UpdateShake();
    }

    // the frames swing to point the player back at the areas; a new shake starts over
    void Shake() => shakeStart = Time.unscaledTime;

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

    // the step's holes in this overlay's units, from the bottom-left corner: its targets, found again every frame, so
    // they follow their elements wherever the UI puts them. holes that overlap or touch (rows one above the other,
    // slots side by side) are one hole, with one frame
    List<Rect> Holes()
    {
        var holes = new List<Rect>();
        if (step.targets != null)
            foreach (var find in step.targets)
                if (find != null && Around(find(), out var hole)) holes.Add(hole);
        return MergeTouching(holes.Where(h => h.width > 0 && h.height > 0).ToList(), Mathf.Max(0f, style.frameWidth));
    }

    // any two closer than `slack` (their frames would meet) become the box around both, again and again until no
    // two are. for things in a row or a column that box is just them; an L of them would take in its corner
    static List<Rect> MergeTouching(List<Rect> holes, float slack)
    {
        bool merged = true;
        while (merged)
        {
            merged = false;
            for (int i = 0; i < holes.Count && !merged; i++)
                for (int j = i + 1; j < holes.Count && !merged; j++)
                {
                    Rect a = holes[i], b = holes[j];
                    if (a.xMin - slack > b.xMax || b.xMin - slack > a.xMax || a.yMin - slack > b.yMax || b.yMin - slack > a.yMax)
                        continue; // apart
                    holes[i] = Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                                               Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
                    holes.RemoveAt(j);
                    merged = true;
                }
        }
        return holes;
    }

    // the box around a game element, plus the style's Target Padding, in this overlay's units from the bottom-left
    // corner, kept on screen. all four corners count, so a rotated element (an object facing sideways) still fits.
    // false when the element is missing or hidden: that target gets no hole this frame
    bool Around(RectTransform target, out Rect hole)
    {
        hole = default;
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        target.GetWorldCorners(corners);
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        foreach (var c in corners)
        {
            var p = (Vector2)root.InverseTransformPoint(c) - root.rect.min;
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        var size = root.rect.size;
        float pad = style.targetPadding;
        hole = Rect.MinMaxRect(
            Mathf.Clamp(min.x - pad, 0, size.x), Mathf.Clamp(min.y - pad, 0, size.y),
            Mathf.Clamp(max.x + pad, 0, size.x), Mathf.Clamp(max.y + pad, 0, size.y));
        return true;
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
                piece.color = Dim;
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
                    piece.color = UIPalette.Current.tutorial;
                    Place(piece.rectTransform, edge);
                }
        HideFrom(frames, f);

        if (holes.Count == 0)
        {
            PlaceBubble(new Rect(size / 2, Vector2.zero), BubbleSide.Center);
            return;
        }
        // next to all the holes together (or the step's Bubble By elements only), keeping its gap from the frames,
        // not from the holes
        var by = holes;
        if (step.bubbleBy != null)
        {
            by = new List<Rect>();
            foreach (var find in step.bubbleBy)
                if (find != null && Around(find(), out var r)) by.Add(r);
            if (by.Count == 0) by = holes;
        }
        var all = Rect.MinMaxRect(by.Min(h => h.xMin) - w, by.Min(h => h.yMin) - w,
                                  by.Max(h => h.xMax) + w, by.Max(h => h.yMax) + w);
        PlaceBubble(all, step.bubble);
    }

    // the dim over everything but the step's holes: light enough that the board and the HUD around them still read,
    // dark enough that the bubble stands out from the board's background (0.3 blended in, 0.65 hid too much)
    static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);

    // the Next button's label, dark on the palette's Tutorial yellow; the Skip button, left of Next
    static readonly Color NextTextColor = new Color(0.08f, 0.08f, 0.10f);
    static readonly Color SkipColor = new Color(0.27f, 0.29f, 0.34f);

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
