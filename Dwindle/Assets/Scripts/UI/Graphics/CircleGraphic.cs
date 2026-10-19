using UnityEngine;
using UnityEngine.UI;

// draws a solid circle (or a ring) in its rect, with no sprite, and a soft edge so it doesn't look jagged
public class CircleGraphic : MaskableGraphic
{
    [Range(0.1f, 1f)] public float scale = 0.92f; // the circle's width as a fraction of the rect (the old Knob sprite left a margin too)
    [Range(0f, 1f)] public float thickness = 1f;  // 1 = a solid circle; less = a ring this fraction of the radius wide
    [Range(16, 128)] public int segments = 72;
    [Range(0f, 4f)] public float softness = 1.2f; // the edge's fade, in screen pixels

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        Vector2 c = r.center;
        float outer = Mathf.Min(r.width, r.height) / 2f * scale, inner = outer * (1f - thickness);
        float f = softness / (canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f); // screen pixels to this rect's units

        // rings of vertices from the middle out, each with its opacity; the bands between them make the shape
        var radii = new System.Collections.Generic.List<(float r, float a)>();
        if (thickness >= 1f) radii.Add((0f, 1f));
        else { radii.Add((Mathf.Max(0f, inner - f / 2f), 0f)); radii.Add((inner + f / 2f, 1f)); }
        radii.Add((Mathf.Max(0f, outer - f / 2f), 1f));
        radii.Add((outer + f / 2f, 0f));

        var vert = UIVertex.simpleVert;
        int rings = radii.Count;
        for (int i = 0; i <= segments; i++)
        {
            float ang = i * Mathf.PI * 2f / segments;
            var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            foreach (var (rad, a) in radii)
            {
                var col = (Color32)color; col.a = (byte)(col.a * a);
                vert.color = col;
                vert.position = c + d * rad;
                vh.AddVert(vert);
            }
            if (i == 0) continue;
            int now = i * rings, before = now - rings;
            for (int k = 0; k + 1 < rings; k++)
            {
                vh.AddTriangle(before + k, now + k, now + k + 1);
                vh.AddTriangle(before + k, now + k + 1, before + k + 1);
            }
        }
    }

    // only the circle itself takes clicks, not the corners of its rect
    public override bool Raycast(Vector2 sp, Camera eventCamera)
    {
        if (!base.Raycast(sp, eventCamera)) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, sp, eventCamera, out var p)) return false;
        Rect r = GetPixelAdjustedRect();
        return (p - r.center).magnitude <= Mathf.Min(r.width, r.height) / 2f * scale;
    }
}
