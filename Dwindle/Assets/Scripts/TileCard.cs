using TMPro;
using UnityEngine;
using UnityEngine.UI;

// a button with a tile icon and a label for the supply slots
// the removed-tiles list and the recovery choices
public class TileCard : MonoBehaviour
{
    public Image background;
    public Button button;
    public RectTransform iconRoot;
    public TMP_Text label;

    TileView icon;

    public void SetTile(TileView tilePrefab, TileType type, Color color)
    {
        if (icon == null) icon = Instantiate(tilePrefab, iconRoot);
        icon.Show(type, color);
    }
}
