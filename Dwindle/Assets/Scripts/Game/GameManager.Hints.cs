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
    public int HintStage => hints.Stage;     // 0 to 3
    public bool PurpleLost => hints.PurpleLost;
    public bool HintHidden { get; set; }     // Hide hint: the overlay is hidden, the stage stays
    public string HintTilesText => Hints.TilesText(level); // stage 1, under the score
    // stage 2, fixed when shown: who arrives in which delivery (same number = together), and where groups meet
    public List<(string objectName, int number)> HintBadges { get; private set; } = new List<(string, int)>();
    public List<(Vector2Int square, int number)> HintJunctions { get; private set; } = new List<(Vector2Int, int)>();

    // stage 3: the plan's tiles in order (numbered from 1, the plan's own order), and which plan it is. chosen at the
    // first press; follows the board to another plan while steps are left to show (see FollowBoard)
    List<PlanStep> stage3Steps = new List<PlanStep>();
    int stage3Family, stage3Order;
    public bool HintAnswerDone => hints.Stage >= Hints.Stages && hints.Revealed >= stage3Steps.Count;
    // the hint button is lit: there's a solution, the level is on, and something is left to show (UseHint itself waits out a move)
    public bool HintAvailable => level != null && level.solution != null && !levelOver && !HintAnswerDone;

    // the steps shown so far, each with its number and how it stands (see PlanMatcher.StepStates)
    public List<(Vector2Int square, TileType tile, int number, StepState state)> HintGhosts
    {
        get
        {
            var res = new List<(Vector2Int, TileType, int, StepState)>();
            if (hints.Stage < Hints.Stages) return res;
            var states = PlanMatcher.StepStates(level, stage3Family, stage3Order, stage3Steps, CurrentState());
            for (int i = 0; i < hints.Revealed && i < stage3Steps.Count; i++)
                res.Add((stage3Steps[i].square, stage3Steps[i].tile, i + 1, states[i]));
            return res;
        }
    }

    // stage 3: when the game asks for an arrow, the plan's way, which pulses purple. only where the tile hint is shown
    // too: the junction's square, or for a door the square the object went into it from (null: none, or hidden)
    Dir? HintArrow(MovingObject o, DoorSpec door = null)
    {
        if (hints.Stage < Hints.Stages || HintHidden) return null;
        var square = door == null ? o.pos : door.cell;
        if (!stage3Steps.Take(hints.Revealed).Any(s => s.square == square)) return null;
        return PlanMatcher.ExpectedArrow(level, stage3Family, o.spec.name,
            new ArrowRecord { square = door == null ? o.pos : (Vector2Int?)null, door = door?.label },
            arrowsSoFar.TryGetValue(o.spec.name, out var a) ? a : null);
    }

    // the hint button: the next stage, or one more ghost
    // pressed while objects move, it waits and runs once the board is still (see Update), so the press isn't lost
    public void UseHint()
    {
        hintQueued = false;
        if (level == null || level.solution == null || levelOver || inputLocked) return;
        if (busy) { hintQueued = true; return; }
        var s = level.solution;
        switch (hints.Stage)
        {
            case 0:
                hints.Stage = 1;
                break;
            case 1:
            {
                var st = CurrentState();
                var plan = PlanMatcher.Closest(level, st) ?? PlanMatcher.Nearest(level, st);
                HintBadges = PlanMatcher.Badges(level, plan.family, plan.order);
                HintJunctions = PlanMatcher.Junctions(level, plan.family, plan.order);
                hints.Stage = 2;
                break;
            }
            case 2:
                FreezeStage3();
                hints.Stage = 3;
                hints.Revealed = 1;
                break;
            default:
                if (hints.Revealed >= stage3Steps.Count) return;
                hints.Revealed++;
                break;
        }
        HintHidden = false;
        Report(new GameAction { kind = GameActionKind.Hint, hintStage = hints.Stage, answerStep = hints.Revealed }); // for analytics
        HintsChanged?.Invoke();
    }

    // Hide / Show: the hints go away and come back; the stage stays (the next Hint press shows them again)
    public void ToggleHintHidden()
    {
        HintHidden = !HintHidden;
        HintsChanged?.Invoke();
    }

    // the board's hints: stage 2's numbers and frames, or stage 3's ghosts in their place
    void DrawHints()
    {
        if (level == null || board.Cells == null) return;
        if (!busy) FollowBoard(); // mid-round the board is half moved: it's judged once everything has stopped
        if (HintHidden) { board.ShowHints(new List<(string, int)>(), new List<(Vector2Int, int)>(), new List<(Vector2Int, TileType, int, StepState)>()); return; }
        bool two = hints.Stage == 2;
        board.ShowHints(two ? HintBadges : new List<(string, int)>(), two ? HintJunctions : new List<(Vector2Int, int)>(), HintGhosts);
    }

    // which plan stage 3 starts with, its whole sequence from step 1 in the plan's own order (tiles already down that
    // fit show as done): the plan the board fits (on an empty board, the level's answer plan: the best plan with the
    // fewest steps), or else the nearest one (the ghosts show where the board differs)
    void FreezeStage3()
    {
        var s = level.solution;
        var st = CurrentState();
        var plan = PlanMatcher.Closest(level, st) ?? PlanMatcher.Nearest(level, st);
        var steps = PlanMatcher.FromStart(level, plan.family, plan.order, st.tiles);
        if (steps == null) { plan = (s.answerFamily, s.answerOrder); steps = PlanMatcher.FromStart(level, plan.family, plan.order); }
        stage3Steps = steps ?? new List<PlanStep>();
        (stage3Family, stage3Order) = plan;
    }

    // while steps are left to show: when the board stops fitting stage 3's plan but fits another whose first steps are
    // the ones already shown, stage 3 follows it (the shown numbers stay; the next ones are the new plan's). with no
    // such plan, or every step shown, it stays as it is
    // while it still fits, its tiles follow the board: a tile the plan also allows on a square counts as that step's
    void FollowBoard()
    {
        if (hints.Stage < Hints.Stages || hints.Revealed >= stage3Steps.Count) return;
        var st = CurrentState();
        if (PlanMatcher.Fits(level, stage3Family, stage3Order, st))
        {
            // still this plan: its tiles picked again for the board (a tile it also allows counts; the rest from the supply left)
            var states = PlanMatcher.StepStates(level, stage3Family, stage3Order, stage3Steps, st);
            stage3Steps = PlanMatcher.Refit(level, stage3Family, stage3Order, stage3Steps, states, st) ?? stage3Steps;
            return;
        }
        var shown = stage3Steps.Take(hints.Revealed).ToList();
        foreach (var plan in PlanMatcher.Plans(level))
        {
            if (plan == (stage3Family, stage3Order) || !PlanMatcher.Fits(level, plan.family, plan.order, st)) continue;
            var steps = PlanMatcher.Continue(level, plan.family, plan.order, shown, st.tiles);
            if (steps == null) continue;
            stage3Steps = steps;
            (stage3Family, stage3Order) = plan;
            return;
        }
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
        list.Add(new ArrowRecord { square = door == null ? o.pos : (Vector2Int?)null, door = door, way = o.heading });
    }

    // HandleDeliveries: who arrived on this placement, on which square, with which tiles down (before any breaks)
    void RecordDelivery(List<MovingObject> deliveredNow)
    {
        var occupied = new HashSet<Vector2Int>();
        for (int x = 0; x < Cells.GetLength(0); x++)
            for (int y = 0; y < Cells.GetLength(1); y++)
                if (Cells[x, y]?.tile != null) occupied.Add(new Vector2Int(x, y));
        deliveriesSoFar.Add(new DeliveryRecord { objects = deliveredNow.Select(o => o.spec.name).ToArray(), at = lastPlaced, occupied = occupied });
    }

    // LoadLevel (and so Restart, Replay, Next, picking a level): a fresh start, hints included
    void ResetHintsForLoad()
    {
        hints.Clear();
        hintQueued = false;
        HintHidden = false;
        HintBadges = new List<(string, int)>();
        HintJunctions = new List<(Vector2Int, int)>();
        stage3Steps = new List<PlanStep>();
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
    public bool AnimateUndo { get => board.animateUndo; set => board.animateUndo = value; }
    public bool UnlockAll { get => Session.unlockAll; set => Session.unlockAll = value; }
}
