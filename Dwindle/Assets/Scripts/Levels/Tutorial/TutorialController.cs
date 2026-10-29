using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// runs a level's tutorial (the pages are in Tutorials.cs) over the game, once per launch for each level
public class TutorialController : MonoBehaviour
{
    [Tooltip("The overlay is made as this canvas's last child, over every screen")]
    public Canvas canvas;
    public TutorialStyle style = new TutorialStyle();
    [Tooltip("Action steps: a click inside the area that the game doesn't act on shakes the frames after this long " +
             "(some actions, like a junction's arrow, reach the game a frame or two after the click)")]
    [Min(0f)] public float missSeconds = 0.2f;

    readonly HashSet<int> finished = new HashSet<int>(); // levels whose tutorial was seen to the end this launch
    TutorialOverlay overlay;
    Coroutine running;

    GameManager game;     // reports the player's actions (GameManager.ActionDone) while a tutorial runs
    TutorialStep current; // the step on screen
    bool actionMatched;   // the game reported the action the current step waits for
    float missSince = -1; // when a click inside the area went unanswered by the game; -1 = none pending

    public bool Running => running != null;

    // starts the level's tutorial, unless it has none or it was already finished since the game started.
    // the steps get the game, so they can check it or set it up (e.g. GameManager.ForceNextRemoval)
    public void TryStart(int levelIndex, GameManager game)
    {
        Stop();
        if (finished.Contains(levelIndex)) return;
        var steps = Tutorials.For(Levels.All[levelIndex], game); // the level names its tutorial (LevelData.tutorial)
        if (steps == null || steps.Count == 0) return;
        if (canvas == null) { Debug.LogError("TutorialController needs its Canvas set."); return; }
        if (overlay == null) overlay = TutorialOverlay.Create(canvas, style);
        this.game = game;
        game.ActionDone += OnAction;
        running = StartCoroutine(Run(levelIndex, steps));
    }

    // leaving the level: the tutorial goes away and counts as not finished
    public void Stop()
    {
        if (running != null) StopCoroutine(running);
        running = null;
        StopListening();
        if (overlay != null) overlay.Hide();
    }

    void StopListening()
    {
        if (game != null)
        {
            game.ActionDone -= OnAction;
            game.LockInput(false); // never leave the game locked behind a tutorial that's gone
        }
        game = null;
        current = null;
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
            current = step;
            actionMatched = false;
            missSince = -1;
            step.onShow?.Invoke();
            game.LockInput(step.hoverOnly); // a hover-only step lets the pointer through but not the clicks
            overlay.Show(step);
            yield return null; // the click that ended the last step can't end this one too
            while (!StepDone(step)) yield return null;
            game.LockInput(false);
            step.onDone?.Invoke();
        }
        overlay.Hide();
        StopListening();
        finished.Add(levelIndex);
        running = null;
    }

    bool StepDone(TutorialStep step) => step.advance switch
    {
        Advance.NextButton => overlay.ConsumeNext(),
        Advance.ClickInArea => overlay.ReleasedInArea(),
        Advance.Action => ActionStepDone(),
        _ => step.condition == null || step.condition(),
    };

    // the game reported a player action: does the step on screen wait for it?
    void OnAction(GameAction action)
    {
        if (current != null && current.advance == Advance.Action && current.action != null && current.action(action))
            actionMatched = true;
    }

    // done once the game reports the step's action. a click inside the area that the game doesn't act on
    // (it missed the button, say) shakes the frames, so the player sees it didn't count
    bool ActionStepDone()
    {
        if (actionMatched) return true;
        if (overlay.ReleasedInArea()) missSince = Time.unscaledTime;
        if (missSince >= 0 && Time.unscaledTime - missSince >= missSeconds)
        {
            missSince = -1;
            overlay.Shake();
        }
        return false;
    }

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
