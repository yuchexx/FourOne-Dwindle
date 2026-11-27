using UnityEngine;

// a tile view turning through every junction tile (+, the four T turns, the bridge), like the board's stage 1 ghost
// (the guide's example pictures)
public class GuideTileCycle : MonoBehaviour
{
    public TileView view;
    public Color color = Color.white;
    public float seconds = 1.5f;
    static readonly TileType[] Tiles = { TileType.Cross, TileType.TeeNES, TileType.TeeESW, TileType.TeeSWN, TileType.TeeWNE, TileType.Bridge };
    int shown = -1;

    void Update()
    {
        if (view == null) return;
        int i = (int)(Time.unscaledTime / Mathf.Max(0.2f, seconds)) % Tiles.Length;
        if (i == shown) return;
        shown = i;
        view.Show(Tiles[i], color);
    }
}
