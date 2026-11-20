using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// runs a level's tutorial (the pages are in Tutorials.cs) over the game, once per launch for each level, then its
// coach (TutorialCoach), if it has one, until the player has done what the coach teaches
public class TutorialController : MonoBehaviour
{
    [Tooltip("The overlay is made as this canvas's last child, over every screen")]
    public Canvas canvas;
    public TutorialStyle style = new TutorialStyle();
    [Tooltip("Coach: how long the board stays as it is after a delivery that resets the level, so the player sees it " +
             "(nothing can be clicked meanwhile)")]
    [Min(0f)] public float resetDelay = 1f;
    [Tooltip("Coach: the note after a reset closes by itself after this long (or with its X)")]
    [Min(0f)] public float noteSeconds = 15f;

    readonly HashSet<int> finished = new HashSet<int>(); // levels whose tutorial was seen to the end this launch
    TutorialOverlay overlay;
    Coroutine running;

    GameManager game; // the game the steps on screen are on (handed to what comes after them)

    // the coach of the level that's on (one level is on at a time); every other level's coach waits in `parked`, as
    // it was, until its level loads again. once Done (taught) its guides are down for good, but
    // it still watches (an Undo can still come) until its level next ends; then it's over for the rest of the launch
    TutorialCoach coach;
    int coachLevel = -1;
    bool coachTaught;      // it replied Done
    readonly Dictionary<int, (TutorialCoach coach, bool taught)> parked = new(); // by level: coaches whose level isn't on
    GameManager coachGame; // set while the coach is listening (its level is on)
    Coroutine resetting;   // a Reset waiting to reload the level
    Coroutine undoing;     // an Undo waiting to take the placement back
    [Tooltip("Coach: how long the wrong way shows before it's undone (nothing can be clicked meanwhile)")]
    [Min(0f)] public float undoDelay = 0.4f;
    bool noteOnResume;     // the level is reloading for a Reset: show the note once it's set up again

    // a tutorial's level-end steps (Tutorials.EndStepsFor): waiting for its level to end, then the steps
    readonly HashSet<int> endOwed = new(); // levels whose level-end steps haven't played yet: they wait again on each load
    GameManager endGame; // set while waiting
    int endLevel = -1;
    GameManager endPlaying; // the game its steps are on, to let go of if they're cut short
    const float EndStart = 0.6f; // before its first step: the last delivery shows first
    // how far over what scored them a step's points (TutorialStep.pops) float: clear of the frame over a ring or a slot
    const float PopAbove = 14f;


    // starts the level's tutorial, unless it has none or it was already finished since the game started.
    // the steps get the game, so they can check it or set it up (e.g. GameManager.ShowRoadLine).
    // called on every level load: a coach for this level is set up again here
    public void TryStart(int levelIndex, GameManager game)
    {
        Stop();
        if (parked.Remove(levelIndex, out var p)) // its coach, as it was left
        {
            coach = p.coach;
            coachLevel = levelIndex;
            coachTaught = p.taught;
            ResumeCoach(game);
        }
        if (endOwed.Contains(levelIndex)) WatchForEnd(game, levelIndex); // a restart doesn't lose them
        if (finished.Contains(levelIndex)) return;
        var steps = Tutorials.For(Levels.All[levelIndex], game); // the level names its tutorial (LevelData.tutorial)
        if (steps == null) return;
        if (canvas == null) { Debug.LogError("TutorialController needs its Canvas set."); return; }
        if (overlay == null) overlay = TutorialOverlay.Create(canvas, style); // made even with no steps: a coach's or level-end steps need it
        if (steps.Count == 0) { AfterSteps(levelIndex, game); return; } // no steps at the start: what comes after them starts now
        this.game = game;
        running = StartCoroutine(Run(steps, g => AfterSteps(levelIndex, g)));
    }

    // the tutorial's steps ended (or it has none at the start): it counts as seen, and its coach takes over; with none,
    // its level-end steps (if any) wait for the level's end from now
    void AfterSteps(int levelIndex, GameManager g)
    {
        finished.Add(levelIndex);
        if (!StartCoach(levelIndex, g) && Tutorials.HasEnd(Levels.All[levelIndex])) WatchForEnd(g, levelIndex);
    }

    // the Tutorial button (GameManager.ReplayTutorial): the level's tutorial counts as never seen, and its coach and
    // level-end steps, if they're on or owed, are dropped, so the next TryStart runs it all from the start
    public void Forget(int levelIndex)
    {
        finished.Remove(levelIndex);
        if (coach != null && coachLevel == levelIndex) EndCoach();
        parked.Remove(levelIndex);
        endOwed.Remove(levelIndex);
        CancelEnd();
    }

    // leaving the level (or loading it again): the tutorial goes away and counts as not finished; a coach pauses, and
    // so does the level-end steps' wait (TryStart picks it up again on the level's next load)
    public void Stop()
    {
        if (running != null) StopCoroutine(running);
        running = null;
        game = null;
        PauseCoach();
        if (coach != null) // it waits for its level to load again
        {
            parked[coachLevel] = (coach, coachTaught);
            coach = null;
            coachLevel = -1;
            coachTaught = false;
        }
        CancelEnd();
        if (overlay != null) overlay.Hide();
    }

    // each step in turn, until the player presses its Next, then `after` with the game (the tutorial's: its coach
    // starts). Back (on a backable step) shows the one before again, without its delay. one coroutine all through, so
    // Stop stops all of it
    IEnumerator Run(List<TutorialStep> steps, System.Action<GameManager> after)
    {
        bool cameBack = false; // this step shows again after a Back: its delay was waited for the first time
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.delay > 0 && !cameBack)
            {
                overlay.Block();
                yield return new WaitForSeconds(step.delay);
            }
            cameBack = false;
            step.onShow?.Invoke();
            if (step.pops != null) foreach (var p in step.pops) overlay.ShowPop(p.text, PopAbove, p.near);
            bool canSkip = SkipFrom(steps, i);
            bool canBack = step.backable && i > 0;
            overlay.Show(step, canSkip, canBack);
            yield return null; // the click that ended the last step can't end this one too
            bool skipped = false, back = false;
            while (!overlay.ConsumeNext())
            {
                if (canSkip && overlay.ConsumeSkip()) { skipped = true; break; }
                if (canBack && overlay.ConsumeBack()) { back = true; break; }
                yield return null;
            }
            step.onDone?.Invoke(); // what it set up goes either way: Back shows the step before from its own onShow
            if (step.pops != null) overlay.ClearPops();
            if (back)
            {
                i -= 2; // the loop's i++ lands on the step before
                cameBack = true;
                continue;
            }

            // Skip: the rest of the steps go by unseen. each still runs its onShow and onDone, so what they set up and
            // undo stays in balance (an end window held, say, is let go)
            if (skipped)
                while (i + 1 < steps.Count)
                {
                    i++;
                    steps[i].onShow?.Invoke();
                    steps[i].onDone?.Invoke();
                }
        }
        var g = game;
        overlay.Hide();
        game = null;
        running = null;
        after?.Invoke(g);
    }

    // ---- the coach ----

    // the steps just ended: the level's coach, if its tutorial has one, takes over (false: it has none)
    bool StartCoach(int levelIndex, GameManager g)
    {
        var c = Tutorials.CoachFor(Levels.All[levelIndex], g);
        if (c == null) return false;
        PauseCoach(); // one for this level already on gives way (others wait in `parked`, untouched)
        parked.Remove(levelIndex);
        coach = c;
        coachLevel = levelIndex;
        coachTaught = false;
        noteOnResume = false;
        ResumeCoach(g);
        return true;
    }

    // its level is on (the steps just ended, or the level loaded again): it listens and puts its guides up (not once
    // it's taught: they're down for good)
    void ResumeCoach(GameManager g)
    {
        coachGame = g;
        g.ActionDone += OnCoachAction;
        if (!coachTaught) coach.onLoaded?.Invoke();
        if (!coachTaught && coach.removalSteps != null) g.onRemoval = OnRemoval;
        if (!coachTaught && coach.doorSteps != null) g.onDoorQuestion = OnDoorQuestion;
        if (noteOnResume && !string.IsNullOrEmpty(coach.resetNote)) overlay.ShowNote(coach.resetNote, noteSeconds, coach.noteAbove);
        noteOnResume = false;
    }

    // its level is left or loads again: it stops listening (the board, and its guides, are gone or about to be)
    void PauseCoach()
    {
        if (coachGame != null)
        {
            coachGame.ActionDone -= OnCoachAction;
            coachGame.onRemoval = null;
            coachGame.onDoorQuestion = null;
        }
        coachGame = null;
        if (resetting != null) StopCoroutine(resetting); // a reset cut short by leaving (LoadLevel lets the level end go)
        resetting = null;
        if (undoing != null) StopCoroutine(undoing);     // an undo too
        undoing = null;
    }

    // over for the rest of the launch
    void EndCoach()
    {
        PauseCoach();
        coach = null;
        coachLevel = -1;
        coachTaught = false;
    }

    void OnCoachAction(GameAction action)
    {
        if (coach == null) return;
        if (action.kind == GameActionKind.LevelEnd && coachTaught) { EndCoach(); return; } // its level is through
        if (coach.onAction == null) return;
        switch (coach.onAction(action))
        {
            case CoachReply.Reset:
                if (resetting == null && undoing == null) resetting = StartCoroutine(ResetLevel());
                break;
            case CoachReply.Undo:
                if (resetting == null && undoing == null) undoing = StartCoroutine(UndoWrongWay(action));
                break;
            case CoachReply.Done:
                if (coachTaught) break;
                coachTaught = true;
                var g = coachGame;
                g.onRemoval = null; // taught: tiles break without a word from now on
                g.onDoorQuestion = null; // and doors' arrows show without one
                var after = coach.doneSteps;
                if (Tutorials.HasEnd(Levels.All[coachLevel])) WatchForEnd(g, coachLevel);
                overlay.HideNotes();
                if (after != null && after.Count > 0) // its steps, shown as the tutorial's are
                {
                    game = g;
                    running = StartCoroutine(Run(after, null));
                }
                break;
        }
    }

    // a delivered object's tile just broke, the deliveries waiting on the answer: the coach's steps for it, if it has
    // any, show now, and the deliveries go on when they end (true: hold them till then)
    bool OnRemoval(Arrival arrival, string[] together)
    {
        var steps = coach?.removalSteps?.Invoke(arrival, together);
        if (steps == null || steps.Count == 0) return false;
        var g = coachGame;
        if (running != null) StopCoroutine(running);
        game = g;
        running = StartCoroutine(Run(steps, done => done.ReleaseRemoval()));
        return true;
    }

    // an object coming out of a door just got its arrows: the coach's steps for it, if it has any, show over them (the
    // arrows can't be clicked till they end; the game waits for that click anyway)
    void OnDoorQuestion(string objectName, string door)
    {
        var steps = coach?.doorSteps?.Invoke(objectName, door);
        if (steps == null || steps.Count == 0) return;
        if (running != null) StopCoroutine(running);
        game = coachGame;
        running = StartCoroutine(Run(steps, null));
    }

    // the slip (a wrong way, a tile in the wrong spot) shows for Undo Delay, with the level held from ending (a wrong
    // way can leave everyone blocked) and nothing clickable; then the placement is undone, as with the Undo button,
    // and the coach's note for it shows (over any note already there), or, the first time, its firstUndoSteps. Undo is
    // called from here, not from inside the round that reported the action
    IEnumerator UndoWrongWay(GameAction action)
    {
        var g = coachGame;
        var note = coach?.undoNoteFor?.Invoke(action) ?? coach?.undoNote;
        var above = coach?.noteAbove;
        g.HoldLevelEnd(true);
        overlay.Block();
        yield return new WaitForSecondsRealtime(undoDelay);
        g.UndoPlacement(); // the whole placement, whatever undo by step is set to
        g.HoldLevelEnd(false);
        // the coach's first undo: its steps in the middle of the screen in place of the note, once the placement is
        // undone (so nothing moves under them). `undoing` stays set till they end, so no other reset or undo starts
        // a slip of its own kind (TutorialCoach.firstUndoStepsFor) has its own first steps, apart from the usual ones
        var own = coach?.firstUndoStepsFor?.Invoke(action);
        var first = coach == null ? null
            : own != null ? (coach.firstUndoShownFor.Contains(own) ? null : own)
            : !coach.firstUndoShown ? coach.firstUndoSteps : null;
        if (first != null && first.Count > 0)
        {
            if (own != null) coach.firstUndoShownFor.Add(own);
            else coach.firstUndoShown = true;
            if (running != null) StopCoroutine(running);
            game = g;
            running = StartCoroutine(Run(first, done => undoing = null));
            yield break;
        }
        undoing = null;
        overlay.Unblock();
        if (!string.IsNullOrEmpty(note)) overlay.ShowNote(note, noteSeconds, above);
    }

    // ---- the level-end steps ----

    // waits for the level to end (GameManager reports LevelEnd as it does, before the end window's wait). owed from now
    // until they play: a level end with someone left behind, or a restart, doesn't use them up
    void WatchForEnd(GameManager g, int levelIndex)
    {
        CancelEnd();
        endOwed.Add(levelIndex);
        endGame = g;
        endLevel = levelIndex;
        g.ActionDone += OnEndAction;
    }

    // left, or loaded again, before it ends or while its steps show (Stop stops them): nothing more of them
    void CancelEnd()
    {
        if (endGame != null) endGame.ActionDone -= OnEndAction;
        endGame = null;
        if (endPlaying != null)
        {
            endPlaying.hud.HoldScoreHistory(false);
            endPlaying.HoldEndWindow(false);
        }
        endPlaying = null;
    }

    // the level ended: with everyone delivered, the tutorial's level-end steps (Tutorials.EndStepsFor) show while the
    // end window waits, with the score's history held open in full
    void OnEndAction(GameAction action)
    {
        if (action.kind != GameActionKind.LevelEnd) return;
        var g = endGame;
        int levelIndex = endLevel;
        CancelEnd();
        if (action.delivered < Levels.All[levelIndex].objects.Count)
        {
            WatchForEnd(g, levelIndex); // not this time: the Undo in the end window, or a restart, can still get there
            return;
        }
        endOwed.Remove(levelIndex); // they play now, once
        var steps = Tutorials.EndStepsFor(Levels.All[levelIndex], g); // made now, so they can use the end's state
        if (steps == null || steps.Count == 0) return;
        if (steps[0].delay <= 0) steps[0].delay = EndStart;
        steps[steps.Count - 1].buttonText ??= "End"; // the end window comes next
        g.HoldEndWindow(true);
        g.hud.HoldScoreHistory(true);
        endPlaying = g;
        if (running != null) StopCoroutine(running);
        game = g; // shown as the tutorial's steps are
        running = StartCoroutine(Run(steps, done =>
        {
            done.hud.HoldScoreHistory(false);
            endPlaying = null;
            done.HoldEndWindow(false); // the end window shows now
        }));
    }

    // the delivery that called for the reset stays on screen for Reset Delay, with the level held from ending and
    // nothing clickable; then the level loads again, and the coach comes back with its note (TryStart, ResumeCoach).
    // the first time, its firstResetSteps show first, and the level loads again without the note once they end
    IEnumerator ResetLevel()
    {
        var g = coachGame;
        g.HoldLevelEnd(true);
        overlay.Block();
        yield return new WaitForSecondsRealtime(resetDelay);
        // the coach's first reset: its steps in the middle of the screen in place of the note, and the level loads
        // again once they end (no note then). the level end stays held, and `resetting` stays set till then, so a
        // delivery meanwhile starts no other reset
        var first = coach != null && !coach.firstResetShown ? coach.firstResetSteps : null;
        if (first != null && first.Count > 0)
        {
            coach.firstResetShown = true;
            if (running != null) StopCoroutine(running);
            game = g;
            running = StartCoroutine(Run(first, done =>
            {
                resetting = null;
                done.ReloadLevel();
            }));
            yield break;
        }
        resetting = null; // before the reload, whose Stop would otherwise stop this coroutine mid-way
        noteOnResume = true;
        g.ReloadLevel();
    }

    // a skippable step gets Skip unless it's the last (there Next does the same): Skip passes all the steps after it
    static bool SkipFrom(List<TutorialStep> steps, int i) => steps[i].skippable && i + 1 < steps.Count;
}
