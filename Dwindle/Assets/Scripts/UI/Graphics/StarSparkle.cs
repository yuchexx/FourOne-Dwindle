using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// a purple star's glitter (look K): a glitter surface inside the star with flecks catching the light, small glitter of
// thin fading rays with a tiny star in the middle, and now and then a bigger ray glint with a small star, all inside
// the star's shape. StarSparkle.Set(star, on) adds it to a star, or stops it.
// Images only (a shape drawn by its own Graphic made at run time doesn't show here), in a nested Canvas so their
// changes each frame don't rebuild the whole screen
public class StarSparkle : MonoBehaviour
{
    [Header("Small glitter (per star, at 80 wide; smaller stars get fewer)")]
    [Min(0)] public int specks = 40;
    [Min(0.1f)] public float speckSize = 2.2f;            // the biggest speck, in pixels (sets its rays' and star's share)
    [Min(0.05f)] public float blink = 0.4f;               // one speck's flash, seconds (each 0.5x to 1.5x)
    [Min(0f)] public float rest = 0.45f;                  // average pause before a speck flashes again, elsewhere
    [Min(0f)] public float speckRay = 0.14f;              // its rays each way, in star widths (for the biggest speck)
    [Min(0f)] public float speckStar = 0.05f;             // its star, in star widths (0: rays only)
    [Header("Ray glints")]
    [Min(0.05f)] public float glintEvery = 0.85f;         // average time between glints over three stars
    [Min(0.1f)] public float glintSeconds = 0.8f;
    [Min(0f)] public float glintRay = 0.4f;               // rays each way, in star widths
    [Min(0f)] public float glintStar = 0.09f;             // its star, in star widths (0: rays only)
    [Header("Glitter surface")]
    public bool surface = true;
    [Min(0.2f)] public float grain = 1f;                  // fleck size, in pixels
    [Range(0f, 1f)] public float surfaceTwinkle = 0.5f;   // flecks catching the light (0: still)
    [Min(0)] public int twinkles = 40;

    public static void Set(Graphic star, bool on)
    {
        if (star == null) return;
        var s = star.GetComponent<StarSparkle>();
        if (on) { if (s == null) s = star.gameObject.AddComponent<StarSparkle>(); s.enabled = true; }
        else if (s != null) s.enabled = false;
    }

    class Bit { public RectTransform root; public Image h, v, star; public float start, len, restUntil, size; }
    class Fleck { public Image img; public float x, y, phase, freq; }

    [System.NonSerialized] RectTransform fx;
    [System.NonSerialized] List<Bit> bits = new List<Bit>(), glints = new List<Bit>();
    [System.NonSerialized] List<Fleck> flecks = new List<Fleck>();
    [System.NonSerialized] float nextGlint, builtSide = -1;
    [System.NonSerialized] StarGraphic shape;

    void OnEnable()
    {
        shape = GetComponent<StarGraphic>();
        if (fx != null) fx.gameObject.SetActive(true);
    }

    void OnDisable()
    {
        if (fx != null) fx.gameObject.SetActive(false);
    }

    float Side { get { var r = ((RectTransform)transform).rect; return Mathf.Min(r.width, r.height); } }

    void Build(float side)
    {
        foreach (Transform old in transform) if (old.name == "StarFx" || old.name == "Sparkle") Destroy(old.gameObject); // ours, a copied star's, or the old look's
        bits = new List<Bit>(); glints = new List<Bit>(); flecks = new List<Fleck>();
        builtSide = side;
        var go = new GameObject("StarFx", typeof(RectTransform), typeof(Canvas), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        fx = (RectTransform)go.transform;
        fx.SetParent(transform, false);
        Stretch(fx);

        // the surface: the glitter picture, cut to the star's shape with a smooth edge (a Mask's edge is jagged), and the
        // twinkling flecks, each inside the star
        if (surface)
        {
            var crt = fx;
            var glitter = NewImage("Glitter", crt, null);
            Stretch(glitter.rectTransform);
            float scale = CanvasScale();
            glitter.sprite = GlitterSprite(Mathf.Clamp(Mathf.RoundToInt(side * scale / 4f) * 4, 8, 256), Mathf.Max(0.4f, grain * scale), shape != null ? shape.scale : 1f, shape != null ? shape.innerRatio : 0.45f);
            int n = Mathf.RoundToInt(twinkles * Mathf.Clamp(side / 80f, 0.3f, 1f));
            for (int i = 0; i < n; i++)
            {
                var at = InStar(side) / side;
                var f = new Fleck { img = NewImage("Fleck", crt, Dot()), x = at.x, y = at.y, phase = Random.Range(0f, 6.3f), freq = Random.Range(1.5f, 4f) };
                f.img.rectTransform.sizeDelta = Vector2.one * Mathf.Max(1f, grain * 1.8f);
                flecks.Add(f);
            }
        }
        int count = Mathf.RoundToInt(specks * Mathf.Clamp(side / 80f, 0.55f, 1.5f));
        for (int i = 0; i < count; i++) bits.Add(NewBit("Speck"));
        nextGlint = Time.unscaledTime + Random.Range(0.2f, glintEvery * 3f);
    }

    Bit NewBit(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(fx, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        var b = new Bit { root = rt, h = NewImage("H", rt, RaySprite()), v = NewImage("V", rt, RaySprite()), star = NewImage("Star", rt, SlimStar()) };
        b.v.rectTransform.localEulerAngles = new Vector3(0, 0, 90);
        go.SetActive(false);
        return b;
    }

    static Image NewImage(string name, Transform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var img = go.GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img.sprite = sprite; img.raycastTarget = false;
        return img;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f); }

    float CanvasScale() { var c = GetComponentInParent<Canvas>(); return c != null ? Mathf.Max(0.5f, c.rootCanvas.scaleFactor) : 1f; }

    // a random point inside the star's shape, from the rect's middle
    Vector2 InStar(float side)
    {
        float outer = side / 2f * (shape != null ? shape.scale : 1f), inner = outer * (shape != null ? shape.innerRatio : 0.45f);
        var c = new Vector2(0, -outer * 0.06f);
        for (int t = 0; t < 30; t++)
        {
            var p = new Vector2(Random.Range(-outer, outer), Random.Range(-outer, outer));
            if (Inside(p, outer, inner)) return p + c;
        }
        return c;
    }

    static bool Inside(Vector2 p, float outer, float inner)
    {
        bool inside = false;
        for (int i = 0, j = 9; i < 10; j = i++)
        {
            Vector2 a = Corner(i, outer, inner), b = Corner(j, outer, inner);
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }
    static Vector2 Corner(int i, float outer, float inner)
    {
        float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (i % 2 == 0 ? outer : inner);
    }

    void Update()
    {
        float side = Side;
        if (side < 4f) return;
        if (fx == null || Mathf.Abs(side - builtSide) > 6f) Build(side);
        float now = Time.unscaledTime;
        bool big = side >= 50f;

        foreach (var f in flecks)
        {
            if (f.img == null) continue;
            float a = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(now * f.freq + f.phase)), 6f) * surfaceTwinkle;
            f.img.rectTransform.anchoredPosition = new Vector2(f.x, f.y) * side;
            f.img.color = new Color(1f, 1f, 1f, a);
        }

        foreach (var b in bits)
        {
            if (b.root == null) continue;
            float t = b.len > 0 ? (now - b.start) / b.len : 2f;
            if (t >= 1f)
            {
                if (b.restUntil <= 0f) { b.restUntil = now + Random.Range(0.05f, rest * 2f); b.root.gameObject.SetActive(false); }
                if (now < b.restUntil) continue;
                b.root.anchoredPosition = InStar(side);
                b.size = Random.Range(0.5f, speckSize * (big ? 1f : 0.64f)) / speckSize;
                b.start = now; b.len = blink * Random.Range(0.5f, 1.5f); b.restUntil = 0f; t = 0f;
                b.root.gameObject.SetActive(true);
            }
            float k = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            Draw(b, side * speckRay * b.size * (0.5f + 0.5f * k), Mathf.Max(0.6f, side * 0.01f), side * speckStar * b.size * k, k);
        }

        if (glintRay > 0f || glintStar > 0f)
        {
            if (now >= nextGlint)
            {
                var g = glints.Find(x => x.root != null && !x.root.gameObject.activeSelf) ?? AddGlint();
                g.root.anchoredPosition = InStar(side);
                g.start = now; g.root.gameObject.SetActive(true);
                nextGlint = now + glintEvery * 3f * Random.Range(0.5f, 1.5f); // three stars share the row's rate
            }
            foreach (var g in glints)
            {
                if (g.root == null || !g.root.gameObject.activeSelf) continue;
                float t = (now - g.start) / glintSeconds;
                if (t >= 1f) { g.root.gameObject.SetActive(false); continue; }
                float k = Mathf.Sin(t * Mathf.PI);
                Draw(g, side * glintRay, Mathf.Max(0.7f, side * 0.014f), side * glintStar * k, k);
            }
        }
    }

    Bit AddGlint() { var g = NewBit("Glint"); g.root.SetAsLastSibling(); glints.Add(g); return g; }

    // a + of thin rays fading toward their ends (reach: each way) and a small slim star in the middle
    static void Draw(Bit b, float reach, float width, float star, float alpha)
    {
        var size = new Vector2(reach * 2f, width);
        b.h.rectTransform.sizeDelta = size; b.v.rectTransform.sizeDelta = size;
        var c = new Color(1f, 1f, 1f, 0.95f * alpha);
        b.h.color = c; b.v.color = c;
        b.star.enabled = star > 0.2f;
        if (b.star.enabled) { b.star.rectTransform.sizeDelta = Vector2.one * star * 2f; b.star.color = new Color(1f, 1f, 1f, Mathf.Sqrt(alpha)); }
    }

    // ---- the pictures, made once

    static Sprite ray, slim, dot;
    static readonly Dictionary<string, Sprite> glitters = new Dictionary<string, Sprite>();

    // a line, full in the middle and fading to nothing at both ends
    static Sprite RaySprite()
    {
        if (ray != null) return ray;
        const int w = 128, h = 4;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / w * 2f - 1f), edge = y == 0 || y == h - 1 ? 0.45f : 1f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(1f - u, 1.6f) * edge));
            }
        tex.Apply();
        return ray = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    // a slim four-point star
    static Sprite SlimStar()
    {
        if (slim != null) return slim;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / n * 2f - 1f), v = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                float d = Mathf.Pow(Mathf.Pow(u, 0.5f) + Mathf.Pow(v, 0.5f), 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) / 0.06f)));
            }
        tex.Apply();
        return slim = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite Dot()
    {
        if (dot != null) return dot;
        const int n = 16;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float r = new Vector2((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f).magnitude;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - r) / 0.3f)));
            }
        tex.Apply();
        return dot = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    // purple flecks, dark to white, packed together (the purple from the palette's purple star), cut to the star's shape
    // as StarGraphic draws it (scale, inner corners, sitting a little low), its edge smoothed by sampling each pixel 4x4
    static Sprite GlitterSprite(int px, float grainPx, float starScale, float innerRatio)
    {
        var p = UIPalette.Current.purpleStar;
        string key = px + "|" + grainPx.ToString("0.00") + "|" + starScale.ToString("0.00") + "|" + innerRatio.ToString("0.00") + "|" + ColorUtility.ToHtmlStringRGB(p);
        if (glitters.TryGetValue(key, out var s) && s != null) return s;
        var shades = new (Color c, float w)[] { (p * 0.62f, 0.22f), (p * 0.78f, 0.2f), (p, 0.18f), (Color.Lerp(p, Color.white, 0.3f), 0.2f), (Color.Lerp(p, Color.white, 0.6f), 0.13f), (Color.white, 0.07f) };
        var pixels = new Color[px * px];
        var baseC = p * 0.9f; baseC.a = 1f;
        for (int i = 0; i < pixels.Length; i++) pixels[i] = baseC;
        int count = Mathf.RoundToInt(px * px / (3.2f * grainPx * grainPx));
        for (int i = 0; i < count; i++)
        {
            float t = Random.value; var col = shades[shades.Length - 1].c;
            foreach (var (c, w) in shades) { t -= w; if (t <= 0f) { col = c; break; } }
            col.a = 1f;
            float a = Random.Range(0.65f, 1f), r = grainPx * Random.Range(0.35f, 1.25f);
            float cx = Random.value * px, cy = Random.value * px;
            for (int y = Mathf.Max(0, (int)(cy - r)); y <= Mathf.Min(px - 1, (int)(cy + r)); y++)
                for (int x = Mathf.Max(0, (int)(cx - r)); x <= Mathf.Min(px - 1, (int)(cx + r)); x++)
                {
                    float d = new Vector2(x + 0.5f - cx, y + 0.5f - cy).magnitude;
                    if (d > r + 0.5f) continue;
                    int idx = y * px + x;
                    pixels[idx] = Color.Lerp(pixels[idx], col, a * Mathf.Clamp01(r - d + 0.5f));
                }
        }
        float outer = px / 2f * starScale, inner = outer * innerRatio, half = px / 2f;
        for (int y = 0; y < px; y++)
            for (int x = 0; x < px; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < 4; sy++)
                    for (int sx = 0; sx < 4; sx++)
                        if (Inside(new Vector2(x + (sx + 0.5f) / 4f - half, y + (sy + 0.5f) / 4f - half + outer * 0.06f), outer, inner)) hit++;
                pixels[y * px + x].a = hit / 16f;
            }
        var tex = new Texture2D(px, px, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(pixels); tex.Apply();
        return glitters[key] = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 100f);
    }
}
