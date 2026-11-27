using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// the tutorial's look, set on TutorialController (in Play mode, changes show from the next step). the frames and the
// Next button and the floating points are in the UI Palette's Tutorial color (a yellow), Skip and Back a fixed grey
// (SkipColor), the dim fixed too (Dim); the notes are in the UI Palette's Accent (the theme color)
[Serializable]
public class TutorialStyle
{
     [System.NonSerialized] public float frameWidth = 4f;                              // 0 = no frame
     [System.NonSerialized] public float targetPadding = 6f;  // around a target's element (TutorialStep.targets), so the frame doesn't touch it
     [System.NonSerialized] public float shakeDistance = 8f;  // how far the frames swing when the dim is clicked; 0 = no shake
     [System.NonSerialized] public float shakeSeconds = 0.35f;
     [System.NonSerialized] public Color bubbleColor = new Color(0.13f, 0.14f, 0.17f);
     [System.NonSerialized] public Color textColor = Color.white;
     [System.NonSerialized] public float fontSize = 26f;
     [System.NonSerialized] public float bubbleWidth = 560f;
     [System.NonSerialized] public float padding = 20f;  // inside the bubble
     [System.NonSerialized] public Vector2 buttonSize = new Vector2(120f, 44f);
     [System.NonSerialized] public float gap = 16f;      // between the areas and the bubble, and between the bubble and the screen edge
    [Header("Notes (a coach's, in the bottom-right corner, in the UI Palette's Accent)")]
     [System.NonSerialized] public float noteWidth = 400f;
    // from the screen's right and bottom edges (lined up with Restart's right edge). the notes also keep Gap above
    // whatever their coach names (TutorialCoach.noteAbove: the supply bar and the buttons), wherever they are; several
    // stack up, the newest at the bottom, Gap apart
     [System.NonSerialized] public Vector2 noteMargin = new Vector2(22f, 24f);
     [System.NonSerialized] public float noteCloseSize = 32f;                  // its X button, in the top-right corner
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
    Image bubble, button, skipButton, backButton;
    TMP_Text text, buttonLabel, skipLabel, backLabel;

    TutorialStep step;
    // the overlay showing a step that lets hint tips through, if any; its bubble still counts as covering the board
    public static TutorialOverlay HoverHints { get; private set; }
    public bool IsBubble(Transform t) => t.IsChildOf(bubble.transform);
    void OnDisable() { if (HoverHints == this) HoverHints = null; }
    Vector2 bubbleSize;
    bool nextClicked, skipClicked, backClicked;
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

    // floating points (ShowPop): bold text in the UI Palette's Tutorial yellow, over the elements it's about, rising
    // a little as it fades in
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

        backButton = MakeImage("BackButton", bubble.rectTransform, raycast: true);
        var brt = backButton.rectTransform; // the bottom-right corner too, moved left of Next (and Skip) in Show
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(1, 0);
        var back = backButton.gameObject.AddComponent<Button>();
        back.targetGraphic = backButton;
        back.onClick.AddListener(() => backClicked = true);

        backLabel = MakeText("Label", brt);
        backLabel.text = "Back";
        backLabel.fontStyle = FontStyles.Bold;
        backLabel.alignment = TextAlignmentOptions.Center;
        Stretch(backLabel.rectTransform);

        // icons in place of the words (the plan): next, skip, back
        foreach (var img in new[] { bubble, button, skipButton, backButton }) Round(img); // rounded corners
        nextIcon = Icon(nrt, "nextpage"); Icon(srt, "endlvl"); Icon(brt, "prevpage");
        buttonLabel.gameObject.SetActive(false); skipLabel.gameObject.SetActive(false); backLabel.gameObject.SetActive(false);

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
        Round(bubble);
        bubble.color = palette.tutorial; // like the guide boxes
        var ort = bubble.rectTransform; // from the bottom-right corner of the screen
        ort.anchorMin = ort.anchorMax = ort.pivot = new Vector2(1, 0);
        var note = new Note { bubble = bubble, message = message, until = seconds > 0 ? Time.unscaledTime + seconds : -1 };

        float closeSize = style.noteCloseSize;
        float textWidth = style.noteWidth - 2 * style.padding - closeSize;
        var text = MakeText("Text", ort);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = NextTextColor;
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
        x.color = NextTextColor;
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
        if (step == null && !blocker.gameObject.activeSelf && notes.Count == 0 && pops.Count == 0 && (errorBox == null || !errorBox.gameObject.activeSelf)) gameObject.SetActive(false);
    }

    // the end of a Block: things can be clicked again; any notes and floating points stay
    public void Unblock()
    {
        blocker.gameObject.SetActive(false);
        HideIfEmpty();
    }

    // floating points, e.g. "+100": centered over all of `near` together (missing or hidden ones don't count;
    // none found: not shown), its bottom `above` over their top once it has risen. nothing blocked by it, and over
    // the dim. stays until ClearPops or Hide
    public Pop ShowPop(string message, float above, params Func<RectTransform>[] near)
    {
        var t = MakeText("Pop", root);
        t.text = message;
        t.color = UIPalette.Current.tutorial; // the tutorial's yellow, as the frames
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

    // canSkip: Skip shows left of Next; canBack: Back shows left of them (TutorialController decides which steps get them)
    public void Show(TutorialStep s, bool canSkip = false, bool canBack = false)
    {
        step = s;
        HoverHints = s != null && s.hoverHints ? this : (HoverHints == this ? null : HoverHints);
        nextClicked = skipClicked = backClicked = false;
        StopShake();
        HideNotes(); // a step takes over the screen

        // the style again each step, so Inspector changes in Play mode show up (the dim and frames take theirs in Layout)
        bubble.color = UIPalette.Current.tutorial; // every guide box in the tutorial colour, dark text (the plan)
        button.color = BubbleButton; // dark on the tutorial colour
        buttonLabel.color = Color.white;
        skipButton.color = backButton.color = SkipColor;
        skipLabel.color = backLabel.color = Color.white;
        text.color = NextTextColor;
        text.fontSize = style.fontSize;
        buttonLabel.fontSize = skipLabel.fontSize = backLabel.fontSize = style.fontSize * 0.85f;

        bool doIt = s.doneWhen != null || s.until != null; // a "do it" step: no button, and the framed areas take clicks (the dim still blocks the rest)
        if (doIt) canSkip = canBack = false;
        SetDimAndBubble(true); // Block may have hidden them
        dimLayer.gameObject.SetActive(!doIt); // a "do it" step blocks nothing
        blocker.gameObject.SetActive(!doIt); // only the bubble's buttons take clicks
        button.gameObject.SetActive(!doIt);
        skipButton.gameObject.SetActive(canSkip);
        backButton.gameObject.SetActive(canBack);
        buttonLabel.text = string.IsNullOrEmpty(s.buttonText) ? "Next" : s.buttonText;
        SetNextIcon(s.buttonIcon); // the arrow, or the step's own icon (e.g. next level)

        // the bubble is as tall as its text, plus the buttons below it. a step in the middle of the screen (nothing
        // framed) with text is the Bubble Width in the Style; any other is as wide as its longest line or its buttons,
        // whichever is wider: up to that Bubble Width (longer lines wrap there), or with no limit for a step whose width
        // is below 0. a step's own width wins over both. no text: just the buttons
        bool hasText = !string.IsNullOrEmpty(s.text);
        bool centered = s.targets == null || s.targets.Length == 0;
        int buttons = 1 + (canSkip ? 1 : 0) + (canBack ? 1 : 0); // in a row from the right: Next, Skip, Back
        float buttonsWidth = buttons * style.buttonSize.x + (buttons - 1) * style.gap;
        float snug = Mathf.Max(hasText ? text.GetPreferredValues(s.text).x + 1f : 0, buttonsWidth) + 2 * style.padding;
        float width = s.bubbleWidth > 0 ? s.bubbleWidth
            : s.bubbleWidth < 0 ? snug
            : centered && hasText ? style.bubbleWidth
            : Mathf.Min(snug, style.bubbleWidth);
        if (doIt || s.bubble == BubbleSide.TopLeft) width = s.bubbleWidth > 0 ? s.bubbleWidth : TopLeftWidth; // do-it and top-left boxes: always the same width (also fits beside the Settings window)
        float textWidth = width - 2 * style.padding;
        text.text = hasText ? s.text : "";
        float textHeight = hasText ? text.GetPreferredValues(s.text, textWidth, 0).y : 0;
        text.rectTransform.anchoredPosition = new Vector2(0, -style.padding);
        text.rectTransform.sizeDelta = new Vector2(-2 * style.padding, textHeight);
        button.rectTransform.anchoredPosition = new Vector2(-style.padding, style.padding);
        button.rectTransform.sizeDelta = style.buttonSize;
        float slot = style.buttonSize.x + style.gap; // from one button's right edge to the next one's
        skipButton.rectTransform.anchoredPosition = new Vector2(-style.padding - slot, style.padding);
        skipButton.rectTransform.sizeDelta = style.buttonSize;
        backButton.rectTransform.anchoredPosition = new Vector2(-style.padding - slot * (canSkip ? 2 : 1), style.padding);
        backButton.rectTransform.sizeDelta = style.buttonSize;
        // a picture under the text (TutorialStep.drawPicture): a box as wide as the text, made fresh for each step
        if (picture != null) Destroy(picture.gameObject);
        picture = null;
        float pictureRoom = s.drawPicture != null && s.pictureHeight > 0f ? s.pictureHeight + style.gap : 0f;
        if (pictureRoom > 0f)
        {
            picture = (RectTransform)new GameObject("Picture", typeof(RectTransform)).transform;
            picture.SetParent(bubble.rectTransform, false);
            picture.anchorMin = new Vector2(0f, 1f); picture.anchorMax = new Vector2(1f, 1f); picture.pivot = new Vector2(0.5f, 1f);
            picture.anchoredPosition = new Vector2(0f, -style.padding - textHeight - style.gap);
            picture.sizeDelta = new Vector2(-2 * style.padding, s.pictureHeight);
            s.drawPicture(picture);
        }
        bubbleSize = new Vector2(width, doIt ? style.padding + textHeight + pictureRoom + style.padding
            : style.padding + textHeight + pictureRoom + (hasText ? style.gap : 0) + style.buttonSize.y + style.padding);

        root.SetAsLastSibling(); // over every screen and pop-up
        gameObject.SetActive(true);
        Layout();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        if (picture != null) { Destroy(picture.gameObject); picture = null; }
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
    // true once per click on Back
    public bool ConsumeBack()
    {
        bool clicked = backClicked;
        backClicked = false;
        return clicked;
    }

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
        if (errorBox != null && errorBox.gameObject.activeSelf && Time.unscaledTime > errorUntil) { errorBox.gameObject.SetActive(false); HideIfEmpty(); } // a wrong move's box goes after its time
        if (errorBox != null && errorBox.gameObject.activeSelf) PlaceError(); // up when the yellow box goes, down when one comes back
        // the pause menu (or Rules) open: the guide steps aside till it closes
        if (hideGroup == null) { hideGroup = root.GetComponent<CanvasGroup>(); if (hideGroup == null) hideGroup = root.gameObject.AddComponent<CanvasGroup>(); } // not ??: Unity's missing component isn't a real null
        bool hide = HideWhile != null && HideWhile();
        stepHidden = step != null && step.hideWhen != null && step.hideWhen(); // the step's own: its box and target frames go, its label boxes stay (TutorialStep.hideWhen)
        if (bubble != null) bubble.rectTransform.localScale = stepHidden ? Vector3.zero : Vector3.one;
        hideGroup.alpha = hide ? 0f : 1f; hideGroup.blocksRaycasts = !hide;
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
        if (step.targets != null && !stepHidden)
            foreach (var find in step.targets)
                if (find != null && Around(find(), out var hole)) holes.Add(hole);
        var merged = MergeTouching(holes.Where(h => h.width > 0 && h.height > 0).ToList(), Mathf.Max(0f, style.frameWidth));
        foreach (var b in LabelBoxes()) if (b != null) merged.Add(b.Value); // one box around each labelled group (never merged)
        return merged;
    }

    // each labelled group's box; two side by side are pulled apart so their outlines keep a gap between them
    List<Rect?> LabelBoxes()
    {
        var boxes = new List<Rect?>();
        if (step?.labels == null) return boxes;
        foreach (var (parts, _) in step.labels) boxes.Add(Union(parts, out var u) ? u : (Rect?)null);
        float w = Mathf.Max(0f, style.frameWidth), gap = 8f;
        for (int i = 0; i < boxes.Count; i++)
            for (int j = 0; j < boxes.Count; j++)
            {
                if (i == j || boxes[i] == null || boxes[j] == null) continue;
                Rect a = boxes[i].Value, b = boxes[j].Value;
                bool rows = a.yMin < b.yMax && b.yMin < a.yMax; // side by side
                if (!rows || a.center.x >= b.center.x) continue; // a is the left one
                float need = 2 * w + gap;
                if (b.xMin - a.xMax >= need) continue;
                float mid = (a.xMax + b.xMin) / 2f;
                float da = Mathf.Max(0f, a.xMax - (mid - need / 2f)), db = Mathf.Max(0f, (mid + need / 2f) - b.xMin);
                a.xMax -= da; a.xMin += da; b.xMin += db; b.xMax -= db; // each narrowed on both sides, so it stays centred on its content
                boxes[i] = a; boxes[j] = b;
            }
        if (step.align != null)
            foreach (var (bi, side, to) in step.align)
            {
                if (bi >= boxes.Count || to >= boxes.Count || boxes[bi] == null || boxes[to] == null) continue;
                Rect a = boxes[bi].Value, o = boxes[to].Value;
                // that side moves to the other box's, and the opposite side moves the same amount the other way: still centred
                switch (side)
                {
                    case 'L': { float d = a.xMin - o.xMin; a.xMin -= d; a.xMax += d; break; }
                    case 'R': { float d = o.xMax - a.xMax; a.xMax += d; a.xMin -= d; break; }
                    case 'T': { float d = o.yMax - a.yMax; a.yMax += d; a.yMin -= d; break; }
                    case 'B': { float d = a.yMin - o.yMin; a.yMin -= d; a.yMax += d; break; }
                }
                boxes[bi] = a;
            }
        return boxes;
    }

    // the box around every part that's on screen
    bool Union(Func<RectTransform>[] parts, out Rect u)
    {
        u = default; bool any = false;
        if (parts == null) return false;
        foreach (var find in parts)
        {
            if (find == null || !Around(find(), out var r) || r.width <= 0 || r.height <= 0) continue;
            u = any ? Rect.MinMaxRect(Mathf.Min(u.xMin, r.xMin), Mathf.Min(u.yMin, r.yMin), Mathf.Max(u.xMax, r.xMax), Mathf.Max(u.yMax, r.yMax)) : r;
            any = true;
        }
        return any;
    }

    // any two that touch (closer than `slack`: their frames would meet) side by side or one above the other (sharing
    // at least half of the smaller one's height, or width) become the box around both, again and again until no two
    // do. for things in a row or a column that box is just them; two that only meet at a corner (squares diagonal to
    // each other) stay apart
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
                    float overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                    float overlapY = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                    if (overlapX < -slack || overlapY < -slack) continue; // apart
                    if (overlapX < 0.5f * Mathf.Min(a.width, b.width) && overlapY < 0.5f * Mathf.Min(a.height, b.height))
                        continue; // only corner to corner
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
        float pad = style.targetPadding, e = Mathf.Max(0f, style.frameWidth); // kept a frame's width in, so every edge shows
        hole = Rect.MinMaxRect(
            Mathf.Clamp(min.x - pad, e, size.x - e), Mathf.Clamp(min.y - pad, e, size.y - e),
            Mathf.Clamp(max.x + pad, e, size.x - e), Mathf.Clamp(max.y + pad, e, size.y - e));
        return true;
    }

    void Layout()
    {
        if (step == null) return;
        PlaceWordBadges();
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
                piece.color = step.dim ? DimOn : Dim;
                Place(piece.rectTransform, Rect.MinMaxRect(xs[start], ys[j], xs[i], ys[j + 1]));
            }
        }
        HideFrom(dims, d);

        // each hole's frame sits on the dim, just outside the hole, so it covers nothing in the hole
        float w = Mathf.Max(0f, style.frameWidth);
        int f = 0;
        if (w > 0)
            foreach (var h in holes)
            {
                // one rounded outline per box (whole units, so every side draws the same width)
                var piece = Pooled(frames, f++, frameLayer, "Frame", raycast: false);
                piece.sprite = RoundedOutline(); piece.type = Image.Type.Sliced; piece.pixelsPerUnitMultiplier = RoundScale / w;
                piece.color = UIPalette.Current.tutorial;
                Place(piece.rectTransform, Rect.MinMaxRect(Mathf.Round(h.xMin - w), Mathf.Round(h.yMin - w), Mathf.Round(h.xMax + w), Mathf.Round(h.yMax + w)));
            }
        HideFrom(frames, f);

        PlaceLabels();
        if (underLabel is Rect ul)
        {
            // under that label, lined up with its left edge, the same gap as between its box and the label
            Place(bubble.rectTransform, new Rect(new Vector2(ul.xMin, ul.yMin - 6f - bubbleSize.y), bubbleSize));
            return;
        }
        if (holes.Count == 0 && step.bubbleBy != null) // nothing framed, but placed by some elements (e.g. beside the boards)
        {
            var near = new List<Rect>();
            foreach (var find in step.bubbleBy)
                if (find != null && Around(find(), out var r)) near.Add(r);
            if (near.Count > 0)
            {
                var by0 = Rect.MinMaxRect(near.Min(h => h.xMin) - w, near.Min(h => h.yMin) - w, near.Max(h => h.xMax) + w, near.Max(h => h.yMax) + w);
                PlaceBubble(by0, step.bubble); // nothing framed: the top-left spot stays, even over the boards' edge (all dimmed)
                return;
            }
        }
        if (holes.Count == 0)
        {
            bool doIt = step.doneWhen != null || step.until != null; // nothing framed: a do-it step's bubble goes to the top, out of the way
            if (doIt) PlaceBubble(Rect.zero, BubbleSide.TopLeft);
            else PlaceBubble(new Rect(size / 2, Vector2.zero), BubbleSide.Center);
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
        bool toDo = (step.doneWhen != null || step.until != null) && !step.keepSide; // every "do it" box sits top-left, like a to-do
        PlaceBubble(all, toDo ? BubbleSide.TopLeft : step.bubble);
        if (step.alignTop != null && Around(step.alignTop(), out var at)) // its top level with that element's top
            Place(bubble.rectTransform, new Rect(new Vector2(bubble.rectTransform.anchoredPosition.x, Mathf.Max(style.gap, at.yMax - bubbleSize.y)), bubbleSize));
        if (!toDo && step.bubbleBy != null && !ReferenceEquals(by, holes)
            && new Rect(bubble.rectTransform.anchoredPosition, bubbleSize).Overlaps(all))
            NextToHoles(holes, w); // no room beside the boards: next to what the step frames instead
        AvoidHoles(holes, w);
    }

    // next to the framed areas together, on the side with the most room, the first side that covers none of them
    void NextToHoles(List<Rect> holes, float w)
    {
        var size = root.rect.size;
        var u = Rect.MinMaxRect(holes.Min(h => h.xMin) - w, holes.Min(h => h.yMin) - w, holes.Max(h => h.xMax) + w, holes.Max(h => h.yMax) + w);
        var framed = holes.Select(h => Rect.MinMaxRect(h.xMin - w, h.yMin - w, h.xMax + w, h.yMax + w)).ToList();
        var sides = new (BubbleSide side, float room)[] {
            (BubbleSide.Left, u.xMin - bubbleSize.x), (BubbleSide.Right, size.x - u.xMax - bubbleSize.x),
            (BubbleSide.Above, size.y - u.yMax - bubbleSize.y), (BubbleSide.Below, u.yMin - bubbleSize.y) };
        foreach (var (side, _) in sides.OrderByDescending(s => s.room))
        {
            PlaceBubble(u, side);
            if (!framed.Any(f => f.Overlaps(new Rect(bubble.rectTransform.anchoredPosition, bubbleSize)))) return;
        }
    }

    // a bubble pushed onto a framed area (no room on its side) moves next to the framed areas instead: left, right,
    // above or below them, the first that covers none
    void AvoidHoles(List<Rect> holes, float w)
    {
        var framed = holes.Select(h => Rect.MinMaxRect(h.xMin - w, h.yMin - w, h.xMax + w, h.yMax + w)).ToList();
        bool Covers(Rect r) => framed.Any(f => f.Overlaps(r));
        var rt = bubble.rectTransform;
        if (!Covers(new Rect(rt.anchoredPosition, bubbleSize))) return;
        var kept = rt.anchoredPosition;
        foreach (var f in framed)
            foreach (var side in new[] { BubbleSide.Left, BubbleSide.Right, BubbleSide.Above, BubbleSide.Below })
            {
                PlaceBubble(f, side);
                if (!Covers(new Rect(rt.anchoredPosition, bubbleSize))) return;
            }
        rt.anchoredPosition = kept; // nowhere free: as it was
    }

    // an icon (Resources/UIIcons) filling a button, white
    // rounded corners on a box (the sliced rounded sprite)
    public static void Round(Image img) { } // square corners (rounded was tried and turned down)

    static Image Icon(RectTransform button, string name)
    {
        var img = MakeImage("Icon", button, raycast: false);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(28f, 28f);
        var tex = Resources.Load<Texture2D>("UIIcons/" + name);
        if (tex != null) img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        img.preserveAspect = true;
        img.color = Color.white;
        return img;
    }

    // the Next button's icon for a step (TutorialStep.buttonIcon, else the next arrow)
    Image nextIcon; string nextIconName = "nextpage";
    void SetNextIcon(string name)
    {
        name = string.IsNullOrEmpty(name) ? "nextpage" : name;
        if (nextIcon == null || name == nextIconName) return;
        var tex = Resources.Load<Texture2D>("UIIcons/" + name);
        if (tex != null) nextIcon.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        nextIconName = name;
    }

    // a wrong move's message: an error-coloured box just under the to-do box (top left), for a few seconds
    Image errorBox; TMP_Text errorText; float errorUntil;
    public void ShowError(string message, float seconds)
    {
        if (errorBox == null)
        {
            errorBox = MakeImage("ErrorBox", root, raycast: false);
            Round(errorBox);
            errorText = MakeText("Text", errorBox.rectTransform);
            errorText.font = text.font;
            errorText.alignment = TextAlignmentOptions.MidlineLeft;
            Stretch(errorText.rectTransform);
            errorText.rectTransform.offsetMin = new Vector2(style.padding, 6f);
            errorText.rectTransform.offsetMax = new Vector2(-style.padding, -6f);
        }
        errorBox.color = UIPalette.Current.danger;
        errorText.color = Color.white;
        errorText.fontSize = style.fontSize * 0.85f;
        errorText.text = message;
        PlaceError();
        errorBox.gameObject.SetActive(true);
        if (!gameObject.activeSelf) { if (step == null) SetDimAndBubble(false); gameObject.SetActive(true); root.SetAsLastSibling(); } // shown even with no step up
        errorBox.transform.SetAsLastSibling();
        errorUntil = Time.unscaledTime + seconds;
    }
    // under the guide box, as wide as it, as tall as its text
    void PlaceError()
    {
        // upper left: under the yellow to-do box while one is there, else up in its spot
        var b = bubble.rectTransform;
        float x = style.gap * 2, top = root.rect.size.y - TopLeftBelow;
        bool yellowThere = bubble.gameObject.activeInHierarchy && Mathf.Abs(b.anchoredPosition.x - x) < 1f;
        float w = TopLeftWidth;
        float h = errorText.GetPreferredValues(errorText.text, w - 2 * style.padding, 0f).y + 12f;
        float y = yellowThere ? b.anchoredPosition.y - 8f - h : top - h;
        FromBottomLeft(errorBox.rectTransform);
        Place(errorBox.rectTransform, new Rect(x, y, w, h));
    }

    public void HideError() { if (errorBox != null) errorBox.gameObject.SetActive(false); }

    public static Func<bool> HideWhile; // set by the game: true while the pause menu or Rules is open
    bool stepHidden; // the step's hideWhen is true right now
    CanvasGroup hideGroup;
    Rect? underLabel; // the label the bubble goes under (TutorialStep.bubbleUnderLabel), set by PlaceLabels
    readonly List<TMP_Text> labelPool = new();
    readonly List<Image> labelBgs = new();
    Image LabelBg(int i) { while (labelBgs.Count <= i) labelBgs.Add(MakeImage("LabelBox", frameLayer, raycast: false)); return labelBgs[i]; }

    // each labelled group's line, just outside its box: above it in the lower half of the screen, below it in the upper
    // half; lined up with the box's left edge (its right edge for a box on the right side of the screen)
    void PlaceLabels()
    {
        underLabel = null;
        int n = 0;
        var size = root.rect.size;
        float w = Mathf.Max(0f, style.frameWidth);
        var boxes = LabelBoxes();
        if (step?.labels != null)
            for (int li = 0; li < step.labels.Length; li++)
            {
                if (boxes[li] == null) continue;
                var r = boxes[li].Value;
                var msg = step.labels[li].text;
                if (n == labelPool.Count)
                {
                    var t = MakeText("BoxLabel", frameLayer);
                    t.font = text.font;
                    t.fontStyle = FontStyles.Bold;
                    t.outlineWidth = 0.25f; t.outlineColor = new Color32(0, 0, 0, 220); // readable on anything
                    t.textWrappingMode = TextWrappingModes.Normal;
                    labelPool.Add(t);
                }
                var tx = labelPool[n++];
                tx.gameObject.SetActive(true);
                tx.color = UIPalette.Current.tutorial;
                tx.fontSize = style.fontSize;
                tx.fontStyle = FontStyles.Bold; tx.outlineWidth = 0.25f; // set before measuring (a boxed label on an earlier step left it normal)
                msg = msg.ToUpperInvariant(); // capitals
                tx.text = msg;
                float maxW = Mathf.Max(260f, r.width + 40f);
                var ts = (Vector2)tx.GetPreferredValues(msg, maxW, 0f) + new Vector2(2f, 2f); // measured in capitals, as shown
                ts.x = Mathf.Min(ts.x, maxW);
                bool above = r.center.y < size.y / 2;
                bool right = r.center.x > size.x * 0.6f;
                float x = right ? r.xMax + w - ts.x : r.xMin - w;
                float y = above ? r.yMax + w + 6f : r.yMin - w - 6f - ts.y;
                char sideCh = step.labelSides != null && li < step.labelSides.Length ? step.labelSides[li] : ' ';
                if (sideCh == 'L') { x = r.xMin - w - 10f - ts.x; y = r.center.y - ts.y / 2f; right = true; }
                else if (sideCh == 'A') { x = r.center.x - ts.x / 2f; y = r.yMax + w + 6f; }
                else if (sideCh == 'B') { x = r.center.x - ts.x / 2f; y = r.yMin - w - 6f - ts.y; }
                // clear of the other boxes: slide it sideways past any box it would sit on
                for (int oi = 0; oi < boxes.Count; oi++)
                {
                    if (oi == li || boxes[oi] == null) continue;
                    var o = boxes[oi].Value;
                    var me = new Rect(x, y, ts.x, ts.y);
                    var ob = Rect.MinMaxRect(o.xMin - w, o.yMin - w, o.xMax + w, o.yMax + w);
                    if (!me.Overlaps(ob)) continue;
                    x = right ? ob.xMin - 6f - ts.x : ob.xMax + 6f;
                }
                var trt = tx.rectTransform;
                trt.anchorMin = trt.anchorMax = Vector2.zero; trt.pivot = Vector2.zero;
                trt.sizeDelta = ts;
                trt.anchoredPosition = new Vector2(Mathf.Clamp(x, 4f, size.x - ts.x - 4f), Mathf.Clamp(y, 4f, size.y - ts.y - 4f));
                if (li == step.bubbleUnderLabel) underLabel = new Rect(trt.anchoredPosition, ts); // for the bubble below it
                tx.alignment = right ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.BottomLeft;
                // as a small guide box, left of its box (TutorialStep.labelsAsBoxes)
                var bg = LabelBg(n - 1);
                bg.gameObject.SetActive(step.labelsAsBoxes);
                tx.outlineWidth = step.labelsAsBoxes ? 0f : 0.25f;
                if (step.labelsAsBoxes)
                {
                    tx.color = NextTextColor; tx.fontStyle = FontStyles.Normal; tx.text = step.labels[li].text; // like the guide box text
                    // the main box's width, as far from the window's left edge as the main box is from its right (TutorialStep.bubbleBy = the window)
                    float bw = bubbleSize.x > 0 ? bubbleSize.x : TopLeftWidth;
                    if (sideCh == 'A') bw = Mathf.Min(bw, tx.GetPreferredValues(tx.text).x + 2 * style.padding + 4f); // above: as wide as its text
                    var ps = new Vector2(bw - 2 * style.padding, tx.GetPreferredValues(tx.text, bw - 2 * style.padding, 0f).y + 2f);
                    var bs = new Vector2(bw, ps.y + 2 * style.padding);
                    float edge = r.xMin - w;
                    if (step.bubbleBy != null && Union(step.bubbleBy, out var win)) edge = win.xMin - w;
                    var bpos = sideCh == 'A' ? new Vector2(r.center.x - bs.x / 2f, r.yMax + w + style.gap) // above its box, centred (labelSides A)
                        : new Vector2(edge - style.gap - bs.x, r.center.y - bs.y / 2f);
                    FromBottomLeft(bg.rectTransform); Place(bg.rectTransform, new Rect(bpos, bs));
                    bg.color = UIPalette.Current.tutorial;
                    trt.sizeDelta = ps; trt.anchoredPosition = bpos + new Vector2(style.padding, style.padding);
                    tx.alignment = TextAlignmentOptions.MidlineLeft;
                    tx.transform.SetAsLastSibling();
                }
            }
        for (int i = n; i < labelPool.Count; i++) labelPool[i].gameObject.SetActive(false);
        for (int i = n; i < labelBgs.Count; i++) labelBgs[i].gameObject.SetActive(false);
    }


    // words marked <link="badge">…</link> in a step's text get an accent rectangle behind them (the word itself white),
    // like the board's number badges: TMP's own <mark> only tints over the text, it can't fill behind it
    RectTransform picture; // the step's picture, if any (TutorialStep.drawPicture)
    readonly List<Image> wordBadges = new();
    readonly List<StarGraphic> wordStars = new(); // words marked <link="pstar"> are drawn as the purple star
    static StarGraphic starModel;
    void PlaceWordBadges()
    {
        int used = 0, stars = 0;
        if (text != null && text.gameObject.activeInHierarchy && (text.text.Contains("<link=\"badge\">") || text.text.Contains("<link=\"pstar\">")))
        {
            text.ForceMeshUpdate();
            var info = text.textInfo;
            for (int li = 0; li < info.linkCount; li++)
            {
                var link = info.linkInfo[li];
                string id = link.GetLinkID();
                if ((id != "badge" && id != "pstar") || link.linkTextLength == 0) continue;
                Vector2 min = new Vector2(float.MaxValue, 0f), max = new Vector2(float.MinValue, 0f);
                int first = link.linkTextfirstCharacterIndex;
                for (int c = first; c < first + link.linkTextLength && c < info.characterCount; c++)
                {
                    var ch = info.characterInfo[c];
                    if (!ch.isVisible && id != "pstar") continue; // the star's see-through placeholder letters still count
                    min.x = Mathf.Min(min.x, ch.bottomLeft.x); max.x = Mathf.Max(max.x, ch.topRight.x);
                }
                if (min.x > max.x) continue;
                var line = info.lineInfo[info.characterInfo[first].lineNumber];
                min.y = line.descender; max.y = line.ascender;
                if (id == "pstar") // three purple stars (as the end window shows them), drawn in the word's place, as tall as the line
                for (int k = 0; k < 3; k++)
                {
                    while (wordStars.Count <= stars)
                    {
                        // a copy of a star already in the game (a level card's): a star made from scratch here didn't draw
                        var model = starModel != null ? starModel : starModel = FindAnyObjectByType<StarGraphic>(FindObjectsInactive.Include);
                        var sg = model != null ? Instantiate(model, text.transform.parent, false) : new GameObject("WordStar", typeof(RectTransform), typeof(StarGraphic)).GetComponent<StarGraphic>();
                        sg.name = "WordStar";
                        if (model == null) sg.transform.SetParent(text.transform.parent, false);
                        foreach (Transform child in sg.transform) Destroy(child.gameObject); // the star alone
                        sg.raycastTarget = false; sg.enabled = true;
                        sg.rectTransform.localScale = Vector3.one; sg.rectTransform.localEulerAngles = Vector3.zero;
                        wordStars.Add(sg);
                    }
                    var star = wordStars[stars++];
                    star.gameObject.SetActive(true);
                    star.color = UIPalette.Current.purpleStar;
                    star.SetAllDirty();
                    StarSparkle.Set(star, true); // as purple stars do everywhere
                    var srt = star.rectTransform;
                    if (srt.GetSiblingIndex() < text.transform.GetSiblingIndex()) srt.SetAsLastSibling(); // over the (see-through) placeholder letters
                    Vector3 smin = srt.parent.InverseTransformPoint(text.transform.TransformPoint(new Vector3(min.x, min.y, 0f)));
                    Vector3 smax = srt.parent.InverseTransformPoint(text.transform.TransformPoint(new Vector3(max.x, max.y, 0f)));
                    float side = Mathf.Min(Mathf.Abs(smax.y - smin.y) * 0.95f, Mathf.Abs(smax.x - smin.x) / 3f);
                    srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
                    srt.sizeDelta = Vector2.one * side;
                    srt.localPosition = (smin + smax) / 2f + new Vector3((k - 1) * side, 0f, 0f); // left, middle, right
                }
                if (id == "pstar") continue;
                while (wordBadges.Count <= used) wordBadges.Add(MakeImage("WordBadge", (RectTransform)text.transform.parent, raycast: false));
                var b = wordBadges[used++];
                b.gameObject.SetActive(true);
                b.color = UIPalette.Current.accent;
                var brt = b.rectTransform;
                if (brt.GetSiblingIndex() > text.transform.GetSiblingIndex()) brt.SetSiblingIndex(text.transform.GetSiblingIndex()); // just behind the text (moved only when in front: setting it every frame swapped it with the text each time, so it flickered)
                Vector3 pmin = brt.parent.InverseTransformPoint(text.transform.TransformPoint(new Vector3(min.x - 5f, min.y, 0f)));
                Vector3 pmax = brt.parent.InverseTransformPoint(text.transform.TransformPoint(new Vector3(max.x + 5f, max.y, 0f)));
                brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
                brt.sizeDelta = new Vector2(pmax.x - pmin.x, pmax.y - pmin.y);
                brt.localPosition = (pmin + pmax) / 2f;
            }
        }
        for (int i = used; i < wordBadges.Count; i++) wordBadges[i].gameObject.SetActive(false);
        for (int i = stars; i < wordStars.Count; i++) wordStars[i].gameObject.SetActive(false);
    }

    // the dim over everything but the step's holes: light enough that the board and the HUD around them still read,
    // dark enough that the bubble stands out from the board's background (0.3 blended in, 0.65 hid too much)
    static readonly Color Dim = new Color(0f, 0f, 0f, 0f); // no visible dim (the plan): read steps still block clicks, do-it steps don't
    static readonly Color DimOn = new Color(0f, 0f, 0f, 0.55f); // a step that asks for it (TutorialStep.dim)

    // rounded shapes (sliced sprites made in code): a filled box and an outline; RoundScale = the outline texture's border
    const float RoundScale = 6f;
    static Sprite roundedFill, roundedOutline;
    public static Sprite RoundedFill() => roundedFill != null ? roundedFill : roundedFill = MakeRounded(64, 16, 0);
    static Sprite RoundedOutline() => roundedOutline != null ? roundedOutline : roundedOutline = MakeRounded(64, 0, (int)RoundScale); // square corners
    static Sprite MakeRounded(int n, int radius, int ring)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        if (radius == 0) // a square outline, ring pixels wide
        {
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    tex.SetPixel(x, y, new Color(1, 1, 1, x < ring || y < ring || x >= n - ring || y >= n - ring ? 1f : 0f));
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(ring, ring, ring, ring));
        }
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, n - radius), cy = Mathf.Clamp(y + 0.5f, radius, n - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)); // past the straight part, from the corner's centre
                float outer = Mathf.Clamp01(radius - d + 0.5f);
                float a = ring > 0 ? outer * Mathf.Clamp01(d - (radius - ring) + 0.5f) : outer;
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        float b = radius + 1;
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    // the Next button's label, dark on the palette's Tutorial yellow; the Skip button, left of Next
    public static readonly Color NextTextColor = new Color(0.08f, 0.08f, 0.10f); // the Tutorial button's too
    static readonly Color SkipColor = new Color(0.27f, 0.29f, 0.34f);
    const float TopLeftWidth = 330f, TopLeftBelow = 85f; // the to-do box: its width, and how far under the screen's top
    static readonly Color BubbleButton = new Color(0.13f, 0.14f, 0.17f);

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
            BubbleSide.TopLeft => new Vector2(g * 2, size.y - TopLeftBelow - (step != null ? step.topLeftDrop : 0f) - b.y), // just under the title
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
