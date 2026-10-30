using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the title screen, the Levels page, the rules and Settings pop-ups, and the Leave and End screens
public class MenuView : MonoBehaviour
{
    [Header("Screens")]
    public GameObject startScreen; // the title screen: Start, Levels, Rules, Settings
    public GameObject levelsScreen;
    public GameObject gameScreen;
    public GameObject rulesPopup;

    [Header("Title screen")]
    [Tooltip("The Start button's text: Start, or Continue: Level N once a level is done")]
    public TMP_Text startLabel;
    public LevelSelectView levelSelect;
    public SettingsView settings;

    [Header("In a level")]
    public LeaveDialog leaveDialog;
    public EndWindow endWindow;

    [Header("Rules pages")]
    public GameObject[] rulesPages;
    public Button previousRulesButton;
    public Button nextRulesButton;
    public TMP_Text rulesPageIndicator;

    int currentRulesPage;

    // the title screen. startText: the Start button's text
    public void ShowStart(string startText = null)
    {
        CloseOverlays();
        startScreen.SetActive(true);
        if (levelsScreen != null) levelsScreen.SetActive(false);
        gameScreen.SetActive(false);
        if (startLabel != null && startText != null) startLabel.text = startText;
    }

    public void ShowGame()
    {
        CloseOverlays();
        startScreen.SetActive(false);
        if (levelsScreen != null) levelsScreen.SetActive(false);
        gameScreen.SetActive(true);
    }

    public void CloseOverlays()
    {
        rulesPopup.SetActive(false);
        HideMenu();
        if (settings != null) settings.Close();
    }

    // the title screen's Levels button
    public void ShowLevels()
    {
        if (levelsScreen == null || levelSelect == null) { Debug.LogWarning("MenuView: Levels Screen and Level Select aren't set up yet."); return; }
        CloseOverlays();
        startScreen.SetActive(false);
        levelsScreen.SetActive(true);
        levelSelect.Refresh();
    }

    // the Levels page's Back button
    public void HideLevels()
    {
        levelsScreen.SetActive(false);
        startScreen.SetActive(true);
    }

    // the title screen's Settings button (and the Settings panel's Close)
    public void ShowSettings()
    {
        if (settings == null) { Debug.LogWarning("MenuView: Settings isn't set up yet."); return; }
        settings.Open();
    }
    public void HideSettings() { if (settings != null) settings.Close(); }

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

    // hides the Leave and End screens
    public void HideMenu()
    {
        if (leaveDialog != null) leaveDialog.Close();
        if (endWindow != null) endWindow.Close();
    }
}
