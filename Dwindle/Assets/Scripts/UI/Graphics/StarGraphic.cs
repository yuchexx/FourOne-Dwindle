using UnityEngine;
using UnityEngine.UI;

// draws a five-pointed star in its rect, point up, with no sprite, a soft edge so it doesn't look jagged,
// and an optional outline in another color
public class StarGraphic : MaskableGraphic
{
    [Range(2, 12)] public int points = 5;
    [Range(0.1f, 0.9f)] public float innerRatio = 0.45f; // the inner corners' distance from the middle, as a fraction of the points'
    [Range(0.1f, 1f)] public float scale = 1f;           // the star's size as a fraction of the rect
    [Range(0f, 4f)] public float softness = 1.2f;        // the edge's fade, in screen pixels
    [Min(0f)] public float outlineWidth = 4f;            // in this rect's units; used when the outline color isn't clear
    [SerializeField] Color outline = Color.clear;

    // the outline's color; clear = no outline
    public Color outlineColor
    {
        get => outline;
        set { if (outline == value) return; outline = value; SetVerticesDirty(); }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float outer = Mathf.Min(r.width, r.height) / 2f * scale, inner = outer * innerRatio;
        float f = softness / (canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f);
        // a five-pointed star is a little shorter than wide, so it sits slightly low to look centered
        Vector2 c = r.center - new Vector2(0, outer * 0.06f);

        int n = points * 2;
        var shape = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.PI / 2f + i * Mathf.PI / points; // starts at the top point
            shape[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (i % 2 == 0 ? outer : inner);
        }

        // with an outline: the whole star in the outline color, then the fill on top, set in by the outline's width
        bool outlined = outline.a > 0f && outlineWidth > 0f;
        if (outlined) AddPolygon(vh, shape, c, outline, f);
        AddPolygon(vh, outlined ? Inset(shape, outlineWidth) : shape, c, color, f);
    }

    // a filled polygon fanned from the middle, with a fringe outside its edge that fades to clear
    static void AddPolygon(VertexHelper vh, Vector2[] shape, Vector2 middle, Color color, float fade)
    {
        int n = shape.Length, start = vh.currentVertCount;
        var vert = UIVertex.simpleVert;
        var solid = (Color32)color; var clear = solid; clear.a = 0;
        vert.color = solid; vert.position = middle; vh.AddVert(vert);
        var push = Inset(shape, -fade); // the same outline moved out by the fade
        for (int i = 0; i < n; i++)
        {
            vert.color = solid; vert.position = shape[i]; vh.AddVert(vert); // start + 1 + 2i
            vert.color = clear; vert.position = push[i]; vh.AddVert(vert);  // start + 2 + 2i
        }
        for (int i = 0; i < n; i++)
        {
            int a = start + 1 + 2 * i, b = start + 1 + 2 * ((i + 1) % n);
            vh.AddTriangle(start, a, b);
            vh.AddTriangle(a, a + 1, b + 1);
            vh.AddTriangle(a, b + 1, b);
        }
    }

    // the outline moved inward by `by` (outward if negative), keeping each edge parallel (a mitered offset)
    static Vector2[] Inset(Vector2[] shape, float by)
    {
        int n = shape.Length;
        var res = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 prev = shape[(i + n - 1) % n], p = shape[i], next = shape[(i + 1) % n];
            // outward normals of the two edges at this corner (the outline runs counterclockwise)
            Vector2 n1 = new Vector2(p.y - prev.y, prev.x - p.x).normalized, n2 = new Vector2(next.y - p.y, p.x - next.x).normalized;
            res[i] = p - (n1 + n2) / Mathf.Max(0.05f, 1f + Vector2.Dot(n1, n2)) * by;
        }
        return res;
    }
}
