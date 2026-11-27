using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the Levels page: every level's card in rows, in pages (< and >), each colour group's cards in its grey shade (its first
// level tinted toward the accent), then a card past the last level that opens the ending: a regular card while
// locked, a whole row once open
public class LevelSelectView : MonoBehaviour
{
    [Tooltip("Back goes to the title screen")]
    public MenuView menu;

    [Header("Cards")]
    public LevelCard cardTemplate;
    [Min(1)] public int cardsPerRow = 4;
    [Tooltip("A short row (fewer than Cards Per Row) starts at the left, under the full rows; off: centered")]
    public bool leftAlignRows = true;
    [Tooltip("Rows of cards on one page; more go on the next page")]
    [Min(1)] public int rowsPerPage = 4;
    [Tooltip("Space between cards side by side")]
    public float cardSpacing = 24f;
    [Tooltip("Space between rows")]
    public float rowSpacing = 24f;
    [Tooltip("A card's text; {n} is the level's number")]
    public string cardText = "Level {n}";
    public string lockedText = "Locked";

    [Header("Pages")]
    public Button prevButton; // <
    public Button nextButton; // >
    public TMP_Text pageLabel;
    [Tooltip("{p} is the page's number, {total} the number of pages")]
    public string pageText = "{p}/{total}";

    // the card past the last level, once it's passed: opens the ending screen
    const string EndCardText = "The End";
    // each colour group's own colour (the ending's confetti uses them)
    [NonSerialized] public Color[] groupColors = { new Color32(0x6B, 0x70, 0xF0, 0xFF), new Color32(0xE0, 0x52, 0x4F, 0xFF), new Color32(0x3F, 0xAE, 0x6A, 0xFF), new Color32(0xEF, 0x8A, 0x2C, 0xFF), new Color32(0xC2, 0x56, 0x9B, 0xFF), new Color32(0x4A, 0x7F, 0xE0, 0xFF) };
    // card greys: the first group the lightest slate, the last the darkest, the ones between evenly darker; each group's
    // first level this far toward the accent
    static readonly Color TopSectionShade = new Color32(0x6A, 0x6F, 0x84, 0xFF), BottomSectionShade = new Color32(0x40, 0x44, 0x52, 0xFF);
    const float FirstLevelTint = 0.4f;

    readonly List<LevelCard> cards = new List<LevelCard>();
    readonly List<string> names = new List<string>();   // each card's text: "Level 1"
    readonly List<int> colourOf = new List<int>();      // each card's colour group (LevelData.colorGroup)
    readonly List<int> pageOf = new List<int>();        // each card's page
    int levelCount;                                      // the levels' cards (the end card comes after them)
    bool EndOpen => levelCount > 0 && Session.FinaleOpen; // every level starred, or Unlock all (editor)
    bool laidOutOpen; // EndOpen when the cards were last laid out
    Color cardColor;
    Vector2 labelAnchorMin, labelAnchorMax, labelPosition, labelSize;
    TextAlignmentOptions labelAlignment;
    Vector2 cardSize, firstCard; // the template's size and spot (the top row of a page)
    int page, pageCount = 1;

    void OnValidate()
    {
        if (Application.isPlaying && cards.Count > 0) { Layout(); Refresh(); }
    }

    // one card per level, then the end card; onPick gets the level's index (the end card: the level count)
    public void Build(IList<LevelData> levels, Action<int> onPick)
    {
        foreach (var c in cards) Destroy(c.gameObject);
        cards.Clear(); names.Clear(); colourOf.Clear();
        levelCount = levels.Count;
        cardTemplate.gameObject.SetActive(false);
        if (cardTemplate.button.targetGraphic != null) cardColor = cardTemplate.button.targetGraphic.color;
        var label = cardTemplate.label.rectTransform;
        labelAnchorMin = label.anchorMin; labelAnchorMax = label.anchorMax;
        labelPosition = label.anchoredPosition; labelSize = label.sizeDelta;
        labelAlignment = cardTemplate.label.alignment;
        var templateRect = (RectTransform)cardTemplate.transform;
        cardSize = templateRect.sizeDelta; firstCard = templateRect.anchoredPosition;
        for (int i = 0; i <= levels.Count; i++)
        {
            bool end = i == levels.Count;
            var card = Instantiate(cardTemplate, cardTemplate.transform.parent);
            card.name = end ? "EndCard" : $"Level{i + 1}Card";
            card.button.onClick = new Button.ButtonClickedEvent(); // drops anything set on the template in the Inspector
            int index = i; // copy for the click handler
            card.button.onClick.AddListener(() => onPick(index));
            foreach (var edge in card.tutorialFrame) edge.gameObject.SetActive(false); // (the old locked-tutorial frame)
            cards.Add(card);
            names.Add(end ? EndCardText : cardText.Replace("{n}", levels[i].label ?? (i + 1).ToString()));
            colourOf.Add(end ? -1 : levels[i].colorGroup);
        }
        if (prevButton != null) { prevButton.onClick.RemoveListener(PrevPage); prevButton.onClick.AddListener(PrevPage); }
        if (nextButton != null) { nextButton.onClick.RemoveListener(NextPage); nextButton.onClick.AddListener(NextPage); }
        Layout();
        Refresh();
    }

    // a card's grey: its group's shade, a group's first level tinted toward the accent; locked: faded toward the page
    Color CardShade(int i, bool open)
    {
        bool first = i == 0 || colourOf[i] != colourOf[i - 1];
        var c = SectionShade(Mathf.Max(0, colourOf[i]));
        if (first) c = Color.Lerp(c, UIPalette.Current.accent, FirstLevelTint);
        return open ? c : Color.Lerp(c, UIPalette.Current.background, 0.4f);
    }

    static Color SectionShade(int group) => Color.Lerp(TopSectionShade, BottomSectionShade, Levels.GroupNames.Length > 1 ? (float)group / (Levels.GroupNames.Length - 1) : 0f);

    // each colour group's grey as the cards show it: the ending screen's swatches
    public Color[] SectionColors()
    {
        var res = new Color[Levels.GroupNames.Length];
        for (int g = 0; g < res.Length; g++) res[g] = SectionShade(g);
        return res;
    }

    // rows of cardsPerRow, rowsPerPage rows a page; the end card: once open, a row of its own as wide as a row
    void Layout()
    {
        pageOf.Clear();
        laidOutOpen = EndOpen;
        int row = 0, inRow = 0;
        float RowY(int r) => firstCard.y - (r % rowsPerPage) * (cardSize.y + rowSpacing);
        for (int i = 0; i < cards.Count; i++)
        {
            var rt = (RectTransform)cards[i].transform;
            if (i == levelCount && laidOutOpen) // the open end card
            {
                if (inRow > 0) { row++; inRow = 0; }
                rt.sizeDelta = new Vector2(cardsPerRow * cardSize.x + (cardsPerRow - 1) * cardSpacing, cardSize.y);
                rt.anchoredPosition = new Vector2(firstCard.x, RowY(row));
            }
            else
            {
                rt.sizeDelta = cardSize;
                int across = leftAlignRows ? cardsPerRow : Mathf.Min(cardsPerRow, cards.Count - (i - inRow));
                rt.anchoredPosition = new Vector2(firstCard.x + (inRow - (across - 1) / 2f) * (cardSize.x + cardSpacing), RowY(row));
            }
            pageOf.Add(row / rowsPerPage);
            if (++inRow == cardsPerRow || (i == levelCount && laidOutOpen)) { row++; inRow = 0; }
        }
        pageCount = Mathf.Max(1, (row + (inRow > 0 ? 1 : 0) + rowsPerPage - 1) / rowsPerPage);
        ShowPage(page);
    }

    // the Levels page's Back: to the title screen
    public void Back() { if (menu != null) menu.HideLevels(); }

    // the Levels button: on the last opened level's page (before any, the level to play next)
    public void Open()
    {
        Layout(); // the end card may have just opened
        Refresh();
        int level = Session.lastOpened >= 0 ? Session.lastOpened : Session.StartLevel(levelCount);
        ShowPage(level >= 0 && level < pageOf.Count ? pageOf[level] : 0);
    }

    public void PrevPage() => ShowPage(page - 1);
    public void NextPage() => ShowPage(page + 1);

    // only that page's cards; < and > greyed at the ends, hidden with one page
    public void ShowPage(int p)
    {
        page = Mathf.Clamp(p, 0, pageCount - 1);
        for (int i = 0; i < cards.Count; i++) cards[i].gameObject.SetActive(pageOf[i] == page);
        bool several = pageCount > 1;
        if (prevButton != null) { prevButton.gameObject.SetActive(several); prevButton.interactable = page > 0; }
        if (nextButton != null) { nextButton.gameObject.SetActive(several); nextButton.interactable = page < pageCount - 1; }
        if (pageLabel != null)
        {
            pageLabel.gameObject.SetActive(several);
            pageLabel.text = pageText.Replace("{p}", (page + 1).ToString()).Replace("{total}", pageCount.ToString());
        }
    }

    // locks and stars, from this session's results
    public void Refresh()
    {
        if (cards.Count > 0 && laidOutOpen != EndOpen) Layout(); // the end card opened or closed (Unlock all)
        var palette = UIPalette.Current;
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (i >= levelCount) // the end card: the accent, its name, no stars; locked (faded) till the ending opens
            {
                card.button.interactable = EndOpen;
                if (card.button.targetGraphic != null) card.button.targetGraphic.color = EndOpen ? palette.accent : Color.Lerp(cardColor, palette.accent, 0.35f);
                card.label.text = EndOpen ? names[i] : lockedText;
                PlaceLabel(card.label, false); // centred on the card
                foreach (var st in card.stars) st.gameObject.SetActive(false);
                continue;
            }
            var record = Session.Get(i);
            bool open = Session.IsUnlocked(i);
            card.button.interactable = open;
            if (card.button.targetGraphic != null) card.button.targetGraphic.color = CardShade(i, open);
            card.label.text = open ? names[i] : lockedText;
            PlaceLabel(card.label, open);
            for (int s = 0; s < card.stars.Length; s++)
            {
                bool earned = s < record.stars;
                card.stars[s].gameObject.SetActive(open);
                card.stars[s].color = earned ? (record.purple ? palette.purpleStar : palette.star) : palette.starEmpty;
                StarSparkle.Set(card.stars[s], open && earned && record.purple); // purple stars sparkle
            }
        }
    }

    // open: where the template's Label is. locked (and the end card): the whole card, centered
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
}
