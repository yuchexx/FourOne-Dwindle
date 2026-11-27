using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Redo reapplies only actions removed by Undo. A new placement, move, take-back or direction starts a new branch.
public partial class GameManager
{
    class RedoStep
    {
        public Snapshot placement;
        public ChoicePoint choice;
        public ChoicePoint[] answers;
    }

    readonly Stack<RedoStep> redoSteps = new Stack<RedoStep>();
    readonly Queue<ChoicePoint> redoAnswers = new Queue<ChoicePoint>();
    bool redoingPlacement;
    bool redoingChoice;

    bool CanRedo
    {
        get
        {
            if (levelOver || redoSteps.Count == 0 || redoingChoice || board.UndoPlaying || board.Dragging) return false;
            var step = redoSteps.Peek();
            if (step.choice != null)
                return asking != null && asking.Value.o == objects[step.choice.obj]
                    && asking.Value.door == step.choice.door && asking.Value.ways.Contains(step.choice.direction);
            if (busy) return false;

            var snapshot = step.placement;
            var action = snapshot.action;
            var at = level.At(action.board, action.square.x, action.square.y);
            var cell = Cells[at.x, at.y];
            if (snapshot.takeBack) return cell.tile == action.tile && !cell.locked;
            if (cell.tile != null || cell.obstacle || cell.closed) return false;
            if (snapshot.move)
            {
                if (snapshot.movedFrom == null) return false;
                var from = snapshot.movedFrom.Value;
                var source = Cells[from.x, from.y];
                return source.tile == action.tile && !source.locked;
            }
            return action.tile is TileType tile && inventory[tile] > 0;
        }
    }

    void ClearRedo()
    {
        redoSteps.Clear();
        redoAnswers.Clear();
        redoingChoice = false;
    }

    void RecordAction(Snapshot snapshot)
    {
        if (!redoingPlacement) ClearRedo();
        history.Record(snapshot);
    }

    void RememberRedoPlacement(Snapshot snapshot)
    {
        redoSteps.Push(new RedoStep
        {
            placement = snapshot,
            answers = snapshot.choices.Where(c => c.answered).ToArray(),
        });
        redoAnswers.Clear();
        redoingChoice = false;
    }

    void RememberRedoChoice(ChoicePoint choice)
    {
        redoSteps.Push(new RedoStep { choice = choice });
        redoAnswers.Clear();
        redoingChoice = false;
    }

    // The scene's Redo button calls this. Each press reapplies one Undo, including a step back to an arrow question.
    public void Redo()
    {
        if (!CanRedo) return;
        var step = redoSteps.Peek();
        if (step.choice != null)
        {
            if (!board.TryChooseDirection(step.choice.direction)) return;
            redoSteps.Pop();
            redoingChoice = true;
            hud.SetRedoAvailable(false);
            Report(new GameAction { kind = GameActionKind.Redo, undid = GameActionKind.ChooseDirection }); // for analytics
            return;
        }

        board.FinishUndo();
        DropPickedUp();
        board.HideGhost();
        selected = null;
        foreach (var answer in step.answers) redoAnswers.Enqueue(answer);

        var snapshot = step.placement;
        var action = snapshot.action;
        var at = level.At(action.board, action.square.x, action.square.y);
        var before = history.CanUndo ? history.Peek() : null;
        redoingPlacement = true;
        try
        {
            if (snapshot.takeBack) OnCellRightClicked(at);
            else if (snapshot.move) OnTileMoved(snapshot.movedFrom.Value, at);
            else
            {
                selected = action.tile;
                PlaceSelected(at);
            }
        }
        finally
        {
            redoingPlacement = false;
        }
        // the step goes only once it was replayed (it recorded a new snapshot); refused, it stays for another try
        bool replayed = history.CanUndo && history.Peek() != before;
        if (replayed)
        {
            if (redoSteps.Count > 0 && redoSteps.Peek() == step) redoSteps.Pop();
            // for analytics: what it put back (the placement itself was reported as usual)
            Report(new GameAction { kind = GameActionKind.Redo, undid = action.kind, tile = action.tile, board = action.board, square = action.square });
        }
        else redoAnswers.Clear();
        selected = null;
        RefreshAll();
    }

    // a redone placement answers this question itself (AskDirectionWithRedo): no arrows show, so no arrow sound or tints
    bool RedoAnswers(MovingObject o, List<Dir> ways, DoorSpec door)
    {
        if (redoAnswers.Count == 0) return false;
        var answer = redoAnswers.Peek();
        return objects[answer.obj] == o && answer.door == door && ways.Contains(answer.direction);
    }

    // Replaying a whole placement also restores the directions the player chose during that placement.
    // A new arrow click clears the remaining Redo actions, even when the player chooses the same direction.
    IEnumerator AskDirectionWithRedo(MovingObject o, List<Dir> ways, DoorSpec door)
    {
        if (redoAnswers.Count > 0)
        {
            var answer = redoAnswers.Peek();
            if (objects[answer.obj] == o && answer.door == door && ways.Contains(answer.direction))
            {
                redoAnswers.Dequeue();
                o.heading = answer.direction;
                yield break;
            }
            ClearRedo();
        }

        if (hints.Stage >= Hints.Stages && !Session.HintGuideSeen(3) && HintArrow(o, door) != null) StartCoroutine(ArrowGuideSoon()); // the answer's first arrow: its guide step, once
        if (door == null) yield return board.AskDirection(o, ways, HintArrow(o));
        else
        {
            var partner = level.Partner(door);
            onDoorQuestion?.Invoke(o.spec.name, door.label); // for the tutorial (GameManager.Tutorial.cs): a step over these arrows
            yield return board.AskDirectionAt(o, board.EdgePos(partner.cell, partner.side), ways, HintArrow(o, door));
        }
        if (!redoingChoice) ClearRedo();
        redoingChoice = false;
        hud.SetRedoAvailable(CanRedo);
    }
}
