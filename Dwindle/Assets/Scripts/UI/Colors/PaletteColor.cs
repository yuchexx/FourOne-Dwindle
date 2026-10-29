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
        c.a *= alpha;
        graphic.color = c;
    }
}
