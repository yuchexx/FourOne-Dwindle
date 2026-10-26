using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the supply bar at the top, the info panel, the removed-tiles panel and the log
public class HudView : MonoBehaviour
{
    [Header("Prefabs")]
    public TileView tilePrefab;
    public TileCard slotPrefab; // one slot per tile type in the supply bar
    public TileCard cardPrefab; // one card per removed tile type

    [Header("Supply bar")]
    public RectTransform slotsRoot;
    public float groupGap = 24f; // extra space between the +, bridge, T, L and I groups
    public float minSlotWidth = 70f; // slots shrink down to this when a level hands out many tile types

    [Header("Info")]
    public TMP_Text levelTitleText;
    public TMP_Text goalText;
    public TMP_Text bonusText;
    public TMP_Text selectedText;
    public TMP_Text logText;
    [Tooltip("The score log in the lower left: points from deliveries, doors and delivering together, and the tiles " +
             "they break. Left empty, it takes over Log Text's spot (the activity log, hidden in the scene)")]
    public TMP_Text scoreLogText;
    [Min(1)] public int scoreLogLines = 8;
    [Tooltip("A dark box behind the score log, made in code")]
    public bool scoreLogBox = true;
    public Color scoreLogBoxColor = new Color(0.13f, 0.14f, 0.17f, 0.9f);
    [Tooltip("Space between the box's edges and the text inside")]
    public float scoreLogBoxPadding = 10f;
    [Tooltip("Space between the SCORE text above and the box's top (on top of the info panel's own spacing; can be below 0)")]
    public float scoreLogBoxGap = 0f;
    [Tooltip("The box is centered in the info panel, this far from its left and right edges")]
    public float scoreLogBoxSideMargin = 20f;
    [Tooltip("The box's height. Lines that don't fit are cut off at its bottom (the oldest, since the newest is on top)")]
    public float scoreLogBoxHeight = 300f;

    [Header("Removed tiles and recovery")]
    public RectTransform crossRemovedRoot;    // + tiles
    public RectTransform cornerRemovedRoot;   // the four L tiles
    public RectTransform straightRemovedRoot; // the two I tiles
    public GameObject choiceDimmer;  // darkens everything except the removed panel
    public TMP_Text removedHeader;   // "Removed:" / "Pick one to get back:"

    [Header("Undo")]
    public Button undoButton; // sits after ChoiceDimmer in the hierarchy, so it stays clickable during a recovery choice

    [Header("Stuck notice (nobody left can be delivered)")]
    public StuckBanner stuckNotice;

    [Header("Colors")]
    public Color tileColor = new Color(0.28f, 0.31f, 0.38f);
    public Color slotColor = new Color(0.32f, 0.34f, 0.40f);
    public Color selectedSlotColor = new Color(0.95f, 0.78f, 0.25f);
    public Color emptySlotColor = new Color(0.22f, 0.23f, 0.27f); // a tile type you have none of (when the bar's own color isn't used)
    public bool emptySlotUsesBarColor = true; // an empty slot's background is the top bar's color, so it blends in
    [Range(0f, 1f)] public float emptySlotDarken = 0.6f; // how much darker a slot with none left is (0 = not at all)
    public Color emptyLabelColor = new Color(0.55f, 0.56f, 0.60f);
    public Color recoverySelectedColor = new Color(0.95f, 0.78f, 0.25f); // the card you click to get back
    public Color flashColor = new Color(1f, 0.45f, 0.45f);
    public Color earnedFlashColor = new Color(0.45f, 0.85f, 0.55f); // the + slot when delivering together earns one
    public Color headingColor = new Color(1f, 0.82f, 0.2f);         // PROGRESS and STAR GOALS in the info panel (#FFD133)
    public float flashHoldSeconds = 0.2f;
    public float flashFadeSeconds = 0.2f;

    readonly Dictionary<TileType, TileCard> slots = new Dictionary<TileType, TileCard>();
    readonly List<string> log = new List<string>();
    readonly HashSet<TileType> flashingSlots = new HashSet<TileType>();
    TileType? lastSelected;
    Dictionary<TileType, int> lastInventory;

    // nothing is picked from the Removed panel any more, and the log and the board show what broke, so it's hidden
    void Awake()
    {
        if (crossRemovedRoot != null && crossRemovedRoot.parent != null) crossRemovedRoot.parent.gameObject.SetActive(false);

        // no score log of its own: it uses the activity log's text, which then shows only the score lines
        if (scoreLogText == null && logText != null)
        {
            scoreLogText = logText;
            scoreLogText.gameObject.SetActive(true);
        }
        MakeScoreLogBox(); // the box is always made (it also places the log); Score Log Box off just hides its color
        ShowScoreLog();
    }

    // the dark box, with the log's text moved inside it. the info panel lays its children out in a column (a Vertical
    // Layout Group), so a holder takes the box's place in that column, right under the SCORE text, and the box sits in
    // the holder, centered on the panel. a fixed size; the text fills it minus the padding, starting at the top-left,
    // and lines past its bottom are cut off
    RectTransform scoreHolder, scoreBox;
    LayoutElement scoreHolderSize;
    Image scoreBoxImage;

    void MakeScoreLogBox()
    {
        if (scoreLogText == null || !(scoreLogText.transform.parent is RectTransform panel)) return;
        var text = scoreLogText.rectTransform;

        var holder = new GameObject("ScoreLog", typeof(RectTransform), typeof(LayoutElement));
        scoreHolder = (RectTransform)holder.transform;
        scoreHolder.SetParent(panel, false);
        scoreHolder.SetSiblingIndex(text.GetSiblingIndex());
        scoreHolderSize = holder.GetComponent<LayoutElement>();

        var go = new GameObject("ScoreLogBox", typeof(RectTransform), typeof(Image));
        scoreBox = (RectTransform)go.transform;
        scoreBox.SetParent(scoreHolder, false);
        scoreBoxImage = go.GetComponent<Image>();
        scoreBoxImage.raycastTarget = false;

        text.SetParent(scoreBox, false);
        text.anchorMin = Vector2.zero; // fills the box, minus the padding (see LayOutScoreLogBox)
        text.anchorMax = Vector2.one;
        text.pivot = new Vector2(0.5f, 0.5f);
        scoreLogText.alignment = TextAlignmentOptions.TopLeft;
        scoreLogText.overflowMode = TextOverflowModes.Truncate; // too many lines: the bottom (oldest) ones are cut off
        LayOutScoreLogBox();
    }

    // places and sizes the box from the settings; in Play mode, changing them in the Inspector shows right away
    void LayOutScoreLogBox()
    {
        if (scoreBox == null) return;
        var panel = (RectTransform)scoreHolder.parent;
        float height = Mathf.Max(20f, scoreLogBoxHeight);
        // the holder's place in the column: as tall as the gap plus the box (the column sets its width and position)
        scoreHolderSize.minHeight = scoreHolderSize.preferredHeight = Mathf.Max(0f, scoreLogBoxGap) + height;
        scoreHolder.sizeDelta = new Vector2(scoreHolder.sizeDelta.x, scoreHolderSize.preferredHeight);

        // the box: at the holder's top (after the gap), centered on the panel rather than on the holder, which the
        // column's padding pushes off center
        var column = panel.GetComponent<HorizontalOrVerticalLayoutGroup>();
        float padLeft = column != null ? column.padding.left : 0f, padRight = column != null ? column.padding.right : 0f;
        float panelWidth = panel.rect.width;
        float holderCenter = padLeft + (panelWidth - padLeft - padRight) / 2f; // from the panel's left edge
        scoreBox.anchorMin = scoreBox.anchorMax = scoreBox.pivot = new Vector2(0.5f, 1f);
        scoreBox.anchoredPosition = new Vector2(panelWidth / 2f - holderCenter, -scoreLogBoxGap);
        scoreBox.sizeDelta = new Vector2(Mathf.Max(20f, panelWidth - 2 * scoreLogBoxSideMargin), height);
        LayoutRebuilder.MarkLayoutForRebuild(panel);
        scoreBoxImage.color = scoreLogBox ? scoreLogBoxColor : Color.clear;

        var text = scoreLogText.rectTransform;
        text.offsetMin = new Vector2(scoreLogBoxPadding, scoreLogBoxPadding);
        text.offsetMax = new Vector2(-scoreLogBoxPadding, -scoreLogBoxPadding);
    }

    bool scoreBoxChanged;
    void OnValidate() => scoreBoxChanged = true;
    void Update()
    {
        if (!scoreBoxChanged) return;
        scoreBoxChanged = false;
        LayOutScoreLogBox();
    }

    // every tile type gets a slot with its count, x0 for the ones the level doesn't hand out
    public void BuildSlots(IEnumerable<TileType> types, Action<TileType> onSelect)
    {
        // the top bar's color, for empty slots: the nearest image behind the slots
        var bar = slotsRoot.GetComponentInParent<Image>();
        barColor = bar != null ? bar.color : emptySlotColor;
        Clear(slotsRoot);
        slots.Clear();
        var list = types.ToList();
        float slotWidth = SlotWidth(list);
        TileCategory? previous = null;
        foreach (var t in list)
        {
            var category = Tiles.Category(t);
            if (previous != null && category != previous)
            {
                var gap = new GameObject("Gap", typeof(RectTransform));
                var rt = (RectTransform)gap.transform;
                rt.SetParent(slotsRoot, false);
                rt.sizeDelta = new Vector2(groupGap, 1);
            }
            previous = category;

            var slot = Instantiate(slotPrefab, slotsRoot);
            var slotRect = (RectTransform)slot.transform;
            slotRect.sizeDelta = new Vector2(slotWidth, slotRect.sizeDelta.y);
            slot.SetTile(tilePrefab, t, tileColor);
            var type = t; // copy for the click handler below
            slot.button.onClick.AddListener(() => onSelect(type));
            slots[t] = slot;
        }
    }

    // the prefab's width, or less if that many slots plus the gaps would overflow the bar
    float SlotWidth(List<TileType> types)
    {
        float width = ((RectTransform)slotPrefab.transform).sizeDelta.x;
        var layout = slotsRoot.GetComponent<HorizontalLayoutGroup>();
        float spacing = layout != null ? layout.spacing : 0f;
        int gaps = types.Select(Tiles.Category).Distinct().Count() - 1;
        int items = types.Count + Mathf.Max(0, gaps);
        float available = slotsRoot.rect.width - Mathf.Max(0, gaps) * groupGap - spacing * (items - 1);
        if (types.Count == 0 || available <= 0) return width;
        return Mathf.Clamp(available / types.Count, minSlotWidth, width);
    }

    // while objects move or wait for a direction, tiles can't be picked. the slots' CanvasGroup lets the mouse pass
    // straight through every card then: no hover, no press, no click (and a card under the mouse un-highlights)
    public void SetSupplyPaused(bool paused)
    {
        var group = slotsRoot.GetComponent<CanvasGroup>();
        if (group == null) group = slotsRoot.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = !paused;
        group.interactable = !paused;
    }

    public void RefreshInventory(Dictionary<TileType, int> inventory, TileType? selected)
    {
        lastSelected = selected;
        lastInventory = inventory;
        foreach (var t in slots.Keys)
        {
            slots[t].label.text = "x" + inventory[t];
            slots[t].label.color = inventory[t] > 0 ? slotPrefab.label.color : emptyLabelColor;
            slots[t].SetAvailable(inventory[t] > 0, emptySlotDarken);
            if (flashingSlots.Contains(t)) continue;
            slots[t].SetBackground(SlotColor(t));
        }
        selectedText.text = "Selected: " + (selected == null ? "none" : Tiles.Name(selected.Value));
    }

    // yellow when selected, darker when there are none of it left
    Color SlotColor(TileType t) =>
        lastSelected == t ? selectedSlotColor :
        lastInventory != null && lastInventory[t] <= 0 ? (emptySlotUsesBarColor ? barColor : emptySlotColor) : slotColor;

    Color barColor;

    // red by default (a tile lost), or another color, e.g. green for the + earned by delivering together
    public void FlashSlot(TileType t, Color? color = null)
    {
        if (slots.ContainsKey(t)) StartCoroutine(FlashSlotRoutine(t, color ?? flashColor));
    }

    IEnumerator FlashSlotRoutine(TileType t, Color flash)
    {
        var img = slots[t].background;
        flashingSlots.Add(t);
        img.color = flash;
        for (float f = 0; f < flashHoldSeconds + flashFadeSeconds; f += Time.deltaTime)
        {
            img.color = Color.Lerp(flash, slotColor, Mathf.Clamp01((f - flashHoldSeconds) / flashFadeSeconds));
            yield return null;
        }
        flashingSlots.Remove(t);
        slots[t].SetBackground(SlotColor(t));
    }

    public void ResetSlotFlashes()
    {
        StopAllCoroutines();
        flashingSlots.Clear();
    }

    public void RefreshInfo(LevelData level, int delivered, int total, int tilesLeft, int score, bool scoreStar)
    {
        levelTitleText.text = level.title.ToUpperInvariant();

    string progressColor = ColorUtility.ToHtmlStringRGB(headingColor);
    const string scoreColor = "7C83F7";
    const string successColor = "61C98B";

    goalText.text =
        $"<b><color=#{progressColor}>PROGRESS</color></b>\n" +
        $"<size=24>{delivered} / {total}</size> delivered";

    bonusText.text =
        $"<b><color=#{progressColor}>SCORE</color></b>\n" + // the same yellow as PROGRESS
        $"<size=24>{score} / {level.targetScore}</size>" +
        (level.bestTiles > 0 ? $"\n<size=16><color=#{scoreColor}>3rd star: best plans use {level.bestTiles} tiles</color></size>" : "") +
        (scoreStar
            ? $"\n<size=16><color=#{successColor}>Target reached</color></size>"
            : "");
    }

    // 3 sections of the panel: junctions (+, bridge, T) on top, the four L tiles, then the two I tiles
    public void RefreshRemoved(List<RemovedEntry> removed, Action<TileType> onPick = null)
    {
        Clear(crossRemovedRoot);
        Clear(cornerRemovedRoot);
        Clear(straightRemovedRoot);
        int crossCards = 0, cornerCards = 0, straightCards = 0;

        foreach (var t in Tiles.All)
        {
            int count = removed.Count(e => e.type == t);
            if (count == 0) continue;
            var category = Tiles.Category(t);
            var parent = category switch
            {
                TileCategory.Corner => cornerRemovedRoot,
                TileCategory.Straight => straightRemovedRoot,
                _ => crossRemovedRoot,
            };
            if (category == TileCategory.Corner) cornerCards++;
            else if (category == TileCategory.Straight) straightCards++;
            else crossCards++;

            var card = Instantiate(cardPrefab, parent);
            card.SetTile(tilePrefab, t, tileColor);
            card.label.text = "x" + count;

            card.button.enabled = onPick != null; // otherwise the card is just a display
            if (onPick != null)
            {
                var type = t;       // copies for the click handler below
                var clicked = card;
                card.button.onClick.AddListener(() =>
                {
                    clicked.background.color = recoverySelectedColor; // show what was picked
                    onPick(type);
                });
            }
        }

        crossRemovedRoot.gameObject.SetActive(crossCards > 0);
        cornerRemovedRoot.gameObject.SetActive(cornerCards > 0);
        straightRemovedRoot.gameObject.SetActive(straightCards > 0);
    }

    // everything but the removed panel is dimmed and the header asks for a pick
    public void SetChoiceMode(bool choosing)
    {
        choiceDimmer.SetActive(choosing);
        removedHeader.text = choosing ? "Pick one to get back:" : "Removed:";
    }

    // the stuck notice (nobody left can be delivered)
    public bool StuckNoticeShowing => stuckNotice != null && stuckNotice.Showing;

    public void ShowStuckNotice(Action onUndo, Action onRestart, Action onEnd)
    {
        if (stuckNotice != null) stuckNotice.Show(onUndo, onRestart, onEnd);
    }

    public void HideStuckNotice()
    {
        if (stuckNotice != null) stuckNotice.Hide();
    }

    [Header("Toast feedback")]
public ToastView toast;


public void ShowToast(string message)
{
    if (toast != null)
        toast.Show(message);
}

public void ShowToast(string message, Color color)
{
    if (toast != null)
        toast.Show(message, color);
}

public void ClearToasts()
{
    if (toast != null)
        toast.Clear();
}

    public void SetUndoAvailable(bool available)
    {
        if (undoButton != null) undoButton.interactable = available;
    }

    public void Log(string msg)
    {
        log.Insert(0, "- " + msg); // newest first
        if (log.Count > 12) log.RemoveAt(log.Count - 1);
        ShowLog();
    }

    public void ClearLog()
    {
        SetLog(new List<string>());
        SetScoreLog(new List<string>());
    }

    // a copy of the log lines, for an undo snapshot
    public List<string> GetLog() => new List<string>(log);

    public void SetLog(List<string> lines)
    {
        log.Clear();
        log.AddRange(lines);
        ShowLog();
    }

    // the activity log, unless the score log has taken over its text
    void ShowLog()
    {
        if (logText != null && logText != scoreLogText) logText.text = string.Join("\n", log);
    }

    // ---- the score log (lower left): what each delivery scored, instead of a pop-up over the board

    readonly List<string> scoreLog = new List<string>();

    // points: shown in front, e.g. "+150" or "+50 x 2"
    public void LogScore(string points, string what, Color color)
    {
        string hex = ColorUtility.ToHtmlStringRGB(color);
        scoreLog.Insert(0, $"<color=#{hex}><b>{points}</b></color>  {what}"); // newest first
        if (scoreLog.Count > scoreLogLines) scoreLog.RemoveAt(scoreLog.Count - 1);
        ShowScoreLog();
    }

    // a copy, for an undo snapshot
    public List<string> GetScoreLog() => new List<string>(scoreLog);

    public void SetScoreLog(List<string> lines)
    {
        scoreLog.Clear();
        if (lines != null) scoreLog.AddRange(lines);
        ShowScoreLog();
    }

    void ShowScoreLog()
    {
        if (scoreLogText == null) return;
        scoreLogText.text = scoreLog.Count > 0 ? string.Join("\n", scoreLog) : "<color=#8C8F99>Deliveries score here.</color>";
    }

    static void Clear(Transform root)
    {
        foreach (Transform child in root) Destroy(child.gameObject);
    }
}
