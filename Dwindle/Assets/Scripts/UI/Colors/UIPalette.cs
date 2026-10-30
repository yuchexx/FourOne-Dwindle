using UnityEngine;

// the game's UI colors, in one place. make the asset with Create > Dwindle > UI Palette and keep it at
// Assets/Resources/UIPalette (a folder named Resources, a file named UIPalette); until then the defaults below are used.
// a PaletteColor on a UI object keeps that object's color in step with this, and scripts that color things themselves
// (level cards, stars, switches) read it too. the objects' own colors (Red, Blue...) are part of the levels, not this
[CreateAssetMenu(menuName = "Dwindle/UI Palette", fileName = "UIPalette")]
public class UIPalette : ScriptableObject
{
    public enum Role { Background, Panel, Button, Text, MutedText, Accent, Star, StarEmpty, Success, Danger, Dim, Hint }

    [Header("Surfaces")]
    [Tooltip("Behind everything: the title screen, the Levels page")]
    public Color background = Hex("#202227");
    [Tooltip("Pop-ups and boxes on top of the background")]
    public Color panel = Hex("#30333D");
    public Color button = Hex("#52545E");
    [Tooltip("The dimmed backdrop behind a pop-up")]
    public Color dim = new Color(0f, 0f, 0f, 0.6f);

    [Header("Text")]
    public Color text = Hex("#FFFFFF");
    [Tooltip("Subtitles, hints, anything secondary")]
    public Color mutedText = Hex("#B3B3B3");

    [Header("Meaning")]
    [Tooltip("Hints and best plans: purple stars, tutorial levels, the chosen setting, switches that are on")]
    public Color accent = Hex("#7C83F7");
    public Color star = Hex("#FFD133");
    [Tooltip("A star still to earn")]
    public Color starEmpty = Hex("#1C1D22");
    [Tooltip("Good news: a delivery, an earned tile")]
    public Color success = Hex("#61C98B");
    [Tooltip("Bad news: a lost tile, a blocked object")]
    public Color danger = Hex("#FF7373");
    [Tooltip("The hints drawn on the board: numbers, frames, ghost tiles")]
    public Color hint = Hex("#403B8A");

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
    public static event System.Action Changed;
    void OnValidate() { if (current == this || current == null) Changed?.Invoke(); }

    static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
}
