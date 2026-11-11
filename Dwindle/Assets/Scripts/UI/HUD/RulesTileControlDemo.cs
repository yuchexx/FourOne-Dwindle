using TMPro;
using UnityEngine;

// A single board and supply demonstrate the four states without changing the running level.
public sealed class RulesTileControlDemo : MonoBehaviour
{
    public TMP_FontAsset font;

    const float Duration = 22f;
    const float CellSize = 90f;
    static readonly Vector2 StartPoint = new(-45f - CellSize * RulesDemoUI.OutsideOffset, 130);
    static readonly Vector2 GoalPoint = new(225f + CellSize * RulesDemoUI.OutsideOffset, 130);
    static readonly Vector2 SupplyPoint = new(-330, 130);
    static readonly Vector2 PlacedPoint = new(90, 40);
    static readonly Vector2 MovedPoint = new(90, 130);
    static readonly Vector2 ReturnPoint = new(180, 40);

    RectTransform tile;
    RectTransform supplyTile;
    RectTransform returnedTile;
    RectTransform firstPathTile;
    RectTransform lastPathTile;
    RectTransform cursor;
    RectTransform actor;
    CircleGraphic matchingRing;
    RectTransform editableOutline;
    RectTransform lockedOutline;
    RectTransform lockMark;
    TMP_Text phaseLabel;
    TMP_Text caption;
    TMP_Text stateLabel;
    float startedAt;

    void Awake()
    {
        var root = RulesDemoUI.Group("TileControlDemonstration", transform, Vector2.zero, new Vector2(950, 560));
        RulesDemoUI.Label("PlacementSettings", root, new Vector2(0, 268), new Vector2(950, 32),
            "Choose <b>Click</b>, <b>Drag</b>, or <b>Both</b> in <b>Settings</b>.", 22, font);
        phaseLabel = RulesDemoUI.Label("CurrentPhase", root, new Vector2(0, 226), new Vector2(900, 38),
            "<noparse><Place></noparse>", 27, font);
        phaseLabel.color = RulesDemoUI.LightGreen;
        phaseLabel.fontStyle = FontStyles.Bold;
        caption = RulesDemoUI.Label("PhaseExplanation", root, new Vector2(0, -214), new Vector2(940, 60),
            "<color=#F5C454><b>Drag</b></color> a tile from the supply to place it.", 22, font);

        var scene = RulesDemoUI.Group("PlacementScene", root, new Vector2(70, -114), new Vector2(950, 360));
        scene.localScale = Vector3.one * 1.15f;
        RulesDemoUI.Box("SupplyPanel", scene, new Vector2(-330, 84), new Vector2(170, 248), RulesDemoUI.Panel);
        RulesDemoUI.Label("SupplyHeading", scene, new Vector2(-330, 225), new Vector2(180, 32), "SUPPLY", 22, font);
        RulesDemoUI.Label("BoardHeading", scene, new Vector2(110, 225), new Vector2(360, 32), "BOARD", 22, font);

        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                RulesDemoUI.Box("EmptyCell", scene, new Vector2(column * 90, 40 + row * 90),
                    new Vector2(90, 90), new Color(.16f, .17f, .21f));
            }
        }

        RulesDemoUI.EndLine("Start line", scene, new Vector2(-45, 130), StartPoint,
            RulesDemoUI.Red, CellSize, RulesDemoUI.ObjectRatio);
        RulesDemoUI.EndLine("Goal line", scene, new Vector2(225, 130), GoalPoint,
            RulesDemoUI.Red, CellSize, RulesDemoUI.RingRatio);
        supplyTile = RulesDemoUI.Tile("SupplyStraightTile", scene, SupplyPoint, TileType.StraightH, 88);
        RulesDemoUI.Tile("SupplyCornerTile", scene, new Vector2(-330, 40), TileType.CornerNE, 88);
        firstPathTile = RulesDemoUI.Tile("FirstPathTile", scene, new Vector2(0, 130), TileType.StraightH, 88);
        lastPathTile = RulesDemoUI.Tile("LastPathTile", scene, new Vector2(180, 130), TileType.StraightH, 88);
        RulesDemoUI.Box("FirstConnection", scene, new Vector2(45, 130), new Vector2(2, 14), RulesDemoUI.Path);
        RulesDemoUI.Box("SecondConnection", scene, new Vector2(135, 130), new Vector2(2, 14), RulesDemoUI.Path);
        returnedTile = RulesDemoUI.Tile("UnusedReturnTile", scene, ReturnPoint, TileType.StraightH, 88);
        tile = RulesDemoUI.Tile("EditableTile", scene, SupplyPoint, TileType.StraightH, 88);
        matchingRing = RulesDemoUI.Ring("MatchingRing", scene, GoalPoint, RulesDemoUI.Red, CellSize * RulesDemoUI.RingRatio);

        editableOutline = RulesDemoUI.Outline("UnusedHighlight", scene, PlacedPoint,
            new Vector2(88, 88), RulesDemoUI.Gold, 3);
        lockedOutline = RulesDemoUI.Outline("LockedHighlight", scene, MovedPoint,
            new Vector2(88, 88), RulesDemoUI.Muted, 3);
        lockMark = RulesDemoUI.Lock("UsedTileLock", scene, MovedPoint + new Vector2(25, -24), 23);
        stateLabel = RulesDemoUI.Label("TileState", scene, new Vector2(110, -20), new Vector2(600, 30),
            "UNUSED - still editable", 20, font);

        actor = RulesDemoUI.Actor("MovingObject", scene, StartPoint, RulesDemoUI.Red, CellSize * RulesDemoUI.ObjectRatio);
        cursor = RulesDemoUI.Cursor("TileControlCursor", scene, SupplyPoint);
    }

    void OnEnable()
    {
        startedAt = Time.unscaledTime;
        if (tile != null) Render(0f);
    }

    void Update()
    {
        Render(Mathf.Repeat(Time.unscaledTime - startedAt, Duration));
    }

    void Render(float time)
    {
        bool returning = time >= 15f;
        actor.anchoredPosition = Travel(StartPoint, GoalPoint, time, 6.3f, 9.3f);
        actor.gameObject.SetActive(time < 9.7f);
        bool used = time >= 6.3f && actor.anchoredPosition.x >= MovedPoint.x;
        bool delivered = time >= 9.7f;
        matchingRing.thickness = delivered ? 1f : .22f;
        matchingRing.SetVerticesDirty();
        supplyTile.gameObject.SetActive(time < 1f || time >= 19f);
        tile.gameObject.SetActive(time >= 1f);
        returnedTile.gameObject.SetActive(time < 19f);
        returnedTile.anchoredPosition = Travel(ReturnPoint, SupplyPoint, time, 18f, 19f);
        editableOutline.gameObject.SetActive(time >= 2.9f && !used || returning && time < 18f);
        lockedOutline.gameObject.SetActive(used);
        lockMark.gameObject.SetActive(delivered);
        RulesDemoUI.SetUsed(tile, used);
        RulesDemoUI.SetUsed(firstPathTile, time >= 6.3f && actor.anchoredPosition.x >= 0f);
        RulesDemoUI.SetUsed(lastPathTile, time >= 6.3f && actor.anchoredPosition.x >= 180f);
        stateLabel.text = returning ? "UNUSED - can be returned" : used ? "USED - locked in place" : "UNUSED - still editable";
        stateLabel.color = used && !returning ? RulesDemoUI.Muted : RulesDemoUI.LightGreen;

        tile.anchoredPosition = time < 4f
            ? Travel(SupplyPoint, PlacedPoint, time, 1f, 2.9f)
            : Travel(PlacedPoint, MovedPoint, time, 5.1f, 6.3f);
        editableOutline.anchoredPosition = returning ? returnedTile.anchoredPosition : tile.anchoredPosition;
        cursor.gameObject.SetActive(time < 6.3f || time >= 9.7f);

        if (time < 4f)
        {
            phaseLabel.text = "<noparse><Place></noparse>";
            caption.text = "<color=#F5C454><b>Drag</b></color> a tile from the supply to place it.";
            cursor.anchoredPosition = time < 1f
                ? Travel(SupplyPoint + new Vector2(-45, 35), SupplyPoint, time, 0f, .8f)
                : tile.anchoredPosition;
            RulesDemoUI.Press(cursor, time >= 1f && time < 2.9f);
        }
        else if (time < 9.7f)
        {
            phaseLabel.text = "<noparse><Move></noparse>";
            caption.text = time < 6.3f ? "<color=#F5C454><b>Unused</b></color> tiles can still be <color=#F5C454><b>moved</b></color>." : "The object follows the completed path.";
            cursor.anchoredPosition = tile.anchoredPosition;
            RulesDemoUI.Press(cursor, time >= 5.1f && time < 6.3f);
        }
        else if (time < 15f)
        {
            phaseLabel.text = "<noparse><Locked></noparse>";
            caption.text = "<color=#F5C454><b>Used tiles</b></color> can <color=#F5C454><b>no longer</b></color> be <color=#F5C454><b>moved</b></color> or <color=#F5C454><b>returned</b></color>.";
            cursor.anchoredPosition = time < 11f
                ? Travel(ReturnPoint, MovedPoint, time, 9.7f, 10.4f)
                : Travel(MovedPoint, PlacedPoint, time, 11f, 12.3f);
            RulesDemoUI.Press(cursor, time >= 11f && time < 12.3f);
        }
        else
        {
            phaseLabel.text = "<noparse><Take Back></noparse>";
            caption.text = "<color=#F5C454><b>Right-click</b></color> an <color=#F5C454><b>unused</b></color> tile to return it to the supply.";
            cursor.anchoredPosition = Travel(PlacedPoint, ReturnPoint, time, 15f, 16.2f);
            RulesDemoUI.Press(cursor, time >= 16.5f && time < 18f, true);
        }
    }

    static Vector2 Travel(Vector2 from, Vector2 to, float time, float start, float end)
    {
        float progress = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, time));
        return Vector2.LerpUnclamped(from, to, progress);
    }
}
