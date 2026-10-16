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
    TileType type;
    Color tileColor;
    Image iconBack;      // the light square behind the tile (the Icon object's image)
    Color iconBackColor;

    public void SetTile(TileView tilePrefab, TileType type, Color color)
    {
        if (icon == null) icon = Instantiate(tilePrefab, iconRoot);
        this.type = type;
        tileColor = color;
        icon.Show(type, color);
    }

    // darkens the icon square and the tile, and stops clicks, for a tile type you have none of
    public void SetAvailable(bool available, float darken)
    {
        if (iconBack == null)
        {
            iconBack = iconRoot.GetComponent<Image>();
            if (iconBack != null) iconBackColor = iconBack.color;
        }
        if (iconBack != null)
            iconBack.color = available ? iconBackColor : Color.Lerp(iconBackColor, Color.black, darken);
        if (icon != null)
            icon.Show(type, available ? tileColor : Color.Lerp(tileColor, Color.black, darken));
        button.interactable = available;
    }
}
