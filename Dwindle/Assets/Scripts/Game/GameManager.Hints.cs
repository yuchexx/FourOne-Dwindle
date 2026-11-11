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

    // stage 3 (see Stage3): made at its first press, null before. placedForHints: a placement waiting to be checked
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
    // pressed while objects move, it waits and runs once the board is still (see Update), so the press isn't lost
    public void UseHint()
    {
        hintQueued = false;
        if (level == null || level.solution == null || levelOver) return;
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
                stage3 = new Stage3(level, CurrentState());
                hints.Stage = 3;
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
        var st = CurrentState(); // the board, once for this redraw
        if (!busy) FollowBoard(st); // mid-round the board is half moved: it's judged once everything has stopped
        if (HintHidden) { board.ShowHints(new List<(string, int)>(), new List<(Vector2Int, int)>(), new List<(Vector2Int, TileType, int, StepState)>()); return; }
        bool two = hints.Stage == 2;
        board.ShowHints(two ? HintBadges : new List<(string, int)>(), two ? HintJunctions : new List<(Vector2Int, int)>(), HintGhosts(st));
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
        list.Add(new ArrowRecord { square = door == null ? o.pos : (Vector2Int?)null, door = door, way = o.heading,
                                   from = door == null ? (Vector2Int?)null : o.pos }); // o.pos: still the square it went in from
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

    // LoadLevel (and so Restart, Replay, Next, picking a level): a fresh start, hints included
    void ResetHintsForLoad()
    {
        hints.Clear();
        hintQueued = false;
        HintHidden = false;
        HintBadges = new List<(string, int)>();
        HintJunctions = new List<(Vector2Int, int)>();
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
    public bool AnimateUndo { get => board.animateUndo; set => board.animateUndo = value; }
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
