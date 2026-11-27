using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the Settings pop-up
public class SettingsView : MonoBehaviour
{
    public GameObject panel;
    [Header("Placing tiles: one button per choice (the chosen one in the palette's Accent, the others in Button)")]
    public Button clickButton;
    public Button dragButton;
    public Button bothButton;
    [Header("Switches")]
    public Toggle undoAnimationToggle;
    public Toggle undoByStepToggle; // undo goes back to the last arrow question first
    public Toggle showBreaksToggle; // hovering a placement crosses out the tiles it would break
    [Header("Sound: 0 to 1 sliders")]
    public Slider musicSlider;
    public Slider effectsSlider;
    public Toggle unlockAllToggle;
    public Toggle compactSupplyToggle; // the supply shows only this level's tiles (and the +)
    public Toggle showTutorialsToggle;
    public Toggle hintTipsToggle; // hovering a stage 2 hint mark says what it means
    [Header("Reset progress: the button opens the confirm pop-up; its buttons call ConfirmReset and CloseResetConfirm")]
    public Button resetProgressButton;
    public GameObject resetConfirmPanel;
    public GameManager game;
    public LevelSelectView levelSelect; // its locks change with Unlock all

    [System.Serializable]
    public class Tab
    {
        public Button button;
        public GameObject page;
        public TMP_Text label;   // optional: Text when chosen, MutedText otherwise (the button itself is then left alone)
        public GameObject marker; // optional: the bar beside the chosen tab
    }
    [Header("Tabs: each button shows its page; opens on the first. without a label the chosen button is filled Accent, the others see-through")]
    public Tab[] tabs = new Tab[0];

    void Start()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int k = i;
            if (tabs[i].button != null) tabs[i].button.onClick.AddListener(() => ShowTab(k));
        }
        Choice(clickButton, Session.Placement.Click);
        Choice(dragButton, Session.Placement.Drag);
        Choice(bothButton, Session.Placement.Both);
        undoAnimationToggle.onValueChanged.AddListener(on => game.AnimateUndo = on);
        if (undoByStepToggle != null) undoByStepToggle.onValueChanged.AddListener(on => game.UndoByStep = on);
        if (showBreaksToggle != null) showBreaksToggle.onValueChanged.AddListener(on => game.ShowBreaks = on);
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(v => Session.musicVolume = v);
        if (effectsSlider != null) effectsSlider.onValueChanged.AddListener(v => Session.effectsVolume = v);
        if (!Session.DevTools) HideRow(unlockAllToggle.transform); // Unlock all: the editor only
        unlockAllToggle.onValueChanged.AddListener(on =>
        {
            game.UnlockAll = on;
            if (levelSelect != null) levelSelect.Refresh();
        });
        if (compactSupplyToggle != null) compactSupplyToggle.onValueChanged.AddListener(on => game.CompactSupply = on);
        if (showTutorialsToggle != null) showTutorialsToggle.onValueChanged.AddListener(on => game.ShowTutorials = on);
        if (hintTipsToggle != null) hintTipsToggle.onValueChanged.AddListener(on => game.ShowHintTips = on);
        if (resetProgressButton != null) resetProgressButton.onClick.AddListener(() => { if (resetConfirmPanel != null) resetConfirmPanel.SetActive(true); });
        CloseResetConfirm();
    }

    // a row taken off its page (its "...Row" object, else the page's child holding it); the rows under it move up
    // into its place (a layout group on the page does that itself)
    void HideRow(Transform t)
    {
        Transform page = null;
        foreach (var tab in tabs) if (tab.page != null && t.IsChildOf(tab.page.transform)) page = tab.page.transform;
        Transform row = t;
        while (row.parent != null && row.parent != page && !row.name.EndsWith("Row")) row = row.parent;
        if (!(row is RectTransform rrt) || row.parent == null) { t.gameObject.SetActive(false); return; }
        float y = rrt.anchoredPosition.y, next = float.NegativeInfinity;
        foreach (Transform s in row.parent)
            if (s != row && s.gameObject.activeSelf && s is RectTransform srt && srt.anchoredPosition.y < y - 1f) next = Mathf.Max(next, srt.anchoredPosition.y);
        row.gameObject.SetActive(false);
        if (row.parent.GetComponent<LayoutGroup>() != null || float.IsNegativeInfinity(next)) return;
        float shift = y - next; // one row's step
        foreach (Transform s in row.parent)
            if (s != row && s is RectTransform srt && srt.anchoredPosition.y < y - 1f) srt.anchoredPosition += new Vector2(0f, shift);
    }

    // the confirm pop-up's Reset button: everything resets and it's back to the start screen
    public void ConfirmReset()
    {
        CloseResetConfirm();
        game.ResetProgress();
        Close();
    }

    // the confirm pop-up's Cancel button
    public void CloseResetConfirm()
    {
        if (resetConfirmPanel != null) resetConfirmPanel.SetActive(false);
    }

    void Choice(Button b, Session.Placement p)
    {
        if (b == null) return;
        b.onClick.AddListener(() => { Session.placement = p; ShowPlacement(); });
    }

    // the chosen way is highlighted
    void ShowPlacement()
    {
        void Mark(Button b, Session.Placement p) { if (b != null && b.targetGraphic != null) b.targetGraphic.color = Session.placement == p ? UIPalette.Current.accent : UIPalette.Current.button; }
        Mark(clickButton, Session.Placement.Click);
        Mark(dragButton, Session.Placement.Drag);
        Mark(bothButton, Session.Placement.Both);
    }

    public void Open()
    {
        undoAnimationToggle.SetIsOnWithoutNotify(game.AnimateUndo);
        if (undoByStepToggle != null) undoByStepToggle.SetIsOnWithoutNotify(game.UndoByStep);
        if (showBreaksToggle != null) showBreaksToggle.SetIsOnWithoutNotify(game.ShowBreaks);
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(Session.musicVolume);
        if (effectsSlider != null) effectsSlider.SetValueWithoutNotify(Session.effectsVolume);
        unlockAllToggle.SetIsOnWithoutNotify(game.UnlockAll);
        if (compactSupplyToggle != null) compactSupplyToggle.SetIsOnWithoutNotify(game.CompactSupply);
        if (showTutorialsToggle != null) showTutorialsToggle.SetIsOnWithoutNotify(game.ShowTutorials);
        if (hintTipsToggle != null) hintTipsToggle.SetIsOnWithoutNotify(game.ShowHintTips);
        ShowPlacement();
        CloseResetConfirm();
        panel.SetActive(true);
        ShowTab(0); // after the panel is on, so nothing recolors the tabs after it
    }

    // the chosen tab's page shows, the others hide
    // for the tutorial: the tab whose page holds this control
    public void ShowPageOf(Component c)
    {
        if (c == null) return;
        for (int k = 0; k < tabs.Length; k++)
            if (tabs[k].page != null && c.transform.IsChildOf(tabs[k].page.transform)) { ShowTab(k); return; }
    }

    void ShowTab(int i)
    {
        for (int k = 0; k < tabs.Length; k++)
        {
            if (tabs[k].page != null) tabs[k].page.SetActive(k == i);
            var p = UIPalette.Current;
            if (tabs[k].marker != null) tabs[k].marker.SetActive(k == i);
            if (tabs[k].label != null) tabs[k].label.color = k == i ? p.text : p.mutedText;
            else if (tabs[k].button != null && tabs[k].button.targetGraphic != null)
                tabs[k].button.targetGraphic.color = k == i ? p.accent : Color.clear; // the others see-through (still clickable)
        }
    }

    public void Close()
    {
        CloseResetConfirm();
        panel.SetActive(false);
        Session.SaveNow(); // the volume sliders
    }
}
