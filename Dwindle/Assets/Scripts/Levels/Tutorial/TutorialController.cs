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
    Coroutine undoing;     // an Undo waiting to take the placement back
    const float UndoDelay = 0.4f; // how long a slip shows before it's undone (nothing can be clicked meanwhile)

    // a tutorial's level-end steps (Tutorials.EndStepsFor): waiting for its level to end, then the steps
    readonly HashSet<int> endOwed = new(); // levels whose level-end steps haven't played yet: they wait again on each load
    GameManager endGame; // set while waiting
    int endLevel = -1;
    GameManager endPlaying; // the game its steps are on, to let go of if they're cut short
    const float EndStart = 0.6f; // before its first step: the last delivery shows first
    // how far over what scored them a step's points (TutorialStep.pops) float: clear of the frame over a ring or a slot
    const float PopAbove = 14f;


    // starts the level's tutorial, unless it has none or it was already finished since the game started.
    // the steps get the game, so they can check it or set it up.
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
        running = StartCoroutine(Run(steps, g =>
        {
            // a tutorial that's only one popup (no coach, no level-end steps): once it's closed, the button can bring it back
            if (steps.Count == 1 && Tutorials.StepsOnly(Levels.All[levelIndex])) g.EarnTutorialButton();
            AfterSteps(levelIndex, g);
        }));
    }

    // a "do it" step stopped part-way (a restart, leaving, or other steps taking over): its listener goes and any level
    // end it was holding is let go, so nothing of it carries into the next run
    GameManager doItGame; Action<GameAction> doItHandler;
    void DropDoIt()
    {
        if (doItGame != null && doItHandler != null) { doItGame.ActionDone -= doItHandler; doItGame.ClearLevelEndHold(); }
        doItGame = null; doItHandler = null;
    }

    // steps shown now, in the middle of a level (a guided level's coach moving to its next stage); after runs once they end.
    // waits while other steps are on screen
    public void Play(List<TutorialStep> steps, GameManager game, System.Action after = null)
    {
        if (steps == null || steps.Count == 0 || canvas == null) { after?.Invoke(); return; }
        if (overlay == null) overlay = TutorialOverlay.Create(canvas, style);
        StartCoroutine(PlayWhenFree(steps, game, after));
    }

    IEnumerator PlayWhenFree(List<TutorialStep> steps, GameManager game, System.Action after)
    {
        while (running != null) yield return null;
        this.game = game;
        running = StartCoroutine(Run(steps, _ => after?.Invoke()));
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

    // Reset progress: every tutorial counts as never seen, with its coach and level-end steps from the start
    public void ForgetAll()
    {
        Stop();
        if (coach != null) EndCoach();
        finished.Clear();
        parked.Clear();
        endOwed.Clear();
        CancelEnd();
    }

    // leaving the level (or loading it again): the tutorial goes away and counts as not finished; a coach pauses, and
    // so does the level-end steps' wait (TryStart picks it up again on the level's next load)
    public void Stop()
    {
        if (overlay != null) overlay.HideError(); // a restart or leaving clears a wrong move's red box
        if (running != null) { StopCoroutine(running); DropDoIt(); }
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
            if (step.chooseWhenShown?.Invoke() is TutorialStep chosen) { steps[i] = chosen; step = chosen; } // a step decided only now (e.g. real arrows up, or an example)
            step.onShow?.Invoke();
            if (step.pops != null) foreach (var p in step.pops) overlay.ShowPop(p.text, PopAbove, p.near);
            bool canBack = step.backable && i > 0;
            overlay.Show(step, canBack);
            yield return null; // the click that ended the last step can't end this one too
            bool back = false;
            if ((step.doneWhen != null || step.until != null) && game != null)
            {
                // a "do it" step: it ends when the player does it; anything else it names as wrong is undone, with a note
                var dg = game;
                bool done = false; string wrongNote = null; int wrongFrame = -1;
                void OnAct(GameAction a)
                {
                    if (step.doneWhen != null && step.doneWhen(a))
                    {
                        done = true;
                        // a move is a take-back and a placement in the same frame: a "wrong" take-back just before it was the move's first half
                        if (wrongFrame == Time.frameCount) { wrongNote = null; dg.HoldLevelEnd(false); }
                    }
                    else if (step.wrong?.Invoke(a) is string n) { wrongNote = n; wrongFrame = Time.frameCount; dg.HoldLevelEnd(true); } // held at once, so a dead end can't end the level first
                }
                dg.ActionDone += OnAct; doItGame = dg; doItHandler = OnAct; // kept, so a step stopped part-way can let go (DropDoIt)
                while (wrongNote != null || (!done && !(step.until?.Invoke() ?? false))) // a slip is handled before the step can end
                {
                    if (wrongNote != null)
                    {
                        var n = wrongNote; wrongNote = null;
                        overlay.HideError(); // the last one closes; it opens again after the undo, so each slip shows
                        dg.HoldLevelEnd(true);
                        yield return new WaitForSecondsRealtime(UndoDelay);
                        if (step.wrongBackToArrows) dg.UndoChoiceStep(false); else dg.UndoPlacement(); // a wrong arrow: back to its arrows (the tile stays)
                        dg.HoldLevelEnd(false);
                        overlay.ShowError(n, 3f); // red, under the to-do box
                    }
                    yield return null;
                }
                dg.ActionDone -= OnAct; doItGame = null; doItHandler = null;
            }
            else
                while (!overlay.ConsumeNext())
                {
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
        ResumeCoach(g);
        return true;
    }

    // its level is on (the steps just ended, or the level loaded again): it listens and puts its guides up (not once
    // it's taught: they're down for good)
    void ResumeCoach(GameManager g)
    {
        coachGame = g;
        g.ActionDone += OnCoachAction;
        if (!coachTaught && coach.removalSteps != null) g.onRemoval = OnRemoval;
    }

    // its level is left or loads again: it stops listening (the board, and its guides, are gone or about to be)
    void PauseCoach()
    {
        if (coachGame != null)
        {
            coachGame.ActionDone -= OnCoachAction;
            coachGame.onRemoval = null;
        }
        coachGame = null;
        if (undoing != null) StopCoroutine(undoing); // an undo cut short by leaving
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
            case CoachReply.Undo:
                if (undoing == null) undoing = StartCoroutine(UndoWrongWay(action));
                break;
            case CoachReply.Done:
                if (coachTaught) break;
                coachTaught = true;
                coachGame.onRemoval = null; // taught: tiles break without a word from now on
                if (Tutorials.HasEnd(Levels.All[coachLevel])) WatchForEnd(coachGame, coachLevel);
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
        if (running != null) { StopCoroutine(running); DropDoIt(); }
        game = g;
        running = StartCoroutine(Run(steps, done => done.ReleaseRemoval()));
        return true;
    }

    // the slip (a wrong way, or one object delivered alone) shows for UndoDelay, with the level held from ending (a
    // wrong way can leave everyone blocked) and nothing clickable; then it's undone at once, with no rewind: a wrong
    // way back to its arrow question (the tile stays), anything else the whole placement. then the coach's note for it
    // shows in the red box. Undo is called from here, not from inside the round that reported the action
    IEnumerator UndoWrongWay(GameAction action)
    {
        var g = coachGame;
        var note = coach?.undoNoteFor?.Invoke(action);
        g.HoldLevelEnd(true);
        overlay.Block();
        yield return new WaitForSecondsRealtime(UndoDelay);
        if (action.kind == GameActionKind.ChooseDirection) g.UndoChoiceStep(false);
        else g.UndoPlacement(false); // the whole placement, whatever undo by step is set to
        g.HoldLevelEnd(false);
        undoing = null;
        overlay.Unblock();
        if (!string.IsNullOrEmpty(note)) overlay.ShowError(note, 5f);
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
        if (running != null) { StopCoroutine(running); DropDoIt(); }
        game = g; // shown as the tutorial's steps are
        running = StartCoroutine(Run(steps, done =>
        {
            done.hud.HoldScoreHistory(false);
            endPlaying = null;
            done.HoldEndWindow(false); // the end window shows now
        }));
    }
}
