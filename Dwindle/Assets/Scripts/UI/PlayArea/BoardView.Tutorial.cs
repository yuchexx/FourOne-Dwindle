using System.Collections.Generic;
using UnityEngine;

// for the tutorial (Level 24's linked doors): each door's bar and badge, by letter. part of BoardView
public partial class BoardView
{
    // each door's bar and its letter's badge, in the level's order (MakeDoor), for Target.Door
    readonly List<(DoorSpec door, RectTransform bar, RectTransform badge)> doorViews = new();

    // a door's bar, or the badge with its letter (which reaches past the bar's sides), by its letter and which end of
    // the pair (0 = the first in Levels.cs); null when there's no such door
    public RectTransform DoorPart(string label, int which, bool badge)
    {
        foreach (var (door, bar, letterBadge) in doorViews)
            if (door.label == label && which-- == 0) return badge ? letterBadge : bar;
        return null;
    }
}
