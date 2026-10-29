using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// the hint button, this session's progress and the settings. part of GameManager
public partial class GameManager
{
    readonly Hints hints = new Hints();

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
    public List<Vector2Int> HintJunctions { get; private set; } = new List<Vector2Int>();

    // stage 3, fixed at its first press: the plan's tiles in order, and which plan it is
    List<PlanStep> stage3Steps = new List<PlanStep>();
    int stage3Family, stage3Order;
    public bool HintAnswerDone => hints.Stage >= Hints.Stages && hints.Revealed >= stage3Steps.Count;

    // the ghosts to draw: revealed steps whose tile isn't down yet, is a different tile, or made a delivery the plan
    // didn't have (the player undoes to follow it again)
    public List<(Vector2Int square, TileType tile, int number)> HintGhosts
    {
        get
        {
            var res = new List<(Vector2Int, TileType, int)>();
            if (hints.Stage < Hints.Stages) return res;
            foreach (int i in PlanMatcher.PendingSteps(level, stage3Family, stage3Order, stage3Steps, CurrentState()))
                if (i < hints.Revealed) res.Add((stage3Steps[i].square, stage3Steps[i].tile, i + 1));
            return res;
        }
    }

    // stage 3: when the game asks for an arrow, the plan's way (null when there's no hint for it)
    public Dir? HintArrow(string objectName, Vector2Int? square, string door) =>
        hints.Stage < Hints.Stages ? null : PlanMatcher.ExpectedArrow(level, stage3Family, objectName,
            new ArrowRecord { square = square, door = door }, arrowsSoFar.TryGetValue(objectName, out var a) ? a : null);

    // the hint button: the next stage, or one more ghost
    public void UseHint()
    {
        if (level == null || level.solution == null || levelOver || busy || inputLocked) return;
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

    // which plan stage 3 shows, fixed from here on:
    //   an empty board: the answer
    //   a board that fits a plan: that plan's tiles still to place
    //   a board that fits none: the nearest plan's whole sequence from step 1 (the ghosts show where the board differs)
    void FreezeStage3()
    {
        var s = level.solution;
        var st = CurrentState();
        (int family, int order) plan = (s.answerFamily, s.answerOrder);
        List<PlanStep> steps = null;
        if (st.tiles.Count > 0 || st.deliveries.Count > 0)
        {
            var fit = PlanMatcher.Closest(level, st);
            if (fit != null) { plan = fit.Value; steps = PlanMatcher.NextSteps(level, plan.family, plan.order, st); }
            else { plan = PlanMatcher.Nearest(level, st); steps = PlanMatcher.FromStart(level, plan.family, plan.order); }
        }
        if (steps == null) { plan = (s.answerFamily, s.answerOrder); steps = PlanMatcher.AnswerSteps(level); }
        stage3Steps = steps;
        (stage3Family, stage3Order) = plan;
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
        HintHidden = false;
        HintBadges = new List<(string, int)>();
        HintJunctions = new List<Vector2Int>();
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
