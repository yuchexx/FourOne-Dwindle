using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// an icon button (an icon, maybe its name under it, no background): under the mouse the icon and name take the hover
// color and grow a little; faded while the button can't be used
[RequireComponent(typeof(Button))]
public class IconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("The icon's Image. Empty: the first Image under the button")]
    public Graphic icon;
    [Tooltip("The name under the icon. Empty: the first text under the button (none for icon-only buttons)")]
    public Graphic label;
    [Tooltip("What grows under the mouse. Empty: the button itself. Set it when the button holds more than its icon and name (the Hints button's menu)")]
    public Transform zoom;

    [Header("Hover")]
    [Tooltip("On: the hover color is the palette's accent. Off: Hover Color")]
    public bool accentHover = true;
    public Color hoverColor = new Color32(0x7C, 0x83, 0xF7, 0xFF);
    [Tooltip("How much bigger under the mouse (1 = no change)")]
    [Range(1f, 1.5f)] public float hoverScale = 1.1f;
    [Tooltip("Seconds to grow and shrink (0 = at once)")]
    [Min(0f)] public float growSeconds = 0.08f;
    [Tooltip("How visible the icon and name are while the button can't be used (1 = no change)")]
    [Range(0f, 1f)] public float disabledAlpha = 0.35f;

    enum Look { Normal, Hover, Disabled }
    Button button;
    Look look = Look.Normal;
    Color iconColor, labelColor; // their own colors, kept while another look shows
    Vector3 baseScale;
    bool hovered, ready;

    void Awake()
    {
        button = GetComponent<Button>();
        button.transition = Selectable.Transition.None; // the icon and name show the hover, not a background
        if (icon == null) foreach (var g in GetComponentsInChildren<Image>(true)) if (g.gameObject != gameObject) { icon = g; break; }
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (zoom == null) zoom = transform;
        baseScale = zoom.localScale;
        ready = true;
    }

    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e) => hovered = false;

    void OnDisable()
    {
        hovered = false;
        if (!ready) return;
        SetLook(Look.Normal);
        zoom.localScale = baseScale;
    }

    void Update()
    {
        bool usable = button.IsInteractable();
        SetLook(!usable ? Look.Disabled : hovered ? Look.Hover : Look.Normal);
        var target = usable && hovered ? baseScale * hoverScale : baseScale;
        if (zoom.localScale == target) return;
        float step = growSeconds <= 0f ? float.MaxValue : (hoverScale - 1f) * baseScale.x * Time.unscaledDeltaTime / growSeconds;
        zoom.localScale = Vector3.MoveTowards(zoom.localScale, target, Mathf.Max(step, 0.0001f));
    }

    void SetLook(Look next)
    {
        if (next == look) return;
        if (look == Look.Normal) // leaving its own colors: keep them (a palette change may have set new ones)
        {
            if (icon != null) iconColor = icon.color;
            if (label != null) labelColor = label.color;
        }
        look = next;
        var hover = accentHover && UIPalette.Current != null ? UIPalette.Current.accent : hoverColor;
        if (icon != null) icon.color = Pick(iconColor, hover);
        if (label != null) label.color = Pick(labelColor, hover);
    }

    Color Pick(Color own, Color hover) => look switch
    {
        Look.Hover => new Color(hover.r, hover.g, hover.b, own.a),
        Look.Disabled => new Color(own.r, own.g, own.b, own.a * disabledAlpha),
        _ => own,
    };
}
