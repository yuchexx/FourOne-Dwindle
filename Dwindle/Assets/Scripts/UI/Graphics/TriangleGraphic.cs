using UnityEngine;
using UnityEngine.UI;

// draws a solid triangle pointing up with a soft edge so it doesn't look jagged
public class TriangleGraphic : MaskableGraphic
{
    [Range(0f, 4f)] public float softness = 1.2f; // the edge's fade, in screen pixels

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float f = softness / (canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f); // screen pixels to this rect's units

        // bottom-left, the tip at top-center, bottom-right
        var corners = new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.center.x, r.yMax), new Vector2(r.xMax, r.yMin) };
        Vector2 middle = (corners[0] + corners[1] + corners[2]) / 3f;

        var vert = UIVertex.simpleVert;
        var solid = (Color32)color; var clear = solid; clear.a = 0;
        for (int i = 0; i < 3; i++)
        {
            // each corner pulled in by half the fade, plus a copy pushed out by half that fades to clear
            Vector2 away = (corners[i] - middle).normalized;
            vert.color = solid; vert.position = corners[i] - away * f / 2f; vh.AddVert(vert); // 2i
            vert.color = clear; vert.position = corners[i] + away * f / 2f; vh.AddVert(vert); // 2i + 1
        }
        vh.AddTriangle(0, 2, 4); // the solid middle
        for (int i = 0; i < 3; i++)
        {
            int a = 2 * i, b = 2 * ((i + 1) % 3);
            vh.AddTriangle(a, a + 1, b + 1);
            vh.AddTriangle(a, b + 1, b);
        }
    }
}
