using TMPro;
using UnityEngine;
using UnityEngine.UI;

// small UI parts built in code: images, texts, rows and badges. none of them take the mouse
public static class UIBuilder
{
    public static void Clear(Transform root)
    {
        foreach (Transform child in root) Object.Destroy(child.gameObject);
    }

    public static Image NewImage(string name, Transform parent, Color color)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // one line, never wrapped. font null: TextMeshPro's default
    public static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TMP_FontAsset font = null)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.rectTransform.SetParent(parent, false);
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    // left to right, each part at its own size, centered up and down
    public static RectTransform NewRow(string name, Transform parent, float spacing)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(parent, false);
        var h = go.GetComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        return (RectTransform)go.transform;
    }

    // a square of this size in a row
    public static void FixedSize(GameObject go, float size)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = size;
        le.minHeight = le.preferredHeight = size;
    }

    public static void Fill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // a badge's white bold number (or #), filling it
    public static TextMeshProUGUI BadgeLabel(RectTransform badge, string text, float size, TMP_FontAsset font = null)
    {
        var t = NewText("Number", badge, text, size, Color.white, font);
        Fill(t.rectTransform);
        t.rectTransform.localEulerAngles = Vector3.zero;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        return t;
    }

    // the board's circle: an object with only its body showing
    public static ObjectView Circle(ObjectView prefab, Transform parent, Color color, float size)
    {
        var circle = Object.Instantiate(prefab, parent);
        circle.Setup(color, size);
        foreach (var g in circle.GetComponentsInChildren<Graphic>(true))
            if (g != circle.body) g.gameObject.SetActive(false);
        circle.body.raycastTarget = false;
        return circle;
    }
}
