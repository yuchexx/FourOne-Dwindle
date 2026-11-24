using UnityEngine;

// the game's UI colors, in one place. make the asset with Create > Dwindle > UI Palette and keep it at
// Assets/Resources/UIPalette (a folder named Resources, a file named UIPalette); until then the defaults below are used.
// a PaletteColor on a UI object keeps that object's color in step with this, and scripts that color things themselves
// (level cards, stars, switches) read it too. the objects' own colors (Red, Blue...) are part of the levels, not this
[CreateAssetMenu(menuName = "Dwindle/UI Palette", fileName = "UIPalette")]
public class UIPalette : ScriptableObject
{
    public enum Role { Background, Panel, Button, Text, MutedText, Accent, Star, StarEmpty, Success, Danger, Dim, Hint, Tutorial, PurpleStar, HintAccent, Hover, ButtonText, AccentText, Outline, BoardFrame, HintText }

    [Header("Surfaces")]
    [Tooltip("Behind everything: the title screen, the Levels page")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("background")]
    Color baseBackground = Hex("#202227");
    [Tooltip("Pop-ups and boxes on top of the background")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("panel")]
    Color basePanel = Hex("#30333D");
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("button")]
    Color baseButton = Hex("#52545E");
    [Tooltip("The dimmed backdrop behind a pop-up")]
    public Color dim = new Color(0f, 0f, 0f, 0.6f);

    [Header("Text")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("text")]
    Color baseText = Hex("#FFFFFF");
    [Tooltip("Subtitles, hints, anything secondary")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("mutedText")]
    Color baseMutedText = Hex("#B3B3B3");

    [Header("Meaning")]
    [Tooltip("Default UI accent color before a player chooses a theme")]
    [UnityEngine.Serialization.FormerlySerializedAs("accent")]
    public Color defaultAccent = Hex("#7C83F7");

    public Color accent => ThemeAccent;
    
    [Tooltip("Icon buttons under the mouse (IconHover)")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("hover")]
    Color baseHover = Hex("#7C83F7");

    [Header("Fixed Gameplay Colors")]
    [Tooltip("Purple achievement stars. Not affected by theme selection.")]
    public Color purpleStar = Hex("#7C83F7");
    [Tooltip("Completed hint stages and hint badges. Not affected by theme selection.")]
    public Color hintAccent = Hex("#7C83F7");

    [Tooltip("The tutorial's frames and its Next button (TutorialOverlay)")]
    public Color tutorial = Hex("#F2C740");
    public Color star = Hex("#FFD133");
    [Tooltip("A star still to earn")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("starEmpty")]
    Color baseStarEmpty = Hex("#1C1D22");
    [Tooltip("Good news: a delivery, an earned tile")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("success")]
    Color baseSuccess = Hex("#61C98B");
    [Tooltip("Bad news: a lost tile, a blocked object")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("danger")]
    Color baseDanger = Hex("#FF7373");
    [Tooltip("The hints drawn on the board: numbers, frames, ghost tiles")]
    public Color hint = Hex("#403B8A");


    static bool lightMode, contrast;
    public static bool LightMode => Application.isPlaying && lightMode;
    public static bool HighContrast => Application.isPlaying && contrast;
    public Color background => LightMode ? Hex(HighContrast ? "#FFFFFF" : "#F2F3F7") : HighContrast ? Color.black : baseBackground;
    public Color panel => LightMode ? Color.white : HighContrast ? Hex("#111318") : basePanel;
    public Color button => LightMode ? Hex(HighContrast ? "#FFFFFF" : "#DFE2EB") : HighContrast ? Hex("#252A34") : baseButton;
    public Color text => LightMode ? Hex("#171A23") : baseText;
    public Color mutedText => LightMode ? Hex(HighContrast ? "#252A34" : "#535967") : HighContrast ? Hex("#E0E4ED") : baseMutedText;
    public Color hover => ReadableAccent;
    public Color success => LightMode ? Hex("#17643A") : baseSuccess;
    public Color danger => LightMode ? Hex("#AD2339") : baseDanger;
    public Color starEmpty => LightMode ? Hex("#8A91A0") : baseStarEmpty;
    public Color outline => LightMode ? Hex("#535967") : HighContrast ? Color.white : Hex("#8E95A6");
    public Color boardFrame => Hex("#30333D"); // keep the board background consistent in both interface modes
    public Color accentText => BestText(accent);
    public Color ReadableAccent => EnsureContrast(accent, panel, HighContrast ? 7f : 4.5f);

    public static float Luminance(Color c)
    {
        float Linear(float v) => v <= .04045f ? v / 12.92f : Mathf.Pow((v + .055f) / 1.055f, 2.4f);
        return .2126f * Linear(c.r) + .7152f * Linear(c.g) + .0722f * Linear(c.b);
    }
    public static float ContrastRatio(Color a, Color b)
    {
        float x = Luminance(a), y = Luminance(b);
        return (Mathf.Max(x, y) + .05f) / (Mathf.Min(x, y) + .05f);
    }
    public static Color BestText(Color fill) => ContrastRatio(Color.black, fill) > ContrastRatio(Color.white, fill) ? Color.black : Color.white;
    public static Color EnsureContrast(Color color, Color surface, float minimum)
    {
        if (ContrastRatio(color, surface) >= minimum) return color;
        Color target = BestText(surface);
        float low = 0f, high = 1f;
        for (int i = 0; i < 16; i++)
        {
            float mid = (low + high) / 2f;
            if (ContrastRatio(Color.Lerp(color, target, mid), surface) >= minimum) high = mid; else low = mid;
        }
        return Color.Lerp(color, target, high);
    }
    public static void SetAppearance(bool light, bool highContrast)
    {
        lightMode = light;
        contrast = highContrast;
        Changed?.Invoke();
    }

    // The player's selected UI color.
    // Null means the default accent is used.
    static Color? chosenThemeColor;

    public Color ThemeAccent
    {
        get
        {
            var selected = Application.isPlaying && chosenThemeColor.HasValue ? chosenThemeColor.Value : defaultAccent;
            return HighContrast ? EnsureContrast(selected, panel, 4.5f) : selected;
        }
    }

    public Color Get(Role role) => role switch
    {
        Role.Background => background,
        Role.Panel => panel,
        Role.Button => button,
        Role.Text => text,
        Role.MutedText => mutedText,
        Role.Accent => accent,
        Role.Star => star,
        Role.StarEmpty => starEmpty,
        Role.Success => success,
        Role.Danger => danger,
        Role.Hint => hint,
        Role.Tutorial => tutorial,
        Role.PurpleStar => purpleStar,
        Role.HintAccent => hintAccent,
        Role.Hover => hover,
        Role.ButtonText => text,
        Role.AccentText => accentText,
        Role.Outline => outline,
        Role.BoardFrame => boardFrame,
        Role.HintText => BestText(hint),
        _ => dim,
    };

    static UIPalette current;
    public static UIPalette Current
    {
        get
        {
            if (current == null) current = Resources.Load<UIPalette>("UIPalette");
            if (current == null) current = CreateInstance<UIPalette>(); // no asset yet: the defaults
            return current;
        }
    }

    // a color was changed in the Inspector: everything using the palette updates
    // notifies UI components when the palette changes.
    public static event System.Action Changed;

    void OnValidate()
    {
        if (current == this || current == null)
            Changed?.Invoke();
    }

    static Color Hex(string hex) =>
        ColorUtility.TryParseHtmlString(hex, out var c)
            ? c
            : Color.magenta;

    // Changes the current runtime theme color.
    public static void SetThemeColor(Color color)
    {
        color.a = 1f;
        chosenThemeColor = color;
        Changed?.Invoke();
    }

    // Clears runtime theme state before a new Play session.
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetThemeColor()
    {
        chosenThemeColor = null;
        lightMode = contrast = false;
    }
}
