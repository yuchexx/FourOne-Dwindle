using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// a small box by the mouse saying what a stage 2 hint mark means, the objects drawn as their icons (BoardView fills it).
// its Image, Horizontal Layout Group and Content Size Fitter are set in the Inspector; pivot top-left
public class HintTooltip : MonoBehaviour
{
    [Tooltip("Optional: the text's font (empty: TextMeshPro's default)")]
    public TMP_FontAsset font;
    public float fontSize = 18f;
    public float iconSize = 24f;
    public Vector2 offset = new Vector2(18f, -18f); // from the mouse, in screen pixels

    object shown; // the parts it shows, so hovering the same mark doesn't rebuild it

    // parts: a text, or (text null) an object's color, drawn as its icon
    public void Show(List<(string text, Color color)> parts, ObjectView objectPrefab, Vector2 screenPos)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!ReferenceEquals(parts, shown))
        {
            shown = parts;
            UIBuilder.Clear(transform);
            foreach (var (text, color) in parts)
                if (text != null) UIBuilder.NewText("Text", transform, text, fontSize, UIPalette.Current.text, font);
                else Icon(color, objectPrefab);
        }
        Place(screenPos);
    }

    public void Hide()
    {
        shown = null;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    void Icon(Color color, ObjectView prefab)
    {
        var holder = new GameObject("Icon", typeof(RectTransform));
        holder.transform.SetParent(transform, false);
        UIBuilder.FixedSize(holder, iconSize);
        var icon = Instantiate(prefab, holder.transform);
        icon.Setup(color, UIBuilder.FitIcon(icon.transform, prefab.transform, iconSize));
        icon.Face(Dir.E);
        foreach (var g in icon.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
    }

    // below-right of the mouse; on its other side near the screen's right or bottom edge
    void Place(Vector2 screenPos)
    {
        var rt = (RectTransform)transform;
        var canvas = GetComponentInParent<Canvas>().rootCanvas;
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 size = rt.rect.size * canvas.scaleFactor;
        float x = screenPos.x + offset.x + size.x > Screen.width ? screenPos.x - offset.x - size.x : screenPos.x + offset.x;
        float y = screenPos.y + offset.y - size.y < 0f ? screenPos.y - offset.y + size.y : screenPos.y + offset.y;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rt.parent, new Vector2(x, y), cam, out var local);
        rt.localPosition = local;
    }
}
