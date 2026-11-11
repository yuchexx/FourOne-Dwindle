using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared drawing parts for the Rules demonstrations. They never receive gameplay input.
public static class RulesDemoUI
{
    // Match BoardView's object, destination and direction-choice proportions.
    public const float ObjectRatio = .55f;
    public const float RingRatio = .62f;
    public const float ChoiceRatio = .25f;
    public const float ChoiceOffset = .39f;
    public const float OutsideOffset = .446f;
    public static readonly Color Gold = new Color(.96f, .77f, .33f);
    public static readonly Color Red = new Color(1f, .29f, .32f);
    public static readonly Color Blue = new Color(.31f, .49f, 1f);
    public static readonly Color Path = new Color(.16f, .18f, .23f);
    public static readonly Color TileFill = new Color(.82f, .84f, .89f);
    public static readonly Color UsedFill = new Color(.45f, .47f, .53f);
    public static readonly Color Text = new Color(.96f, .97f, 1f);
    public static readonly Color Muted = new Color(.75f, .78f, .84f);
    public static readonly Color Panel = new Color(.235f, .245f, .30f);

    public static RectTransform Group(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Place(rect, position, size);
        return rect;
    }

    static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static Image Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var image = UIBuilder.NewImage(name, parent, color);
        Place(image.rectTransform, position, size);
        return image;
    }

    public static TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size,
        string text, float fontSize, TMP_FontAsset font, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var label = UIBuilder.NewText(name, parent, text, fontSize, Text, font);
        Place(label.rectTransform, position, size);
        label.alignment = alignment;
        return label;
    }

    public static RectTransform Tile(string name, Transform parent, Vector2 position, TileType type,
        float size = 76f, bool used = false)
    {
        var tile = Box(name, parent, position, Vector2.one * size, used ? UsedFill : TileFill);
        var view = tile.gameObject.AddComponent<TileView>();
        view.center = Box("Center", tile.transform, Vector2.zero, Vector2.one * size * .16f, Path);
        view.armN = Arm("North", tile.transform, new Vector2(.42f, .5f), new Vector2(.58f, 1f));
        view.armE = Arm("East", tile.transform, new Vector2(.5f, .42f), new Vector2(1f, .58f));
        view.armS = Arm("South", tile.transform, new Vector2(.42f, 0f), new Vector2(.58f, .5f));
        view.armW = Arm("West", tile.transform, new Vector2(0f, .42f), new Vector2(.5f, .58f));
        view.Show(type, Path);
        return tile.rectTransform;
    }

    static Image Arm(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var image = UIBuilder.NewImage(name, parent, Path);
        image.rectTransform.anchorMin = min;
        image.rectTransform.anchorMax = max;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        return image;
    }

    public static void SetUsed(RectTransform tile, bool used)
    {
        tile.GetComponent<Image>().color = used ? UsedFill : TileFill;
    }

    public static RectTransform Actor(string name, Transform parent, Vector2 position, Color color,
        float size = 28f, Dir facing = Dir.E)
    {
        var actor = Group(name, parent, position, Vector2.one * size);
        actor.gameObject.AddComponent<CanvasRenderer>();
        var body = actor.gameObject.AddComponent<CircleGraphic>();
        body.color = color;
        body.raycastTarget = false;
        var arrow = Group("Heading", actor, Vector2.zero, Vector2.one * size * .38f);
        arrow.gameObject.AddComponent<CanvasRenderer>();
        var triangle = arrow.gameObject.AddComponent<TriangleGraphic>();
        triangle.color = Color.white;
        triangle.raycastTarget = false;
        Face(actor, facing);
        return actor;
    }

    public static void Face(RectTransform actor, Dir direction)
    {
        actor.localEulerAngles = new Vector3(0f, 0f, DirUtil.Angle(direction));
    }

    public static CircleGraphic Ring(string name, Transform parent, Vector2 position, Color color, float size = 34f)
    {
        var rect = Group(name, parent, position, Vector2.one * size);
        rect.gameObject.AddComponent<CanvasRenderer>();
        var ring = rect.gameObject.AddComponent<CircleGraphic>();
        ring.color = color;
        ring.thickness = .22f;
        ring.raycastTarget = false;
        return ring;
    }

    public static void EndLine(string name, Transform parent, Vector2 edge, Vector2 endpoint,
        Color color, float cellSize, float endpointRatio)
    {
        Vector2 direction = (endpoint - edge).normalized;
        float length = Vector2.Distance(edge, endpoint) - cellSize * endpointRatio * .92f * .5f;
        float dot = cellSize * .05f;
        float gap = cellSize * .06f;
        if (length < dot) return;
        int count = Mathf.Max(1, Mathf.FloorToInt((length + gap) / (dot + gap)));
        float first = (length - (count * dot + (count - 1) * gap)) * .5f + dot * .5f;
        var line = Group(name, parent, Vector2.zero, Vector2.zero);
        color.a *= .75f;
        for (int i = 0; i < count; i++)
        {
            float offset = first + i * (dot + gap);
            Box("Dot", line, edge + direction * offset, Vector2.one * dot, color);
        }
    }

    public static RectTransform Outline(string name, Transform parent, Vector2 position, Vector2 size,
        Color color, float thickness = 3f)
    {
        var root = Group(name, parent, position, size);
        Box("Top", root, new Vector2(0, size.y / 2), new Vector2(size.x, thickness), color);
        Box("Bottom", root, new Vector2(0, -size.y / 2), new Vector2(size.x, thickness), color);
        Box("Left", root, new Vector2(-size.x / 2, 0), new Vector2(thickness, size.y), color);
        Box("Right", root, new Vector2(size.x / 2, 0), new Vector2(thickness, size.y), color);
        return root;
    }

    public static RectTransform Cursor(string name, Transform parent, Vector2 position)
    {
        var root = Group(name, parent, position, new Vector2(34, 44));
        var arrow = Group("Pointer", root, new Vector2(5, -10), new Vector2(19, 29));
        arrow.gameObject.AddComponent<CanvasRenderer>();
        arrow.localEulerAngles = new Vector3(0, 0, 28);
        var graphic = arrow.gameObject.AddComponent<TriangleGraphic>();
        graphic.color = Color.white;
        graphic.raycastTarget = false;
        var press = Ring("Press", root, Vector2.zero, Gold, 38);
        press.gameObject.SetActive(false);
        var right = Box("RightButton", root, new Vector2(12, -18), new Vector2(6, 13), Gold);
        right.gameObject.SetActive(false);
        return root;
    }

    public static void Press(RectTransform cursor, bool pressed, bool right = false)
    {
        cursor.Find("Press").gameObject.SetActive(pressed);
        cursor.Find("RightButton").gameObject.SetActive(pressed && right);
    }

    public static RectTransform Lock(string name, Transform parent, Vector2 position, float size = 24f)
    {
        var root = Group(name, parent, position, Vector2.one * size);
        Ring("Shackle", root, new Vector2(0, size * .18f), Gold, size * .65f);
        Box("Body", root, new Vector2(0, -size * .12f), new Vector2(size * .7f, size * .5f), Gold);
        return root;
    }

}
