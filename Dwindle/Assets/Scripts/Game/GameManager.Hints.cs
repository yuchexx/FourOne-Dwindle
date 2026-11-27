using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// the hint button, this session's progress and the settings. part of GameManager
public partial class GameManager
{
    readonly Hints hints = new Hints();
    bool hintQueued; // the hint button was pressed during a move

    // what the player has done, for matching the board against the best plans (kept by undo's snapshots)
    List<DeliveryRecord> deliveriesSoFar = new List<DeliveryRecord>();
    Dictionary<string, List<ArrowRecord>> arrowsSoFar = new Dictionary<string, List<ArrowRecord>>();
    Vector2Int lastPlaced; // the square placed in the round being played

    public event System.Action HintsChanged; // the hint button and the board's hint overlay should redraw
    public int HintStage => hints.Stage;     // 0 to 2
    public bool PurpleLost => hints.PurpleLost;
    public bool HintHidden { get; set; }     // Hide hint: the overlay is hidden, the stage stays
    // stage 1, fixed when shown: who arrives in which delivery (same number = together), and where groups meet
    public List<(string objectName, int number)> HintBadges { get; private set; } = new List<(string, int)>();
    public List<(Vector2Int square, int number)> HintJunctions { get; private set; } = new List<(Vector2Int, int)>();

    // stage 2, the answer (Stage3, named for the old third stage): made at its first press, null before. placedForHints: a placement waiting to be checked
    Stage3 stage3;
    bool placedForHints;
    public bool HintAnswerDone => stage3 != null && hints.Revealed >= stage3.Steps.Count;
    // the hint button is lit: there's a solution, the level is on, and something is left to show (UseHint itself waits out a move)
    public bool HintAvailable => level != null && level.solution != null && !levelOver && !HintAnswerDone;

    // the steps shown so far, each with its number and how it stands. mid-move (an arrow being picked) only the new tile
    // and turns are judged, the rest once everything stops
    List<(Vector2Int square, TileType tile, int number, StepState state)> HintGhosts(PlayerState st)
    {
        var res = new List<(Vector2Int, TileType, int, StepState)>();
        if (stage3 == null) return res;
        var states = stage3.States(st, moving: busy, round: busy && placedForHints ? lastPlaced : (Vector2Int?)null);
        for (int i = 0; i < hints.Revealed && i < stage3.Steps.Count; i++)
            res.Add((stage3.Steps[i].square, stage3.Steps[i].tile, i + 1, states[i]));
        return res;
    }

    // stage 3: when the game asks for an arrow, the way to take, which pulses purple (null: none, or hidden). o.pos: the
    // junction, or for a door the square it went in from (the door's own square can be the one across its edge)
    Dir? HintArrow(MovingObject o, DoorSpec door = null)
    {
        if (stage3 == null || HintHidden) return null;
        var here = new ArrowRecord { square = door == null ? o.pos : (Vector2Int?)null, door = door?.label };
        return stage3.Arrow(CurrentState(), o.spec.name, here, o.pos, hints.Revealed, lastPlaced);
    }

    // the hint button: the next stage, or one more ghost
    // pressed while objects move, it waits and runs once they stop or the arrows show (see Update), so the press isn't lost.
    // while the arrows show it runs at once
    public void UseHint()
    {
        hintQueued = false;
        if (level == null || level.solution == null || levelOver) return;
        if (busy && asking == null) { hintQueued = true; return; }
        var s = level.solution;
        switch (hints.Stage)
        {
            case 0:
                {
                    var st = CurrentState();
                    ShowOrderOf(PlanMatcher.Closest(level, st) ?? PlanMatcher.Nearest(level, st));
                    if (Stage1Empty) // nothing for stage 1 to show: straight to the answer
                    {
                        stage3 = new Stage3(level, st);
                        hints.Stage = 2;
                        hints.Revealed = 1;
                        break;
                    }
                    hints.Stage = 1;
                    break;
                }
            case 1:
                stage3 = new Stage3(level, CurrentState());
                hints.Stage = 2;
                hints.Revealed = 1;
                break;
            default:
                if (hints.Revealed >= stage3.Steps.Count) return;
                hints.Revealed++;
                break;
        }
        HintHidden = false;
        Report(new GameAction { kind = GameActionKind.Hint, hintStage = hints.Stage, answerStep = hints.Revealed }); // for analytics
        HintsChanged?.Invoke();
        RefreshHintedArrow();
        if (!Session.HintGuideSeen(hints.Stage)) StartCoroutine(HintGuideSoon(hints.Stage)); // the first time a stage opens: its guide
    }

    // stage 1 has no meeting square to show (e.g. Level 2): Hint goes straight to the answer
    // worked out once per level, from the board as it starts (so the Hints button's dots don't change as you play)
    bool? stage1Empty;
    public bool Stage1Empty => stage1Empty ??= WorkOutStage1Empty();
    bool WorkOutStage1Empty()
    {
        {
            if (level?.solution == null) return false;
            var st = CurrentState();
            var plan = orderPlan ?? PlanMatcher.Closest(level, st) ?? PlanMatcher.Nearest(level, st);
            return PlanMatcher.Junctions(level, plan.family, plan.order).Count == 0; // no meeting square: nothing to show but the order (e.g. Level 2: Red, then Blue)
        }
    }

    // the answer's arrow step, the first time arrows come up with the answer open (seen key 3): over the arrows, once
    System.Collections.IEnumerator ArrowGuideSoon()
    {
        yield return null; // the arrows are made first
        if (tutorial == null || board.HintedArrowRect == null) yield break;
        Session.MarkHintGuideSeen(3);
        tutorial.Play(Tutorials.ArrowGuide(this), this);
    }

    // the hint guide for the stage shown now (the Hint help button), or for a stage that just opened (once, saved)
    public void ShowHintGuide() { if (hints.Stage >= 1) StartCoroutine(HintGuideSoon(hints.Stage)); }
    System.Collections.IEnumerator HintGuideSoon(int stage)
    {
        yield return null; // the hints are drawn first, so the guide can point at them
        if (tutorial == null || levelOver || hints.Stage != stage) yield break;
        Session.MarkHintGuideSeen(stage);
        if (stage >= Hints.Stages) Session.MarkHintGuideSeen(3); // the answer's guide covers the arrow too
        var hb = FindAnyObjectByType<HintButton>();
        tutorial.Play(Tutorials.HintGuide(this, hb, stage), this, () => { if (hb != null) hb.HoldOpen(false); });
    }

    // the arrows showing now: the one the hint points to pulses (a hint used, hidden or shown while they show)
    void RefreshHintedArrow()
    {
        if (asking != null) board.SetHintedArrow(HintArrow(asking.Value.o, asking.Value.door));
    }

    // Hide / Show: the hints go away and come back; the stage stays (the next Hint press shows them again)
    public void ToggleHintHidden()
    {
        HintHidden = !HintHidden;
        HintsChanged?.Invoke();
        RefreshHintedArrow();
    }

    // the board's hints: stage 1's numbers and frames, or the answer's ghosts (stage 2) in their place
    void DrawHints()
    {
        if (level == null || board.Cells == null) return;
        var st = CurrentState(); // the board, once for this redraw
        if (!busy) FollowBoard(st); // mid-round the board is half moved: it's judged once everything has stopped
        if (!busy && hints.Stage == 1) FollowOrder(st); // stage 1 follows the board too
        if (HintHidden) { heardSteps = null; board.ShowHints(new List<(string, int)>(), new List<(Vector2Int, int)>(), new List<(Vector2Int, TileType, int, StepState)>()); return; }
        bool order = hints.Stage == 1;
        var ghosts = HintGhosts(st);
        HintSounds(ghosts);
        board.ShowHints(order ? HintBadges : new List<(string, int)>(), order ? HintJunctions : new List<(Vector2Int, int)>(), ghosts, objects?.Where(o => o.completed).Select(o => o.spec.name).ToList()); // delivered: their junctions show as done
    }

    // the answer's sounds: a step newly wrong (its number pulses) or the tile just placed newly right (number filled in),
    // against the last redraw. one sound per frame, wrong over right, played at the frame's end unless an Undo, Hint or
    // Restart came that frame (those redraw quietly)
    Dictionary<(Vector2Int, TileType), StepState> heardSteps; // null: the next redraw only sets the baseline
    AudioManager.Sfx? hintSound;
    int quietFrame = -1;
    void HintSounds(List<(Vector2Int square, TileType tile, int number, StepState state)> ghosts)
    {
        var now = new Dictionary<(Vector2Int, TileType), StepState>();
        foreach (var g in ghosts) now[(g.square, g.tile)] = g.state;
        var before = heardSteps;
        heardSteps = now;
        if (before == null) return;
        foreach (var kv in now)
        {
            if (!before.TryGetValue(kv.Key, out var was) || was == kv.Value) continue; // newly shown steps make no sound
            if (kv.Value == StepState.Early) hintSound = AudioManager.Sfx.HintWrong;
            else if (kv.Value == StepState.Done && kv.Key.Item1 == lastPlaced && hintSound == null) hintSound = AudioManager.Sfx.HintRight;
        }
    }
    void QuietHintSounds() { quietFrame = Time.frameCount; hintSound = null; }
    void PlayHintSound()
    {
        if (hintSound is AudioManager.Sfx s && quietFrame != Time.frameCount) AudioManager.Play(s);
        hintSound = null;
    }

    // stage 1's plan: its order numbers and meeting squares
    (int family, int order)? orderPlan;
    void ShowOrderOf((int family, int order) plan)
    {
        orderPlan = plan;
        HintBadges = PlanMatcher.Badges(level, plan.family, plan.order);
        HintJunctions = PlanMatcher.Junctions(level, plan.family, plan.order);
    }

    // once everything has stopped: while stage 1's plan can still be finished it stays (the numbers don't jump around);
    // else the closest plan that fits the board takes its place
    void FollowOrder(PlayerState st)
    {
        if (orderPlan is (int f, int o) && PlanMatcher.Reachable(level, f, o, st)) return;
        var next = PlanMatcher.Closest(level, st);
        if (next != null && next != orderPlan) ShowOrderOf(next.Value);
    }

    // stage 1's order for the hint box: each delivery's objects in order (colour, delivered yet)
    public List<List<(Color color, bool delivered)>> HintOrderLine()
    {
        var res = new List<List<(Color, bool)>>();
        var badges = hints.Stage >= 2 && stage3 != null ? PlanMatcher.Badges(level, stage3.Plan.family, stage3.Plan.order) : HintBadges; // the answer's plan from stage 2
        foreach (var g in badges.GroupBy(b => b.number).OrderBy(g => g.Key))
        {
            var group = new List<(Color, bool)>();
            foreach (var (name, _) in g)
            {
                var o = objects?.FirstOrDefault(x => x.spec.name == name);
                if (o != null) group.Add((o.spec.color, o.completed));
            }
            res.Add(group);
        }
        return res;
    }

    // stage 3, once everything has stopped: follows the board (see Stage3.Follow), with the placement this round if any
    void FollowBoard(PlayerState st)
    {
        bool placed = placedForHints;
        placedForHints = false; // before stage 3 nothing is checked, so nothing waits
        if (stage3 == null) return;
        int revealed = hints.Revealed;
        stage3.Follow(st, ref revealed, placed ? lastPlaced : (Vector2Int?)null);
        hints.Revealed = revealed;
    }

    PlayerState CurrentState()
    {
        var st = new PlayerState
        {
            deliveries = new List<DeliveryRecord>(deliveriesSoFar),
            arrows = arrowsSoFar.ToDictionary(kv => kv.Key, kv => new List<ArrowRecord>(kv.Value)),
            supply = new Dictionary<TileType, int>(inventory),
        };
        for (int x = 0; x < Cells.GetLength(0); x++)
            for (int y = 0; y < Cells.GetLength(1); y++)
                if (Cells[x, y]?.tile != null) st.tiles[new Vector2Int(x, y)] = Cells[x, y].tile.Value;
        foreach (var o in objects) st.passed[o.spec.name] = new List<Vector2Int>(o.path);
        return st;
    }

    // an arrow the player picked, at a junction (door null) or coming out of a door
    void RecordArrow(MovingObject o, string door)
    {
        if (!arrowsSoFar.TryGetValue(o.spec.name, out var list)) arrowsSoFar[o.spec.name] = list = new List<ArrowRecord>();
        list.Add(new ArrowRecord
        {
            square = door == null ? o.pos : (Vector2Int?)null,
            door = door,
            way = o.heading,
            from = door == null ? (Vector2Int?)null : o.pos
        }); // o.pos: still the square it went in from
    }

    // HandleDeliveries: who arrived on this placement, on which square, with which tiles down (before any breaks)
    void RecordDelivery(List<MovingObject> deliveredNow)
    {
        var occupied = new HashSet<Vector2Int>();
        for (int x = 0; x < Cells.GetLength(0); x++)
            for (int y = 0; y < Cells.GetLength(1); y++)
                if (Cells[x, y]?.tile != null) occupied.Add(new Vector2Int(x, y));
        deliveriesSoFar.Add(new DeliveryRecord { objects = deliveredNow.Select(o => o.spec.name).ToArray(), at = lastPlaced, occupied = occupied, happened = true });
    }

    // for analytics: per best plan [family][order], how many deliveries so far happened as its, in order from the first
    public int[][] DeliveriesInOrder()
    {
        var families = level?.solution?.families;
        if (families == null) return new int[0][];
        return families.Select((f, fi) => (f.orders ?? new DeliveryOrder[0])
            .Select((o, oi) => PlanMatcher.MatchingDeliveries(level, fi, oi, deliveriesSoFar)).ToArray()).ToArray();
    }

    // LoadLevel (and so Restart, Replay, Next, picking a level): a fresh start, hints included
    void ResetHintsForLoad()
    {
        hints.Clear();
        stage1Empty = null; // this level's own
        hintQueued = false;
        HintHidden = false;
        HintBadges = new List<(string, int)>();
        HintJunctions = new List<(Vector2Int, int)>();
        orderPlan = null;
        stage3 = null;
        placedForHints = false;
        deliveriesSoFar = new List<DeliveryRecord>();
        arrowsSoFar = new Dictionary<string, List<ArrowRecord>>();
        HintsChanged?.Invoke();
    }

    // FinishLevel: keeps the level's best this session. purple = 3 stars without the answer hint
    bool SaveResult(int stars)
    {
        bool purple = stars == 3 && !hints.PurpleLost;
        Session.Save(levelIndex, stars, purple);
        return purple;
    }

    // Settings
    public bool AnimateUndo { get => Session.animateUndo; set { Session.animateUndo = value; board.animateUndo = value; } }
    public bool UndoByStep { get => Session.undoByStep; set => Session.undoByStep = value; }
    public bool ShowBreaks { get => Session.showBreaks; set { Session.showBreaks = value; if (!value) board.ClearBreakMarks(); } }
    public bool UnlockAll { get => Session.unlockAll; set => Session.unlockAll = value; }
    // the tutorials from the next level start; for the tutorial: its button by the title changes right away
    public bool ShowTutorials
    {
        get => Session.showTutorials;
        set { Session.showTutorials = value; RefreshTutorialButton(); }
    }
    public bool ShowHintTips { get => Session.showHintTips; set => Session.showHintTips = value; }
    public bool CompactSupply
    {
        get => Session.compactSupply;
        set
        {
            Session.compactSupply = value;
            if (level == null) return;
            hud.BuildSlots(SupplyTypes(), SelectTile, SupplyDragStart, e => board.MoveDrag(e), SupplyDragEnd); // the dock changes right away
            RefreshAll();
        }
    }
}
