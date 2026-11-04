using UnityEngine;
using UnityEngine.UI;

// makes a Toggle look like an on/off switch
[RequireComponent(typeof(Toggle))]
public class SwitchToggle : MonoBehaviour
{
    public Image track;
    public RectTransform knob;
    [Tooltip("Seconds for the knob to slide across")]
    [Min(0f)] public float slideSeconds = 0.12f;

    Toggle toggle;
    float at = -1; // 0 = off, 1 = on; -1 = not placed yet

    void Awake() => toggle = GetComponent<Toggle>();

    void OnEnable() => at = -1; // opening the panel shows the switch where it is, without sliding

    void Update()
    {
        float target = toggle.isOn ? 1f : 0f;
        at = at < 0 || slideSeconds <= 0 ? target : Mathf.MoveTowards(at, target, Time.unscaledDeltaTime / slideSeconds);
        float travel = (((RectTransform)track.transform).rect.width - knob.rect.width) / 2f - 4f;
        knob.anchoredPosition = new Vector2(Mathf.Lerp(-travel, travel, at), 0);
        track.color = Color.Lerp(UIPalette.Current.button, UIPalette.Current.accent, at); // the palette's Button off, Accent on
    }
}
