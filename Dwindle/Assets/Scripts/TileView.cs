using UnityEngine;
using UnityEngine.UI;

public class TileView : MonoBehaviour
{
    public Image center;
    public Image armN, armE, armS, armW;

    const float BridgeHalfWidth = 0.14f; // the horizontal road spans 0.33-0.67 of the tile
    const float BridgeGap = 0.04f;       // the gap between the two roads

    (Vector2 min, Vector2 max)[] prefabAnchors;

    public void Show(TileType type, Color color)
    {
        center.color = color;
        SetArm(armN, Tiles.Has(type, Dir.N), color);
        SetArm(armE, Tiles.Has(type, Dir.E), color);
        SetArm(armS, Tiles.Has(type, Dir.S), color);
        SetArm(armW, Tiles.Has(type, Dir.W), color);

        var arms = new[] { armN, armE, armS, armW };
        if (prefabAnchors == null)
        {
            prefabAnchors = new (Vector2, Vector2)[arms.Length];
            for (int i = 0; i < arms.Length; i++)
                prefabAnchors[i] = (arms[i].rectTransform.anchorMin, arms[i].rectTransform.anchorMax);
        }
        for (int i = 0; i < arms.Length; i++)
        {
            arms[i].rectTransform.anchorMin = prefabAnchors[i].min;
            arms[i].rectTransform.anchorMax = prefabAnchors[i].max;
        }

        if (type == TileType.Bridge)
        {
            float low = 0.5f - BridgeHalfWidth, high = 0.5f + BridgeHalfWidth;
            SetY(armE, low, high);
            SetY(armW, low, high);
            SetY(armN, high + BridgeGap, 1f);
            SetY(armS, 0f, low - BridgeGap);
        }
    }

    static void SetY(Image arm, float yMin, float yMax)
    {
        var rt = arm.rectTransform;
        rt.anchorMin = new Vector2(rt.anchorMin.x, yMin);
        rt.anchorMax = new Vector2(rt.anchorMax.x, yMax);
    }

    static void SetArm(Image arm, bool open, Color color)
    {
        arm.color = color;
        arm.enabled = open;
    }
}
