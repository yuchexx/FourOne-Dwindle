using System;
using System.Collections.Generic;
using UnityEngine;

// what the game remembers: each level's best stars and whether it earned purple, the settings, and analytics' player id
// and try counts. saved in the browser (PlayerPrefs), so a reload keeps it; Reset progress clears it
public static class Session
{
    const string Key = "dwindle."; // every saved value starts with this

    // Settings: every level can be played, whatever the stars
    static bool unlockAllValue;
    public static bool unlockAll { get => unlockAllValue; set { unlockAllValue = value; SetBool("unlockAll", value); } }

    // Settings: how tiles are placed - click a tile then a square, drag a tile onto a square, or either
    public enum Placement { Both, Click, Drag }
    static Placement placementValue;
    public static Placement placement { get => placementValue; set { placementValue = value; SetInt("placement", (int)value); } }

    // Settings: the supply shows only the tiles this level hands out (the + always, since it can be earned)
    static bool compactSupplyValue;
    public static bool compactSupply { get => compactSupplyValue; set { compactSupplyValue = value; SetBool("compactSupply", value); } }

    // Settings: tutorial levels show their step-by-step boxes
    static bool showTutorialsValue;
    public static bool showTutorials { get => showTutorialsValue; set { showTutorialsValue = value; SetBool("showTutorials", value); } }

    // Settings: hovering a stage 2 hint mark says what it means
    static bool showHintTipsValue;
    public static bool showHintTips { get => showHintTipsValue; set { showHintTipsValue = value; SetBool("showHintTips", value); } }

    // Settings: undo plays the move backwards
    static bool animateUndoValue;
    public static bool animateUndo { get => animateUndoValue; set { animateUndoValue = value; SetBool("animateUndo", value); } }

    // Settings: undo goes back to the last arrow question first, then the placement (on by default)
    static bool undoByStepValue;
    public static bool undoByStep { get => undoByStepValue; set { undoByStepValue = value; SetBool("undoByStep", value); } }

    // Settings: hovering a placement (or an arrow) crosses out the tiles it would break (on by default)
    static bool showBreaksValue;
    public static bool showBreaks { get => showBreaksValue; set { showBreaksValue = value; SetBool("showBreaks", value); } }

    // Settings > Sound: 0 to 1
    static float musicVolumeValue, effectsVolumeValue;
    public static float musicVolume { get => musicVolumeValue; set { musicVolumeValue = value; SetFloat("musicVolume", value); } }
    public static float effectsVolume { get => effectsVolumeValue; set { effectsVolumeValue = value; SetFloat("effectsVolume", value); } }

    // the best result per level (index in Levels.All)
    public struct Record
    {
        public int stars;    // 0 to 3
        public bool purple;  // 3 stars without the answer hint
    }
    static readonly Dictionary<int, Record> records = new Dictionary<int, Record>();

    public static Record Get(int level) => records.TryGetValue(level, out var r) ? r : default;

    // the level opened most recently (-1: none yet); the Levels page opens on its page
    static int lastOpenedValue = -1;
    public static int lastOpened { get => lastOpenedValue; set { lastOpenedValue = value; SetInt("lastOpened", value); } }

    // keeps the best: the most stars, and purple once it's been earned
    public static void Save(int level, int stars, bool purple)
    {
        var r = Get(level);
        r.stars = Math.Max(r.stars, stars);
        r.purple |= purple;
        records[level] = r;
        PlayerPrefs.SetInt($"{Key}level{level}.stars", r.stars);
        SetBool($"level{level}.purple", r.purple);
    }

    // enough stars to unlock the next level (most levels 1, teach levels 3: LevelData.starsToPass)
    public static bool Passed(int level) => Get(level).stars >= Levels.All[level].starsToPass;

    // open when it's the first level, the one before it is passed, or Unlock all is on
    public static bool IsUnlocked(int level) => unlockAll || level == 0 || Passed(level - 1);

    // where Start goes: the first level not passed (so Level 1 until it's done), or the last level once all are
    public static int StartLevel(int count)
    {
        for (int i = 0; i < count; i++)
            if (!Passed(i)) return i;
        return count - 1;
    }

    // for analytics: one id per player that lasts between visits (a new one after Reset progress)
    public static string PlayerId
    {
        get
        {
            string id = PlayerPrefs.GetString(Key + "playerId", "");
            if (id == "") { id = Guid.NewGuid().ToString(); PlayerPrefs.SetString(Key + "playerId", id); PlayerPrefs.Save(); }
            return id;
        }
    }

    // for analytics: counts one more try on a level, before or after it's passed, and returns the new total
    public static int AddTry(int level, bool afterPassing)
    {
        string k = $"{Key}level{level}.{(afterPassing ? "triesAfter" : "triesBefore")}";
        int n = PlayerPrefs.GetInt(k, 0) + 1;
        PlayerPrefs.SetInt(k, n);
        PlayerPrefs.Save();
        return n;
    }

    // Settings > Reset progress: stars, settings, the player id and try counts all go back to a first visit
    public static void ResetAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Load();
    }

    // the level list's version: change it when levels are reordered, so old stars don't land on other levels
    const string LevelOrder = "plan-135";

    // a new level order: stars, purple, tries and the last level opened go; settings and the player id stay
    static void ForgetLevels()
    {
        for (int i = 0; i < 200; i++)
            foreach (var k in new[] { "stars", "purple", "triesBefore", "triesAfter" }) PlayerPrefs.DeleteKey($"{Key}level{i}.{k}");
        PlayerPrefs.DeleteKey(Key + "lastOpened");
        PlayerPrefs.SetString(Key + "levelOrder", LevelOrder);
        PlayerPrefs.Save();
    }

    static void SetBool(string name, bool value) => SetInt(name, value ? 1 : 0);
    static void SetInt(string name, int value) { PlayerPrefs.SetInt(Key + name, value); PlayerPrefs.Save(); }
    static void SetFloat(string name, float value) => PlayerPrefs.SetFloat(Key + name, value); // a slider: written out by SaveNow
    public static void SaveNow() => PlayerPrefs.Save(); // Settings closed: the volumes go to disk
    static bool GetBool(string name, bool fallback) => PlayerPrefs.GetInt(Key + name, fallback ? 1 : 0) == 1;

    // reads what was saved (defaults on a first visit). also runs each time Play is pressed in the editor
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Load()
    {
        unlockAllValue = GetBool("unlockAll", false);
        placementValue = (Placement)PlayerPrefs.GetInt(Key + "placement", (int)Placement.Both);
        compactSupplyValue = GetBool("compactSupply", false);
        showTutorialsValue = GetBool("showTutorials", true);
        showHintTipsValue = GetBool("showHintTips", true);
        animateUndoValue = GetBool("animateUndo", true);
        undoByStepValue = GetBool("undoByStep", true);
        showBreaksValue = GetBool("showBreaks", true);
        musicVolumeValue = PlayerPrefs.GetFloat(Key + "musicVolume", 0.8f);
        effectsVolumeValue = PlayerPrefs.GetFloat(Key + "effectsVolume", 0.8f);
        if (PlayerPrefs.GetString(Key + "levelOrder", "") != LevelOrder) ForgetLevels(); // stars are saved by place in the list
        lastOpenedValue = PlayerPrefs.GetInt(Key + "lastOpened", -1);
        if (lastOpenedValue >= Levels.All.Count) lastOpenedValue = -1; // the levels changed since
        records.Clear();
        for (int i = 0; i < Levels.All.Count; i++)
        {
            int stars = PlayerPrefs.GetInt($"{Key}level{i}.stars", 0);
            bool purple = GetBool($"level{i}.purple", false);
            if (stars > 0 || purple) records[i] = new Record { stars = stars, purple = purple };
        }
    }
}
