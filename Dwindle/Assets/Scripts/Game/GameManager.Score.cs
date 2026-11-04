using UnityEngine;

// score and stars, and the Leave and End screens that show them. part of GameManager
public partial class GameManager
{
    int deliveryPoints, comboPoints, teleportPoints, unusedPoints; // kept per source, so undo can put each back
    int Score => deliveryPoints + comboPoints + teleportPoints + unusedPoints;
    bool ScoreStar => Score >= level.targetScore;

    // 1 = all but one delivered, 2 = all delivered, 3 = all delivered and the target score
    int Stars()
    {
        if (Delivered < level.Required) return 0;
        if (Delivered < objects.Count) return 1;
        return ScoreStar ? 3 : 2;
    }

    // ended: the End screen; otherwise the Leave screen
    void ShowMenu(bool ended)
    {
        int stars = Stars();
        bool isLast = levelIndex >= Levels.All.Count - 1;

        // the stars earned so far and the goal under each
        bool[] earned = { Delivered >= level.Required, Delivered >= objects.Count, stars == 3 };
        string[] goals = { $"{level.Required} of {objects.Count}", $"{objects.Count} of {objects.Count}", $"{level.targetScore} points" };
        if (!ended)
        {
            if (menu.leaveDialog != null) menu.leaveDialog.Show($"Leave {level.title}?", earned, goals, $"Delivered {Delivered} · Score {Score}");
            return;
        }
        if (menu.endWindow == null) return; // not set up yet (Step 3)
        bool purple = stars == 3 && !hints.PurpleLost;
        menu.endWindow.Show(EndTitle(stars, purple), earned, purple, goals,
            stars == 3 ? $"Score {Score} · target reached!" : $"Score {Score}",
            EndNote(stars, purple, isLast),
            canUndo: history.CanUndo, canNext: stars >= 1 && !isLast);
    }

    // the end window's title: one of a few per result (0-3 stars, purple), never last time's for that result. picked
    // once per ending (FinishLevel clears it), so reopening the window keeps it
    static readonly string[][] EndTitles =
    {
        new[] { "So close", "Almost there", "Not quite" },
        new[] { "Good start!", "Getting there!", "Nearly!" },
        new[] { "All home!", "Nice work!", "Well done!" },
        new[] { "Solved!", "Great plan!", "Nailed it!" },
        new[] { "Genius!", "Brilliant!", "Masterful!" },
    };
    static readonly int[] lastEndTitle = { -1, -1, -1, -1, -1 };
    static readonly System.Random titleRandom = new System.Random(); // apart from the game's own randomness
    string endTitle;

    string EndTitle(int stars, bool purple)
    {
        if (endTitle != null) return endTitle;
        int group = purple ? 4 : stars;
        var titles = EndTitles[group];
        int pick;
        do pick = titleRandom.Next(titles.Length); while (titles.Length > 1 && pick == lastEndTitle[group]);
        lastEndTitle[group] = pick;
        return endTitle = titles[pick];
    }

    // the end window's short line: a tip toward the next star, or a cheer
    string EndNote(int stars, bool purple, bool isLast)
    {
        string note =
            stars == 3 ? (purple ? "You found the best plan on your own." : "Now try it without the answer.")
            : stars == 2 ? Tip(level.bestPlanHint)
            : Tip(level.finishHint); // levels where delivering everyone needs the trick
        if (isLast && stars > 0) note = note == null ? "All levels done!" : note + "\nAll levels done!";
        return note;
    }

    static string Tip(string hint) => string.IsNullOrEmpty(hint) ? null : "Tip: " + hint;
}
