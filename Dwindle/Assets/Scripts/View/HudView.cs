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

    [Header("Removed tiles and recovery")]
    public RectTransform crossRemovedRoot;    // + tiles
    public RectTransform cornerRemovedRoot;   // the four L tiles
    public RectTransform straightRemovedRoot; // the two I tiles
    public GameObject choiceDimmer;  // darkens everything except the removed panel
    public TMP_Text removedHeader;   // "Removed:" / "Pick one to get back:"

    [Header("Undo")]
    public Button undoButton; // sits after ChoiceDimmer in the hierarchy, so it stays clickable during a recovery choice

    [Header("Colors")]
    public Color tileColor = new Color(0.28f, 0.31f, 0.38f);
    public Color slotColor = new Color(0.32f, 0.34f, 0.40f);
    public Color selectedSlotColor = new Color(0.95f, 0.78f, 0.25f);
    public Color emptySlotColor = new Color(0.22f, 0.23f, 0.27f); // a tile type you have none of
    [Range(0f, 1f)] public float emptySlotDarken = 0.6f; // how much darker a slot with none left is (0 = not at all)
    public Color emptyLabelColor = new Color(0.55f, 0.56f, 0.60f);
    public Color recoverySelectedColor = new Color(0.95f, 0.78f, 0.25f); // the card you click to get back
    public Color flashColor = new Color(1f, 0.45f, 0.45f);
    public float flashHoldSeconds = 0.2f;
    public float flashFadeSeconds = 0.2f;

    readonly Dictionary<TileType, TileCard> slots = new Dictionary<TileType, TileCard>();
    readonly List<string> log = new List<string>();
    readonly HashSet<TileType> flashingSlots = new HashSet<TileType>();
    TileType? lastSelected;
    Dictionary<TileType, int> lastInventory;

    public void BuildSlots(IEnumerable<TileType> types, Action<TileType> onSelect)
    {
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
            slots[t].background.color = SlotColor(t);
        }
        selectedText.text = "Selected: " + (selected == null ? "none" : Tiles.Name(selected.Value));
    }

    // yellow when selected, darker when there are none of it left
    Color SlotColor(TileType t) =>
        lastSelected == t ? selectedSlotColor :
        lastInventory != null && lastInventory[t] <= 0 ? emptySlotColor : slotColor;

    public void FlashSlot(TileType t)
    {
        if (slots.ContainsKey(t)) StartCoroutine(FlashSlotRoutine(t));
    }

    IEnumerator FlashSlotRoutine(TileType t)
    {
        var img = slots[t].background;
        flashingSlots.Add(t);
        img.color = flashColor;
        for (float f = 0; f < flashHoldSeconds + flashFadeSeconds; f += Time.deltaTime)
        {
            img.color = Color.Lerp(flashColor, slotColor, Mathf.Clamp01((f - flashHoldSeconds) / flashFadeSeconds));
            yield return null;
        }
        flashingSlots.Remove(t);
        img.color = SlotColor(t);
    }

    public void ResetSlotFlashes()
    {
        StopAllCoroutines();
        flashingSlots.Clear();
    }

    public void RefreshInfo(LevelData level, int delivered, int total, int tilesLeft, int score, bool scoreStar)
    {
        levelTitleText.text = level.title;

        goalText.text =
            "Match objects to their colored rings.\n\n" +
            "<b><color=#3EE6B0>PROGRESS</color></b>\n" +
            $"{delivered}/{total} delivered | {tilesLeft} tiles left\n\n" +
            "<b><color=#3EE6B0>STAR GOALS</color></b>\n" +
            $"1st star: Deliver {level.Required}\n" +
            $"2nd star: Deliver all {total}";

        bonusText.text =
            $"3rd star: Score {level.targetScore}\n" +
            $"<size=20><b>Score: {score}</b></size>{(scoreStar ? "  (target reached)" : "")}";
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

    public void SetUndoAvailable(bool available)
    {
        if (undoButton != null) undoButton.interactable = available;
    }

    public void Log(string msg)
    {
        log.Insert(0, "- " + msg); // newest first
        if (log.Count > 12) log.RemoveAt(log.Count - 1);
        logText.text = string.Join("\n", log);
    }

    public void ClearLog() => SetLog(new List<string>());

    // a copy of the log lines, for an undo snapshot
    public List<string> GetLog() => new List<string>(log);

    public void SetLog(List<string> lines)
    {
        log.Clear();
        log.AddRange(lines);
        logText.text = string.Join("\n", log);
    }

    static void Clear(Transform root)
    {
        foreach (Transform child in root) Destroy(child.gameObject);
    }
}
