// THEME COLORS: switched off (kept for later). the player picks the accent color in Settings: Purple, Blue, Teal, Rose.
// to turn it on: uncomment the class below, make the UIPalette changes listed under it, and give Settings four
// buttons whose On Click call ChoosePurple / ChooseBlue / ChooseTeal / ChooseRose (and a text for selectedColorText)

// using TMPro;
// using UnityEngine;
//
// public class ThemeColorSettings : MonoBehaviour
// {
//     public TMP_Text selectedColorText;
//
//     public void ChoosePurple() => Choose("Purple", UIPalette.Current.defaultAccent);
//     public void ChooseBlue() => Choose("Blue", new Color32(0x39, 0x77, 0xC7, 0xFF));
//     public void ChooseTeal() => Choose("Teal", new Color32(0x24, 0x7E, 0x78, 0xFF));
//     public void ChooseRose() => Choose("Rose", new Color32(0xAF, 0x4D, 0x79, 0xFF));
//
//     void Choose(string name, Color color)
//     {
//         UIPalette.SetThemeColor(color);
//         if (selectedColorText != null) selectedColorText.text = "Selected: " + name;
//     }
// }

// UIPalette.cs changes it needs:
//
// 1. the accent field keeps its saved value under a new name, and "accent" reads the player's choice, so everything
//    that already uses accent (PaletteColor's Accent role, the switches, the placement buttons) follows it:
//
//     [UnityEngine.Serialization.FormerlySerializedAs("accent")]
//     public Color defaultAccent = Hex("#7C83F7");
//     public Color accent => ThemeAccent;
//
// 2. the choice itself (this session only), and a way to set it:
//
//     static Color? chosenThemeColor;
//
//     // the original accent until the player chooses another
//     public Color ThemeAccent => Application.isPlaying && chosenThemeColor.HasValue ? chosenThemeColor.Value : defaultAccent;
//
//     public static void SetThemeColor(Color color)
//     {
//         color.a = 1f;
//         chosenThemeColor = color;
//         Changed?.Invoke();
//     }
//
//     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
//     static void ResetThemeColor() => chosenThemeColor = null;
//
// SettingsView.cs: so the placement buttons recolor right away when the theme changes:
//
//     void OnEnable() => UIPalette.Changed += ShowPlacement;
//     void OnDisable() => UIPalette.Changed -= ShowPlacement;
