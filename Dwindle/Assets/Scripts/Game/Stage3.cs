using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// stage 3 of the hints (the answer), apart from GameManager so its tests (Assets/Editor/Tests) run this very code: the
// best plan it shows and its numbered steps, following the board to another plan, how each step stands, the tiles whose
// placing left no way to finish, and the way to take at a junction or door
public class Stage3
{
    readonly LevelData level;
    public List<PlanStep> Steps { get; private set; } = new List<PlanStep>();
    public (int family, int order) Plan { get; private set; }
    readonly HashSet<Vector2Int> brokePlan = new HashSet<Vector2Int>(); // shown as the wrong moment
    bool onTrack = true; // some best plan could still be finished, as of the last check
    public IReadOnlyCollection<Vector2Int> Marked => brokePlan; // for the tests
    public bool OnTrack => onTrack;

    // the plan it starts with, its whole sequence in the plan's own order (tiles already down that fit show as done): the
    // plan the board fits (on an empty board, the level's answer: the best plan with the fewest steps), or else the
    // nearest one (the ghosts show where the board differs)
    public Stage3(LevelData level, PlayerState st)
    {
        this.level = level;
        var s = level.solution;
        var plan = PlanMatcher.Closest(level, st) ?? PlanMatcher.Nearest(level, st);
        var steps = PlanMatcher.FromStart(level, plan.family, plan.order, st.tiles);
        if (steps == null) { plan = (s.answerFamily, s.answerOrder); steps = PlanMatcher.FromStart(level, plan.family, plan.order); }
        Steps = steps ?? new List<PlanStep>();
        Plan = plan;
    }

    // the best plans, this one first
    IEnumerable<(int family, int order)> PlansFromThis() => new[] { Plan }.Concat(PlanMatcher.Plans(level).Where(p => p != Plan));

    // some best plan can still be finished from here (round: mid-move, the square whose round is being played)
    bool CanFinish(PlayerState st, Vector2Int? round) =>
        PlanMatcher.Plans(level).Any(p => PlanMatcher.Reachable(level, p.family, p.order, st, round));

    // once everything has stopped: stays on a plan that can still be finished, or moves to one. with none, the tile just
    // placed (placed; null if this check isn't for a placement) is what broke it, if the board was on track before. the
    // mark stays even if that tile breaks in the delivery it caused, and goes once the board is back on track (an undo)
    // revealed: how many steps are shown (moving to another plan can show more, so every square shown before stays)
    public void Follow(PlayerState st, ref int revealed, Vector2Int? placed)
    {
        if (FollowPlan(st, ref revealed)) { brokePlan.Clear(); onTrack = true; return; }
        if (placed != null && onTrack) brokePlan.Add(placed.Value);
        onTrack = false;
    }

    bool FollowPlan(PlayerState st, ref int revealed)
    {
        if (PlanMatcher.Reachable(level, Plan.family, Plan.order, st))
        {
            // still this plan (even with every step shown): its tiles picked again for the board (a tile it also allows
            // counts, e.g. a + for a corner; the rest from the supply left)
            var states = PlanMatcher.StepStates(level, Plan.family, Plan.order, Steps, st);
            Steps = PlanMatcher.Refit(level, Plan.family, Plan.order, Steps, states, st) ?? Steps;
            return true;
        }
        // steps left to show: another plan whose first steps are the ones shown (their numbers stay)
        if (revealed < Steps.Count)
        {
            var shown = Steps.Take(revealed).ToList();
            foreach (var plan in PlanMatcher.Plans(level))
            {
                if (plan == Plan || !PlanMatcher.Reachable(level, plan.family, plan.order, st)) continue;
                var steps = PlanMatcher.Continue(level, plan.family, plan.order, shown, st.tiles);
                if (steps == null) continue;
                (Steps, Plan) = (steps, plan);
                return true;
            }
        }
        // else any plan that can still be finished (e.g. the same roads delivered in another order), numbered again
        var shownSquares = new HashSet<Vector2Int>(Steps.Take(revealed).Select(s => s.square));
        foreach (var plan in PlanMatcher.Plans(level).OrderBy(p => p.family != Plan.family)) // the same roads first
        {
            if (plan == Plan || !PlanMatcher.Reachable(level, plan.family, plan.order, st)) continue;
            var steps = PlanMatcher.FromStart(level, plan.family, plan.order, st.tiles);
            if (steps == null) continue;
            int last = steps.FindLastIndex(s => shownSquares.Contains(s.square));
            (Steps, Plan) = (steps, plan);
            revealed = Mathf.Min(steps.Count, Mathf.Max(revealed, last + 1));
            return true;
        }
        return false;
    }

    // how each step stands (see PlanMatcher.StepStates). moving: mid-move (an arrow being picked); round: the square
    // whose round it is, shown as the wrong moment at once if it already leaves no way to finish, even with the right turns
    public StepState[] States(PlayerState st, bool moving, Vector2Int? round)
    {
        var marked = brokePlan;
        if (round != null && onTrack && !CanFinish(st, round)) marked = new HashSet<Vector2Int>(brokePlan) { round.Value };
        return PlanMatcher.StepStates(level, Plan.family, Plan.order, Steps, st, marked, moving);
    }

    // the way to take at a junction (here.square) or out of a door (here.door), shown only where that square's step is:
    // at, the object's square (for a door, the square it went in from). the way of a best plan that can still be
    // finished (this one first), so following it keeps the tiles light; else of one what has happened so far fits
    public Dir? Arrow(PlayerState st, string objectName, ArrowRecord here, Vector2Int at, int revealed, Vector2Int round)
    {
        if (!Steps.Take(revealed).Any(s => s.square == at)) return null;
        var picked = st.arrows.TryGetValue(objectName, out var a) ? a : null;
        foreach (bool finishable in new[] { true, false })
            foreach (var plan in PlansFromThis())
            {
                if (!PlanMatcher.FitsSoFar(level, plan.family, plan.order, st)) continue; // mid-move: what has happened so far
                if (finishable && !PlanMatcher.Reachable(level, plan.family, plan.order, st, round)) continue;
                var way = PlanMatcher.ExpectedArrow(level, plan.family, objectName, here, picked);
                if (way != null) return way;
            }
        return PlanMatcher.ExpectedArrow(level, Plan.family, objectName, here, picked);
    }
}
