using TMPro;
using UnityEngine;

// Keeps the Restart button just left of the score's visible text (top right), its middle level with the level title's
// (where the Tutorial button's middle is too). The score's width changes with its digits, so this follows it every frame.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(RectTransform))]
public class RestartButtonLayout : MonoBehaviour
{
    [Min(0f)] public float gap = 20f;

    TMP_Text score, title; // HudView's score and level title texts, found once

    void LateUpdate()
    {
        if (score == null)
        {
            var hud = FindAnyObjectByType<HudView>();
            if (hud == null || hud.scoreText == null || hud.levelTitleText == null) return;
            score = hud.scoreText;
            title = hud.levelTitleText;
        }
        if (!score.gameObject.activeInHierarchy) return;

        var button = (RectTransform)transform;
        var parent = button.parent;
        var text = score.rectTransform;
        score.ForceMeshUpdate();

        // the left end of the score's text, and the middle of the title's box (the Tutorial button's), in the button's parent
        float left = score.textBounds.size.x > 0 ? score.textBounds.min.x : text.rect.xMin; // no text yet: its box
        var edge = parent.InverseTransformPoint(text.TransformPoint(new Vector3(left, 0f, 0f)));
        edge.y = parent.InverseTransformPoint(title.rectTransform.TransformPoint(title.rectTransform.rect.center)).y;

        var size = button.rect.size;
        var scale = button.localScale;
        button.localPosition = new Vector3(
            edge.x - gap - (1f - button.pivot.x) * size.x * scale.x,
            edge.y + (button.pivot.y - 0.5f) * size.y * scale.y,
            button.localPosition.z);
    }
}
