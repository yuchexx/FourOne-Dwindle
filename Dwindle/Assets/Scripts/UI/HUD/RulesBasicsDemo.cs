using TMPro;
using UnityEngine;

public class RulesBasicsDemo : MonoBehaviour
{
    public TMP_FontAsset font;
    const float CellSize = 90f;
    static readonly float Endpoint = 225f + CellSize * RulesDemoUI.OutsideOffset;
    static readonly float BlueStart = 45f + CellSize * RulesDemoUI.OutsideOffset;
    readonly RectTransform[] route = new RectTransform[5];
    RectTransform red, blue, missing, entry, cursor, gap;
    CircleGraphic redRing;
    TMP_Text stage;
    float startedAt;

    void Awake()
    {
        RulesDemoUI.Label("Tutorial", transform, new Vector2(0, 295), new Vector2(940, 32),
            "<b>New here? Try Level 1 for a guided tutorial.</b>", 23, font);
        RulesDemoUI.Label("DwindleRule", transform, new Vector2(0, 246), new Vector2(940, 32),
            "Each delivery removes a traveled tile.", 22, font);
        var board = RulesDemoUI.Group("DwindleBoard", transform, new Vector2(0, -13), new Vector2(760, 420));
        RulesDemoUI.Box("BoardBackground", board, Vector2.zero, new Vector2(760, 420), RulesDemoUI.Panel);
        RulesDemoUI.EndLine("Red start line", board, new Vector2(-225, 62), new Vector2(-Endpoint, 62),
            RulesDemoUI.Red, CellSize, RulesDemoUI.ObjectRatio);
        RulesDemoUI.EndLine("Red goal line", board, new Vector2(225, 62), new Vector2(Endpoint, 62),
            RulesDemoUI.Red, CellSize, RulesDemoUI.RingRatio);
        RulesDemoUI.EndLine("Blue start line", board, new Vector2(45, -118), new Vector2(BlueStart, -118),
            RulesDemoUI.Blue, CellSize, RulesDemoUI.ObjectRatio);
        RulesDemoUI.EndLine("Blue goal line", board, new Vector2(0, 107), new Vector2(0, 107 + CellSize * RulesDemoUI.OutsideOffset),
            RulesDemoUI.Blue, CellSize, RulesDemoUI.RingRatio);
        for (int i = 0; i < route.Length; i++)
        {
            var position = new Vector2(-180 + i * 90, 62);
            RulesDemoUI.Box("Cell" + i, board, position, Vector2.one * 90, RulesDemoUI.Path);
            route[i] = RulesDemoUI.Tile("RouteTile" + i, board, position,
                i == 2 ? TileType.Cross : TileType.StraightH, 88);
        }
        for (int i = 0; i < 4; i++)
            RulesDemoUI.Box("HorizontalConnection", board, new Vector2(-135 + i * 90, 62), new Vector2(2, 14), RulesDemoUI.Path);
        RulesDemoUI.Box("UpperVerticalConnection", board, new Vector2(0, 17), new Vector2(14, 2), RulesDemoUI.Path);
        RulesDemoUI.Box("LowerVerticalConnection", board, new Vector2(0, -73), new Vector2(14, 2), RulesDemoUI.Path);
        RulesDemoUI.Box("BlueEntryCell", board, new Vector2(0, -118), Vector2.one * 90, RulesDemoUI.Path);
        entry = RulesDemoUI.Tile("BlueEntry", board, new Vector2(0, -118), TileType.CornerNE, 88);
        RulesDemoUI.Tile("BlueWaitingPath", board, new Vector2(0, -28), TileType.StraightV, 88);
        missing = RulesDemoUI.Tile("TileToPlace", board, new Vector2(-180, -94), TileType.StraightH, 88);
        gap = RulesDemoUI.Outline("RemovedSharedTile", board, new Vector2(0, 62), Vector2.one * 88, RulesDemoUI.Red);
        redRing = RulesDemoUI.Ring("RedDestination", board, new Vector2(Endpoint, 62), RulesDemoUI.Red, CellSize * RulesDemoUI.RingRatio);
        RulesDemoUI.Ring("BlueDestination", board, new Vector2(0, 107 + CellSize * RulesDemoUI.OutsideOffset),
            RulesDemoUI.Blue, CellSize * RulesDemoUI.RingRatio);
        red = RulesDemoUI.Actor("RedObject", board, new Vector2(-Endpoint, 62), RulesDemoUI.Red, CellSize * RulesDemoUI.ObjectRatio);
        blue = RulesDemoUI.Actor("BlueObject", board, new Vector2(BlueStart, -118), RulesDemoUI.Blue, CellSize * RulesDemoUI.ObjectRatio, Dir.W);
        cursor = RulesDemoUI.Cursor("PlacementCursor", board, Vector2.zero);
        stage = RulesDemoUI.Label("AnimationStatus", transform, new Vector2(0, -274), new Vector2(940, 60),
            "Complete the path.", 21, font);
        stage.color = RulesDemoUI.Gold;
    }

    void OnEnable()
    {
        startedAt = Time.unscaledTime;
        if (red != null) Render(0f);
    }

    void Update()
    {
        Render(Mathf.Repeat(Time.unscaledTime - startedAt, 16f));
    }

    void Render(float t)
    {
        float drag = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.2f, 2.8f, t));
        float travel = Mathf.InverseLerp(3.2f, 6.3f, t);
        float redX = Mathf.Lerp(-Endpoint, Endpoint, travel);
        missing.anchoredPosition = Vector2.Lerp(new Vector2(-180, -94), new Vector2(-90, 62), drag);
        missing.gameObject.SetActive(t < 2.9f);
        cursor.gameObject.SetActive(t < 3f);
        cursor.anchoredPosition = missing.anchoredPosition + new Vector2(20, -20);
        RulesDemoUI.Press(cursor, t >= 1.2f && t < 2.8f);
        for (int i = 0; i < route.Length; i++)
        {
            route[i].gameObject.SetActive(i != 1 || t >= 2.9f);
            RulesDemoUI.SetUsed(route[i], t >= 3.2f && redX >= -180 + 90 * i);
        }
        route[2].gameObject.SetActive(t < 7.7f);
        red.anchoredPosition = new Vector2(redX, 62);
        red.gameObject.SetActive(t < 6.6f);
        redRing.thickness = t >= 6.6f ? 1f : .22f;
        redRing.SetVerticesDirty();
        gap.gameObject.SetActive(t >= 7.7f);
        entry.gameObject.SetActive(t >= 8.8f);
        blue.anchoredPosition = t < 10f
            ? Vector2.Lerp(new Vector2(BlueStart, -118), new Vector2(0, -118), Mathf.InverseLerp(9f, 10f, t))
            : Vector2.Lerp(new Vector2(0, -118), new Vector2(0, -28), Mathf.InverseLerp(10f, 11.2f, t));
        RulesDemoUI.Face(blue, t < 10f ? Dir.W : Dir.N);
        stage.text = t < 3.2f ? "Complete the path." :
            t < 6.6f ? "The object uses the path." :
            t < 7.7f ? "Delivered!" : t < 9f ? "One traveled tile is removed." :
            t < 12.5f ? "The remaining object now needs a new route." :
            "<b>Plan ahead for the paths you still need!</b>";
    }
}
