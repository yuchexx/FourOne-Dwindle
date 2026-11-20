using System;
using System.Collections.Generic;
using System.Linq;

// the "Doors" tutorial (on the level with l.tutorial = "Doors"): one step framing both pairs of doors; then the player
// plays the level, and its coach shows, once, that Blue coming out of the right board's door B (not on the board's
// edge) can go either way; at the level's end, a step on what using doors scored
public static partial class Tutorials
{
    static List<TutorialStep> Doors(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Doors teleport objects.",
            targets = Target.Door(game, "A", 0).Concat(Target.Door(game, "A", 1))
                .Concat(Target.Door(game, "B", 0)).Concat(Target.Door(game, "B", 1)).ToArray(), // both ends of A and of B
            buttonText = "End",
        },
    };

    // no guides and no slips undone: just the step on Blue's way out of door B, as its arrows come up. Blue's only
    // arrows on this level are there, so its choice is Done (the step won't show again)
    static TutorialCoach DoorsCoach(GameManager game) => new()
    {
        onAction = a => a.kind == GameActionKind.ChooseDirection && a.objectName == "Blue" ? CoachReply.Done : CoachReply.None,
        doorSteps = (name, door) => name != "Blue" || door != "B" ? null : new List<TutorialStep>
        {
            new TutorialStep
            {
                text = "This door isn't on the board's edge,\nso you can choose which way to come out.",
                targets = new[] { Target.Object(game, "Blue"), Target.Arrow(game, Dir.W), Target.Arrow(game, Dir.E) },
                buttonText = "End",
            },
        },
    };

    // once everyone is delivered and the level ends: each delivery that used doors, framed on its ring, the door it came
    // out of (its "+50" over it) and its row in the score's history, before the end window shows
    static List<TutorialStep> DoorsEnd(GameManager game)
    {
        var log = game.hud.GetScoreLog(); // newest first, as the history shows it: an entry's index is its row's
        var targets = new List<Func<UnityEngine.RectTransform>>();
        var rows = new List<Func<UnityEngine.RectTransform>>();
        var pops = new List<TutorialPop>();
        for (int i = 0; i < log.Count; i++)
        {
            var e = log[i];
            if (!e.door || e.objects == null || e.objects.Length == 0) continue; // not a delivery that used doors
            var color = e.objects[0]; // the rows know objects by color
            var door = new Func<UnityEngine.RectTransform>[] { () => game.DoorOutRect(color, false), () => game.DoorOutRect(color, true) };
            targets.Add(() => game.RingRectOf(color));
            targets.AddRange(door);
            rows.Add(Target.ScoreRow(game, i));
            pops.Add(new TutorialPop { text = $"+{game.pointsPerTeleport}", near = door });
        }
        targets.AddRange(rows);
        return new List<TutorialStep>
        {
            new TutorialStep
            {
                text = $"Congrats!\nDelivering an object using doors\nscores {game.pointsPerTeleport} extra,\nhowever many doors it goes through.",
                targets = targets.ToArray(),
                bubbleBy = rows.ToArray(), // left of the rows, out of the board's way (as IntroEnd's)
                bubble = BubbleSide.Left,
                pops = pops.ToArray(),
                buttonText = "End",
            },
        };
    }
}
