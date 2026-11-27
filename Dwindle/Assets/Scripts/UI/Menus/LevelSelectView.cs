using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the Levels page: a part map, then that part's cards in pages (< and >), each section on new rows under its heading.
// without a part map: every card in pages, each part under its heading
public class LevelSelectView : MonoBehaviour
{
    [Header("Part map (optional)")]
    [Tooltip("The first screen: a tile per part. Leave empty for one list of every level")]
    public PartMapView partMap;
    [Tooltip("The list's objects (cards, headings, < >, the part's title), hidden while the part map shows")]
    public GameObject listRoot;
    [Tooltip("Shows the open part's name; back on the part map it shows its own text again (e.g. the page's Title)")]
    public TMP_Text partTitle;
    [Tooltip("Back from the part map goes to the title screen")]
    public MenuView menu;
    [Tooltip("No Part Map set: build a simple one in code from the card template (a preview until the scene has its own)")]
    public bool autoPartMap = true;
    [Tooltip("The made-in-code part map as a small board with a road, a walking ball and a ring (off: plain tiles)")]
    public bool autoBoardMap = true;
    [Tooltip("A part's name on the map and above its list: {n} its number (1, 2, 3...), {name} its name")]
    public string partText = "{n}. {name}";
    [Tooltip("The made-in-code part map: tile size, tiles per row, the first row's height")]
    public Vector2 autoTileSize = new Vector2(300f, 130f);
    [Min(1)] public int autoTilesPerRow = 3;
    public float autoFirstRowY = 160f;

    [Header("Cards")]
    public LevelCard cardTemplate;
    [Min(1)] public int cardsPerRow = 4;
    [Tooltip("A short row (fewer than Cards Per Row) starts at the left, under the full rows; off: centered")]
    public bool leftAlignRows = true;
    [Tooltip("Inside a part: each section starts a new row under its heading. Off: all the part's levels in one run, each heading over the card where its section starts (in Play mode, changes show right away)")]
    public bool sectionRows = true;
    [Tooltip("Rows of cards on one page; more go on the next page")]
    [Min(1)] public int rowsPerPage = 4;
    [Tooltip("Space between cards side by side")]
    public float cardSpacing = 24f;
    [Tooltip("Space between rows")]
    public float rowSpacing = 24f;
    [Tooltip("A card's text; {n} is the level's number")]
    public string cardText = "Level {n}";
    [Tooltip("A teach level's card text; {n} is its letter")]
    public string lessonText = "Lesson {n}";
    public string lockedText = "Locked";

    [Header("Pages")]
    public Button prevButton; // <
    public Button nextButton; // >
    public TMP_Text pageLabel;
    [Tooltip("{p} is the page's number, {total} the number of pages")]
    public string pageText = "{p}/{total}";

    [Header("Colour groups (the plan: one list, no part map)")]
     [System.NonSerialized] public bool colourGroups = true; // on: no part map or headings; each card in its colour group's colour, 4 a row straight through
     [System.NonSerialized] public bool celebrateCleared = false; // the "cleared" pop when a colour group is done (off: to be shown on the cards instead)
     [System.NonSerialized] public string endCardText = "The End"; // the card past the last level, once it's passed: opens the ending screen
    int levelCount;                        // the levels' cards (the end card, if any, comes after them)
    bool EndOpen => levelCount > 0 && Session.FinaleOpen; // editor: the last level passed; a build: every level starred
     [System.NonSerialized] public Color[] groupColors = { new Color32(0x6B, 0x70, 0xF0, 0xFF), new Color32(0xE0, 0x52, 0x4F, 0xFF), new Color32(0x3F, 0xAE, 0x6A, 0xFF), new Color32(0xEF, 0x8A, 0x2C, 0xFF), new Color32(0xC2, 0x56, 0x9B, 0xFF), new Color32(0x4A, 0x7F, 0xE0, 0xFF) }; // placeholder
     [System.NonSerialized] public float celebrateSeconds = 2.5f;

    [Header("Parts (in Play mode, changes show right away)")]
    [Tooltip("Each part (LevelData.section) starts on a new row under its heading")]
    public bool showParts = true;
    [Tooltip("A heading text to copy for each part on a page (hidden itself); its pivot sits above the part's first row")]
    public TMP_Text headingTemplate;
    [Tooltip("Extra space above a part's first row, where its heading goes")]
    public float headingSpace = 40f;
    [Tooltip("Moves every heading, in pixels (x right, y up)")]
    public Vector2 headingOffset;
    [Tooltip("A part that carries on from the page before; {name} is its name")]
    public string continuedText = "{name} (continued)";

    [Header("Tutorial levels (in Play mode, changes show right away)")]
    [Tooltip("An open tutorial level's card: a deeper purple than Accent, so purple stars still show on it")]
     [System.NonSerialized] public Color tutorialCardColor = new Color32(0x40, 0x3B, 0x8A, 0xFF);
    [Header("Colour groups: grey card shades (off: each card in its group's colour)")]
     [System.NonSerialized] public bool shadeCards = true;
     [System.NonSerialized] public Color topSectionShade = new Color32(0x6A, 0x6F, 0x84, 0xFF);    // section 1: the lightest slate
     [System.NonSerialized] public Color bottomSectionShade = new Color32(0x40, 0x44, 0x52, 0xFF); // the last section: the darkest (still clear of the page); the ones between step evenly darker
     [System.NonSerialized] public float firstLevelTint = 0.4f;        // each section's first level: its section's grey this far toward the accent (purple-ish)
    [Tooltip("A locked tutorial level's frame: thickness in screen pixels, so all four edges come out the same")]
     [System.NonSerialized] public int frameWidth = 3;
    [Tooltip("The frame's color (default: a lighter version of the palette's Accent)")]
     [System.NonSerialized] public Color frameColor = new Color32(0x40, 0x3B, 0x8A, 0xFF);

    readonly List<LevelCard> cards = new List<LevelCard>();
    readonly List<bool> hasTutorial = new List<bool>();
    readonly List<string> names = new List<string>();   // each card's text: "Lesson A" or "Level 1"
    readonly List<string> partOf = new List<string>();  // each card's part (LevelData.section, carried forward)
    readonly List<string> groupOf = new List<string>(); // each card's section (LevelData.group, carried forward within a part)
    readonly List<string> parts = new List<string>();   // part names in order
    readonly List<int> pageOf = new List<int>();        // each card's page, -1 when not in the shown part
    readonly List<TMP_Text> headings = new List<TMP_Text>();
    readonly List<int> headingPage = new List<int>();   // each heading's page
    Color cardColor;
    Vector2 labelAnchorMin, labelAnchorMax, labelPosition, labelSize;
    TextAlignmentOptions labelAlignment;
    Vector2 cardSize, firstCard; // the template's size and spot (the top row of a page)
    int page, pageCount = 1;
    int shownPart = -1; // the open part with a part map, -1 for every card
    bool listShown = true; // false while the part map shows
    bool ByPart => partMap != null && !colourGroups;
    readonly List<int> colourOf = new List<int>(); // each card's colour group (LevelData.colorGroup)
    IList<LevelData> shownLevels;

    void OnValidate()
    {
        if (!Application.isPlaying || cards.Count == 0) return;
        Layout(); Refresh();
        if (ByPart) { var labels = new List<string>(); for (int p = 0; p < parts.Count; p++) labels.Add(PartLabel(p)); partMap.Rename(labels); }
        if (ByPart && listShown && shownPart >= 0 && partTitle != null) partTitle.text = PartLabel(shownPart);
    }

    // one card per level; onPick gets the level's index
    public void Build(IList<LevelData> levels, Action<int> onPick)
    {
        foreach (var c in cards) Destroy(c.gameObject);
        cards.Clear();
        hasTutorial.Clear();
        names.Clear();
        partOf.Clear();
        groupOf.Clear();
        parts.Clear();
        colourOf.Clear();
        shownLevels = levels;
        levelCount = levels.Count;
        string part = null, group = null;
        cardTemplate.gameObject.SetActive(false);
        if (headingTemplate != null) headingTemplate.gameObject.SetActive(false);
        if (cardTemplate.button.targetGraphic != null) cardColor = cardTemplate.button.targetGraphic.color;
        var label = cardTemplate.label.rectTransform;
        labelAnchorMin = label.anchorMin; labelAnchorMax = label.anchorMax;
        labelPosition = label.anchoredPosition; labelSize = label.sizeDelta;
        labelAlignment = cardTemplate.label.alignment;
        var templateRect = (RectTransform)cardTemplate.transform;
        cardSize = templateRect.sizeDelta; firstCard = templateRect.anchoredPosition;
        for (int i = 0; i < levels.Count; i++)
        {
            var card = Instantiate(cardTemplate, cardTemplate.transform.parent);
            card.name = $"Level{i + 1}Card";
            card.button.onClick = new Button.ButtonClickedEvent(); // drops anything set on the template in the Inspector
            int index = i; // copy for the click handler
            card.button.onClick.AddListener(() => onPick(index));
            cards.Add(card);
            hasTutorial.Add(!string.IsNullOrEmpty(levels[i].tutorial) || levels[i].teach); // tutorial and teach levels
            names.Add((levels[i].teach && !colourGroups ? lessonText : cardText).Replace("{n}", levels[i].label ?? (i + 1).ToString()));
            colourOf.Add(levels[i].colorGroup);
            if (!string.IsNullOrEmpty(levels[i].section) && levels[i].section != part) { part = levels[i].section; group = null; }
            if (!string.IsNullOrEmpty(levels[i].group)) group = levels[i].group;
            partOf.Add(part);
            groupOf.Add(group);
            if (parts.Count == 0 || parts[parts.Count - 1] != part) parts.Add(part);
        }
        if (colourGroups) // the end card: after the last level, shown once it's passed
        {
            var end = Instantiate(cardTemplate, cardTemplate.transform.parent);
            end.name = "EndCard";
            end.button.onClick = new Button.ButtonClickedEvent();
            end.button.onClick.AddListener(() => onPick(levels.Count));
            cards.Add(end);
            hasTutorial.Add(false);
            names.Add(endCardText);
            colourOf.Add(-1);
            partOf.Add(part);
            groupOf.Add(group);
        }
        // a part with only one section (Challenge, Finale): no heading, the page's title already says it
        foreach (var p in parts)
        {
            var names = new HashSet<string>();
            for (int i = 0; i < cards.Count; i++) if (partOf[i] == p && groupOf[i] != null) names.Add(groupOf[i]);
            if (names.Count <= 1) for (int i = 0; i < cards.Count; i++) if (partOf[i] == p) groupOf[i] = null;
        }
        if (prevButton != null) { prevButton.onClick.RemoveListener(PrevPage); prevButton.onClick.AddListener(PrevPage); }
        if (nextButton != null) { nextButton.onClick.RemoveListener(NextPage); nextButton.onClick.AddListener(NextPage); }
        if (partMap == null && autoPartMap && parts.Count > 1 && !colourGroups) partMap = MakePartMap();
        if (colourGroups && partMap != null) partMap.gameObject.SetActive(false); // a scene part map stays hidden
        if (ByPart) { var labels = new List<string>(); for (int p = 0; p < parts.Count; p++) labels.Add(PartLabel(p)); partMap.Build(labels, ShowPart); }
        Layout();
        Refresh();
    }

    // a part map made in code: a copy of the card template per part (part name, stars), over the whole screen
    PartMapView MakePartMap()
    {
        var screen = cardTemplate.transform.parent;
        var go = new GameObject("PartMap (auto)", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(screen, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;

        var board = autoBoardMap ? FindFirstObjectByType<BoardView>(FindObjectsInactive.Include) : null;
        if (board != null && board.tilePrefab != null && board.objectPrefab != null)
        {
            var bm = go.AddComponent<PartBoardMap>();
            bm.tilePrefab = board.tilePrefab;
            bm.objectPrefab = board.objectPrefab;
            bm.ringPrefab = board.destinationPrefab;
            bm.textTemplate = cardTemplate.label;
            bm.nameColor = bm.starsColor = cardTemplate.label.color;
            bm.buttonStyle = cardTemplate.button;
            if (cardTemplate.button.targetGraphic != null) bm.cellColor = cardTemplate.button.targetGraphic.color;
            bm.ballColor = UIPalette.Current.accent; // the object and its ring in the accent color
            bm.traveledColor = UIPalette.Current.accent; // the traveled road too
            bm.ringHoleColor = UIPalette.Current.background; // the ring's inner circle matches the page
            if (partTitle == null) { var t = screen.Find("Title"); if (t != null) partTitle = t.GetComponent<TMP_Text>(); }
            return bm;
        }

        var card = Instantiate(cardTemplate, rt);
        card.name = "PartTile (auto)";
        foreach (var s in card.stars) s.gameObject.SetActive(false);
        foreach (var f in card.tutorialFrame) f.gameObject.SetActive(false);
        var trt = (RectTransform)card.transform;
        trt.sizeDelta = autoTileSize;
        trt.anchoredPosition = new Vector2(0f, autoFirstRowY);
        var title = card.label;
        var tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 0.4f); tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
        title.alignment = TextAlignmentOptions.Center;
        var stars = Instantiate(title, card.transform);
        stars.name = "Stars";
        stars.fontSize = title.fontSize * 0.7f;
        var sr = stars.rectTransform;
        sr.anchorMin = Vector2.zero; sr.anchorMax = new Vector2(1f, 0.4f); sr.offsetMin = sr.offsetMax = Vector2.zero;

        var tile = card.gameObject.AddComponent<PartTile>();
        tile.button = card.button; tile.title = title; tile.stars = stars;
        Destroy(card); // the LevelCard part; the tile keeps its button and texts
        var map = go.AddComponent<PartMapView>();
        map.tileTemplate = tile;
        map.tilesPerRow = autoTilesPerRow;
        map.tileSpacing = cardSpacing; map.rowSpacing = rowSpacing;
        if (partTitle == null) { var t = screen.Find("Title"); if (t != null) partTitle = t.GetComponent<TMP_Text>(); }
        return map;
    }

    // the list's objects on or off: listRoot when set, else the cards, headings and < > themselves
    void SetList(bool show)
    {
        listShown = show;
        if (listRoot != null) listRoot.SetActive(show);
        if (show) return;
        foreach (var c in cards) c.gameObject.SetActive(false);
        foreach (var h in headings) h.gameObject.SetActive(false);
        if (prevButton != null) prevButton.gameObject.SetActive(false);
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (pageLabel != null) pageLabel.gameObject.SetActive(false);
    }

    string PartLabel(int p) => partText.Replace("{n}", (p + 1).ToString()).Replace("{name}", parts[p] ?? "");

    // the heading a card's row goes under: its section inside a part map's part, else its part (none with colour groups)
    string HeadOf(int i) => colourGroups ? null : ByPart ? (sectionRows ? groupOf[i] : null) : showParts ? partOf[i] : null;

    // a card's colour with colour groups: grey shades (sections alternate two shades, a section's first level lighter
    // so it stands out, the stars readable on all), or the group's own colour when shadeCards is off. locked: darker
    Color CardShade(int i, bool open)
    {
        Color c;
        if (!shadeCards) c = GroupColor(colourOf[i]);
        else
        {
            bool first = i == 0 || colourOf[i] != colourOf[i - 1];
            int g = Mathf.Max(0, colourOf[i]);
            c = first ? Color.Lerp(SectionShade(g), UIPalette.Current.accent, firstLevelTint) : SectionShade(g);
        }
        return open ? c : Color.Lerp(c, UIPalette.Current.background, 0.4f);
    }

    Color SectionShade(int group) => Color.Lerp(topSectionShade, bottomSectionShade, Levels.GroupNames.Length > 1 ? (float)group / (Levels.GroupNames.Length - 1) : 0f); // darker each section

    // each colour group's colour as the cards show it (its shade, or its own colour): the ending screen's swatches
    public Color[] SectionColors()
    {
        var res = new Color[Levels.GroupNames.Length];
        for (int g = 0; g < res.Length; g++) res[g] = shadeCards ? SectionShade(g) : GroupColor(g);
        return res;
    }

    Color GroupColor(int group) => groupColors.Length > 0 ? groupColors[Mathf.Clamp(group, 0, groupColors.Length - 1)] : cardColor;
    bool Shown(int i) => !ByPart || shownPart < 0 || partOf[i] == parts[shownPart];

    // rows: a row ends when full or where a new heading starts. pages: rowsPerPage rows each. a heading's first row on
    // a page gets the heading above it ("(continued)" when it began on the page before)
    void Layout()
    {
        foreach (var h in headings) Destroy(h.gameObject);
        headings.Clear();
        headingPage.Clear();
        pageOf.Clear();
        for (int i = 0; i < cards.Count; i++) pageOf.Add(-1);

        // the rows of shown cards, each as (cards in it)
        var rows = new List<List<int>>();
        int prev = -1;
        for (int i = 0; i < cards.Count; i++)
        {
            if (!Shown(i)) continue;
            bool newHead = prev >= 0 && HeadOf(i) != HeadOf(prev);
            if (prev < 0 || newHead || rows[rows.Count - 1].Count == cardsPerRow) rows.Add(new List<int>());
            rows[rows.Count - 1].Add(i);
            prev = i;
        }
        pageCount = Mathf.Max(1, (rows.Count + rowsPerPage - 1) / rowsPerPage);

        float y = firstCard.y;
        for (int r = 0; r < rows.Count; r++)
        {
            int p = r / rowsPerPage;
            if (r % rowsPerPage == 0) y = firstCard.y; // a new page starts at the top
            var row = rows[r];
            int across = leftAlignRows ? cardsPerRow : row.Count;
            float CardX(int k) => firstCard.x + (k - (across - 1) / 2f) * (cardSize.x + cardSpacing);
            if (ByPart && !sectionRows)
            {
                // one run of cards: each section's heading sits over the card where it starts (and over the first
                // card of a page that carries on a section)
                var starts = new List<(int k, string text)>();
                for (int k = 0; k < row.Count; k++)
                {
                    int i = row[k];
                    bool sectionStarts = i == 0 || !Shown(i - 1) || groupOf[i] != groupOf[i - 1];
                    if (!string.IsNullOrEmpty(groupOf[i]) && sectionStarts) starts.Add((k, groupOf[i]));
                    else if (k == 0 && r % rowsPerPage == 0 && !string.IsNullOrEmpty(groupOf[i])) starts.Add((0, continuedText.Replace("{name}", groupOf[i])));
                }
                if (starts.Count > 0)
                {
                    y -= headingSpace;
                    for (int s = 0; s < starts.Count; s++)
                    {
                        float left = CardX(starts[s].k) - cardSize.x / 2f;
                        float right = s + 1 < starts.Count ? CardX(starts[s + 1].k) - cardSize.x / 2f - cardSpacing : CardX(cardsPerRow - 1) + cardSize.x / 2f;
                        AddHeadingAt(starts[s].text, p, y + cardSize.y / 2f + headingSpace / 2f, left, right - left);
                    }
                }
            }
            else
            {
                string head = HeadOf(row[0]);
                bool headStarts = r == 0 || HeadOf(rows[r - 1][0]) != head;
                if (!string.IsNullOrEmpty(head) && (headStarts || r % rowsPerPage == 0))
                {
                    y -= headingSpace;
                    AddHeading(headStarts ? head : continuedText.Replace("{name}", head), p, y + cardSize.y / 2f + headingSpace / 2f);
                }
            }
            for (int k = 0; k < row.Count; k++)
            {
                ((RectTransform)cards[row[k]].transform).anchoredPosition = new Vector2(CardX(k), y);
                pageOf[row[k]] = p;
            }
            y -= cardSize.y + rowSpacing;
        }
        ShowPage(page);
    }

    // the part map, with the list hidden
    public void ShowMap()
    {
        if (!ByPart) return;
        Refresh();
        partMap.gameObject.SetActive(true);
        SetList(false);
        if (partTitle != null && mapTitle != null) partTitle.text = mapTitle;
    }
    string mapTitle; // partTitle's own text, shown on the part map

    // a part's list, on the last opened level's page when it is in this part (else its first page)
    public void ShowPart(int p)
    {
        if (parts.Count == 0) return;
        shownPart = Mathf.Clamp(p, 0, parts.Count - 1);
        if (ByPart) partMap.gameObject.SetActive(false);
        listShown = true;
        if (listRoot != null) listRoot.SetActive(true);
        if (partTitle != null) { mapTitle ??= partTitle.text; partTitle.text = PartLabel(shownPart); }
        page = 0;
        Layout();
        Refresh();
        int level = Session.lastOpened;
        ShowPage(level >= 0 && level < pageOf.Count && pageOf[level] >= 0 ? pageOf[level] : 0);
    }

    // Reset progress: the part map starts over too
    public void ForgetProgress() { if (partMap != null) partMap.Forget(); }

    // the Levels page's Back: from a part's list to the part map, from the map (or a plain list) to the title screen
    public void Back()
    {
        if (!BackToMap() && menu != null) menu.HideLevels();
    }

    // from a part's list to the part map; false when already there (or there is no map). MenuView.HideLevels asks
    // this first, so the existing Back button works without rewiring
    public bool BackToMap()
    {
        if (!ByPart || !listShown) return false;
        ShowMap();
        return true;
    }

    // a copy of the heading template, centered over the cards at height y, on page p
    void AddHeading(string text, int p, float y)
    {
        if (headingTemplate == null) return;
        var h = Instantiate(headingTemplate, headingTemplate.transform.parent);
        h.name = $"Heading {text}";
        h.text = text;
        h.rectTransform.anchoredPosition = new Vector2(firstCard.x, y) + headingOffset;
        headings.Add(h);
        headingPage.Add(p);
    }

    // a heading from x = left, width wide (left aligned), for one run of cards with headings over their section's first card
    void AddHeadingAt(string text, int p, float y, float left, float width)
    {
        if (headingTemplate == null) return;
        AddHeading(text, p, y);
        var h = headings[headings.Count - 1];
        var rt = h.rectTransform;
        rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
        rt.anchoredPosition = new Vector2(left + width * rt.pivot.x, y) + headingOffset;
        h.alignment = TextAlignmentOptions.Left;
    }

    // the Levels button: the part map (a plain list: on the last opened level's page, before any the level to play next)
    public void Open()
    {
        if (ByPart) { ShowMap(); return; }
        Layout(); // the end card may have just opened
        Refresh();
        int level = Session.lastOpened >= 0 ? Session.lastOpened : Session.StartLevel(levelCount);
        ShowPage(level >= 0 && level < pageOf.Count ? pageOf[level] : 0);
        if (colourGroups && celebrateCleared) CelebrateCleared(); // off for now: the cards will show it later
    }

    // a colour group whose levels are all passed: a short "cleared" pop, once (Session remembers it)
    void CelebrateCleared()
    {
        if (shownLevels == null) return;
        for (int g = 0; g <= colourOf.DefaultIfEmpty(-1).Max(); g++)
        {
            if (Session.Celebrated(g)) continue;
            bool any = false, all = true;
            for (int i = 0; i < cards.Count; i++) if (i < levelCount && colourOf[i] == g) { any = true; all &= Session.Passed(i); }
            if (!any || !all) continue;
            Session.MarkCelebrated(g);
            StartCoroutine(Celebrate(g));
            return; // one at a time
        }
    }

    System.Collections.IEnumerator Celebrate(int g)
    {
        var screen = (RectTransform)cardTemplate.transform.parent;
        var go = new GameObject("GroupCleared", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        var rt = (RectTransform)go.transform;
        rt.SetParent(screen, false);
        rt.sizeDelta = new Vector2(560f, 120f);
        rt.anchoredPosition = new Vector2(0f, 40f);
        var bg = go.GetComponent<Image>();
        bg.color = groupColors.Length > 0 ? groupColors[Mathf.Clamp(g, 0, groupColors.Length - 1)] : UIPalette.Current.accent;
        bg.raycastTarget = false;
        var text = Instantiate(cardTemplate.label, rt);
        text.name = "Text";
        var tr = text.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero; tr.anchoredPosition = Vector2.zero;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        string name = g < Levels.GroupNames.Length ? Levels.GroupNames[g] : "Group";
        text.text = $"<b>{name}</b>\ncleared!";
        var cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime) // pop in
        {
            float k = t / 0.35f;
            float e = 1f + 2.70158f * Mathf.Pow(k - 1f, 3f) + 1.70158f * Mathf.Pow(k - 1f, 2f); // ease out, a little overshoot
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, e);
            cg.alpha = k;
            yield return null;
        }
        rt.localScale = Vector3.one; cg.alpha = 1f;
        yield return new WaitForSecondsRealtime(celebrateSeconds);
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime) { cg.alpha = 1f - t / 0.4f; yield return null; }
        Destroy(go);
    }

    public void PrevPage() => ShowPage(page - 1);
    public void NextPage() => ShowPage(page + 1);

    // only that page's cards and headings; < and > greyed at the ends, hidden with one page
    public void ShowPage(int p)
    {
        page = Mathf.Clamp(p, 0, pageCount - 1);
        if (ByPart && !listShown) { SetList(false); return; } // the part map is up
        for (int i = 0; i < cards.Count; i++) cards[i].gameObject.SetActive(pageOf[i] >= 0 && pageOf[i] == page);
        for (int i = 0; i < headings.Count; i++) headings[i].gameObject.SetActive(headingPage[i] == page);
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
                if (card.tutorialFrame != null) DrawFrame(card, false, frameColor);
                foreach (var st in card.stars) st.gameObject.SetActive(false);
                continue;
            }
            var record = Session.Get(i);
            bool open = Session.IsUnlocked(i);
            card.button.interactable = open;
            if (card.button.targetGraphic != null) card.button.targetGraphic.color = colourGroups ? CardShade(i, open) : open && hasTutorial[i] ? tutorialCardColor : cardColor; // colour groups: grey shades by group (or the group's colour), faded while locked
            card.label.text = open ? names[i] : lockedText;
            PlaceLabel(card.label, open);
            if (card.tutorialFrame != null) DrawFrame(card, !colourGroups && !open && hasTutorial[i], frameColor);
            for (int s = 0; s < card.stars.Length; s++)
            {
                bool earned = s < record.stars;
                card.stars[s].gameObject.SetActive(open);
                card.stars[s].color = earned
    ? (record.purple ? palette.purpleStar : palette.star)
    : palette.starEmpty;
                StarSparkle.Set(card.stars[s], open && earned && record.purple); // purple stars sparkle
            }
        }
        if (!ByPart) return;
        for (int p = 0; p < parts.Count; p++)
        {
            int got = 0, max = 0, first = -1;
            for (int i = 0; i < cards.Count; i++)
            {
                if (partOf[i] != parts[p]) continue;
                if (first < 0) first = i;
                got += Session.Get(i).stars;
                max += 3;
            }
            partMap.Set(p, got, max, first >= 0 && Session.IsUnlocked(first));
        }
        // where the player is: the part of the next level to play, past the last part once every level is passed
        int next = Session.StartLevel(levelCount);
        bool allDone = EndOpen;
        partMap.SetCurrent(allDone ? parts.Count : Mathf.Max(0, parts.IndexOf(partOf[next])));
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
