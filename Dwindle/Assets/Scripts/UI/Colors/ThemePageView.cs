using TMPro;
using UnityEngine;
using UnityEngine.UI;

// build aligned appearance controls with separate saved preferences
public sealed class ThemePageView : MonoBehaviour
{
    public ThemeColorSettings colors;
    public SettingsView settings;
    Button dark, lightButton;
    Toggle contrast, symbols;
    RectTransform content;
    readonly System.Collections.Generic.List<(Graphic graphic, UIPalette.Role role)> graphics = new();
    bool dirty;

    void Awake()
    {
        var theme = (RectTransform)colors.transform;
        theme.anchoredPosition = new Vector2(75, 108);
        Place((RectTransform)theme.Find("ThemeColorHeading"), 0, 32, 500, 36);
        var themeHeading = theme.Find("ThemeColorHeading").GetComponent<TMP_Text>();
        themeHeading.fontSize = 20f;
        themeHeading.alignment = TextAlignmentOptions.MidlineLeft;
        graphics.Add((theme.Find("ThemeColorHeading").GetComponent<TMP_Text>(), UIPalette.Role.Text));
        if (colors.selectedColorText != null) colors.selectedColorText.gameObject.SetActive(false);
        string[] names = { "PurpleTheme", "BlueTheme", "TealTheme", "RoseTheme" };
        for (int i = 0; i < names.Length; i++)
        {
            theme.Find(names[i]).GetComponentInChildren<TMP_Text>(true).fontSize = 18f;
            Place((RectTransform)theme.Find(names[i]), -198 + i * 116, -15, 104, 36);
        }

        content = new GameObject("AppearanceOptions", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(transform, false);
        Place(content, 75, 0, 500, 580);
        Label("ModeLabel", content, "Interface", -158, 20, 184, 32).alignment = TextAlignmentOptions.MidlineLeft;
        dark = Choice("Dark", 82, 20, () => Session.lightMode = false);
        lightButton = Choice("Light", 198, 20, () => Session.lightMode = true);
        var modeHelp = Label("ModeHelp", content, "Choose light or dark surfaces", -70, -9, 360, 24);
        modeHelp.alignment = TextAlignmentOptions.MidlineLeft;
        modeHelp.fontSize = 16;
        graphics.Add((modeHelp, UIPalette.Role.MutedText));
        contrast = Setting("High contrast", "Clearer text and control boundaries", -69, on => Session.highContrast = on);
        symbols = Setting("Object labels", "Match letters on objects and destinations", -144, on => Session.objectSymbols = on);
        Image("Divider", content, UIPalette.Role.Outline, 0, -190, 500, 1);
        var star = new GameObject("PurpleStar", typeof(RectTransform), typeof(CanvasRenderer), typeof(StarGraphic)).GetComponent<StarGraphic>();
        star.transform.SetParent(content, false);
        Place(star.rectTransform, -230, -225, 28, 28);
        star.raycastTarget = false;
        graphics.Add((star, UIPalette.Role.PurpleStar));
        var note = Label("GameplayNote", content, "Stars and hints stay purple.", -15, -225, 370, 32);
        note.alignment = TextAlignmentOptions.MidlineLeft;
        note.fontSize = 20;
    }

    void OnEnable() { UIPalette.Changed += RefreshLater; dirty = true; }
    void OnDisable() => UIPalette.Changed -= RefreshLater;
    void RefreshLater() => dirty = true;
    void LateUpdate()
    {
        if (!dirty) return;
        dirty = false;
        var p = UIPalette.Current;
        foreach (var item in graphics) if (item.graphic != null) item.graphic.color = p.Get(item.role);
        Mark(dark, !Session.lightMode); Mark(lightButton, Session.lightMode);
        contrast.SetIsOnWithoutNotify(Session.highContrast);
        symbols.SetIsOnWithoutNotify(Session.objectSymbols);
        var swatches = new[] { "PurpleTheme", "BlueTheme", "TealTheme", "RoseTheme" };
        for (int i = 0; i < swatches.Length; i++)
        {
            var button = colors.transform.Find(swatches[i]).GetComponent<Button>();
            bool selected = (int)Session.themeColor == i;
            var border = button.GetComponent<Outline>();
            if (border == null) border = button.gameObject.AddComponent<Outline>();
            border.effectDistance = Vector2.one * (selected ? 3f : 1f);
            border.effectColor = selected ? p.text : Color.clear;
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                label.color = UIPalette.BestText(button.targetGraphic.color);
                label.fontStyle = selected ? FontStyles.Bold | FontStyles.Underline : FontStyles.Normal;
            }
        }
    }

    void Mark(Button button, bool selected)
    {
        button.targetGraphic.color = selected ? UIPalette.Current.accent : UIPalette.Current.button;
        button.GetComponentInChildren<TMP_Text>().color = selected ? UIPalette.Current.accentText : UIPalette.Current.text;
        var border = button.GetComponent<Outline>();
        border.effectColor = selected || UIPalette.HighContrast ? UIPalette.Current.outline : Color.clear;
    }
    Button Choice(string name, float x, float y, UnityEngine.Events.UnityAction action)
    {
        var img = Image(name, content, UIPalette.Role.Button, x, y, 104, 36);
        img.raycastTarget = true;
        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(action);
        img.gameObject.AddComponent<Outline>().effectDistance = Vector2.one * 2;
        Label(name + "Label", img.transform, name, 0, 0, 100, 34).fontSize = 18f;
        return button;
    }
    Toggle Setting(string title, string description, float y, UnityEngine.Events.UnityAction<bool> action)
    {
        var label = Label(title, content, title, -70, y + 7, 360, 30);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        var hint = Label(title + "Help", content, description, -70, y - 17, 360, 24);
        hint.alignment = TextAlignmentOptions.MidlineLeft; hint.fontSize = 16;
        graphics.Add((hint, UIPalette.Role.MutedText));
        var toggle = Switch(title + "Switch", content, 215, y, true);
        toggle.onValueChanged.AddListener(action);
        return toggle;
    }
    Toggle Switch(string name, Transform parent, float x, float y, bool interactive)
    {
        var img = Image(name, parent, UIPalette.Role.Button, x, y, 62, 30);
        if (settings != null && settings.undoAnimationToggle != null)
        {
            var existing = settings.undoAnimationToggle.GetComponent<SwitchToggle>();
            if (existing != null && existing.track != null) { img.sprite = existing.track.sprite; img.type = existing.track.type; }
        }
        img.raycastTarget = interactive;
        var knob = new GameObject("Knob", typeof(RectTransform), typeof(CanvasRenderer), typeof(CircleGraphic)).GetComponent<CircleGraphic>();
        knob.transform.SetParent(img.transform, false); knob.raycastTarget = false;
        Place(knob.rectTransform, 0, 0, 24, 24);
        var toggle = img.gameObject.AddComponent<Toggle>();
        toggle.transition = Selectable.Transition.None;
        toggle.targetGraphic = img; toggle.isOn = !interactive;
        toggle.interactable = interactive;
        var nav = toggle.navigation; nav.mode = interactive ? Navigation.Mode.Automatic : Navigation.Mode.None; toggle.navigation = nav;
        var view = img.gameObject.AddComponent<SwitchToggle>();
        view.track = img; view.knob = knob.rectTransform;
        return toggle;
    }
    Image Image(string name, Transform parent, UIPalette.Role role, float x, float y, float w, float h)
    {
        var image = UIBuilder.NewImage(name, parent, UIPalette.Current.Get(role));
        Place(image.rectTransform, x, y, w, h); graphics.Add((image, role)); return image;
    }
    TMP_Text Label(string name, Transform parent, string text, float x, float y, float w, float h)
    {
        var label = UIBuilder.NewText(name, parent, text, 22, UIPalette.Current.text, colors.selectedColorText != null ? colors.selectedColorText.font : null);
        Place(label.rectTransform, x, y, w, h); label.alignment = TextAlignmentOptions.Center;
        graphics.Add((label, UIPalette.Role.Text)); return label;
    }
    static void Place(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h);
    }
}
