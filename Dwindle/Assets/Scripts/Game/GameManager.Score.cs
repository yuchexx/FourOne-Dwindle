using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// score and stars, and the end window that shows them. part of GameManager
public partial class GameManager
{
    // score, kept per source so the end window can show where it came from
    int deliveryPoints, comboPoints, teleportPoints, unusedPoints;
    int comboObjects, teleports, unusedTiles; // how many of each, for the breakdown (teleports: objects that used a door)
    int Score => deliveryPoints + comboPoints + teleportPoints + unusedPoints;
    bool ScoreStar => Score >= level.targetScore;

    // 1 = all but one delivered, 2 = all delivered, 3 = all delivered and the target score
    int Stars()
    {
        if (Delivered < level.Required) return 0;
        if (Delivered < objects.Count) return 1;
        return ScoreStar ? 3 : 2;
    }

    void ShowMenu(bool ended, string reason)
    {
        int stars = Stars();
        bool isLast = levelIndex >= Levels.All.Count - 1;
        string summary =
            (reason != null ? reason + "\n" : "") +
            $"Delivered {Delivered} / {objects.Count}\n" +
            ScoreBreakdown(ended) +
            (stars == 0 ? "\nEarn at least 1 star to unlock the next level." : "") +
            // a nudge toward the 3rd star, without giving the solution away
            (ended && stars == 2 && !string.IsNullOrEmpty(level.bestPlanHint) ? "\n" + level.bestPlanHint : "") +
            // and toward delivering everyone, on levels where that needs a trick
            (ended && stars <= 1 && !string.IsNullOrEmpty(level.finishHint) ? "\n" + level.finishHint : "") +
            (isLast && stars > 0 ? "\nThat was the last level!" : "");

        menu.ShowMenu(
            level.title + (ended ? " complete" : " paused"),
            summary,
            Delivered >= level.Required,
            Delivered >= objects.Count,
            stars == 3, // the score only earns the 3rd star once everyone is delivered
            showResume: !ended,
            showNext: ended && stars >= 1 && !isLast,
            showUndo: ended && history.CanUndo); // nothing to undo if the level was ended before any placement
    }

    // one line per source: what, how many x points, and the subtotal, then the total against the target
    string ScoreBreakdown(bool ended)
    {
        string Line(string what, string count, int points) =>
            $"{what}<pos=58%>{count}<pos=86%>{points}\n";

        bool allDelivered = objects.All(o => o.completed);
        string unused =
            allDelivered ? Line("Unused tiles", $"{unusedTiles} x {pointsPerUnusedTile}", unusedPoints)
            : ended ? "Unused tiles<pos=58%>not counted\n" // only counted when everything is delivered
            : "Unused tiles<pos=58%>counted at the end\n";

        return
            "<align=left><indent=3%>" +
            Line("Deliveries", $"{Delivered} x {pointsPerDelivery}", deliveryPoints) +
            Line("Delivered together", $"{comboObjects} x {comboPointsPerObject}", comboPoints) +
            Line("Used a door", $"{teleports} x {pointsPerTeleport}", teleportPoints) +
            unused +
            $"<b>Total<pos=86%>{Score}</b>\n" +
            $"<color=#A0A2A8>3rd star target<pos=86%>{level.targetScore}</color>" +
            "</indent></align>";
    }
}
