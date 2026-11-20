using TMPro;
using UnityEngine;

// Follow the visible title, after the existing Tutorial button has been positioned.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(RectTransform))]
public class RestartButtonLayout : MonoBehaviour
{
    public TMP_Text levelTitle;
    [Min(0f)] public float gap = 20f;

    readonly Vector3[] corners = new Vector3[4];

    void LateUpdate()
    {
        if (levelTitle == null || !levelTitle.gameObject.activeInHierarchy) return;

        var button = (RectTransform)transform;
        var parent = button.parent;
        var title = levelTitle.rectTransform;
        levelTitle.ForceMeshUpdate();

        var edge = parent.InverseTransformPoint(title.TransformPoint(
            new Vector3(levelTitle.textBounds.max.x, title.rect.center.y, 0f)));

        // GameManager creates this button at runtime on tutorial levels.
        var tutorial = title.parent.Find("TutorialButton") as RectTransform;
        if (tutorial != null && tutorial.gameObject.activeInHierarchy)
        {
            tutorial.GetWorldCorners(corners);
            edge.x = Mathf.Max(edge.x, parent.InverseTransformPoint(corners[2]).x);
        }

        var size = button.rect.size;
        var scale = button.localScale;
        button.localPosition = new Vector3(
            edge.x + gap + button.pivot.x * size.x * scale.x,
            edge.y + (button.pivot.y - 0.5f) * size.y * scale.y,
            button.localPosition.z);
    }
}
