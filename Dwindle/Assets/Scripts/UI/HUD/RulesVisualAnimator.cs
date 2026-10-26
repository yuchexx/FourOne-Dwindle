using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RulesVisualAnimator : MonoBehaviour
{
    readonly Dictionary<string, RectTransform> rt = new();
    readonly Dictionary<string, CanvasGroup> groups = new();
    float startedAt;
    static readonly string[] JunctionExits = { "Left", "Up", "Right" };

    void Awake()
    {
        foreach (var r in GetComponentsInChildren<RectTransform>(true))
            rt[r.name] = r;
        SmoothJunctionPlayer();
    }

    void SmoothJunctionPlayer()
    {
        if (!rt.TryGetValue("P2_Junction_Object", out var player)) return;
        var oldGraphic = player.GetComponent<Graphic>();
        Color playerColor = oldGraphic != null ? oldGraphic.color : new Color(1f, .27f, .3f);
        if (oldGraphic != null) oldGraphic.enabled = false;
        if (rt.TryGetValue("P2_Junction_Object_Tri", out var triangle))
            triangle.gameObject.SetActive(false);
        var smooth = new GameObject("JunctionPlayerSmooth", typeof(RectTransform), typeof(CanvasRenderer));
        var rect = smooth.GetComponent<RectTransform>();
        rect.SetParent(player, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var graphic = smooth.AddComponent<RulesJunctionPlayerGraphic>();
        graphic.color = playerColor;
        graphic.raycastTarget = false;
    }

    void OnEnable() { startedAt = Time.unscaledTime; }
    float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    float Loop(float seconds) => Mathf.Repeat(Time.unscaledTime - startedAt, seconds) / seconds;

    void Move(string name, Vector2 from, Vector2 to, float t)
    {
        if (rt.TryGetValue(name, out var r))
            r.anchoredPosition = Vector2.Lerp(from, to, Ease(t));
    }

    // Fade the whole object, including its triangle and all child graphics.
    void Fade(string name, float alpha)
    {
        if (!rt.TryGetValue(name, out var r)) return;
        if (!groups.TryGetValue(name, out var group))
        {
            group = r.GetComponent<CanvasGroup>();
            if (group == null) group = r.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            groups[name] = group;
        }
        group.alpha = Mathf.Clamp01(alpha);
    }

    void Scale(string name, float scale)
    {
        if (rt.TryGetValue(name, out var r)) r.localScale = Vector3.one * scale;
    }

    void Arrival(string name, Vector2 from, Vector2 goal, float phase, float arrival)
    {
        Move(name, from, goal, phase / arrival);
        Fade(name, phase < arrival ? 1f : 1f - Mathf.Clamp01((phase - arrival) / .12f));
    }

    // A single short pop, then a readable hold. Hidden until the arrival frame.
    void Reward(string name, float phase, float arrival)
    {
        float elapsed = phase - arrival;
        float scale = elapsed < 0f ? 0f :
            elapsed < .07f ? Mathf.Lerp(.55f, 1.16f, Ease(elapsed / .07f)) :
            elapsed < .14f ? Mathf.Lerp(1.16f, 1f, Ease((elapsed - .07f) / .07f)) : 1f;
        Scale(name, scale);
    }

    void Update()
    {
        float match = Loop(2.8f);
        Arrival("P1_Match_Object", new Vector2(-90, 0), new Vector2(90, 0), match, .66f);

        float place = Loop(3f);
        Move("P1_Place_MovingTile", new Vector2(-86, 10), new Vector2(4, 24),
            Mathf.Clamp01((place - .12f) / .52f));

        float auto = Loop(2.8f);
        Arrival("P1_Auto_Object", new Vector2(-88, 0), new Vector2(88, 0), auto, .66f);
        // Take Back is deliberately a static before/after comparison.

        float junction = Loop(3f);
        for (int i = 0; i < JunctionExits.Length; i++)
        {
            float distance = Mathf.Abs(junction - (.17f + i * .3f));
            Scale("P2_Junction_" + JunctionExits[i], 1f + .12f * Mathf.Max(0f, 1f - distance * 10f));
        }

        float bridge = Loop(3f);
        Arrival("P2_Bridge_HObj", new Vector2(-57, 0), new Vector2(57, 0), bridge, .72f);
        Arrival("P2_Bridge_VObj", new Vector2(0, -57), new Vector2(0, 57), bridge, .72f);

        float door = Loop(3.4f);
        if (door < .30f)
        {
            Move("P2_Door_Object", new Vector2(-138, 0), new Vector2(-86, 0), door / .30f);
            Fade("P2_Door_Object", 1f);
        }
        else if (door < .46f)
            Fade("P2_Door_Object", 0f);
        else
        {
            Move("P2_Door_Object", new Vector2(34, 0), new Vector2(92, 0), (door - .46f) / .30f);
            Fade("P2_Door_Object", door < .76f ? 1f : 1f - Mathf.Clamp01((door - .76f) / .1f));
        }

        float delivery = Loop(3.2f);
        Arrival("P3_Delivery_Object", new Vector2(-96, 0), new Vector2(42, 0), delivery, .60f);
        Reward("P3_Delivery_Score", delivery, .60f);

        // The score page uses one door icon: enter, briefly disappear, then exit toward the goal.
        float bonus = Loop(3.6f);
        if (bonus < .28f)
        {
            Move("P3_DoorBonus_Object", new Vector2(-110, 0), new Vector2(-30, 0), bonus / .28f);
            Fade("P3_DoorBonus_Object", 1f);
        }
        else if (bonus < .44f)
            Fade("P3_DoorBonus_Object", 0f);
        else
        {
            Move("P3_DoorBonus_Object", new Vector2(16, 0), new Vector2(44, 0), (bonus - .44f) / .28f);
            Fade("P3_DoorBonus_Object", bonus < .72f ? 1f : 1f - Mathf.Clamp01((bonus - .72f) / .1f));
        }
        Reward("P3_DoorBonus_Score", bonus, .72f);

        // Both players reach their own goal on precisely the same frame.
        float combo = Loop(3.6f);
        const float arrival = .58f;
        Arrival("P3_Combo_Red", new Vector2(-150, 22), new Vector2(-46, 22), combo, arrival);
        Arrival("P3_Combo_Blue", new Vector2(-150, -22), new Vector2(-46, -22), combo, arrival);
        Reward("P3_Combo_Reward", combo, arrival);
        Reward("P3_Combo_Score", combo, arrival);
    }
}

// Runtime-only graphic: one continuous circle and triangle, independent of font glyphs.
public sealed class RulesJunctionPlayerGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var bounds = rectTransform.rect;
        Vector2 center = bounds.center;
        float radius = Mathf.Min(bounds.width, bounds.height) * .5f;
        const int segments = 72;
        mesh.AddVert(center, color, Vector2.zero);
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
            if (i > 0) mesh.AddTriangle(0, i, i + 1);
        }
        int start = mesh.currentVertCount;
        mesh.AddVert(center + new Vector2(-.30f, -.44f) * radius, Color.white, Vector2.zero);
        mesh.AddVert(center + new Vector2(-.30f, .44f) * radius, Color.white, Vector2.zero);
        mesh.AddVert(center + new Vector2(.51f, 0f) * radius, Color.white, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
    }
}
