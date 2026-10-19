using UnityEngine;
using UnityEngine.UI;

// for Tile prefab - placing a tile by turning its four arms on / off
public class TileView : MonoBehaviour
{
    public Image center;
    public Image armN, armE, armS, armW;

    const float BridgeGap = 0.04f; // the gap between the two roads, as a share of the tile

    // the arms' layout in the prefab, so a tile can go back to it
    (Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)[] prefabLayout;
    bool isBridge;

    Image[] Arms => new[] { armN, armE, armS, armW };

    public void Show(TileType type, Color color)
    {
        center.color = color;
        SetArm(armN, Tiles.Has(type, Dir.N), color);
        SetArm(armE, Tiles.Has(type, Dir.E), color);
        SetArm(armS, Tiles.Has(type, Dir.S), color);
        SetArm(armW, Tiles.Has(type, Dir.W), color);

        var arms = Arms;
        if (prefabLayout == null)
        {
            prefabLayout = new (Vector2, Vector2, Vector2, Vector2)[arms.Length];
            for (int i = 0; i < arms.Length; i++)
            {
                var rt = arms[i].rectTransform;
                prefabLayout[i] = (rt.anchorMin, rt.anchorMax, rt.offsetMin, rt.offsetMax);
            }
        }
        for (int i = 0; i < arms.Length; i++)
        {
            var rt = arms[i].rectTransform;
            (rt.anchorMin, rt.anchorMax, rt.offsetMin, rt.offsetMax) = prefabLayout[i];
        }

        isBridge = type == TileType.Bridge;
        center.enabled = !isBridge; // the horizontal road covers the middle
        if (isBridge) LayOutBridge();
    }

    // bridge tile
    void LayOutBridge()
    {
        float height = ((RectTransform)transform).rect.height;
        if (height <= 0) return; // TODO
        var canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        float gap = Mathf.Max(2f, Mathf.Round(height * scale * BridgeGap)) / scale;

        float bandTop = armE.rectTransform.anchorMax.y, bandBottom = armE.rectTransform.anchorMin.y;
        SetY(armN, bandTop, 1f, gap, 0f);
        SetY(armS, 0f, bandBottom, 0f, -gap);
    }

    void OnRectTransformDimensionsChange()
    {
        if (isBridge && prefabLayout != null) LayOutBridge();
    }

    // changes only the vertical layout, keeping the prefab's horizontal one
    static void SetY(Image arm, float anchorMinY, float anchorMaxY, float offsetMinY, float offsetMaxY)
    {
        var rt = arm.rectTransform;
        rt.anchorMin = new Vector2(rt.anchorMin.x, anchorMinY);
        rt.anchorMax = new Vector2(rt.anchorMax.x, anchorMaxY);
        rt.offsetMin = new Vector2(rt.offsetMin.x, offsetMinY);
        rt.offsetMax = new Vector2(rt.offsetMax.x, offsetMaxY);
    }

    static void SetArm(Image arm, bool open, Color color)
    {
        arm.color = color;
        arm.enabled = open;
    }
}
