using UnityEngine;
using UnityEngine.UI;

// draws a solid triangle pointing up
public class TriangleGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();

        var vert = UIVertex.simpleVert;
        vert.color = color;

        vert.position = new Vector3(r.xMin, r.yMin); // bottom-left
        vh.AddVert(vert);
        vert.position = new Vector3(r.center.x, r.yMax); // tip, top-center
        vh.AddVert(vert);
        vert.position = new Vector3(r.xMax, r.yMin); // bottom-right
        vh.AddVert(vert);

        vh.AddTriangle(0, 1, 2);
    }
}
