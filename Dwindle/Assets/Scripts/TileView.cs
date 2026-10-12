using UnityEngine;
using UnityEngine.UI;

// for Tile prefab. placing a tile by turning its four arms on / off.
public class TileView : MonoBehaviour
{
    public Image center;
    public Image armN, armE, armS, armW;

    public void Show(TileType type, Color color)
    {
        center.color = color;
        SetArm(armN, Tiles.Has(type, Dir.N), color);
        SetArm(armE, Tiles.Has(type, Dir.E), color);
        SetArm(armS, Tiles.Has(type, Dir.S), color);
        SetArm(armW, Tiles.Has(type, Dir.W), color);
    }

    static void SetArm(Image arm, bool open, Color color)
    {
        arm.color = color;
        arm.enabled = open;
    }
}
