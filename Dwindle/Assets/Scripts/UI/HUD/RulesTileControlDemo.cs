using TMPro;
using UnityEngine;

// A single board and supply demonstrate the four states without changing the running level.
public sealed class RulesTileControlDemo : MonoBehaviour
{
    public TMP_FontAsset font;

    const float Duration = 21f;
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
    RectTransform editableOutline;
    RectTransform lockedOutline;
    RectTransform lockMark;
    TMP_Text phaseLabel;
    TMP_Text caption;
    TMP_Text stateLabel;
    RectTransform clickCursor;
    RectTransform clickTile;
    RectTransform clickSelection;
    RectTransform clickPlaced;
    float startedAt;

    void Awake()
    {
        var root = RulesDemoUI.Group("TileControlDemonstration", transform, Vector2.zero, new Vector2(950, 560));
        RulesDemoUI.Label("PlacementSettings", root, new Vector2(0, 242), new Vector2(950, 32),
            "Choose Click, Drag, or Both in Settings.", 22, font);
        phaseLabel = RulesDemoUI.Label("CurrentPhase", root, new Vector2(0, 195), new Vector2(900, 38),
            "PLACE", 27, font);
        phaseLabel.color = RulesDemoUI.Gold;
        caption = RulesDemoUI.Label("PhaseExplanation", root, new Vector2(0, 158), new Vector2(940, 34),
            "Drag a tile from the supply to place it.", 22, font);

        var scene = RulesDemoUI.Group("PlacementScene", root, new Vector2(0, -110), new Vector2(950, 360));
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
        RulesDemoUI.Ring("MatchingRing", scene, GoalPoint, RulesDemoUI.Red, CellSize * RulesDemoUI.RingRatio);

        editableOutline = RulesDemoUI.Outline("UnusedHighlight", scene, PlacedPoint,
            new Vector2(88, 88), RulesDemoUI.Gold, 3);
        lockedOutline = RulesDemoUI.Outline("LockedHighlight", scene, MovedPoint,
            new Vector2(88, 88), RulesDemoUI.Muted, 3);
        lockMark = RulesDemoUI.Lock("UsedTileLock", scene, MovedPoint + new Vector2(25, -24), 23);
        stateLabel = RulesDemoUI.Label("TileState", scene, new Vector2(110, -20), new Vector2(600, 30),
            "UNUSED - still editable", 20, font);

        BuildClickExample(root);
        actor = RulesDemoUI.Actor("MovingObject", scene, StartPoint, RulesDemoUI.Red, CellSize * RulesDemoUI.ObjectRatio);
        cursor = RulesDemoUI.Cursor("TileControlCursor", scene, SupplyPoint);
    }

    void BuildClickExample(RectTransform root)
    {
        var example = RulesDemoUI.Group("ClickPlacementExample", root, new Vector2(0, -218), new Vector2(600, 72));
        clickTile = RulesDemoUI.Tile("ClickSupplyTile", example, new Vector2(-125, 0), TileType.StraightH, 42);
        RulesDemoUI.Box("ClickEmptyCell", example, new Vector2(125, 0), new Vector2(46, 46), new Color(.16f, .17f, .21f));
        clickPlaced = RulesDemoUI.Tile("ClickPlacedTile", example, new Vector2(125, 0), TileType.StraightH, 42);
        clickSelection = RulesDemoUI.Outline("ClickSelectedTile", example, new Vector2(-125, 0),
            new Vector2(46, 46), RulesDemoUI.Gold, 2);
        RulesDemoUI.Label("ClickSequence", example, Vector2.zero, new Vector2(190, 30), "click  >  click", 18, font);
        clickCursor = RulesDemoUI.Cursor("ClickExampleCursor", example, new Vector2(-125, 0));
        clickCursor.localScale = Vector3.one * .65f;
        RulesDemoUI.Label("ClickExplanation", root, new Vector2(0, -278), new Vector2(950, 30),
            "Or click a tile, then click where to place it.", 20, font);
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
        bool used = time >= 14.2f;
        supplyTile.gameObject.SetActive(time < 1f || time >= 10.5f);
        tile.gameObject.SetActive(time >= 1f);
        returnedTile.gameObject.SetActive(time < 10.5f);
        returnedTile.anchoredPosition = Travel(ReturnPoint, SupplyPoint, time, 9.5f, 10.5f);
        editableOutline.gameObject.SetActive(time >= 2.9f && time < 8f);
        lockedOutline.gameObject.SetActive(used);
        lockMark.gameObject.SetActive(used);
        RulesDemoUI.SetUsed(tile, used);
        RulesDemoUI.SetUsed(firstPathTile, time >= 13.1f);
        RulesDemoUI.SetUsed(lastPathTile, time >= 15.2f);
        stateLabel.text = used ? "USED - locked in place" : "UNUSED - still editable";
        stateLabel.color = used ? RulesDemoUI.Muted : RulesDemoUI.Gold;

        tile.anchoredPosition = time < 4f
            ? Travel(SupplyPoint, PlacedPoint, time, 1f, 2.9f)
            : Travel(PlacedPoint, MovedPoint, time, 5.1f, 6.7f);
        editableOutline.anchoredPosition = tile.anchoredPosition;
        actor.gameObject.SetActive(time >= 12f && time < 16f);
        actor.anchoredPosition = Travel(StartPoint, GoalPoint, time, 12.5f, 15.7f);

        if (time < 4f)
        {
            phaseLabel.text = "PLACE";
            caption.text = "Drag a tile from the supply to place it.";
            cursor.anchoredPosition = time < 1f
                ? Travel(SupplyPoint + new Vector2(-45, 35), SupplyPoint, time, 0f, .8f)
                : tile.anchoredPosition;
            RulesDemoUI.Press(cursor, time >= 1f && time < 2.9f);
        }
        else if (time < 8f)
        {
            phaseLabel.text = "MOVE";
            caption.text = "Unused tiles can still be moved.";
            cursor.anchoredPosition = tile.anchoredPosition;
            RulesDemoUI.Press(cursor, time >= 5.1f && time < 6.7f);
        }
        else if (time < 12f)
        {
            phaseLabel.text = "TAKE BACK";
            caption.text = "Right-click an unused tile to return it to the supply.";
            cursor.anchoredPosition = Travel(MovedPoint, ReturnPoint, time, 8f, 8.8f);
            RulesDemoUI.Press(cursor, time >= 9f && time < 9.5f, true);
        }
        else
        {
            phaseLabel.text = "LOCKED";
            caption.text = "Used tiles can no longer be moved or returned.";
            cursor.anchoredPosition = time < 16.2f
                ? Travel(ReturnPoint, MovedPoint, time, 15.5f, 16.2f)
                : Travel(MovedPoint, PlacedPoint, time, 17f, 18.2f);
            RulesDemoUI.Press(cursor, time >= 17f && time < 18.2f);
        }

        RenderClickExample(time);
    }

    void RenderClickExample(float time)
    {
        float phase = Mathf.Repeat(time, 6f);
        clickCursor.anchoredPosition = Travel(new Vector2(-125, 0), new Vector2(125, 0), phase, 1.4f, 3f);
        clickSelection.gameObject.SetActive(phase >= .7f && phase < 3.5f);
        clickTile.gameObject.SetActive(phase < 3.5f);
        clickPlaced.gameObject.SetActive(phase >= 3.5f);
        RulesDemoUI.Press(clickCursor, (phase >= .7f && phase < 1.1f) || (phase >= 3.2f && phase < 3.5f));
    }

    static Vector2 Travel(Vector2 from, Vector2 to, float time, float start, float end)
    {
        float progress = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, time));
        return Vector2.LerpUnclamped(from, to, progress);
    }
}
