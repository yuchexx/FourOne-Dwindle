using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the start screen, the rules pop-up and the level menu
public class MenuView : MonoBehaviour
{
    [Header("Screens")]
    public GameObject startScreen;
    public GameObject gameScreen;
    public GameObject rulesPopup;
    public GameObject levelMenu;
    public GameObject tutorialPopup;

    [Header("Rules pages")]
    public GameObject[] rulesPages;
    public Button previousRulesButton;
    public Button nextRulesButton;
    public TMP_Text rulesPageIndicator;

    int currentRulesPage;

    [Header("Level select")]
    public Button levelButtonTemplate; // left empty, the Level1Button on the start screen is used
    public float levelButtonSpacing = 20f;
    [Min(1)] public int levelButtonsPerRow = 4;
    [Tooltip("Level numbers (from 1) whose buttons get the tutorial color")]
    public int[] tutorialLevels = { 1, 6 };
    public Color tutorialButtonColor = new Color(0.30f, 0.66f, 0.58f);

    [Header("Level menu")]
    public TMP_Text menuTitle;
    public TMP_Text menuSummary;
    public Graphic[] menuStars; // StarGraphics
    public GameObject resumeButton;
    public Button nextButton;
    [Tooltip("Shown in Next's place while paused mid-level; its On Click calls GameManager.EndLevelNow")]
    public Button endLevelButton;

    [Header("Level menu layout (in Play mode, changes show right away)")]
    public Vector2 menuPanelSize = new Vector2(700, 640);
    [Tooltip("From the panel's top edge down to the middle of the title")]
    public float titleFromTop = 50f;
    [Tooltip("From the panel's top edge down to the middle of the stars")]
    public float starsFromTop = 135f;
    [Tooltip("From the panel's top edge down to the top of the summary")]
    public float summaryFromTop = 185f;
    [Tooltip("From the panel's bottom edge up to the bottom of the summary")]
    public float summaryFromBottom = 145f;
    [Tooltip("Space left and right of the summary")]
    public float summarySidePadding = 30f;
    [Tooltip("From the panel's bottom edge up to the middle of the buttons")]
    public float buttonsFromBottom = 55f;

    [Header("Colors")]
    public Color starOnColor = new Color(1f, 0.82f, 0.2f);
    public Color starOffColor = new Color(0.32f, 0.33f, 0.37f);
    public Color bonusStarOnColor = new Color(0.45f, 0.80f, 0.95f); // the third star, for the score
    public bool outlineMissedScoreStar = true; // a missed third star is outlined in its color (width: Outline Width on the star)

    [Header("Tutorial")]
    public TMP_Text tutorialTitle;
    public TMP_Text tutorialBody;

    readonly List<Button> levelButtons = new List<Button>();
    float summaryFontSize = -1; // the summary's font size from the editor, kept as the largest it can grow to
    bool layoutChanged;

    // a layout number was changed in the Inspector
    void OnValidate() => layoutChanged = true;

    void Update()
    {
        if (!layoutChanged) return;
        layoutChanged = false;
        if (levelMenu != null && levelMenu.activeInHierarchy) LayOutMenu();
    }

    public void ShowStart()
    {
        CloseOverlays();
        startScreen.SetActive(true);
        gameScreen.SetActive(false);
    }

    public void ShowGame()
    {
        CloseOverlays();
        startScreen.SetActive(false);
        gameScreen.SetActive(true);
    }

    public void CloseOverlays()
    {
        rulesPopup.SetActive(false);
        levelMenu.SetActive(false);

        if (tutorialPopup != null)
            tutorialPopup.SetActive(false);
    }

    public void ShowTutorial(string title, string body)
    {
        tutorialTitle.text = title;
        tutorialBody.text = body;
        tutorialPopup.SetActive(true);
    }

    public void HideTutorial()
    {
        tutorialPopup.SetActive(false);
    }

    // one button per level, cloned from the template, in rows centered where the template sits
    public void BuildLevelButtons(int count, Action<int> onPick)
    {
        var template = levelButtonTemplate != null ? levelButtonTemplate : FindStartButton("Level1Button");
        if (template == null) return;

        foreach (var b in levelButtons) Destroy(b.gameObject);
        levelButtons.Clear();
        // the hand-made Level buttons are replaced by the generated ones
        foreach (Transform child in template.transform.parent)
            if (child.name.StartsWith("Level") && child.name.EndsWith("Button")) child.gameObject.SetActive(false);

        var templateRect = (RectTransform)template.transform;
        Vector2 size = templateRect.sizeDelta, origin = templateRect.anchoredPosition;
        for (int i = 0; i < count; i++)
        {
            int row = i / levelButtonsPerRow, col = i % levelButtonsPerRow;
            int inRow = Mathf.Min(levelButtonsPerRow, count - row * levelButtonsPerRow);
            var button = Instantiate(template, template.transform.parent);
            button.name = $"Level{i + 1}Button (generated)";
            button.gameObject.SetActive(true);
            var rt = (RectTransform)button.transform;
            rt.anchoredPosition = new Vector2(
                (col - (inRow - 1) / 2f) * (size.x + levelButtonSpacing),
                origin.y - row * (size.y + levelButtonSpacing));

            button.onClick = new Button.ButtonClickedEvent(); // drops the template's LoadLevel(0) from the Inspector
            int index = i; // copy for the click handler below
            button.onClick.AddListener(() => onPick(index));

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = $"Level {i + 1}";
            if (Array.IndexOf(tutorialLevels, i + 1) >= 0 && button.targetGraphic != null)
                button.targetGraphic.color = tutorialButtonColor; // a tutorial level
            levelButtons.Add(button);
        }
    }

    Button FindStartButton(string name)
    {
        foreach (var b in startScreen.GetComponentsInChildren<Button>(true))
            if (b.name == name) return b;
        return null;
    }

    public void SetRulesVisible(bool visible)
    {
        rulesPopup.SetActive(visible);

        if (visible)
            ShowRulesPage(0);
    }

    public void ShowNextRulesPage()
    {
        ShowRulesPage(currentRulesPage + 1);
    }

    public void ShowPreviousRulesPage()
    {
        ShowRulesPage(currentRulesPage - 1);
    }

    void ShowRulesPage(int pageIndex)
    {
        if (rulesPages == null || rulesPages.Length == 0)
            return;

        currentRulesPage = Mathf.Clamp(
            pageIndex,
            0,
            rulesPages.Length - 1
        );

        for (int i = 0; i < rulesPages.Length; i++)
            rulesPages[i].SetActive(i == currentRulesPage);

        previousRulesButton.gameObject.SetActive(currentRulesPage > 0);
        nextRulesButton.gameObject.SetActive(
            currentRulesPage < rulesPages.Length - 1
        );

        rulesPageIndicator.text =
            $"{currentRulesPage + 1} / {rulesPages.Length}";
    }

    public void HideMenu() => levelMenu.SetActive(false);

    // the first two stars are delivery tiers; the third is the score target in its own color
    public void ShowMenu(string title, string summary, bool star1, bool star2, bool scoreStar,
                         bool showResume, bool showNext)
    {
        LayOutMenu();
        if (menuStars.Length > 0) menuStars[0].color = star1 ? starOnColor : starOffColor;
        if (menuStars.Length > 1) menuStars[1].color = star2 ? starOnColor : starOffColor;
        if (menuStars.Length > 2)
        {
            menuStars[2].color = scoreStar ? bonusStarOnColor : starOffColor;
            // a missed 3rd star keeps its own color as an outline, so it reads as the score star still to get
            if (menuStars[2] is StarGraphic third) third.outlineColor = scoreStar || !outlineMissedScoreStar ? Color.clear : bonusStarOnColor;
        }

        menuTitle.text = title;
        menuSummary.text = summary;

        resumeButton.SetActive(showResume);
        nextButton.gameObject.SetActive(showNext);
        if (endLevelButton != null) endLevelButton.gameObject.SetActive(showResume); // paused mid-level: End level where Next goes
        levelMenu.SetActive(true);
    }

    // sizes the level menu's panel so the score breakdown fits, and places the title, stars, summary and buttons
    void LayOutMenu()
    {
        var panel = (RectTransform)menuTitle.transform.parent;
        panel.sizeDelta = menuPanelSize;
        float top = menuPanelSize.y / 2f;

        SetY(menuTitle.rectTransform, top - titleFromTop);
        foreach (var star in menuStars) SetY(star.rectTransform, top - starsFromTop);

        // the summary fills the space between the stars and the buttons
        var summaryRect = menuSummary.rectTransform;
        float summaryHeight = Mathf.Max(0f, menuPanelSize.y - summaryFromTop - summaryFromBottom);
        summaryRect.sizeDelta = new Vector2(menuPanelSize.x - 2 * summarySidePadding, summaryHeight);
        SetY(summaryRect, top - summaryFromTop - summaryHeight / 2f);
        menuSummary.alignment = TextAlignmentOptions.Top;
        // keep the font size set in the editor; only shrink if the text doesn't fit
        if (summaryFontSize < 0) summaryFontSize = menuSummary.fontSize;
        menuSummary.fontSizeMax = summaryFontSize;
        menuSummary.fontSizeMin = Mathf.Min(14, summaryFontSize);
        menuSummary.enableAutoSizing = true;

        // the buttons in a row along the bottom
        foreach (Transform child in panel)
            if (child.GetComponent<Button>() != null) SetY((RectTransform)child, -top + buttonsFromBottom);
    }

    // places a rect's center at height y in its parent (the panel's center is 0), whatever its pivot was
    static void SetY(RectTransform rt, float y)
    {
        rt.anchorMin = new Vector2(rt.anchorMin.x, 0.5f);
        rt.anchorMax = new Vector2(rt.anchorMax.x, 0.5f);
        rt.pivot = new Vector2(rt.pivot.x, 0.5f);
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
    }
}
