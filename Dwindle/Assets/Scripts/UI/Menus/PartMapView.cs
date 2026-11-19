using System;
using System.Collections.Generic;
using UnityEngine;

// the Levels page's first screen: one tile per part, in rows; LevelSelectView fills it
public class PartMapView : MonoBehaviour
{
    public PartTile tileTemplate;
    [Min(1)] public int tilesPerRow = 3;
    public float tileSpacing = 24f;
    public float rowSpacing = 24f;
    [Tooltip("{got}: stars earned, {max}: stars in the part")]
    public string starsText = "{got}/{max} stars";
    public string lockedText = "Locked";

    readonly List<PartTile> tiles = new List<PartTile>();

    // one tile per part name; onPick gets the part's index
    public virtual void Build(IList<string> parts, Action<int> onPick)
    {
        foreach (var t in tiles) Destroy(t.gameObject);
        tiles.Clear();
        tileTemplate.gameObject.SetActive(false);
        var rt = (RectTransform)tileTemplate.transform;
        Vector2 size = rt.sizeDelta, first = rt.anchoredPosition;
        int rows = (parts.Count + tilesPerRow - 1) / tilesPerRow;
        for (int i = 0; i < parts.Count; i++)
        {
            var tile = Instantiate(tileTemplate, tileTemplate.transform.parent);
            tile.name = $"Part {parts[i]}";
            tile.gameObject.SetActive(true);
            tile.title.text = parts[i];
            tile.button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            int index = i;
            tile.button.onClick.AddListener(() => onPick(index));
            int r = i / tilesPerRow, inRow = Mathf.Min(tilesPerRow, parts.Count - r * tilesPerRow);
            ((RectTransform)tile.transform).anchoredPosition = new Vector2(
                first.x + (i % tilesPerRow - (inRow - 1) / 2f) * (size.x + tileSpacing),
                first.y - r * (size.y + rowSpacing));
            tiles.Add(tile);
        }
    }

    // a part's stars and lock
    public virtual void Set(int i, int got, int max, bool open)
    {
        if (i < 0 || i >= tiles.Count) return;
        var t = tiles[i];
        t.button.interactable = open;
        if (t.stars != null) t.stars.text = open ? starsText.Replace("{got}", got.ToString()).Replace("{max}", max.ToString()) : lockedText;
        if (t.lockIcon != null) t.lockIcon.SetActive(!open);
    }

    // where the player is: a part, -1 before the first, parts.Count once all are done (the board map shows it)
    public virtual void SetCurrent(int part) { }

    // Reset progress: forget anything saved about where the player was
    public virtual void Forget() { }

    // new part names (the Part Text format changed in the Inspector)
    public virtual void Rename(IList<string> names) { }
}
