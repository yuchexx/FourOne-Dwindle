using TMPro;
using UnityEngine;
using UnityEngine.UI;

// add optional matching labels without changing puzzle behavior
public sealed class ObjectIdentity : MonoBehaviour
{
    RectTransform badge;
    TMP_Text label;
    public static string LabelFor(int index)
    {
        string result = "";
        do { result = (char)('A' + index % 26) + result; index = index / 26 - 1; } while (index >= 0);
        return result;
    }
    public static void Attach(GameObject owner, int index)
    {
        var identity = owner.AddComponent<ObjectIdentity>();
        var background = new GameObject("ObjectLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(CircleGraphic)).GetComponent<CircleGraphic>();
        background.transform.SetParent(owner.transform, false);
        background.color = Color.white; background.raycastTarget = false;
        identity.badge = background.rectTransform;
        var outline = background.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black; outline.effectDistance = Vector2.one;
        var text = UIBuilder.NewText("Letter", background.transform, LabelFor(index), 16, Color.black);
        UIBuilder.Fill(text.rectTransform);
        identity.label = text;
        text.alignment = TextAlignmentOptions.Center; text.fontStyle = FontStyles.Bold;
        identity.Refresh();
    }
    void OnEnable() => UIPalette.Changed += Refresh;
    void OnDisable() => UIPalette.Changed -= Refresh;
    void Refresh() { if (badge != null) badge.gameObject.SetActive(Session.objectSymbols); }
    void LateUpdate()
    {
        if (badge == null || !badge.gameObject.activeSelf) return;
        var rect = (RectTransform)transform;
        float side = Mathf.Clamp(rect.rect.width * .36f, 18f, 30f);
        badge.sizeDelta = Vector2.one * side;
        // keep labels upright and clear of direction and blocked markers
        badge.rotation = Quaternion.identity;
        var offset = new Vector3(rect.rect.width * .4f, -rect.rect.height * .4f, 0f);
        badge.position = rect.position + rect.parent.TransformVector(offset * rect.localScale.x);
        label.fontSize = side * .68f;
    }
}
