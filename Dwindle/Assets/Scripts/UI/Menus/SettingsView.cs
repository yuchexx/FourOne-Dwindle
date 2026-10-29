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
    public Toggle unlockAllToggle;
    public GameManager game;
    public LevelSelectView levelSelect; // its locks change with Unlock all

    void Start()
    {
        Choice(clickButton, Session.Placement.Click);
        Choice(dragButton, Session.Placement.Drag);
        Choice(bothButton, Session.Placement.Both);
        undoAnimationToggle.onValueChanged.AddListener(on => game.AnimateUndo = on);
        unlockAllToggle.onValueChanged.AddListener(on =>
        {
            game.UnlockAll = on;
            if (levelSelect != null) levelSelect.Refresh();
        });
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
        unlockAllToggle.SetIsOnWithoutNotify(game.UnlockAll);
        ShowPlacement();
        panel.SetActive(true);
    }

    public void Close() => panel.SetActive(false);
}
