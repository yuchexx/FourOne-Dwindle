using TMPro;
using UnityEngine;

// the "<" pop-up in a level: the stars so far, then Keep playing, End level, Leave
public class LeaveDialog : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text title;
    public StarRow stars;
    public TMP_Text status; // delivered and score so far

    public void Show(string titleText, bool[] earned, string[] goals, string statusText)
    {
        title.text = titleText;
        stars.Show(earned, false, goals);
        if (status != null) status.text = statusText;
        panel.SetActive(true);
    }

    public void Close() => panel.SetActive(false);
}
