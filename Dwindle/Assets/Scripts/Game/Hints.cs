using System.Collections.Generic;
using System.Linq;

// the hint button's three stages for the level being played:
//   1. how many tiles the best plans use   2. the delivery order   3. the answer, one more step each press
// stage 3 costs the purple stars. undo never changes the hints. restarting before the level ends keeps stage 3, so a
// restart can't win purple back, but clears stages 1 and 2. replaying after the level ended, or leaving it, clears all
public class Hints
{
    public const int Stages = 3;
    public int Stage { get; private set; }       // 0 = none used yet
    public int AnswerSteps { get; private set; } // how many steps of the answer are shown (stage 3)
    public bool PurpleLost => Stage >= Stages;

    // a press: the next stage, or on stage 3 one more step of the answer. false when there's nothing more to show
    public bool Advance(int answerLength)
    {
        if (Stage < Stages)
        {
            Stage++;
            if (Stage == Stages) AnswerSteps = 1; // stage 3 opens on the answer's first step
            return true;
        }
        if (AnswerSteps < answerLength) { AnswerSteps++; return true; }
        return false;
    }

    public void Clear() { Stage = 0; AnswerSteps = 0; }

    // Restart before the level ended
    public void OnRestart() { if (Stage < Stages) Clear(); }

    // stage 1
    public static string TilesText(LevelData l) => $"Best plans use {l.bestTiles} tiles";

    // stage 2: the deliveries of the plan the answer follows, e.g. "Build part of Red's road first. Then Blue and
    // Orange arrive together. Then Red and Green arrive together."
    public static List<string> OrderText(LevelData l)
    {
        var lines = new List<string>();
        var s = l.solution;
        if (s == null) return lines;
        var family = s.families[s.answerFamily];
        var deliveries = family.orders[s.answerOrder].deliveries;
        var arrived = new HashSet<string>();
        for (int k = 0; k < deliveries.Length; k++)
        {
            var d = deliveries[k];
            var first = d.completedBy[0];
            // the "before" tiles are on the roads of objects still to come
            var builders = family.roads.Where(r => !arrived.Contains(r.objectName) && !d.objects.Contains(r.objectName)
                && first.before.Any(b => r.squares.Any(q => q.board == b.board && q.square == b.square)))
                .Select(r => r.objectName).ToList();
            if (builders.Count == 1) lines.Add($"Build part of {builders[0]}'s road first.");
            else if (builders.Count > 1) lines.Add($"Build part of {Names(builders.Select(b => b + "'s").ToList())} roads first.");
            string who = Names(d.objects);
            lines.Add((k == 0 ? "" : "Then ") + (d.objects.Length > 1 ? $"{who} arrive together." : $"{who} arrives."));
            foreach (var o in d.objects) arrived.Add(o);
        }
        if (lines.Count > 0) lines[0] = char.ToUpper(lines[0][0]) + lines[0].Substring(1);
        return lines;
    }

    // stage 3: the answer in presses, a tile with the arrows it makes the player pick right after it
    public static List<GameAction[]> AnswerPresses(LevelData l)
    {
        var steps = new List<GameAction[]>();
        if (l.solution?.answer == null) return steps;
        var current = new List<GameAction>();
        foreach (var a in l.solution.answer)
        {
            if (a.kind == GameActionKind.PlaceTile && current.Count > 0) { steps.Add(current.ToArray()); current.Clear(); }
            current.Add(a);
        }
        if (current.Count > 0) steps.Add(current.ToArray());
        return steps;
    }

    static string Names(IList<string> names) =>
        names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
}
