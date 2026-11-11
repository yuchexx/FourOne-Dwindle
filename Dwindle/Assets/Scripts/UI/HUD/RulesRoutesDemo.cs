using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RulesRoutesDemo : MonoBehaviour
{
    public enum RouteKind { Junction, Bridge, Doors }

    public TMP_FontAsset font;
    public RouteKind kind;

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
        var root = RulesDemoUI.Group("Route demonstration", transform, new Vector2(0, 12), new Vector2(900, 382));
        if (kind != RouteKind.Doors)
            RulesDemoUI.Box("Background", root, Vector2.zero, new Vector2(900, 382), RulesDemoUI.Panel);
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
        RulesDemoUI.Label("Route caption", transform, new Vector2(0, -214), new Vector2(930, 68), caption, 24, font);
        if (kind == RouteKind.Doors)
            RulesDemoUI.Label("Door tutorial", transform, new Vector2(0, -290), new Vector2(930, 30),
                "New to doors? Try Level 11.", 20, font);
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
        Grid(root, "Junction board", Vector2.zero, 98);
        var junction = RulesDemoUI.Tile("Junction tile", root, new Vector2(0, 98), TileType.Cross, 96);
        junctionFill = junction.GetComponent<Image>();
        RulesDemoUI.Tile("Left route", root, new Vector2(-98, 98), TileType.StraightH, 96);
        RulesDemoUI.Tile("Right route", root, new Vector2(98, 98), TileType.StraightH, 96);
        RulesDemoUI.Tile("Lower route", root, Vector2.zero, TileType.StraightV, 96);
        RulesDemoUI.Tile("Lower continuation", root, new Vector2(0, -98), TileType.StraightV, 96);
        RulesDemoUI.Ring("Green destination", root, new Vector2(0, -174), Green, 34);
        actor = RulesDemoUI.Actor("Junction object", root, new Vector2(0, 174), Green, 44, Dir.S);
        actorVisibility = actor.gameObject.AddComponent<CanvasGroup>();
        choices = RulesDemoUI.Group("Direction choices", root, new Vector2(0, 98), new Vector2(96, 96));
        RulesDemoUI.Actor("Left choice", choices, new Vector2(-37, 0), Green, 26, Dir.W);
        RulesDemoUI.Actor("Right choice", choices, new Vector2(37, 0), Green, 26, Dir.E);
        RulesDemoUI.Actor("Down choice", choices, new Vector2(0, -37), Green, 26, Dir.S);
        selection = RulesDemoUI.Ring("Selected direction", choices, new Vector2(0, -37), RulesDemoUI.Gold, 33).rectTransform;
        cursor = RulesDemoUI.Cursor("Direction cursor", root, new Vector2(170, -70));
    }

    void BuildBridge(RectTransform root)
    {
        Grid(root, "Bridge board", Vector2.zero, 98);
        RulesDemoUI.Tile("Bridge", root, Vector2.zero, TileType.Bridge, 96);
        RulesDemoUI.Tile("Left path", root, new Vector2(-98, 0), TileType.StraightH, 96);
        RulesDemoUI.Tile("Right path", root, new Vector2(98, 0), TileType.StraightH, 96);
        RulesDemoUI.Tile("Lower path", root, new Vector2(0, -98), TileType.StraightV, 96);
        RulesDemoUI.Tile("Upper path", root, new Vector2(0, 98), TileType.StraightV, 96);
        RulesDemoUI.Ring("Red destination", root, new Vector2(210, 0), RulesDemoUI.Red, 36);
        RulesDemoUI.Ring("Blue destination", root, new Vector2(0, 174), RulesDemoUI.Blue, 36);
        actor = RulesDemoUI.Actor("Horizontal object", root, new Vector2(-210, 0), RulesDemoUI.Red, 36, Dir.E);
        secondActor = RulesDemoUI.Actor("Vertical object", root, new Vector2(0, -174), RulesDemoUI.Blue, 36, Dir.N);
        actorVisibility = actor.gameObject.AddComponent<CanvasGroup>();
        secondVisibility = secondActor.gameObject.AddComponent<CanvasGroup>();
    }

    void BuildDoors(RectTransform root)
    {
        RulesDemoUI.Box("Entry board panel", root, new Vector2(-230, 0), new Vector2(420, 350), RulesDemoUI.Panel);
        RulesDemoUI.Box("Exit board panel", root, new Vector2(230, 0), new Vector2(420, 350), RulesDemoUI.Panel);
        Grid(root, "Entry board", new Vector2(-230, 0), 82);
        Grid(root, "Exit board", new Vector2(230, 0), 82);
        for (int column = -1; column <= 1; column++)
        {
            RulesDemoUI.Tile("Entry route", root, new Vector2(-230 + column * 82, 0), TileType.StraightH, 80);
            RulesDemoUI.Tile("Exit route", root, new Vector2(230 + column * 82, 0), TileType.StraightH, 80);
        }
        BuildDoor(root, new Vector2(-189, 0), "Entry door");
        BuildDoor(root, new Vector2(189, 0), "Exit door");
        RulesDemoUI.Ring("Destination", root, new Vector2(400, 0), RulesDemoUI.Red, 36);
        actor = RulesDemoUI.Actor("Teleporting object", root, new Vector2(-400, 0), RulesDemoUI.Red, 36, Dir.E);
        actorVisibility = actor.gameObject.AddComponent<CanvasGroup>();
        choices = RulesDemoUI.Group("Door direction choices", root, new Vector2(189, 0), new Vector2(120, 50));
        RulesDemoUI.Actor("Left choice", choices, new Vector2(-40, 0), RulesDemoUI.Red, 26, Dir.W);
        RulesDemoUI.Actor("Right choice", choices, new Vector2(40, 0), RulesDemoUI.Red, 26, Dir.E);
        selection = RulesDemoUI.Ring("Chosen door direction", choices, new Vector2(40, 0), RulesDemoUI.Gold, 33).rectTransform;
        cursor = RulesDemoUI.Cursor("Door choice cursor", root, new Vector2(320, -100));
    }

    void BuildDoor(RectTransform root, Vector2 position, string name)
    {
        RulesDemoUI.Box(name + " edge", root, position, new Vector2(8, 80), DoorFill);
        RulesDemoUI.Box(name + " badge", root, position, new Vector2(28, 30), DoorFill);
        var text = RulesDemoUI.Label(name + " label", root, position, new Vector2(28, 30), "A", 23, font);
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
        cursor.anchoredPosition = Vector2.Lerp(new Vector2(170, -70), new Vector2(12, 48), Step(time, 2.8f, 4.1f));
        RulesDemoUI.Press(cursor, selected);
        actorVisibility.alpha = time < 6.9f ? 1f : 0f;
        RulesDemoUI.Face(actor, Dir.S);
        actor.anchoredPosition = time < 4.8f
            ? Vector2.Lerp(new Vector2(0, 174), new Vector2(0, 98), Step(time, .8f, 2.5f))
            : Vector2.Lerp(new Vector2(0, 98), new Vector2(0, -174), Step(time, 4.8f, 6.8f));
    }

    void RenderBridge(float elapsed)
    {
        float time = Mathf.Repeat(elapsed, 7f);
        actor.anchoredPosition = Vector2.Lerp(new Vector2(-210, 0), new Vector2(210, 0), Step(time, .8f, 4.1f));
        secondActor.anchoredPosition = Vector2.Lerp(new Vector2(0, -174), new Vector2(0, 174), Step(time, 1.8f, 5.1f));
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
        cursor.anchoredPosition = Vector2.Lerp(new Vector2(320, -100), new Vector2(241, -12), Step(time, 4.4f, 5.8f));
        RulesDemoUI.Press(cursor, selected);
        actorVisibility.alpha = time < 3.1f || time >= 4.2f && time < 8.6f ? 1f : 0f;
        actor.anchoredPosition = time < 4.2f
            ? Vector2.Lerp(new Vector2(-400, 0), new Vector2(-189, 0), Step(time, .8f, 3.1f))
            : Vector2.Lerp(new Vector2(189, 0), new Vector2(400, 0), Step(time, 6.5f, 8.4f));
    }

    static float Step(float time, float from, float to)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, time));
    }
}
