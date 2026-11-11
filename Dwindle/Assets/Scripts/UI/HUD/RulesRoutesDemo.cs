using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RulesRoutesDemo : MonoBehaviour
{
    public enum RouteKind { Junction, Bridge, Doors }

    public TMP_FontAsset font;
    public RouteKind kind;

    const float CellSize = 92f;
    const float DoorCellSize = 82f;
    static readonly float Outside = CellSize * (1.5f + RulesDemoUI.OutsideOffset);
    static readonly float DoorOutside = 230f + DoorCellSize * (1.5f + RulesDemoUI.OutsideOffset);

    static readonly Color Green = new Color(.26f, .65f, .37f);
    static readonly Color ChoiceFill = new Color(1f, .88f, .53f);
    static readonly Color DoorFill = new Color(.61f, .63f, .68f);
    RectTransform actor;
    RectTransform secondActor;
    RectTransform cursor;
    RectTransform choices;
    RectTransform selection;
    Image junctionFill;
    CanvasGroup actorVisibility;
    CanvasGroup secondVisibility;
    float startedAt;

    void Awake()
    {
        var root = RulesDemoUI.Group("Route demonstration", transform,
            new Vector2(0, kind == RouteKind.Doors ? -45 : -25), new Vector2(900, 432));
        if (kind != RouteKind.Doors)
            RulesDemoUI.Box("Background", root, Vector2.zero,
                kind == RouteKind.Junction ? new Vector2(430, 420) : new Vector2(480, 432), RulesDemoUI.Panel);
        switch (kind)
        {
            case RouteKind.Junction:
                BuildJunction(root);
                break;
            case RouteKind.Bridge:
                BuildBridge(root);
                break;
            case RouteKind.Doors:
                BuildDoors(root);
                break;
        }
        string caption = kind == RouteKind.Junction ? "Choose a direction."
            : kind == RouteKind.Bridge ? "Cross straight through without connecting paths."
            : "Teleport through a matching door,<br>then choose a direction.";
        RulesDemoUI.Label("Route caption", transform, new Vector2(0, kind == RouteKind.Junction ? -270 : -278),
            new Vector2(930, kind == RouteKind.Doors ? 60 : 40), caption, kind == RouteKind.Junction ? 28 : 24, font);
        if (kind == RouteKind.Doors)
            RulesDemoUI.Label("Door tutorial", transform, new Vector2(0, 182), new Vector2(930, 32),
                "<b>Try Level 11.</b>", 23, font);
    }

    void OnEnable()
    {
        startedAt = Time.unscaledTime;
        Render(0f);
    }

    void Update() => Render(Time.unscaledTime - startedAt);

    void Grid(RectTransform root, string name, Vector2 center, float step)
    {
        for (int row = -1; row <= 1; row++)
            for (int column = -1; column <= 1; column++)
                RulesDemoUI.Box(name + " cell", root, center + new Vector2(column * step, row * step),
                    Vector2.one * (step - 2), RulesDemoUI.TileFill);
    }

    void BuildJunction(RectTransform root)
    {
        RulesDemoUI.EndLine("Start line", root, new Vector2(0, CellSize * 1.5f), new Vector2(0, Outside),
            Green, CellSize, RulesDemoUI.ObjectRatio);
        RulesDemoUI.EndLine("Goal line", root, new Vector2(0, -CellSize * 1.5f), new Vector2(0, -Outside),
            Green, CellSize, RulesDemoUI.RingRatio);
        Grid(root, "Junction board", Vector2.zero, CellSize);
        var junction = RulesDemoUI.Tile("Junction tile", root, new Vector2(0, CellSize), TileType.Cross, CellSize - 2);
        junctionFill = junction.GetComponent<Image>();
        RulesDemoUI.Tile("Left route", root, new Vector2(-CellSize, CellSize), TileType.StraightH, CellSize - 2);
        RulesDemoUI.Tile("Right route", root, new Vector2(CellSize, CellSize), TileType.StraightH, CellSize - 2);
        RulesDemoUI.Tile("Lower route", root, Vector2.zero, TileType.StraightV, CellSize - 2);
        RulesDemoUI.Tile("Lower continuation", root, new Vector2(0, -CellSize), TileType.StraightV, CellSize - 2);
        RulesDemoUI.Ring("Green destination", root, new Vector2(0, -Outside), Green, CellSize * RulesDemoUI.RingRatio);
        actor = RulesDemoUI.Actor("Junction object", root, new Vector2(0, Outside), Green, CellSize * RulesDemoUI.ObjectRatio, Dir.S);
        actorVisibility = actor.gameObject.AddComponent<CanvasGroup>();
        choices = RulesDemoUI.Group("Direction choices", root, new Vector2(0, CellSize), Vector2.one * CellSize);
        float offset = CellSize * RulesDemoUI.ChoiceOffset;
        float size = CellSize * RulesDemoUI.ChoiceRatio;
        RulesDemoUI.Actor("Left choice", choices, new Vector2(-offset, 0), Green, size, Dir.W);
        RulesDemoUI.Actor("Right choice", choices, new Vector2(offset, 0), Green, size, Dir.E);
        RulesDemoUI.Actor("Down choice", choices, new Vector2(0, -offset), Green, size, Dir.S);
        selection = RulesDemoUI.Ring("Selected direction", choices, new Vector2(0, -offset), RulesDemoUI.Gold, size * 1.25f).rectTransform;
        cursor = RulesDemoUI.Cursor("Direction cursor", root, new Vector2(170, -70));
    }

    void BuildBridge(RectTransform root)
    {
        RulesDemoUI.EndLine("Red start line", root, new Vector2(-CellSize * 1.5f, 0), new Vector2(-Outside, 0),
            RulesDemoUI.Red, CellSize, RulesDemoUI.ObjectRatio);
        RulesDemoUI.EndLine("Red goal line", root, new Vector2(CellSize * 1.5f, 0), new Vector2(Outside, 0),
            RulesDemoUI.Red, CellSize, RulesDemoUI.RingRatio);
        RulesDemoUI.EndLine("Blue start line", root, new Vector2(0, -CellSize * 1.5f), new Vector2(0, -Outside),
            RulesDemoUI.Blue, CellSize, RulesDemoUI.ObjectRatio);
        RulesDemoUI.EndLine("Blue goal line", root, new Vector2(0, CellSize * 1.5f), new Vector2(0, Outside),
            RulesDemoUI.Blue, CellSize, RulesDemoUI.RingRatio);
        Grid(root, "Bridge board", Vector2.zero, CellSize);
        RulesDemoUI.Tile("Bridge", root, Vector2.zero, TileType.Bridge, CellSize - 2);
        RulesDemoUI.Tile("Left path", root, new Vector2(-CellSize, 0), TileType.StraightH, CellSize - 2);
        RulesDemoUI.Tile("Right path", root, new Vector2(CellSize, 0), TileType.StraightH, CellSize - 2);
        RulesDemoUI.Tile("Lower path", root, new Vector2(0, -CellSize), TileType.StraightV, CellSize - 2);
        RulesDemoUI.Tile("Upper path", root, new Vector2(0, CellSize), TileType.StraightV, CellSize - 2);
        RulesDemoUI.Ring("Red destination", root, new Vector2(Outside, 0), RulesDemoUI.Red, CellSize * RulesDemoUI.RingRatio);
        RulesDemoUI.Ring("Blue destination", root, new Vector2(0, Outside), RulesDemoUI.Blue, CellSize * RulesDemoUI.RingRatio);
        actor = RulesDemoUI.Actor("Horizontal object", root, new Vector2(-Outside, 0), RulesDemoUI.Red, CellSize * RulesDemoUI.ObjectRatio, Dir.E);
        secondActor = RulesDemoUI.Actor("Vertical object", root, new Vector2(0, -Outside), RulesDemoUI.Blue, CellSize * RulesDemoUI.ObjectRatio, Dir.N);
        actorVisibility = actor.gameObject.AddComponent<CanvasGroup>();
        secondVisibility = secondActor.gameObject.AddComponent<CanvasGroup>();
    }

    void BuildDoors(RectTransform root)
    {
        RulesDemoUI.Box("Entry board panel", root, new Vector2(-230, 0), new Vector2(420, 382), RulesDemoUI.Panel);
        RulesDemoUI.Box("Exit board panel", root, new Vector2(230, 0), new Vector2(420, 382), RulesDemoUI.Panel);
        RulesDemoUI.EndLine("Start line", root, new Vector2(-230 - DoorCellSize * 1.5f, 0), new Vector2(-DoorOutside, 0),
            RulesDemoUI.Red, DoorCellSize, RulesDemoUI.ObjectRatio);
        RulesDemoUI.EndLine("Goal line", root, new Vector2(230 + DoorCellSize * 1.5f, 0), new Vector2(DoorOutside, 0),
            RulesDemoUI.Red, DoorCellSize, RulesDemoUI.RingRatio);
        Grid(root, "Entry board", new Vector2(-230, 0), DoorCellSize);
        Grid(root, "Exit board", new Vector2(230, 0), DoorCellSize);
        for (int column = -1; column <= 1; column++)
        {
            RulesDemoUI.Tile("Entry route", root, new Vector2(-230 + column * DoorCellSize, 0), TileType.StraightH, DoorCellSize - 2);
            RulesDemoUI.Tile("Exit route", root, new Vector2(230 + column * DoorCellSize, 0), TileType.StraightH, DoorCellSize - 2);
        }
        BuildDoor(root, new Vector2(-189, 0), "Entry door");
        BuildDoor(root, new Vector2(189, 0), "Exit door");
        RulesDemoUI.Ring("Destination", root, new Vector2(DoorOutside, 0), RulesDemoUI.Red, DoorCellSize * RulesDemoUI.RingRatio);
        actor = RulesDemoUI.Actor("Teleporting object", root, new Vector2(-DoorOutside, 0), RulesDemoUI.Red, DoorCellSize * RulesDemoUI.ObjectRatio, Dir.E);
        actorVisibility = actor.gameObject.AddComponent<CanvasGroup>();
        choices = RulesDemoUI.Group("Door direction choices", root, new Vector2(189, 0), new Vector2(120, 50));
        float offset = DoorCellSize * RulesDemoUI.ChoiceOffset;
        float size = DoorCellSize * RulesDemoUI.ChoiceRatio;
        RulesDemoUI.Actor("Left choice", choices, new Vector2(-offset, 0), RulesDemoUI.Red, size, Dir.W);
        RulesDemoUI.Actor("Right choice", choices, new Vector2(offset, 0), RulesDemoUI.Red, size, Dir.E);
        selection = RulesDemoUI.Ring("Chosen door direction", choices, new Vector2(offset, 0), RulesDemoUI.Gold, size * 1.25f).rectTransform;
        cursor = RulesDemoUI.Cursor("Door choice cursor", root, new Vector2(320, -100));
    }

    void BuildDoor(RectTransform root, Vector2 position, string name)
    {
        RulesDemoUI.Box(name + " edge", root, position, new Vector2(DoorCellSize * .1f, DoorCellSize * .7f), DoorFill);
        RulesDemoUI.Box(name + " badge", root, position, Vector2.one * DoorCellSize * .3f, DoorFill);
        var text = RulesDemoUI.Label(name + " label", root, position, Vector2.one * DoorCellSize * .3f, "<b>A</b>", DoorCellSize * .22f, font);
        text.color = RulesDemoUI.Path;
    }

    void Render(float elapsed)
    {
        if (actor == null) return;
        switch (kind)
        {
            case RouteKind.Junction:
                RenderJunction(elapsed);
                break;
            case RouteKind.Bridge:
                RenderBridge(elapsed);
                break;
            case RouteKind.Doors:
                RenderDoors(elapsed);
                break;
        }
    }

    void RenderJunction(float elapsed)
    {
        float time = Mathf.Repeat(elapsed, 8f);
        bool choosing = time >= 2.5f && time < 4.8f;
        bool selected = time >= 4.1f && time < 4.8f;
        choices.gameObject.SetActive(choosing);
        selection.gameObject.SetActive(selected);
        junctionFill.color = choosing ? ChoiceFill : time >= 4.8f ? RulesDemoUI.UsedFill : RulesDemoUI.TileFill;
        cursor.gameObject.SetActive(choosing);
        cursor.anchoredPosition = Vector2.Lerp(new Vector2(170, -70),
            new Vector2(12, CellSize * (1f - RulesDemoUI.ChoiceOffset) - 14), Step(time, 2.8f, 4.1f));
        RulesDemoUI.Press(cursor, selected);
        actorVisibility.alpha = time < 6.9f ? 1f : 0f;
        RulesDemoUI.Face(actor, Dir.S);
        actor.anchoredPosition = time < 4.8f
            ? Vector2.Lerp(new Vector2(0, Outside), new Vector2(0, CellSize), Step(time, .8f, 2.5f))
            : Vector2.Lerp(new Vector2(0, CellSize), new Vector2(0, -Outside), Step(time, 4.8f, 6.8f));
    }

    void RenderBridge(float elapsed)
    {
        float time = Mathf.Repeat(elapsed, 7f);
        actor.anchoredPosition = Vector2.Lerp(new Vector2(-Outside, 0), new Vector2(Outside, 0), Step(time, .8f, 4.1f));
        secondActor.anchoredPosition = Vector2.Lerp(new Vector2(0, -Outside), new Vector2(0, Outside), Step(time, 1.8f, 5.1f));
        actorVisibility.alpha = time < 4.4f ? 1f : 0f;
        secondVisibility.alpha = time < 5.4f ? 1f : 0f;
    }

    void RenderDoors(float elapsed)
    {
        float time = Mathf.Repeat(elapsed, 10f);
        bool choosing = time >= 4.2f && time < 6.5f;
        bool selected = time >= 5.8f && time < 6.5f;
        choices.gameObject.SetActive(choosing);
        selection.gameObject.SetActive(selected);
        cursor.gameObject.SetActive(choosing);
        cursor.anchoredPosition = Vector2.Lerp(new Vector2(320, -100),
            new Vector2(189 + DoorCellSize * RulesDemoUI.ChoiceOffset + 12, -12), Step(time, 4.4f, 5.8f));
        RulesDemoUI.Press(cursor, selected);
        actorVisibility.alpha = time < 3.1f || time >= 4.2f && time < 8.6f ? 1f : 0f;
        actor.anchoredPosition = time < 4.2f
            ? Vector2.Lerp(new Vector2(-DoorOutside, 0), new Vector2(-189, 0), Step(time, .8f, 3.1f))
            : Vector2.Lerp(new Vector2(189, 0), new Vector2(DoorOutside, 0), Step(time, 6.5f, 8.4f));
        RulesDemoUI.Face(actor, time >= 4.2f && time < 6.5f ? Dir.N : Dir.E);
    }

    static float Step(float time, float from, float to)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, time));
    }
}
