using TMPro;
using UnityEngine;

public class ThemeColorSettings : MonoBehaviour
{
    public TMP_Text selectedColorText;

    public void ChoosePurple()
    {
        Choose("Purple", UIPalette.Current.defaultAccent);
    }

    public void ChooseBlue()
    {
        Choose("Blue", new Color32(0x39, 0x77, 0xC7, 0xFF));
    }

    public void ChooseTeal()
    {
        Choose("Teal", new Color32(0x24, 0x7E, 0x78, 0xFF));
    }

    public void ChooseRose()
    {
        Choose("Rose", new Color32(0xAF, 0x4D, 0x79, 0xFF));
    }

    void Choose(string name, Color color)
    {
        UIPalette.SetThemeColor(color);

        if (selectedColorText != null)
            selectedColorText.text = "Selected: " + name;
    }
}