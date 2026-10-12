using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

// the supply bar at the top, the info panel, the removed-tiles panel and the log
public class HudView : MonoBehaviour
{
    [Header("Prefabs")]
    public TileView tilePrefab;
    public TileCard slotPrefab; // one slot per tile type in the supply bar
    public TileCard cardPrefab; // one card per removed tile type

    [Header("Supply bar")]
    public RectTransform slotsRoot;
    public float groupGap = 24f; // extra space between the +, L and I groups

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

    [Header("Colors")]
    public Color tileColor = new Color(0.28f, 0.31f, 0.38f);
    public Color slotColor = new Color(0.32f, 0.34f, 0.40f);
    public Color selectedSlotColor = new Color(0.95f, 0.78f, 0.25f);
    public Color recoverySelectedColor = new Color(0.95f, 0.78f, 0.25f); // the card you click to get back
    public Color flashColor = new Color(1f, 0.45f, 0.45f);
    public float flashHoldSeconds = 0.2f;
    public float flashFadeSeconds = 0.2f;

    readonly Dictionary<TileType, TileCard> slots = new Dictionary<TileType, TileCard>();
    readonly List<string> log = new List<string>();
    readonly HashSet<TileType> flashingSlots = new HashSet<TileType>();
    TileType? lastSelected;

    // one slot per tile type, in the order of Tiles.All (+, the four L tiles, the two I tiles) with empty gaps
    public void BuildSlots(Action<TileType> onSelect)
    {
        Clear(slotsRoot);
        slots.Clear();
        TileCategory? previous = null;
        foreach (var t in Tiles.All)
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
            slot.SetTile(tilePrefab, t, tileColor);
            var type = t; // copy for the click handler below
            slot.button.onClick.AddListener(() => onSelect(type));
            slots[t] = slot;
        }
    }

    public void RefreshInventory(Dictionary<TileType, int> inventory, TileType? selected)
    {
        lastSelected = selected;
        foreach (var t in Tiles.All)
        {
            slots[t].label.text = "x" + inventory[t];
            if (flashingSlots.Contains(t)) continue;
            slots[t].background.color = selected == t ? selectedSlotColor : slotColor;
        }
        selectedText.text = "Selected: " + (selected == null ? "none" : Tiles.Name(selected.Value));
    }

    public void FlashSlot(TileType t) => StartCoroutine(FlashSlotRoutine(t));

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
        img.color = lastSelected == t ? selectedSlotColor : slotColor;
    }

    public void ResetSlotFlashes()
    {
        StopAllCoroutines();
        flashingSlots.Clear();
    }

    public void RefreshInfo(LevelData level, int delivered, int total, int tilesLeft, bool bonusMet)
    {
        levelTitleText.text = level.title;

    goalText.text =
        "Match objects to their colored rings.\n\n" +
        "<b>Progress</b>\n" +
        $"{delivered}/{total} delivered | {tilesLeft} tiles left\n\n" +
        "<b>Star Goals</b>\n" +
        $"1st star: Deliver {level.required}\n" +
        $"2nd star: Deliver all {total}";

    bonusText.text =
        $"3rd star (bonus goal):\n" +
        $"{level.BonusText()}\n" +
        $"Bonus: {(bonusMet ? "Complete" : "Not complete")}";
    }

    // 3 sections of the panel: + on top, the four L tiles, then the two I tiles
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
                TileCategory.Cross => crossRemovedRoot,
                TileCategory.Corner => cornerRemovedRoot,
                _ => straightRemovedRoot,
            };
            if (category == TileCategory.Cross) crossCards++;
            else if (category == TileCategory.Corner) cornerCards++;
            else straightCards++;

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

    public void Log(string msg)
    {
        log.Insert(0, "- " + msg); // newest first
        if (log.Count > 12) log.RemoveAt(log.Count - 1);
        logText.text = string.Join("\n", log);
    }

    public void ClearLog()
    {
        log.Clear();
        logText.text = "";
    }

    static void Clear(Transform root)
    {
        foreach (Transform child in root) Destroy(child.gameObject);
    }
}
