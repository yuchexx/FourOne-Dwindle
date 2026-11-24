using UnityEngine;
using UnityEngine.UI;

// gives this Image or text its color from the UI palette (UIPalette), by role. change the palette and it follows,
// in the editor too. on a button's background, the Button's tint (Normal, Highlighted...) still shades it
[ExecuteAlways]
[RequireComponent(typeof(Graphic))]
public class PaletteColor : MonoBehaviour
{
    public UIPalette.Role role = UIPalette.Role.Panel;
    [Tooltip("1 = the palette's own alpha")]
    [Range(0f, 1f)] public float alpha = 1f;

    void OnEnable()
    {
        UIPalette.Changed += Apply;
        Apply();
    }

    void OnDisable() => UIPalette.Changed -= Apply;

    void OnValidate() => Apply();

    public void Apply()
    {
        var graphic = GetComponent<Graphic>();
        if (graphic == null) return;
        var c = UIPalette.Current.Get(role);
        if (role == UIPalette.Role.Accent && graphic is TMPro.TMP_Text)
            c = UIPalette.Current.ReadableAccent;
        if (graphic is Image && (role == UIPalette.Role.Button || role == UIPalette.Role.Panel))
        {
            var border = GetComponent<Outline>();
            if (UIPalette.HighContrast && border == null) border = gameObject.AddComponent<Outline>();
            if (border != null) { border.enabled = UIPalette.HighContrast; border.effectColor = UIPalette.Current.outline; border.effectDistance = Vector2.one * 2f; }
        }
        c.a *= alpha;
        graphic.color = c;
    }
}
