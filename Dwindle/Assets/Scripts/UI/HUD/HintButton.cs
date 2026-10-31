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
    [Tooltip("Optional: the note above Hide / Show, there from stage 2")]
    public HintNote stageNote;
    [Tooltip("Asks before stage 3; its buttons call ShowAnswer and CloseConfirm")]
    public GameObject confirmPanel;
    [Tooltip("The Hide / Show button's text. Its On Click: HintButton > ToggleHidden")]
    public TMP_Text hideLabel;
    public string hideText = "Hide", showText = "Show";
    [Tooltip("Optional: opens Hide / Show on hover, like the More menu. It only opens once a hint is used")]
    public HoverReveal hoverPanel;

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
        if (stageNote != null && stageNote.gameObject.activeInHierarchy) stageNote.Pulse(k);
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
        if (stageNote != null)
        {
            stageNote.Show(stage, game.board);
            stageNote.gameObject.SetActive(stage >= 2 && !hidden);
        }
        if (hideLabel != null) hideLabel.text = hidden ? showText : hideText;
        if (hoverPanel != null) hoverPanel.enabled = stage >= 1; // nothing to show or hide before the first hint
    }
}
