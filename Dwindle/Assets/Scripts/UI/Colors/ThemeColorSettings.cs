
using TMPro;
using UnityEngine;

// Handles the theme selection buttons in Settings.
// Session manages saving, loading, and applying the selected theme.
public class ThemeColorSettings : MonoBehaviour
{
    [Header("Selected Theme Label")]
    public TMP_Text selectedColorText;

    void OnEnable()
    {
        UIPalette.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        UIPalette.Changed -= Refresh;
    }

    public void ChoosePurple()
    {
        Session.themeColor = Session.ThemeColor.Purple;
    }

    public void ChooseBlue()
    {
        Session.themeColor = Session.ThemeColor.Blue;
    }

    public void ChooseTeal()
    {
        Session.themeColor = Session.ThemeColor.Teal;
    }

    public void ChooseRose()
    {
        Session.themeColor = Session.ThemeColor.Rose;
    }

    void Refresh()
    {
        if (selectedColorText != null)
        {
            selectedColorText.text =
                "Selected: " + Session.themeColor;
        }
    }
}
