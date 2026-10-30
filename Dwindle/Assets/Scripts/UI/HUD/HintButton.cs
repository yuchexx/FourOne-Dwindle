using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the hint button: a dot per stage used, the stage 1 line under the score, and a check before stage 3 (the answer),
// which costs the purple stars
public class HintButton : MonoBehaviour
{
    public GameManager game;
    public Button button;
    public Graphic[] dots;           // one per stage, left to right
    public TMP_Text tilesLine;       // stage 1: "Best plans use 8 tiles, 3 left"
    public GameObject noPurpleLabel; // shown once the answer has been used
    [Tooltip("Optional: what stage 2 and 3 draw on the board mean")]
    public TMP_Text stageLine;
    [TextArea] public string stage2Text = "Same number: they arrive together, 1 before 2. Framed square: where a group meets.";
    [TextArea] public string stage3Text = "The plan's tiles, one more each press.";
    [Tooltip("Asks before stage 3; its buttons call ShowAnswer and CloseConfirm")]
    public GameObject confirmPanel;
    [Tooltip("Optional: hides and shows the hints, there once one is used. Its On Click: HintButton > ToggleHidden")]
    public GameObject hideButton;
    public TMP_Text hideLabel;
    public string hideText = "Hide hints", showText = "Show hints";

    void Start()
    {
        game.HintsChanged += Refresh;
        CloseConfirm();
        Refresh();
    }

    void OnDestroy()
    {
        if (game != null) game.HintsChanged -= Refresh;
    }

    void Update()
    {
        bool can = game.HintAvailable;
        if (button.interactable != can) button.interactable = can;
        // the lines pulse with the board's hints
        float k = game.board.HintPulseLevel;
        if (tilesLine != null && tilesLine.gameObject.activeSelf) tilesLine.alpha = k;
        if (stageLine != null && stageLine.gameObject.activeSelf) stageLine.alpha = k;
    }

    public void ToggleHidden() => game.ToggleHintHidden();

    // the button's On Click
    public void Press()
    {
        if (game.HintStage == Hints.Stages - 1 && confirmPanel != null) confirmPanel.SetActive(true);
        else game.UseHint();
    }

    public void ShowAnswer()
    {
        CloseConfirm();
        game.UseHint();
    }

    public void CloseConfirm()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    void Refresh()
    {
        int stage = game.HintStage;
        bool hidden = game.HintHidden;
        var palette = UIPalette.Current;
        for (int i = 0; i < dots.Length; i++)
            if (dots[i] != null) dots[i].color = i < stage ? palette.accent : palette.mutedText;
        if (tilesLine != null)
        {
            tilesLine.gameObject.SetActive(stage >= 1 && !hidden);
            if (stage >= 1) tilesLine.text = game.HintTilesText;
        }
        if (noPurpleLabel != null) noPurpleLabel.SetActive(game.PurpleLost);
        if (stageLine != null)
        {
            string s = stage == 2 ? stage2Text : stage >= 3 ? stage3Text : "";
            stageLine.text = s;
            stageLine.gameObject.SetActive(s.Length > 0 && !hidden);
        }
        if (hideButton != null) hideButton.SetActive(stage >= 1);
        if (hideLabel != null) hideLabel.text = hidden ? showText : hideText;
    }
}
