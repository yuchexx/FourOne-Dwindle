using System;
using System.Collections.Generic;
using UnityEngine;

// what this play session remembers: each level's best stars and whether it earned purple, and the Unlock all setting.
// nothing is saved, so reloading the page starts over
public static class Session
{
    // Settings: every level can be played, whatever the stars
    public static bool unlockAll;

    // Settings: how tiles are placed - click a tile then a square, drag a tile onto a square, or either
    public enum Placement { Both, Click, Drag }
    public static Placement placement = Placement.Both;

    // Settings: the supply shows only the tiles this level hands out (the + always, since it can be earned)
    public static bool compactSupply;

    // Settings: tutorial levels show their step-by-step boxes
    public static bool showTutorials = true;

    // the best result per level (index in Levels.All) this session
    public struct Record
    {
        public int stars;    // 0 to 3
        public bool purple;  // 3 stars without the answer hint
    }
    static readonly Dictionary<int, Record> records = new Dictionary<int, Record>();

    public static Record Get(int level) => records.TryGetValue(level, out var r) ? r : default;

    // keeps the best: the most stars, and purple once it's been earned
    public static void Save(int level, int stars, bool purple)
    {
        var r = Get(level);
        r.stars = Math.Max(r.stars, stars);
        r.purple |= purple;
        records[level] = r;
    }

    // open when it's the first level, the one before it has a star, or Unlock all is on
    public static bool IsUnlocked(int level) => unlockAll || level == 0 || Get(level - 1).stars >= 1;

    // where Start goes: the first level without a star (so Level 1 until it's done), or the last level once all have one
    public static int StartLevel(int count)
    {
        for (int i = 0; i < count; i++)
            if (Get(i).stars == 0) return i;
        return count - 1;
    }

    // a new session each time Play is pressed in the editor
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void StartOver()
    {
        records.Clear();
        unlockAll = false;
        placement = Placement.Both;
        compactSupply = false;
        showTutorials = true;
    }
}
