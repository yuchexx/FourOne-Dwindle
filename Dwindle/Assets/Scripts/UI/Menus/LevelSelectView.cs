using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the Levels page
public class LevelSelectView : MonoBehaviour
{
    public LevelCard cardTemplate;
    [Min(1)] public int cardsPerRow = 4;
    [Tooltip("Space between cards side by side")]
    public float cardSpacing = 24f;
    [Tooltip("Space between rows")]
    public float rowSpacing = 24f;
    [Tooltip("A card's text; {n} is the level's number")]
    public string cardText = "Level {n}";
    public string lockedText = "Locked";

    [Header("Tutorial levels (in Play mode, changes show right away)")]
    [Tooltip("An open tutorial level's card: a deeper purple than Accent, so purple stars still show on it")]
    public Color tutorialCardColor = new Color32(0x4A, 0x4F, 0xA8, 0xFF);
    [Tooltip("A locked tutorial level's frame: thickness in screen pixels, so all four edges come out the same")]
    [Min(1)] public int frameWidth = 3;
    [Tooltip("The frame's color (default: a lighter version of the palette's Accent)")]
    public Color frameColor = new Color32(0xAA, 0xAE, 0xFA, 0xFF);

    readonly List<LevelCard> cards = new List<LevelCard>();
    readonly List<bool> hasTutorial = new List<bool>();
    Color cardColor;
    Vector2 labelAnchorMin, labelAnchorMax, labelPosition, labelSize;
    TextAlignmentOptions labelAlignment;

    void OnValidate()
    {
        if (Application.isPlaying && cards.Count > 0) Refresh();
    }

    // one card per level; onPick gets the level's index
    public void Build(IList<LevelData> levels, Action<int> onPick)
    {
        foreach (var c in cards) Destroy(c.gameObject);
        cards.Clear();
        hasTutorial.Clear();
        cardTemplate.gameObject.SetActive(false);
        if (cardTemplate.button.targetGraphic != null) cardColor = cardTemplate.button.targetGraphic.color;
        var label = cardTemplate.label.rectTransform;
        labelAnchorMin = label.anchorMin; labelAnchorMax = label.anchorMax;
        labelPosition = label.anchoredPosition; labelSize = label.sizeDelta;
        labelAlignment = cardTemplate.label.alignment;
        var templateRect = (RectTransform)cardTemplate.transform;
        Vector2 size = templateRect.sizeDelta, origin = templateRect.anchoredPosition;
        for (int i = 0; i < levels.Count; i++)
        {
            int row = i / cardsPerRow, col = i % cardsPerRow;
            int inRow = Mathf.Min(cardsPerRow, levels.Count - row * cardsPerRow);
            var card = Instantiate(cardTemplate, cardTemplate.transform.parent);
            card.name = $"Level{i + 1}Card";
            card.gameObject.SetActive(true);
            ((RectTransform)card.transform).anchoredPosition = new Vector2(
                origin.x + (col - (inRow - 1) / 2f) * (size.x + cardSpacing),
                origin.y - row * (size.y + rowSpacing));
            card.button.onClick = new Button.ButtonClickedEvent(); // drops anything set on the template in the Inspector
            int index = i; // copy for the click handler
            card.button.onClick.AddListener(() => onPick(index));
            cards.Add(card);
            hasTutorial.Add(!string.IsNullOrEmpty(levels[i].tutorial));
        }
        Refresh();
    }

    // locks and stars, from this session's results
    public void Refresh()
    {
        var palette = UIPalette.Current;
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            var record = Session.Get(i);
            bool open = Session.IsUnlocked(i);
            card.button.interactable = open;
            if (card.button.targetGraphic != null) card.button.targetGraphic.color = open && hasTutorial[i] ? tutorialCardColor : cardColor;
            card.label.text = open ? cardText.Replace("{n}", (i + 1).ToString()) : lockedText;
            PlaceLabel(card.label, open);
            if (card.tutorialFrame != null) DrawFrame(card, !open && hasTutorial[i], frameColor);
            for (int s = 0; s < card.stars.Length; s++)
            {
                bool earned = s < record.stars;
                card.stars[s].gameObject.SetActive(open);
                card.stars[s].color = earned ? (record.purple ? palette.accent : palette.star) : palette.starEmpty;
            }
        }
    }

    // open: where the template's Label is. locked: the whole card, centered
    void PlaceLabel(TMP_Text label, bool open)
    {
        var rt = label.rectTransform;
        label.margin = Vector4.zero;
        if (open)
        {
            rt.anchorMin = labelAnchorMin; rt.anchorMax = labelAnchorMax;
            rt.sizeDelta = labelSize;
            rt.anchoredPosition = labelPosition;
            label.alignment = labelAlignment;
        }
        else
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            label.alignment = TextAlignmentOptions.Center;
        }
    }

    // outline for locked tutorial levels
    void DrawFrame(LevelCard card, bool show, Color color)
    {
        var canvas = card.GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
        float w = frameWidth / scale;
        for (int e = 0; e < card.tutorialFrame.Length; e++)
        {
            var edge = card.tutorialFrame[e];
            edge.gameObject.SetActive(show);
            edge.color = color;
            if (e > 3) continue;
            var rt = edge.rectTransform;
            bool across = e < 2; // top and bottom run across the card, left and right up it
            Vector2 side = e switch { 0 => new Vector2(0.5f, 1f), 1 => new Vector2(0.5f, 0f), 2 => new Vector2(0f, 0.5f), _ => new Vector2(1f, 0.5f) };
            rt.anchorMin = across ? new Vector2(0f, side.y) : new Vector2(side.x, 0f);
            rt.anchorMax = across ? new Vector2(1f, side.y) : new Vector2(side.x, 1f);
            rt.pivot = side;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = across ? new Vector2(0f, w) : new Vector2(w, 0f);
        }
    }
}
