using TMPro;
using UnityEngine;

// recolors the <color=#Match> parts of this text to a palette role; the rest of the text keeps its own colors (in play)
[RequireComponent(typeof(TMP_Text))]
public class PaletteTextColor : MonoBehaviour
{
    public UIPalette.Role role = UIPalette.Role.Accent;
    [Tooltip("The tag color written in the text to replace, as in <color=#7C83F7>")]
    public string match = "#7C83F7";

    TMP_Text text;
    string source; // the text as written in the scene

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        source = text.text;
    }

    void OnEnable()
    {
        UIPalette.Changed += Apply;
        Apply();
    }

    void OnDisable() => UIPalette.Changed -= Apply;

    public void Apply()
    {
        if (text == null || string.IsNullOrEmpty(source) || string.IsNullOrEmpty(match)) return;
        var hex = "#" + ColorUtility.ToHtmlStringRGB(UIPalette.Current.Get(role));
        var tag = "<color=" + (match.StartsWith("#") ? match : "#" + match);
        text.text = System.Text.RegularExpressions.Regex.Replace(source, System.Text.RegularExpressions.Regex.Escape(tag) + "(?=>)",
            "<color=" + hex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
