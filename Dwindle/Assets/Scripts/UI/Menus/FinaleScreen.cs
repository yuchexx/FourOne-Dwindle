using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the ending screen after the last level (GameManager.ShowMenu): a title, the totals, a row per colour group, confetti
// in the groups' colours, and a Continue icon (the end window is under it). built in code, as the level pop-ups
public class FinaleScreen : MonoBehaviour
{
    const float W = 900f, ConfettiSeconds = 7f; // bits that fall out are sent back to the top till then, then the rest fall away
    readonly List<(RectTransform rt, Vector2 v, float spin)> bits = new();
    float started;
    Vector2 screenSize; // the confetti's area
    RectTransform confettiArea; Color[] confettiColors;

    // the Confetti button: a new round falls, and the rain goes on another ConfettiSeconds
    void MoreConfetti()
    {
        MakeConfetti(confettiArea, confettiColors);
        started = Time.unscaledTime;
        AudioManager.Play(AudioManager.Sfx.LevelComplete);
    }
    Action onClose;

    public static FinaleScreen Show(Canvas canvas, TMP_FontAsset font, Color[] groupColors, Action onClose = null, string iconName = "home", Graphic starTemplate = null, Color[] confettiColors = null)
    {
        var p = UIPalette.Current;
        var root = new GameObject("FinaleScreen", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)root.transform;
        rt.SetParent(canvas.rootCanvas.transform, false);
        UIBuilder.Fill(rt);
        root.GetComponent<Image>().color = p.background; // the whole window, opaque: the level and its score are gone
        rt.SetAsLastSibling();
        var cv = root.AddComponent<Canvas>(); cv.overrideSorting = true; cv.sortingOrder = 500; // over everything, the HUD lifted by the end window too
        root.AddComponent<GraphicRaycaster>();
        var me = root.AddComponent<FinaleScreen>();
        me.onClose = onClose;

        // the counts: stars, levels with 3 stars, levels with purple stars (3 stars without the answer hint); per group
        int levels = Levels.All.Count, stars = 0, three = 0, purple = 0;
        var names = Levels.GroupNames;
        int groups = names.Length;
        var gotStars = new int[groups]; var maxStars = new int[groups];
        for (int i = 0; i < levels; i++)
        {
            var r = Session.Get(i); int g = Mathf.Clamp(Levels.All[i].colorGroup, 0, groups - 1);
            stars += r.stars; gotStars[g] += r.stars; maxStars[g] += 3;
            if (r.stars == 3) three++;
            if (r.stars == 3 && r.purple) purple++;
        }

        const float rowH = 40f;
        float h = 120f + 3 * rowH + 40f + groups * rowH + 100f;
        var panel = Box("Panel", rt, Vector2.zero, new Vector2(W, h), Color.clear); // no card: the content straight on the screen
        var prt = panel.rectTransform;
        Text("Title", prt, new Vector2(0f, h / 2 - 40f), new Vector2(W, 64f), "<b>You finished Dwindle!</b>", 52, font, p.text);

        // the totals: all stars, then three gold stars and three purple stars, each with its count of levels
        // a small centred table: the label (or the stars) ends just left of the middle, the count starts just right of it
        const float gap = 12f;
        void Label(float y, string text)
        {
            var t = Text("Label", prt, new Vector2(-gap - 100f, y), new Vector2(200f, rowH), text, 24, font, p.text);
            t.alignment = TextAlignmentOptions.MidlineRight;
        }
        void Count(float y, string text)
        {
            var t = Text("Count", prt, new Vector2(gap + 120f, y), new Vector2(240f, rowH), text, 24, font, p.text);
            t.alignment = TextAlignmentOptions.MidlineLeft;
        }
        void Stars(float y, Color c) { for (int s = 0; s < 3; s++) Star(prt, new Vector2(-gap - 15f - (2 - s) * 36f, y), 30f, c, starTemplate); }
        float y0 = h / 2 - 110f;
        Label(y0, "<b>Total</b>"); Count(y0, $"<b>{stars} / {levels * 3} stars</b>");
        Stars(y0 - rowH, p.star); Count(y0 - rowH, $"{three} / {levels} levels");
        Stars(y0 - 2 * rowH, p.purpleStar); Count(y0 - 2 * rowH, $"{purple} / {levels} levels");

        // a row per colour group: its card shade, its name, its stars
        float top = y0 - 3 * rowH - 30f;
        for (int g = 0; g < groups; g++)
        {
            float y = top - g * rowH;
            var c = groupColors != null && groupColors.Length > 0 ? groupColors[Mathf.Min(g, groupColors.Length - 1)] : p.accent;
            Box("Swatch", prt, new Vector2(-W / 2 + 160f, y), new Vector2(22f, 22f), c);
            var n = Text("Name", prt, new Vector2(-W / 2 + 190f + 180f, y), new Vector2(360f, rowH), names[g], 22, font, p.text);
            n.alignment = TextAlignmentOptions.MidlineLeft;
            var s = Text("GroupStars", prt, new Vector2(W / 2 - 160f - 80f, y), new Vector2(160f, rowH), $"{gotStars[g]} / {maxStars[g]} stars", 22, font, p.text);
            s.alignment = TextAlignmentOptions.MidlineRight;
        }

        // the buttons under the rows, as the game's icon buttons (icon, name under it, no background; hover colour and
        // grow): Confetti (another round) and Home (the title screen)
        IconButton(prt, new Vector2(-60f, -h / 2 + 50f), "confetti", "Confetti", font, p, me.MoreConfetti);
        IconButton(prt, new Vector2(60f, -h / 2 + 50f), iconName, iconName == "home" ? "Home" : "Back", font, p, me.Close);

        me.confettiArea = rt;
        me.confettiColors = confettiColors != null && confettiColors.Length > 0 ? confettiColors : new[] { p.star, p.purpleStar, p.accent, p.text }; // colourful
        me.MakeConfetti(rt, me.confettiColors);
        me.started = Time.unscaledTime;
        AudioManager.Play(AudioManager.Sfx.LevelComplete); // the level-complete sound again
        return me;
    }

    // falling bits in the groups' colours, over the panel
    void MakeConfetti(RectTransform parent, Color[] colors)
    {
        var layer = (RectTransform)new GameObject("Confetti", typeof(RectTransform)).transform;
        layer.SetParent(parent, false);
        UIBuilder.Fill(layer);
        var size = parent.rect.size;
        if (size.x <= 0f) size = new Vector2(1920f, 1080f);
        screenSize = size;
        for (int i = 0; i < 140; i++)
        {
            var c = colors != null && colors.Length > 0 ? colors[i % colors.Length] : Color.white;
            var img = Box("Bit", layer, Vector2.zero, new Vector2(UnityEngine.Random.Range(8f, 14f), UnityEngine.Random.Range(14f, 24f)), c);
            img.sprite = SoftSquare(); // soft edges: a plain rotated square shows jagged (stair-step) edges
            var brt = img.rectTransform;
            brt.anchoredPosition = new Vector2(UnityEngine.Random.Range(-size.x / 2, size.x / 2), size.y / 2 + UnityEngine.Random.Range(20f, size.y * 0.8f)); // above the top, staggered
            brt.localEulerAngles = new Vector3(0, 0, UnityEngine.Random.Range(0f, 360f));
            bits.Add((brt, new Vector2(UnityEngine.Random.Range(-60f, 60f), -UnityEngine.Random.Range(220f, 420f)), UnityEngine.Random.Range(-360f, 360f)));
        }
    }

    // a white square with a soft 1-pixel-wide transparent rim, smoothed when scaled and turned (the UI draws no
    // anti-aliasing, so a plain square's edges look jagged once rotated)
    static Sprite softSquare;
    static Sprite SoftSquare()
    {
        if (softSquare != null) return softSquare;
        const int n = 32; const float rim = 2f;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float edge = Mathf.Min(Mathf.Min(x + 0.5f, n - x - 0.5f), Mathf.Min(y + 0.5f, n - y - 0.5f)); // distance to the nearest side
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((edge - 0.5f) / rim)));
            }
        tex.Apply();
        return softSquare = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(rim + 1, rim + 1, rim + 1, rim + 1));
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime, t = Time.unscaledTime - started;
        foreach (var (rt, v, spin) in bits)
        {
            if (rt == null) continue;
            rt.anchoredPosition += new Vector2(v.x + Mathf.Sin(t * 3f + v.y) * 40f, v.y) * dt; // a little sway
            rt.localEulerAngles += new Vector3(0, 0, spin * dt);
            if (t < ConfettiSeconds && rt.anchoredPosition.y < -screenSize.y / 2 - 40f) // fell out: back to the top, somewhere else
                rt.anchoredPosition = new Vector2(UnityEngine.Random.Range(-screenSize.x / 2, screenSize.x / 2), screenSize.y / 2 + UnityEngine.Random.Range(20f, 200f));
            else if (rt.anchoredPosition.y < -screenSize.y / 2 - 40f) Destroy(rt.gameObject); // the rain is over: gone
        }
    }

    void Close()
    {
        var done = onClose;
        Destroy(gameObject);
        done?.Invoke();
    }

    // an icon button: a white icon with its name under it, no background; IconHover gives the hover colour and grow
    static void IconButton(RectTransform parent, Vector2 pos, string iconName, string name, TMP_FontAsset font, UIPalette p, Action onClick)
    {
        var root = Box(name + "Button", parent, pos, new Vector2(100f, 90f), Color.clear);
        root.raycastTarget = true; // the clickable area (see-through)
        var icon = Box("Icon", root.rectTransform, new Vector2(0f, 12f), new Vector2(48f, 48f), Color.white);
        var tex = Resources.Load<Texture2D>("UIIcons/" + iconName);
        if (tex != null) icon.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        icon.preserveAspect = true;
        Text("Label", root.rectTransform, new Vector2(0f, -30f), new Vector2(100f, 26f), name, 18, font, Color.white);
        var b = root.gameObject.AddComponent<Button>();
        b.targetGraphic = root;
        b.onClick.AddListener(() => onClick());
        root.gameObject.AddComponent<IconHover>(); // after the icon and name: it finds them on Awake
    }

    // a star: a copy of a level card's star (template), else a new StarGraphic
    static void Star(Transform parent, Vector2 pos, float size, Color color, Graphic template)
    {
        var g = template != null ? Instantiate(template, parent, false) : new GameObject("Star", typeof(RectTransform), typeof(StarGraphic)).GetComponent<Graphic>();
        g.name = "Star";
        var rt = g.rectTransform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one; rt.localEulerAngles = Vector3.zero;
        rt.anchoredPosition = pos; rt.sizeDelta = Vector2.one * size;
        g.gameObject.SetActive(true); // the card's copy may be hidden (a locked card hides its stars)
        g.enabled = true;
        g.color = color;
        g.raycastTarget = false;
        StarSparkle.Set(g, color == UIPalette.Current.purpleStar); // purple stars sparkle (a copied card star's sparkle goes otherwise)
        g.SetAllDirty();
    }

    static Image Box(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var img = UIBuilder.NewImage(name, parent, color);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        img.raycastTarget = false;
        return img;
    }

    static TMP_Text Text(string name, Transform parent, Vector2 pos, Vector2 size, string text, float fontSize, TMP_FontAsset font, Color color)
    {
        var t = UIBuilder.NewText(name, parent, text, fontSize, color, font);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }
}
