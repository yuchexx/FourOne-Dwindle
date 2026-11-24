using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

// keep rules text readable on its surface while preserving the original rich text
[RequireComponent(typeof(TMP_Text))]
public sealed class ReadableRulesText : MonoBehaviour
{
    public bool fixedDarkSurface;
    static readonly Regex ColorTag = new(@"<color=([#a-zA-Z0-9]+)>", RegexOptions.IgnoreCase);
    TMP_Text label;
    string source, appliedText;
    Color requested, appliedColor;
    bool ready, wasLight, wasContrast;

    void Awake() { label = GetComponent<TMP_Text>(); requested = label.color; source = label.text; }
    void OnEnable() => ready = false;
    void LateUpdate()
    {
        bool light = UIPalette.LightMode, contrast = UIPalette.HighContrast;
        if (label.text != appliedText) source = label.text;
        if (label.color != appliedColor) requested = label.color;
        if (ready && label.text == appliedText && label.color == appliedColor && light == wasLight && contrast == wasContrast) return;
        var surface = fixedDarkSurface ? RulesDemoUI.Panel : UIPalette.Current.panel;
        float minimum = contrast ? 7f : 4.5f;
        bool neutral = Mathf.Max(requested.r, Mathf.Max(requested.g, requested.b)) - Mathf.Min(requested.r, Mathf.Min(requested.g, requested.b)) < .12f;
        var ink = neutral ? (fixedDarkSurface ? Color.white : UIPalette.Current.text) : requested;
        appliedColor = UIPalette.EnsureContrast(ink, surface, minimum);
        appliedColor.a = requested.a;
        appliedText = ColorTag.Replace(source ?? "", match =>
        {
            if (!ColorUtility.TryParseHtmlString(match.Groups[1].Value, out var original)) return match.Value;
            return "<color=#" + ColorUtility.ToHtmlStringRGB(UIPalette.EnsureContrast(original, surface, minimum)) + ">";
        });
        label.color = appliedColor;
        label.text = appliedText;
        wasLight = light; wasContrast = contrast; ready = true;
    }
}
