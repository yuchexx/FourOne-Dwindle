using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    // a tutorial's score recap (TutorialRecap): waiting for its level to end, then its steps
    readonly HashSet<int> recapOwed = new(); // levels whose recap hasn't played yet: it waits again on each load
    GameManager recapGame; // set while waiting
    int recapLevel = -1;
    GameManager recapPlaying; // the game its steps are on, to let go of if they're cut short
    const float RecapStart = 0.6f; // before its first step: the last delivery shows first
    // how far over what scored them the points float: clear of the frame over a ring or a slot
    const float PopAbove = 14f;

    public bool Running => running != null;

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
        if (recapOwed.Contains(levelIndex)) WatchForRecap(game, levelIndex); // a restart doesn't lose it
        if (finished.Contains(levelIndex)) return;
        var steps = Tutorials.For(Levels.All[levelIndex], game); // the level names its tutorial (LevelData.tutorial)
        if (steps == null || steps.Count == 0) return;
        if (canvas == null) { Debug.LogError("TutorialController needs its Canvas set."); return; }
        if (overlay == null) overlay = TutorialOverlay.Create(canvas, style);
        this.game = game;
        running = StartCoroutine(Run(steps, g =>
        {
            finished.Add(levelIndex);
            // its coach takes over; with none, its recap (if any) waits for the level's end from now
            if (!StartCoach(levelIndex, g) && Tutorials.RecapFor(Levels.All[levelIndex]) != null) WatchForRecap(g, levelIndex);
        }));
    }

    // the Tutorial button (GameManager.ReplayTutorial): the level's tutorial counts as never seen, and its coach and
    // recap, if they're on or owed, are dropped, so the next TryStart runs it all from the start
    public void Forget(int levelIndex)
    {
        finished.Remove(levelIndex);
        if (coach != null && coachLevel == levelIndex) EndCoach();
        parked.Remove(levelIndex);
        recapOwed.Remove(levelIndex);
        CancelRecap();
    }

    // leaving the level (or loading it again): the tutorial goes away and counts as not finished; a coach pauses, and
    // so does a recap's wait (TryStart picks it up again on the level's next load)
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
        CancelRecap();
        if (overlay != null) overlay.Hide();
    }

    // each step in turn, until the player presses its Next, then `after` with the game (the tutorial's: its coach
    // starts). one coroutine all through, so Stop stops all of it
    IEnumerator Run(List<TutorialStep> steps, System.Action<GameManager> after)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.delay > 0)
            {
                overlay.Block();
                yield return new WaitForSeconds(step.delay);
            }
            step.onShow?.Invoke();
            bool canSkip = SkipFrom(steps, i);
            overlay.Show(step, canSkip);
            yield return null; // the click that ended the last step can't end this one too
            bool skipped = false;
            while (!overlay.ConsumeNext())
            {
                if (canSkip && overlay.ConsumeSkip()) { skipped = true; break; }
                yield return null;
            }
            step.onDone?.Invoke();

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
        if (noteOnResume && !string.IsNullOrEmpty(coach.resetNote)) overlay.ShowNote(coach.resetNote, noteSeconds, coach.noteAbove);
        noteOnResume = false;
    }

    // its level is left or loads again: it stops listening (the board, and its guides, are gone or about to be)
    void PauseCoach()
    {
        if (coachGame != null) coachGame.ActionDone -= OnCoachAction;
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
                var after = coach.doneSteps;
                if (Tutorials.RecapFor(Levels.All[coachLevel]) != null) WatchForRecap(g, coachLevel);
                overlay.HideNotes();
                coach.onDone?.Invoke();
                if (after != null && after.Count > 0) // its steps, shown as the tutorial's are
                {
                    game = g;
                    running = StartCoroutine(Run(after, null));
                }
                break;
        }
    }

    // the slip (a wrong way, a tile in the wrong spot) shows for Undo Delay, with the level held from ending (a wrong
    // way can leave everyone blocked) and nothing clickable; then the placement is undone, as with the Undo button,
    // and the coach's note for it shows (over any note already there). Undo is called from here, not from inside the
    // round that reported the action
    IEnumerator UndoWrongWay(GameAction action)
    {
        var g = coachGame;
        var note = coach?.undoNoteFor?.Invoke(action) ?? coach?.undoNote;
        var above = coach?.noteAbove;
        g.HoldLevelEnd(true);
        overlay.Block();
        yield return new WaitForSecondsRealtime(undoDelay);
        undoing = null;
        g.Undo();
        g.HoldLevelEnd(false);
        overlay.Unblock();
        if (!string.IsNullOrEmpty(note)) overlay.ShowNote(note, noteSeconds, above);
    }

    // ---- the score recap ----

    // waits for the level to end (GameManager reports LevelEnd as it does, before the end window's wait). owed from now
    // until it plays: a level end with someone left behind, or a restart, doesn't use it up
    void WatchForRecap(GameManager g, int levelIndex)
    {
        CancelRecap();
        recapOwed.Add(levelIndex);
        recapGame = g;
        recapLevel = levelIndex;
        g.ActionDone += OnRecapAction;
    }

    // left, or loaded again, before it ends or while its steps show (Stop stops them): nothing more of it
    void CancelRecap()
    {
        if (recapGame != null) recapGame.ActionDone -= OnRecapAction;
        recapGame = null;
        if (recapPlaying != null)
        {
            recapPlaying.hud.HoldScoreHistory(false);
            recapPlaying.HoldEndWindow(false);
        }
        recapPlaying = null;
    }

    // the level ended: with everyone delivered, the recap's steps show while the end window waits, with the score's
    // history held open in full
    void OnRecapAction(GameAction action)
    {
        if (action.kind != GameActionKind.LevelEnd) return;
        var g = recapGame;
        int levelIndex = recapLevel;
        CancelRecap();
        if (action.delivered < Levels.All[levelIndex].objects.Count)
        {
            WatchForRecap(g, levelIndex); // not this time: the Undo in the end window, or a restart, can still get there
            return;
        }
        recapOwed.Remove(levelIndex); // it plays now, once
        var recap = Tutorials.RecapFor(Levels.All[levelIndex]);
        if (recap == null) return;
        var steps = RecapSteps(g, recap);
        if (steps.Count == 0) return;
        g.HoldEndWindow(true);
        g.hud.HoldScoreHistory(true);
        recapPlaying = g;
        if (running != null) StopCoroutine(running);
        game = g; // shown as the tutorial's steps are
        running = StartCoroutine(Run(steps, done =>
        {
            done.hud.HoldScoreHistory(false);
            recapPlaying = null;
            done.HoldEndWindow(false); // the end window shows now
        }));
    }

    // the score: first the recap's intro in the middle of the screen (if it has one), then a step for each beat it
    // shows, in the order it scored, each framing what scored (rings, the doors gone through, the supply's slots) with
    // its points over it, and its rows in the history, and naming it over Next; End on the last
    List<TutorialStep> RecapSteps(GameManager g, TutorialRecap recap)
    {
        var log = g.hud.GetScoreLog(); // newest first, as the history shows it
        var entries = new List<ScoreEntry>(log);
        entries.Reverse();
        var steps = new List<TutorialStep>();
        foreach (var beat in Beats(entries))
        {
            if (recap.which != null && !beat.Any(recap.which)) continue; // not one it shows
            var targets = new List<Func<RectTransform>>();
            var badges = new List<(string text, float above, Func<RectTransform>[] near)>();
            foreach (var (_, text, above, near) in Pops(g, beat))
            {
                badges.Add((text, above, near));
                targets.AddRange(near);
            }
            var rows = beat.Select(e => Target.ScoreRow(g, log.IndexOf(e))).ToArray(); // a row's place is its entry's in the log
            targets.AddRange(rows);
            // a delivery through doors: both ends of each door it went through, too
            foreach (var e in beat.Where(x => x.door && x.objects != null && x.objects.Length > 0))
                foreach (var label in g.DoorsUsedBy(e.objects[0]))
                    targets.AddRange(Target.Door(g, label, 0).Concat(Target.Door(g, label, 1)));
            steps.Add(new TutorialStep
            {
                text = NameOf(beat[0]),
                bubbleWidth = -1, // as wide as its word, on one line (TutorialStep.bubbleWidth)
                targets = targets.ToArray(),
                bubbleBy = rows, // left of the rows, out of the board's way
                bubble = BubbleSide.Left,
                onShow = () => { foreach (var b in badges) overlay.ShowPop(b.text, b.above, b.near); },
                onDone = overlay.ClearPops,
            });
        }
        if (steps.Count == 0) return steps; // nothing it shows scored: no recap
        if (!string.IsNullOrEmpty(recap.intro))
            steps.Insert(0, new TutorialStep
            {
                text = recap.intro,
                skippable = true, // Skip: straight to the end window
                // nothing framed: everything is dimmed and the bubble sits in the middle of the screen
            });
        steps[0].delay = RecapStart;
        steps[steps.Count - 1].buttonText = "End"; // the end window comes next
        return steps;
    }

    // what a recap step says over its buttons: what scored
    static string NameOf(ScoreEntry e) =>
        e.door ? "Delivery with doors" : e.ring ? "Delivery" : e.combo ? "Combo delivery" : "Unused tiles";

    // the log (oldest first) in beats: the deliveries one placement made together (the ones the combo row right after
    // them names) as one, everything else on its own
    static List<List<ScoreEntry>> Beats(List<ScoreEntry> entries)
    {
        var beats = new List<List<ScoreEntry>>();
        for (int i = 0; i < entries.Count;)
        {
            if (!entries[i].ring) { beats.Add(new List<ScoreEntry> { entries[i++] }); continue; }
            int j = i;
            while (j < entries.Count && entries[j].ring) j++; // a run of deliveries
            var next = j < entries.Count ? entries[j] : null;
            int together = next != null && next.combo && next.objects != null ? Mathf.Min(next.objects.Length, j - i) : 0;
            for (; i < j - together; i++) beats.Add(new List<ScoreEntry> { entries[i] });
            if (together > 0) beats.Add(entries.GetRange(i, together));
            i = j;
        }
        return beats;
    }

    // a beat's points: a delivery's over its ring (through a door: "+100" there, and the door's "+50" over the door it
    // came out of), a combo's share over each of its rings ("+50" each), the unused tiles' over each supply slot with
    // some left ("+75", or "+75×2")
    static IEnumerable<(ScoreEntry entry, string text, float above, Func<RectTransform>[] near)> Pops(
        GameManager g, List<ScoreEntry> beat)
    {
        foreach (var e in beat)
        {
            if (e.ring && e.objects != null && e.objects.Length > 0)
            {
                var c = e.objects[0];
                var ring = new Func<RectTransform>[] { () => g.RingRectOf(c) };
                if (e.door) // through a door: the delivery's points over its ring, the door's over the door it came out of
                {
                    yield return (e, $"+{g.pointsPerDelivery}", PopAbove, ring);
                    yield return (e, $"+{g.pointsPerTeleport}", PopAbove,
                        new Func<RectTransform>[] { () => g.DoorOutRect(c, false), () => g.DoorOutRect(c, true) });
                }
                else yield return (e, $"+{e.points}", PopAbove, ring);
            }
            else if (e.combo && e.objects != null)
                foreach (var c in e.objects) // each object's share, over its own ring
                    yield return (e, $"+{g.comboPointsPerObject}", PopAbove, new Func<RectTransform>[] { () => g.RingRectOf(c) });
            else // the unused tiles (the only text row)
            {
                bool any = false;
                foreach (var t in Tiles.SupplyOrder)
                {
                    int n = g.TilesLeft(t);
                    if (n <= 0) continue;
                    any = true;
                    string each = $"+{g.pointsPerUnusedTile}";
                    yield return (e, n == 1 ? each : $"{each}\u00D7{n}", PopAbove, new[] { Target.Slot(g, t) });
                }
                if (!any) yield return (e, $"+{e.points}", PopAbove, new[] { Target.Supply(g) }); // all on the board
            }
        }
    }

    // the delivery that called for the reset stays on screen for Reset Delay, with the level held from ending and
    // nothing clickable; then the level loads again, and the coach comes back with its note (TryStart, ResumeCoach)
    IEnumerator ResetLevel()
    {
        var g = coachGame;
        g.HoldLevelEnd(true);
        overlay.Block();
        yield return new WaitForSecondsRealtime(resetDelay);
        resetting = null; // before the reload, whose Stop would otherwise stop this coroutine mid-way
        noteOnResume = true;
        g.ReloadLevel();
    }

    // a skippable step gets Skip unless it's the last (there Next does the same): Skip passes all the steps after it
    static bool SkipFrom(List<TutorialStep> steps, int i) => steps[i].skippable && i + 1 < steps.Count;
}
