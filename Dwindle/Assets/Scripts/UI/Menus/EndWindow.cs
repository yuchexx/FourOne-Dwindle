using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the window when a level ends: stars, score, a short line, and the buttons that can be used, centered
public class EndWindow : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text title;
    public StarRow stars;
    public TMP_Text score;
    public TMP_Text note; // a tip or a cheer; hidden when there's none
    public Button undoButton;
    public Button replayButton;
    public Button homeButton;
    public Button nextButton;
    [Tooltip("Space between the buttons")]
    public float buttonSpacing = 24f;

    public void Show(string titleText, bool[] earned, bool purple, string[] goals, string scoreText, string noteText,
                     bool canUndo, bool canNext)
    {
        title.text = titleText;
        stars.Show(earned, purple, goals);
        score.text = scoreText;
        if (note != null)
        {
            note.text = noteText ?? "";
            note.gameObject.SetActive(!string.IsNullOrEmpty(noteText));
        }
        undoButton.gameObject.SetActive(canUndo);
        nextButton.gameObject.SetActive(canNext);
        panel.SetActive(true);
        LayOutButtons();
    }

    public void Close() => panel.SetActive(false);

    // the shown buttons side by side, centered; Home stays where it's placed
    void LayOutButtons()
    {
        var shown = new[] { undoButton, replayButton, nextButton }
            .Where(b => b != null && b.gameObject.activeSelf).Select(b => (RectTransform)b.transform).ToList();
        float x = -(shown.Sum(r => r.rect.width) + buttonSpacing * (shown.Count - 1)) / 2f;
        foreach (var r in shown)
        {
            float w = r.rect.width;
            r.anchoredPosition = new Vector2(x + w * r.pivot.x, r.anchoredPosition.y);
            x += w + buttonSpacing;
        }
    }
}
