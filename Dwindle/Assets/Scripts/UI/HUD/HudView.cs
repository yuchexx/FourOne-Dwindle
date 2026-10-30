using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

// the supply dock, the level title, the score and its history, Used / Left, and the stuck notice
public class HudView : MonoBehaviour
{
    [Header("Prefabs")]
    public TileView tilePrefab;
    public TileCard slotPrefab; // one slot per tile type in the supply dock

    [Header("Supply dock")]
    public RectTransform slotsRoot;
    public float groupGap = 24f; // extra space between the +, bridge, L, I and T groups
    public float minSlotWidth = 70f; // slots shrink down to this when a level hands out many tile types
    [Tooltip("The panel the slots sit in")]
    public RectTransform supplyDock;
    [Tooltip("On: the code sizes the dock to fit the slots, up to Dock Max Width. Off: the dock keeps the size you set, and the slots fit inside it")]
    public bool fitDockToTiles = true;
    public float dockMaxWidth = 1060f;
    [Tooltip("Space between the dock's edges and the slots: X left and right, Y top and bottom")]
    public Vector2 dockPadding = new Vector2(10f, 10f);
    [Tooltip("The count badge: a graphic with a TMP text inside; copied onto every slot")]
    public GameObject countBadgeTemplate;
    [Tooltip("Where the badge sits on the tile: (0, 0) bottom-left, (1, 0) bottom-right, (1, 1) top-right. In Play mode, changes show right away")]
    public Vector2 badgeCorner = new Vector2(1f, 0f);
    [Tooltip("The badge's center, moved this far from that corner (x right, y up)")]
    public Vector2 badgeOffset = new Vector2(-6f, 6f);
    [FormerlySerializedAs("badgedSlotHeight")] public float slotHeight = 84f;
    [Tooltip("How far the selected or hovered square reaches past the tile (enough to take in the badge)")]
    public float highlightPadding = 8f;
    [Tooltip("The tile picture's size (smaller than the prefab's, so the squares fit narrow slots)")]
    [FormerlySerializedAs("badgedIconSize")] public float iconSize = 56f;
    [Tooltip("A tile you have none of, dimmed: its square and its path")]
    public Color emptyTileBackColor = new Color32(0x6A, 0x6C, 0x72, 0xFF);
    public Color emptyTilePathColor = new Color32(0x3F, 0x42, 0x4C, 0xFF);
    [Tooltip("The badge with some left, and at 0")]
    public Color badgeColor = Color.white;
    public Color badgeTextColor = new Color32(0x1C, 0x1D, 0x22, 0xFF);
    public Color emptyBadgeColor = new Color32(0x3A, 0x3C, 0x44, 0xFF);
    public Color emptyBadgeTextColor = new Color32(0x8C, 0x8F, 0x99, 0xFF);
    [Tooltip("A thin line between the tile groups")]
    public Color groupLineColor = new Color32(0x4A, 0x4D, 0x58, 0xFF);
    [Tooltip("The line's length, as a share of the slot height. In Play mode, changes show right away")]
    [Range(0f, 1f)] public float groupLineLength = 0.6f;
    public float groupLineWidth = 2f;

    [Header("Texts")]
    public TMP_Text levelTitleText;
    public TMP_Text scoreText;        // "Score 420", then "/ 550" under it; hovering it shows the history
    [Tooltip("The target line's size, as a share of the score's")]
    [Range(0.2f, 1f)] public float targetSize = 0.6f;
    public Color targetColor = new Color32(0x7C, 0x83, 0xF7, 0xFF);
    public TMP_Text supplyLeftText;   // "6 Left"
    public TMP_Text supplyUsedText;   // "Used 3"

    [Header("Score history (in Play mode, changes show right away)")]
    [Tooltip("The panel the rows go in (a Vertical Layout Group); its children are replaced by the rows")]
    public RectTransform scoreHistory;
    public ObjectView objectPrefab;          // the same prefabs as BoardManager's
    public DestinationView destinationPrefab;
    [Tooltip("The arrow between an object and its ring: its text size, its box's width, and a nudge up (+) or down (-)")]
    public float historyArrowSize = 18f;
    public float historyArrowWidth = 18f;
    public float historyArrowOffset = 0f;
    public float historyIconSize = 22f;
    public float historyTextSize = 18f;
    [Tooltip("Space between the icons and texts in a row")]
    public float historySpacing = 4f;
    [Tooltip("The least space between a row's icons and its sum and points")]
    public float historyGap = 16f;
    [Tooltip("The points column's width, so every row's points line up on the right")]
    public float pointsWidth = 56f;
    [Tooltip("More rows than this: the oldest fold into one \"N earlier\" row")]
    [Min(2)] public int historyMaxRows = 8;
    public Color historyTextColor = Color.white;
    public Color historyMutedColor = new Color32(0xB3, 0xB3, 0xB3, 0xFF); // the arrow and the sums
    [Tooltip("The door: its bar's length, as a share of History Icon Size (its badge and thickness follow the board's door)")]
    [Range(0.3f, 1.5f)] public float historyDoorSize = 0.9f;
    public Color doorIconColor = new Color(0.62f, 0.64f, 0.68f);
    public Color pointsColor = new Color32(0x61, 0xC9, 0x8B, 0xFF);
    public Color comboPointsColor = new Color32(0x7C, 0x83, 0xF7, 0xFF);

    [Header("Undo")]
    public Button undoButton;

    [Header("Stuck notice (nobody left can be delivered)")]
    public StuckBanner stuckNotice;

    [Header("Colors")]
    public Color tileColor = new Color(0.28f, 0.31f, 0.38f);
    public Color selectedSlotColor = new Color(0.95f, 0.78f, 0.25f);
    public Color flashColor = new Color(1f, 0.45f, 0.45f);
    public Color earnedFlashColor = new Color(0.45f, 0.85f, 0.55f); // the + slot when delivering together earns one
    public float flashHoldSeconds = 0.2f;
    public float flashFadeSeconds = 0.2f;

    readonly Dictionary<TileType, TileCard> slots = new Dictionary<TileType, TileCard>();
    readonly HashSet<TileType> flashingSlots = new HashSet<TileType>();
    TileType? lastSelected;

    void Awake() => ShowScoreLog();

    // every tile type gets a slot with its count badge
    public void BuildSlots(IEnumerable<TileType> types, Action<TileType> onSelect,
                           Action<TileType, PointerEventData> onDragStart = null,
                           Action<PointerEventData> onDrag = null, Action<PointerEventData> onDragEnd = null)
    {
        ListenToSupplyBar();
        Clear(slotsRoot);
        slots.Clear();
        badges.Clear();
        groupLines.Clear();
        hadTiles.Clear();
        var list = types.ToList();
        // as wide as with every tile type shown, so leaving some out (Compact supply) only shortens the dock
        float slotWidth = SlotWidth(Tiles.SupplyOrder.ToList());
        TileCategory? previous = null;
        foreach (var t in list)
        {
            var category = Tiles.Category(t);
            if (previous != null && category != previous)
            {
                // a gap with a thin line in it
                var gap = new GameObject("Gap", typeof(RectTransform));
                var rt = (RectTransform)gap.transform;
                rt.SetParent(slotsRoot, false);
                rt.sizeDelta = new Vector2(groupGap, slotHeight);
                var line = new GameObject("Line", typeof(RectTransform), typeof(Image));
                var lr = (RectTransform)line.transform;
                lr.SetParent(rt, false);
                PlaceGroupLine(lr);
                groupLines.Add(lr);
                var img = line.GetComponent<Image>();
                img.color = groupLineColor;
                img.raycastTarget = false;
            }
            previous = category;

            var slot = Instantiate(slotPrefab, slotsRoot);
            ((RectTransform)slot.transform).sizeDelta = new Vector2(slotWidth, slotHeight);
            AddBadge(slot, t);
            slot.SetTile(tilePrefab, t, tileColor);
            var type = t; // copy for the click handler below
            slot.button.onClick.AddListener(() => onSelect(type));
            slot.onDragStart = onDragStart;
            slot.onDrag = onDrag;
            slot.onDragEnd = onDragEnd;
            // a press on any card, x0 ones too, counts as a press on the supply bar (it finishes an undo rewind); the
            // card's own click still only works when it has tiles
            slot.gameObject.AddComponent<ClickRelay>().onDown = () => onSupplyBarPressed?.Invoke();
            slots[t] = slot;
        }

        // the slots fill the dock minus the padding, and the dock fits the slots
        if (supplyDock != null && slotsRoot.parent == supplyDock)
        {
            slotsRoot.anchorMin = Vector2.zero; slotsRoot.anchorMax = Vector2.one;
            slotsRoot.offsetMin = dockPadding; slotsRoot.offsetMax = -dockPadding;
        }
        if (supplyDock != null && fitDockToTiles)
            supplyDock.sizeDelta = new Vector2(RowWidth(list, slotWidth) + 2 * dockPadding.x, slotHeight + 2 * dockPadding.y);
    }

    readonly Dictionary<TileType, (GameObject badge, Graphic circle, TMP_Text text)> badges = new Dictionary<TileType, (GameObject, Graphic, TMP_Text)>();
    readonly HashSet<TileType> hadTiles = new HashSet<TileType>(); // types given or earned this level

    // the count badge on the icon's lower-right corner; the label goes and the icon moves to the middle.
    // selected, hover and flashes show on a square around the tile and its badge, instead of the whole slot
    void AddBadge(TileCard slot, TileType t)
    {
        slot.label.gameObject.SetActive(false);
        slot.iconRoot.anchoredPosition = new Vector2(slot.iconRoot.anchoredPosition.x, -slotHeight / 2f);
        slot.iconRoot.sizeDelta = Vector2.one * iconSize;
        slot.background.color = Color.clear; // the slot still takes the clicks
        var square = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
        var sr = (RectTransform)square.transform;
        sr.SetParent(slot.transform, false);
        sr.SetAsFirstSibling(); // behind the tile
        sr.anchorMin = sr.anchorMax = slot.iconRoot.anchorMin;
        sr.anchoredPosition = slot.iconRoot.anchoredPosition;
        sr.sizeDelta = slot.iconRoot.sizeDelta + Vector2.one * 2 * highlightPadding;
        var squareImage = square.GetComponent<Image>();
        squareImage.raycastTarget = false;
        slot.background = squareImage;
        if (countBadgeTemplate == null) return;
        var badge = Instantiate(countBadgeTemplate, slot.iconRoot);
        badge.SetActive(true);
        PlaceBadge((RectTransform)badge.transform);
        badges[t] = (badge, badge.GetComponent<Graphic>(), badge.GetComponentInChildren<TMP_Text>(true));
    }

    readonly List<RectTransform> groupLines = new List<RectTransform>();

    // centered in its gap
    void PlaceGroupLine(RectTransform lr)
    {
        lr.anchorMin = new Vector2(0.5f, 0.5f - groupLineLength / 2f); lr.anchorMax = new Vector2(0.5f, 0.5f + groupLineLength / 2f);
        lr.sizeDelta = new Vector2(groupLineWidth, 0);
        lr.anchoredPosition = Vector2.zero;
    }

    void PlaceBadge(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = badgeCorner;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = badgeOffset;
    }

    void OnValidate()
    {
        if (!Application.isPlaying) return;
        foreach (var b in badges.Values) if (b.badge != null) PlaceBadge((RectTransform)b.badge.transform);
        foreach (var lr in groupLines) if (lr != null) PlaceGroupLine(lr);
        historyChanged = true;
    }

    bool historyChanged;
    void Update()
    {
        if (!historyChanged) return;
        historyChanged = false;
        ShowScoreLog();
        if (lastLevel != null) RefreshInfo(lastLevel, lastLeft, lastUsed, lastScore, lastUntravelled);
    }

    // the prefab's width, or less if that many slots plus the gaps would overflow the dock
    float SlotWidth(List<TileType> types)
    {
        float width = ((RectTransform)slotPrefab.transform).sizeDelta.x;
        float room = supplyDock == null ? slotsRoot.rect.width
            : (fitDockToTiles ? dockMaxWidth : supplyDock.rect.width) - 2 * dockPadding.x;
        float available = room - RowWidth(types, 0f);
        if (types.Count == 0 || available <= 0) return width;
        return Mathf.Clamp(available / types.Count, minSlotWidth, width);
    }

    // the row's width with slots this wide: the slots, the gaps between groups, the spacing and the layout's padding
    float RowWidth(List<TileType> types, float slotWidth)
    {
        var layout = slotsRoot.GetComponent<HorizontalLayoutGroup>();
        float spacing = layout != null ? layout.spacing : 0f;
        float padding = layout != null ? layout.padding.horizontal : 0f;
        int gaps = Mathf.Max(0, types.Select(Tiles.Category).Distinct().Count() - 1);
        int items = types.Count + gaps;
        return types.Count * slotWidth + gaps * groupGap + spacing * Mathf.Max(0, items - 1) + padding;
    }

    // during undo's rewind: the counts as they were before the undo (the returning tile hasn't landed yet). cards work
    // as always for what they show: an x0 card can't be hovered or clicked. a press anywhere on the supply bar, x0
    // cards included, finishes the rewind (see onSupplyBarPressed), and the counts catch up
    public void ShowSupplyDuringRewind(Dictionary<TileType, int> shown) => RefreshInventory(shown, null);

    // a press anywhere on the supply bar, cards or the space between them (GameManager finishes a rewind)
    public Action onSupplyBarPressed;

    void ListenToSupplyBar()
    {
        var bar = slotsRoot.GetComponentInParent<Image>();
        if (bar == null || bar.GetComponent<ClickRelay>() != null) return;
        bar.raycastTarget = true;
        bar.gameObject.AddComponent<ClickRelay>().onDown = () => onSupplyBarPressed?.Invoke();
    }

    // where a tile type's picture sits in the supply bar (undo's rewind sends tiles there and back)
    public RectTransform SlotIconRect(TileType t) =>
        slots.TryGetValue(t, out var slot) && slot != null ? (slot.iconRoot != null ? slot.iconRoot : (RectTransform)slot.transform) : null;

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
        foreach (var t in slots.Keys)
        {
            if (inventory[t] > 0) hadTiles.Add(t);
            if (badges.TryGetValue(t, out var b) && b.badge != null) // 0 once used up; no badge for a type never had this level
            {
                b.badge.SetActive(hadTiles.Contains(t));
                if (b.text != null)
                {
                    b.text.text = inventory[t].ToString();
                    b.text.color = inventory[t] > 0 ? badgeTextColor : emptyBadgeTextColor;
                }
                if (b.circle != null) b.circle.color = inventory[t] > 0 ? badgeColor : emptyBadgeColor; // a 0 is quieter
            }
            slots[t].SetAvailable(inventory[t] > 0);
            slots[t].ShowDimmed(inventory[t] <= 0, emptyTileBackColor, emptyTilePathColor);
            if (flashingSlots.Contains(t)) continue;
            slots[t].SetBackground(SlotColor(t));
        }
    }

    // yellow when selected; otherwise a clear white, so the hover lightens it
    Color SlotColor(TileType t) => lastSelected == t ? selectedSlotColor : new Color(1f, 1f, 1f, 0f);

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
            img.color = Color.Lerp(flash, SlotColor(t), Mathf.Clamp01((f - flashHoldSeconds) / flashFadeSeconds));
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

    LevelData lastLevel;
    int lastLeft, lastUsed, lastScore, lastUntravelled;

    // untravelled: tiles on the board nobody has gone through yet (they'd count as unused too): "7 (+1 on board) / 8 Left"
    public void RefreshInfo(LevelData level, int tilesLeft, int tilesUsed, int score, int untravelled = 0)
    {
        (lastLevel, lastLeft, lastUsed, lastScore, lastUntravelled) = (level, tilesLeft, tilesUsed, score, untravelled);
        if (levelTitleText != null) levelTitleText.text = level.title;
        if (scoreText != null)
            scoreText.text = level.targetScore <= 0 ? $"Score {score}" // no target (Level 1): no "/ 0"
                : $"Score {score}\n<size={targetSize * 100f:0}%><color=#{ColorUtility.ToHtmlStringRGB(targetColor)}>/ {level.targetScore}</color></size>";
        if (supplyLeftText != null)
            supplyLeftText.text = untravelled > 0 ? $"{tilesLeft} (+{untravelled} on board) / {tilesLeft + untravelled} Left" : $"{tilesLeft} Left";
        if (supplyUsedText != null) supplyUsedText.text = $"Used {tilesUsed}";
    }

    // the stuck notice (nobody left can be delivered)
    public bool StuckNoticeShowing => stuckNotice != null && stuckNotice.Showing;

    public void ShowStuckNotice(Action onUndo, Action onRestart, Action onEnd)
    {
        if (stuckNotice == null) return;
        stuckNotice.Show(onUndo, onRestart, onEnd); // it moves itself to the end of its parent
        if (scoreHistory != null && scoreHistory.parent == stuckNotice.transform.parent) scoreHistory.SetAsLastSibling(); // the history stays on top
    }

    public void HideStuckNotice()
    {
        if (stuckNotice != null) stuckNotice.Hide();
    }

    public void SetUndoAvailable(bool available)
    {
        if (undoButton != null) undoButton.interactable = available;
    }

    // ---- the score history (hover the score): one row per thing that scored, newest first

    readonly List<ScoreEntry> scoreLog = new List<ScoreEntry>();

    // an object delivered: its icon, a door if it used one, → its filled ring. sum: e.g. "100 + 50 =", or null
    public void LogDelivered(Color color, bool door, int points, string sum) =>
        Add(new ScoreEntry { objects = new[] { color }, door = door, ring = true, points = points, sum = sum });

    // delivered together: one icon per object, then e.g. "50×2 = +100"
    public void LogTogether(Color[] colors, int points, string sum) =>
        Add(new ScoreEntry { objects = colors, points = points, sum = sum, combo = true });

    // a text row, e.g. "3 unused"
    public void LogText(string label, int points, string sum) =>
        Add(new ScoreEntry { label = label, points = points, sum = sum });

    void Add(ScoreEntry e)
    {
        scoreLog.Insert(0, e);
        ShowScoreLog();
    }

    // a copy, for an undo snapshot
    public List<ScoreEntry> GetScoreLog() => new List<ScoreEntry>(scoreLog);

    public void SetScoreLog(List<ScoreEntry> entries)
    {
        scoreLog.Clear();
        if (entries != null) scoreLog.AddRange(entries);
        ShowScoreLog();
    }

    void ShowScoreLog()
    {
        if (scoreHistory == null) return;
        foreach (Transform child in scoreHistory)
        {
            child.gameObject.SetActive(false); // out of the layout now; destroyed at the end of the frame
            Destroy(child.gameObject);
        }
        if (scoreLog.Count == 0)
        {
            HistoryText(NewRow(), "No points yet.", historyMutedColor);
            return;
        }
        int shown = scoreLog.Count > historyMaxRows ? historyMaxRows - 1 : scoreLog.Count;
        for (int i = 0; i < shown; i++) BuildRow(scoreLog[i]);
        if (shown < scoreLog.Count)
        {
            int older = scoreLog.Count - shown;
            BuildRow(new ScoreEntry { label = $"{older} earlier", points = scoreLog.Skip(shown).Sum(e => e.points) });
        }
    }

    void BuildRow(ScoreEntry e)
    {
        var row = NewRow();
        if (e.objects != null)
            for (int i = 0; i < e.objects.Length; i++)
            {
                var c = e.objects[i];
                if (i > 0) HistoryText(row, "+", historyMutedColor); // together: object + object
                var icon = Instantiate(objectPrefab, Holder(row, historyIconSize));
                icon.Setup(c, Shrink(icon.transform, objectPrefab.transform, historyIconSize));
                icon.Face(Dir.E);
            }
        if (e.door)
        {
            // like the board's door on a side edge: a bar with a square badge in its middle (same proportions)
            float length = historyIconSize * historyDoorSize;
            var spot = Holder(row, historyIconSize);
            var fit = spot.GetComponent<LayoutElement>();
            fit.minWidth = fit.preferredWidth = length * 0.43f;
            DoorPart(spot, new Vector2(length * 0.14f, length));
            DoorPart(spot, Vector2.one * length * 0.43f);
        }
        if (e.ring && e.objects != null && e.objects.Length > 0)
        {
            // centered on the glyph itself (Midline), in a box as tall as the icons, so it sits level with their middles
            var box = Holder(row, historyIconSize);
            var fit = box.GetComponent<LayoutElement>();
            fit.minWidth = fit.preferredWidth = historyArrowWidth;
            var arrow = HistoryText(box, "→", historyMutedColor);
            arrow.fontSize = historyArrowSize;
            arrow.alignment = TextAlignmentOptions.Midline;
            var art = arrow.rectTransform;
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            art.offsetMin = art.offsetMax = Vector2.zero;
            art.anchoredPosition = new Vector2(0, historyArrowOffset);
            var ring = Instantiate(destinationPrefab, Holder(row, historyIconSize));
            ring.Setup(e.objects[0], Shrink(ring.transform, destinationPrefab.transform, historyIconSize));
            ring.SetDelivered(true);
        }
        if (e.label != null) HistoryText(row, e.label, historyTextColor);

        var space = new GameObject("Space", typeof(RectTransform), typeof(LayoutElement));
        space.transform.SetParent(row, false);
        var gap = space.GetComponent<LayoutElement>();
        gap.flexibleWidth = 1;
        gap.minWidth = gap.preferredWidth = historyGap;

        if (e.sum != null) HistoryText(row, e.sum, historyMutedColor);
        var points = HistoryText(row, "+" + e.points, e.combo ? comboPointsColor : pointsColor);
        points.fontStyle = FontStyles.Bold;
        points.alignment = TextAlignmentOptions.Right;
        points.gameObject.AddComponent<LayoutElement>().preferredWidth = pointsWidth;

        foreach (var g in row.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
    }

    RectTransform NewRow()
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var rt = (RectTransform)go.transform;
        rt.SetParent(scoreHistory, false);
        var h = go.GetComponent<HorizontalLayoutGroup>();
        h.spacing = historySpacing;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        return rt;
    }

    TMP_Text HistoryText(RectTransform row, string text, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(row, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (scoreText != null) t.font = scoreText.font;
        t.text = text;
        t.fontSize = historyTextSize;
        t.color = color;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    void DoorPart(RectTransform spot, Vector2 size)
    {
        var go = new GameObject("Door", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(spot, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = doorIconColor;
    }

    // a square spot in a row; the icon goes inside it
    RectTransform Holder(RectTransform row, float size)
    {
        var go = new GameObject("Icon", typeof(RectTransform));
        go.transform.SetParent(row, false);
        Fixed(go, size);
        return (RectTransform)go.transform;
    }

    // a board icon drawn at its prefab's own size and scaled down, so its parts keep their places. returns that size
    static float Shrink(Transform icon, Transform prefab, float size)
    {
        var rt = (RectTransform)icon;
        float native = Mathf.Max(1f, ((RectTransform)prefab).sizeDelta.x);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.one * native;
        rt.localScale = Vector3.one * (size / native);
        return native;
    }

    // an icon at a fixed size in its row
    static void Fixed(GameObject go, float size)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = size;
        le.minHeight = le.preferredHeight = size;
    }

    static void Clear(Transform root)
    {
        foreach (Transform child in root) Destroy(child.gameObject);
    }
}

// one row of the score history
public class ScoreEntry
{
    public Color[] objects; // the objects' colors: one when delivered, several when delivered together
    public bool door;       // it went through a door
    public bool ring;       // → and its filled ring
    public string label;    // a text row, e.g. "3 unused"
    public string sum;      // the working before the points, e.g. "100 + 50 =", or null
    public int points;
    public bool combo;      // the points in the combo color
}
