using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// lightens a button under the mouse
[RequireComponent(typeof(Button))]
public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("How much lighter under the mouse: 0 = no change, 1 = white (the supply tiles use 0.22)")]
    [Range(0f, 1f)] public float lighten = 0.22f;
    [Tooltip("Seconds to fade in and out (0 = at once)")]
    [Min(0f)] public float fadeSeconds = 0.08f;

    const string LayerName = "HoverLayer";
    Button button;
    Image layer;
    bool hovered;
    float shown; // the layer's alpha now

    void Awake()
    {
        button = GetComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = colors.normalColor;
        colors.selectedColor = colors.normalColor;
        button.colors = colors;

        // the layer sits on the background graphic, first in its children so the text draws over it.
        // a clone of a button that already had one (a level card) reuses it
        var background = button.targetGraphic != null ? button.targetGraphic.transform : transform;
        var existing = background.Find(LayerName);
        layer = existing != null ? existing.GetComponent<Image>() : new GameObject(LayerName, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var rt = layer.rectTransform;
        rt.SetParent(background, false);
        rt.SetAsFirstSibling();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        layer.raycastTarget = false;
        if (button.targetGraphic is Image shape) // same shape as the background (rounded corners and all)
        {
            layer.sprite = shape.sprite;
            layer.type = shape.type;
            layer.pixelsPerUnitMultiplier = shape.pixelsPerUnitMultiplier;
        }
        Show(0f);
    }

    void OnDisable()
    {
        hovered = false;
        Show(0f);
    }

    void Update()
    {
        float target = hovered && button.IsInteractable() ? lighten : 0f;
        if (Mathf.Approximately(shown, target)) return;
        Show(fadeSeconds <= 0f ? target : Mathf.MoveTowards(shown, target, Mathf.Max(lighten, 0.01f) * Time.unscaledDeltaTime / fadeSeconds));
    }

    void Show(float alpha)
    {
        shown = alpha;
        if (layer != null) layer.color = new Color(1f, 1f, 1f, alpha);
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;
}
