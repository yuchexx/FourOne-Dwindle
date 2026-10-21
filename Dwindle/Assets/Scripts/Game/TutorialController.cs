using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// runs a level's tutorial (the pages are in Tutorials.cs) over the game, once per launch for each level
public class TutorialController : MonoBehaviour
{
    [Tooltip("The overlay is made as this canvas's last child, over every screen")]
    public Canvas canvas;
    public TutorialStyle style = new TutorialStyle();

    readonly HashSet<int> finished = new HashSet<int>(); // levels whose tutorial was seen to the end this launch
    TutorialOverlay overlay;
    Coroutine running;

    public bool Running => running != null;

    // starts the level's tutorial, unless it has none or it was already finished since the game started
    public void TryStart(int levelIndex)
    {
        Stop();
        if (finished.Contains(levelIndex)) return;
        var steps = Tutorials.For(levelIndex);
        if (steps == null || steps.Count == 0) return;
        if (canvas == null) { Debug.LogError("TutorialController needs its Canvas set."); return; }
        if (overlay == null) overlay = TutorialOverlay.Create(canvas, style);
        running = StartCoroutine(Run(levelIndex, steps));
    }

    // leaving the level: the tutorial goes away and counts as not finished
    public void Stop()
    {
        if (running != null) StopCoroutine(running);
        running = null;
        if (overlay != null) overlay.Hide();
    }

    IEnumerator Run(int levelIndex, List<TutorialStep> steps)
    {
        foreach (var step in steps)
        {
            if (step.delay > 0)
            {
                overlay.Block();
                yield return new WaitForSeconds(step.delay);
            }
            overlay.Show(step);
            yield return null; // the click that ended the last step can't end this one too
            while (!StepDone(step)) yield return null;
        }
        overlay.Hide();
        finished.Add(levelIndex);
        running = null;
    }

    bool StepDone(TutorialStep step) => step.advance switch
    {
        Advance.NextButton => overlay.ConsumeNext(),
        Advance.ClickInArea => overlay.ReleasedInArea(),
        _ => step.condition == null || step.condition(),
    };

#if UNITY_EDITOR
    // for writing areas: select a UI object in the Hierarchy (in Play mode for the ones made from code),
    // then pick this from this component's menu. prints the object's area, ready to paste into Tutorials.cs
    [ContextMenu("Log Selected Area")]
    void LogSelectedArea()
    {
        var target = UnityEditor.Selection.activeTransform as RectTransform;
        if (target == null || canvas == null)
        {
            Debug.Log("Select a UI object in the Hierarchy first (and set this component's Canvas).");
            return;
        }

        var root = (RectTransform)canvas.transform;
        var corners = new Vector3[4];
        target.GetWorldCorners(corners); // bottom-left, top-left, top-right, bottom-right
        Vector2 min = (Vector2)root.InverseTransformPoint(corners[0]) - root.rect.min;
        Vector2 max = (Vector2)root.InverseTransformPoint(corners[2]) - root.rect.min;

        // from the canvas's bottom-left in its own units, to the top-left in design units
        var size = root.rect.size;
        var design = TutorialOverlay.DesignSize(canvas);
        float sx = design.x / size.x, sy = design.y / size.y;
        Debug.Log($"area = new Rect({min.x * sx:0}, {(size.y - max.y) * sy:0}, {(max.x - min.x) * sx:0}, {(max.y - min.y) * sy:0}), // {target.name}");
    }
#endif
}
