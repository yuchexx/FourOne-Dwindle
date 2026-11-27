using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the hint button: a dot per stage used, the tile line (from stage 1), and a check before stage 2 (the answer),
// which costs the purple stars
public class HintButton : MonoBehaviour
{
    public GameManager game;
    public Button button;
    public Graphic[] dots;           // one per stage, left to right
    public TMP_Text tilesLine;       // stage 1: "Best plans use 8 tiles, 3 left"
    [Tooltip("Optional: a box behind the stage 1 line, shown and hidden with it")]
    public GameObject tilesBox;
    public GameObject noPurpleLabel; // shown once the answer has been used
    [Tooltip("Optional: the note above Hide / Show, there from stage 1")]
    public HintNote stageNote;
    [Tooltip("Asks before stage 2 (the answer); its buttons call ShowAnswer and CloseConfirm")]
    public GameObject confirmPanel;
    [Tooltip("The Hide / Show button's text. Its On Click: HintButton > ToggleHidden")]
    public TMP_Text hideLabel;
     [System.NonSerialized] public string hideText = "Hide", showText = "Show";
    [Tooltip("Optional: opens Hide / Show on hover, like the More menu. It only opens once a hint is used")]
    public HoverReveal hoverPanel;

    void Start()
    {
        game.HintsChanged += Refresh;
        MakeHideIcon();
        CloseConfirm();
        Refresh();
    }

    void OnDestroy()
    {
        if (game != null) game.HintsChanged -= Refresh;
    }

    void Update()
    {
        bool can = game.HintAvailable;
        if (button.interactable != can) button.interactable = can;
        // the lines pulse with the board's hints
        float k = game.board.HintPulseLevel;
        if (tilesLine != null && tilesLine.gameObject.activeSelf) tilesLine.alpha = k;
        foreach (var (part, baseAlpha) in orderPulse) if (part != null) part.alpha = baseAlpha * k; // the whole order line pulses, not just "Deliver"
        if (stageNote != null && stageNote.gameObject.activeInHierarchy) stageNote.Pulse(k);
    }

    public void ToggleHidden() => game.ToggleHintHidden();

    // the button's On Click
    public void Press()
    {
        bool answerNext = game.HintStage == Hints.Stages - 1 || (game.HintStage == 0 && game.Stage1Empty); // the answer: asked first (stage 1 skipped when it has nothing to show)
        if (answerNext && confirmPanel != null) confirmPanel.SetActive(true);
        else game.UseHint();
    }

    public void ShowAnswer()
    {
        CloseConfirm();
        game.UseHint();
    }

    public void CloseConfirm()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    // the Hide / Show button as the game's icon buttons: its round background goes, an eye icon above its name
    // (eye-closed while the hints show: "Hide"; eye while they're hidden: "Show"), IconHover for the hover look
    Image hideIcon;
    void MakeHideIcon()
    {
        if (hideLabel == null) return;
        var b = hideLabel.GetComponentInParent<Button>(true); // the drop-down it sits in is off until hovered: look in it anyway
        if (b == null) return;
        // no background (it still takes the clicks): the scripts that repaint it (hover, palette colour) go first
        var hover = b.GetComponent<ButtonHover>(); if (hover != null) Destroy(hover);
        foreach (var pc in b.GetComponents<PaletteColor>()) pc.enabled = false;
        b.transition = Selectable.Transition.None;
        if (b.targetGraphic != null) b.targetGraphic.color = Color.clear;
        var brt = (RectTransform)b.transform;
        brt.sizeDelta = ((RectTransform)transform).sizeDelta; // as big as the Hints button itself (76)
        brt.anchoredPosition = new Vector2(0f, hideSlotY);
        // the icon: the Hints button's own icon box (Content > Image)
        var model = transform.Find("Content/Image") as RectTransform;
        var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(b.transform, false);
        rt.SetAsFirstSibling();
        if (model != null) Same(model, rt);
        else { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = new Vector2(0f, -6f); rt.sizeDelta = Vector2.one * 40f; }
        hideIcon = go.GetComponent<Image>();
        hideIcon.preserveAspect = true; hideIcon.raycastTarget = false;
        // the name along the bottom, as on the More menu's buttons; font and size from the Hints button's own name
        var lrt = hideLabel.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f); lrt.pivot = new Vector2(0.5f, 0f);
        lrt.anchoredPosition = new Vector2(0f, 4f); lrt.sizeDelta = new Vector2(0f, 33f);
        var name = transform.Find("Content")?.GetComponentInChildren<TMP_Text>(true);
        if (name != null) { hideLabel.font = name.font; hideLabel.fontSize = name.fontSize; hideLabel.fontStyle = name.fontStyle; }
        hideLabel.alignment = TextAlignmentOptions.Center;
        hideLabel.enableAutoSizing = false;
        hideLabel.color = UIPalette.Current.text;
        SetHideIcon(false);
        var ih = b.GetComponent<IconHover>();
        if (ih == null) ih = b.gameObject.AddComponent<IconHover>(); // after the icon and name: it finds them
        var relay = b.gameObject.AddComponent<HoverRelay>(); // under the mouse the eye flips: it shows what a click will do
        relay.onEnter = () => { hideHovered = true; SetHideIcon(game.HintHidden); };
        relay.onExit = () => { hideHovered = false; SetHideIcon(game.HintHidden); };
        b.onClick.AddListener(() => { hideHovered = false; SetHideIcon(game.HintHidden); }); // after a click it shows the new state, till the mouse comes back
        MakeGuideButton(b);
    }

    [Header("Hide / Show button and the note (in the hover drop-down)")]
    [Tooltip("The Hide / Show button's height above the Hints button (the More menu's first slot is 46)")]
     [System.NonSerialized] public float hideSlotY = 46f;
    [Tooltip("From one drop-down button to the next (the More menu's: 84)")]
     [System.NonSerialized] public float slotStep = 84f;

    // "Guide": a copy of the Hide / Show button in the next slot up, the rules icon; GuideClicked says it was pressed
    // (nothing listens yet: the hint guide is still to be designed). it replaces the note above Hide / Show
    public event System.Action GuideClicked;
    GameObject guideButton;

    // for the hint guide: the parts it points at, and the drop-down held open while it shows Hide / Show and Hint help
    public RectTransform OrderRect => tilesBox != null ? (RectTransform)tilesBox.transform : null;
    public RectTransform HideRect => hideLabel != null ? hideLabel.GetComponentInParent<Button>(true)?.transform as RectTransform : null;
    public RectTransform GuideRect => guideButton != null ? (RectTransform)guideButton.transform : null;
    public void HoldOpen(bool hold) { if (hoverPanel != null && hoverPanel.enabled) hoverPanel.Hold(hold); }
     [System.NonSerialized] public string guideButtonText = "Hint guide"; // (renamed from guideText so the new label applies over a saved one)
    void MakeGuideButton(Button hide)
    {
        var b = Instantiate(hide, hide.transform.parent);
        b.name = "GuideButton";
        guideButton = b.gameObject;
        b.onClick = new Button.ButtonClickedEvent(); // not Hide / Show's
        b.onClick.AddListener(() => { GuideClicked?.Invoke(); game.ShowHintGuide(); }); // the guide for the stage shown now
        foreach (var r in b.GetComponents<HoverRelay>()) Destroy(r);
        var brt = (RectTransform)b.transform;
        brt.anchoredPosition = new Vector2(0f, hideSlotY + slotStep);
        var label = b.GetComponentInChildren<TMP_Text>(true);
        if (label != null) { label.text = guideButtonText; label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Overflow; } // one line, even if a bit wider than the button
        var icon = b.transform.Find("Icon")?.GetComponent<Image>();
        var tex = Resources.Load<Texture2D>("UIIcons/question");
        if (icon != null && tex != null) icon.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    static void Same(RectTransform from, RectTransform to)
    {
        to.anchorMin = from.anchorMin; to.anchorMax = from.anchorMax; to.pivot = from.pivot;
        to.anchoredPosition = from.anchoredPosition; to.sizeDelta = from.sizeDelta;
    }

    bool hideHovered;
    void SetHideIcon(bool hidden)
    {
        if (hideIcon == null) return;
        var tex = Resources.Load<Texture2D>((!hidden ^ hideHovered) ? "UIIcons/eye" : "UIIcons/eye-closed"); // open eye while the hints show; flipped under the mouse
        if (tex == null) Debug.LogWarning("HintButton: Resources/UIIcons/eye.png or eye-closed.png not found (copy them into Assets/Resources/UIIcons)");
        if (tex != null) hideIcon.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    int shownStage;

    // the line under the dock: "Deliver" then each delivery's objects as dots, "then" between deliveries; a dot fades
    // once its object is delivered. the dots and words go in the box's row beside the text (its layout lines them up)
    [Header("Order line (under the dock)")]
     [System.NonSerialized] public float orderDotSize = 18f;
     [System.NonSerialized] public float orderDeliveredAlpha = 0.25f;
    readonly System.Collections.Generic.List<GameObject> orderParts = new();
    readonly System.Collections.Generic.List<(CanvasGroup part, float baseAlpha)> orderPulse = new(); // each dot and word, and its own alpha (a delivered dot is faded)
    void ShowOrder()
    {
        foreach (var g in orderParts) if (g != null) Destroy(g);
        orderParts.Clear();
        orderPulse.Clear();
        tilesLine.text = "Deliver";
        var row = tilesLine.transform.parent;
        var groups = game.HintOrderLine();
        for (int i = 0; i < groups.Count; i++)
        {
            if (i > 0) Word(row, "then");
            foreach (var (color, delivered) in groups[i])
            {
                var holder = new GameObject("OrderDot", typeof(RectTransform), typeof(LayoutElement));
                holder.transform.SetParent(row, false);
                float w = orderDotSize + 6f; // a little space each side
                ((RectTransform)holder.transform).sizeDelta = new Vector2(w, orderDotSize);
                var le = holder.GetComponent<LayoutElement>(); le.preferredWidth = le.minWidth = w; le.preferredHeight = orderDotSize;
                var obj = Instantiate(game.board.objectPrefab, holder.transform); // the game's object: its colour, the white arrow
                obj.Setup(color, 60f); obj.Rect.localScale = Vector3.one * (orderDotSize / 60f); // drawn at the game's size, then shrunk whole: the triangle stays centred as on the board
                obj.Face(Dir.E);
                var rt = obj.Rect;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                foreach (var g in obj.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
                var group = holder.AddComponent<CanvasGroup>(); // faded once delivered; pulses with the line
                group.alpha = delivered ? orderDeliveredAlpha : 1f;
                orderPulse.Add((group, group.alpha));
                orderParts.Add(holder);
            }
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)row); // the box fits its new contents at once
    }

    void Word(Transform row, string s)
    {
        var t = Instantiate(tilesLine, row);
        t.text = s;
        t.name = "OrderWord";
        t.alpha = 1f; // the copy took "Deliver"'s alpha mid-pulse; its group pulses it now
        orderPulse.Add((t.gameObject.AddComponent<CanvasGroup>(), 1f)); // pulses with the line
        orderParts.Add(t.gameObject);
    }

    void Refresh()
    {
        int stage = game.HintStage;
        bool reached = stage == 1 && stage > shownStage; // stage 1 (the order) just came: its menu opens once, like a hover
        shownStage = stage;
        bool hidden = game.HintHidden;
        var palette = UIPalette.Current;
        bool oneStage = game.Stage1Empty; // stage 1 skipped (e.g. Level 2): one dot, for the answer
        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null) continue;
            dots[i].gameObject.SetActive(!oneStage || i == 0);
            dots[i].color = (oneStage ? stage >= Hints.Stages : i < stage) ? palette.hintAccent : palette.mutedText;
        }
        if (tilesLine != null)
        {
            (tilesBox != null ? tilesBox : tilesLine.gameObject).SetActive(stage >= 1 && !hidden);
            if (stage >= 1) ShowOrder(); // the delivery order, as dots beside "Deliver"
        }
        if (noPurpleLabel != null) noPurpleLabel.SetActive(game.PurpleLost);
        if (stageNote != null)
        {
            stageNote.Show(stage, game.board);
            stageNote.gameObject.SetActive(false); // replaced by the guide button
            if (guideButton != null) guideButton.SetActive(stage >= 1 && !hidden); // not while the hints are hidden
        }
        if (hideLabel != null) hideLabel.text = hidden ? showText : hideText;
        SetHideIcon(hidden);
        if (hoverPanel != null)
        {
            hoverPanel.enabled = stage >= 1; // nothing to show or hide before the first hint
            if (reached) hoverPanel.Open();
        }
    }
}
