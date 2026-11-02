using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// for the tutorial (TutorialController and Tutorials.cs): what its steps and coaches look at and do on the level.
// objects go by name and squares by their board's own coordinates, as in Levels.cs. part of GameManager
public partial class GameManager
{
    // an object's circle and its ring on this level (Target.Object and Target.Ring frame them); null when the level
    // has no object of that name
    public RectTransform ObjectRect(string name) => Named(name)?.view.Rect;
    public RectTransform RingRect(string name) { var o = Named(name); return o != null ? (RectTransform)o.dest.transform : null; }
    MovingObject Named(string name) => objects?.Find(o => o.spec.name == name);

    // the ring of the object with this color (the score history's rows know objects by color); null when none has it
    public RectTransform RingRectOf(Color color)
    {
        var o = objects?.Find(x => x.spec.color == color);
        return o != null ? (RectTransform)o.dest.transform : null;
    }

    // the letters of the doors the object with this color went through, each once (the score history's rows know
    // objects by color); empty when none
    public List<string> DoorsUsedBy(Color color)
    {
        var o = objects?.Find(x => x.spec.color == color);
        return o != null ? o.doorsUsed.Select(d => d.door).Distinct().ToList() : new List<string>();
    }

    // the door the object with this color last came out of: its bar, or its letter's badge; null when it used none
    public RectTransform DoorOutRect(Color color, bool badge)
    {
        var o = objects?.Find(x => x.spec.color == color);
        if (o == null || o.doorsUsed.Count == 0) return null;
        var pass = o.doorsUsed[o.doorsUsed.Count - 1];
        var cell = level.At(pass.outBoard, pass.outSquare.x, pass.outSquare.y);
        int which = 0; // which end of its pair, in the level's order (as BoardView.DoorPart counts them)
        foreach (var d in level.doors)
        {
            if (d.label != pass.door) continue;
            if (d.board == pass.outBoard && d.cell == cell && d.side == pass.outSide) return board.DoorPart(d.label, which, badge);
            which++;
        }
        return null;
    }

    // a door's bar, or its letter's badge (see BoardView.DoorPart)
    public RectTransform DoorRect(string label, int which, bool badge) => board.DoorPart(label, which, badge);

    // how many of a tile type are left in the supply
    public int TilesLeft(TileType t) => inventory != null ? inventory.GetValueOrDefault(t) : 0;

    // the road to build, dotted across the board like the lines from its start and to its ring (BoardView.GuideLine);
    // each square's part hides under a tile. squares: the road's squares from the start's side, as (board, x, y) as in
    // Levels.cs, each next to the one before, or through a door: two squares not next to each other are a door's two
    // sides, and the line stops at the door it goes into and goes on from its partner. none = straight from the start
    // to the ring, for an object whose start and ring are in one row or column. gone once the object is delivered,
    // when the level loads again, or with ClearGuides
    public void ShowRoadLine(string name, params (int board, int x, int y)[] squares)
    {
        var o = Named(name);
        if (o == null) return;
        var road = new List<Vector2Int>();
        foreach (var s in squares) road.Add(level.At(s.board, s.x, s.y));
        if (road.Count == 0)
        {
            var step = o.spec.dest - o.spec.start;
            if (step.x != 0 && step.y != 0) return; // not straight: it needs its squares
            var d = new Vector2Int(System.Math.Sign(step.x), System.Math.Sign(step.y));
            for (var c = o.spec.start + d; c != o.spec.dest; c += d) road.Add(c);
        }
        if (road.Count == 0) return; // no square to cross
        // in stretches between doors
        Vector2 from = board.EdgeBetween(o.spec.start, road[0]);
        var stretch = new List<Vector2Int> { road[0] };
        for (int i = 1; i <= road.Count; i++)
        {
            if (i < road.Count && Mathf.Abs(road[i].x - road[i - 1].x) + Mathf.Abs(road[i].y - road[i - 1].y) == 1)
            {
                stretch.Add(road[i]);
                continue;
            }
            if (i == road.Count) // the last stretch, to the ring
            {
                board.GuideLine(o.spec, from, stretch, board.EdgeBetween(o.spec.dest, stretch[stretch.Count - 1]));
                break;
            }
            // a jump: the door on the last square's edge whose partner is on the next square's
            var into = level.doors.FirstOrDefault(d => Touches(d, road[i - 1]) && Touches(level.Partner(d), road[i]));
            if (into == null) return; // not a door: nothing more to draw
            var outOf = level.Partner(into);
            board.GuideLine(o.spec, from, stretch, board.EdgePos(into.cell, into.side));
            from = board.EdgePos(outOf.cell, outOf.side);
            stretch = new List<Vector2Int> { road[i] };
        }
    }

    // the door is on one of the square's edges
    static bool Touches(DoorSpec door, Vector2Int square) => door.cell == square || door.Neighbor == square;

    // no tile can go on the square until OpenSquare or ClearGuides (or the level loads again); it's dimmed and has a big X
    public void CloseSquare(int boardIndex, int x, int y) => board.SetClosed(level.At(boardIndex, x, y), true);

    // a closed square opens, its X fading out (BoardView.OpenAnimated)
    public void OpenSquare(int boardIndex, int x, int y) => board.OpenAnimated(level.At(boardIndex, x, y), 0.4f);

    // the road lines go and the closed squares open
    public void ClearGuides() => board.ClearGuides();

    // while held, a level that's over doesn't end: no end window, no stuck notice, no result saved. for a coach about
    // to reset the level (ReloadLevel), so the attempt it throws away doesn't count as played. LoadLevel lets go
    bool levelEndHeld;
    public void HoldLevelEnd(bool hold) => levelEndHeld = hold;

    // while held, delivering together doesn't flash the + slot (the earned flash) right away: the tutorial plays it
    // later with FlashEarnedPlus, once a step frames the slot. LoadLevel lets go
    bool earnedFlashHeld;
    public void HoldEarnedFlash(bool hold) => earnedFlashHeld = hold;

    // the earned flash on the + slot, `delay` seconds from now; it lets go of the hold too
    public void FlashEarnedPlus(float delay)
    {
        earnedFlashHeld = false;
        StartCoroutine(FlashEarnedLater(delay));
    }

    IEnumerator FlashEarnedLater(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        hud.FlashSlot(TileType.Cross, hud.earnedFlashColor);
    }

    // ---- the Tutorial button, top left, just right of the level's title, on levels with a tutorial ----

    // made from the "More" menu's Rules button (the game screen's button that calls ShowRules) the first time it's
    // needed, so it's round like the menu's buttons, at their size; it goes beside the title, not in the menu. its own
    // click plays the tutorial again (ReplayTutorial) instead
    Button tutorialButton;
    const float TutorialButtonGap = 16f; // from the end of the title's text

    bool MakeTutorialButton()
    {
        if (menu == null || menu.gameScreen == null || hud == null || hud.levelTitleText == null) return false;
        var rules = menu.gameScreen.GetComponentsInChildren<Button>(true).FirstOrDefault(b => Calls(b, nameof(ShowRules)));
        if (rules == null) { Debug.LogWarning("No Rules button in the game screen to make the Tutorial button from."); return false; }

        // beside the title (the menu's own box shows only while the menu is open)
        tutorialButton = Instantiate(rules, hud.levelTitleText.transform.parent);
        tutorialButton.name = "TutorialButton";
        for (int i = 0; i < tutorialButton.onClick.GetPersistentEventCount(); i++)
            tutorialButton.onClick.SetPersistentListenerState(i, UnityEventCallState.Off); // not Rules: the copy's own
        tutorialButton.onClick.AddListener(ReplayTutorial);
        // its background in the theme color (the palette's Accent), as the tutorial's Next button is, through its
        // PaletteColor if it has one, so it follows the palette
        if (tutorialButton.targetGraphic != null)
        {
            var paint = tutorialButton.targetGraphic.GetComponent<PaletteColor>();
            if (paint != null) { paint.role = UIPalette.Role.Accent; paint.Apply(); }
            else tutorialButton.targetGraphic.color = UIPalette.Current.accent;
        }
        var label = tutorialButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "Tutorial"; // at the menu's text size, which fits "Settings" too
        tutorialButton.gameObject.SetActive(false);
        return true;
    }

    bool Calls(Button b, string method)
    {
        for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
            if (b.onClick.GetPersistentMethodName(i) == method && b.onClick.GetPersistentTarget(i) == this) return true;
        return false;
    }

    // called on every level load (LoadLevel)
    void ShowTutorialButton(bool show)
    {
        if (show && tutorialButton == null && !MakeTutorialButton()) return;
        if (tutorialButton == null) return;
        tutorialButton.gameObject.SetActive(show);
        PlaceTutorialButton();
    }

    void LateUpdate() => PlaceTutorialButton();

    // each frame, as the title's text (and so its width) changes with the level: its left edge Tutorial Button Gap
    // right of where the text ends, level with the title's middle
    void PlaceTutorialButton()
    {
        if (tutorialButton == null || !tutorialButton.gameObject.activeSelf || hud == null || hud.levelTitleText == null) return;
        var title = hud.levelTitleText;
        var trt = title.rectTransform;
        var brt = (RectTransform)tutorialButton.transform;
        float end = title.textBounds.size.x > 0 ? title.textBounds.max.x : trt.rect.xMax; // no text yet: the title's box
        var at = brt.parent.InverseTransformPoint(trt.TransformPoint(new Vector3(end, trt.rect.center.y, 0)));
        var size = brt.rect.size;
        brt.localPosition = new Vector3(at.x + TutorialButtonGap + brt.pivot.x * size.x, at.y + (brt.pivot.y - 0.5f) * size.y, 0);
    }

    // the Tutorial button: the level from the start again, its tutorial from its first step and its coach from the
    // start too, even with tutorials off in Settings
    void ReplayTutorial()
    {
        if (tutorial == null || string.IsNullOrEmpty(level?.tutorial)) return;
        tutorial.Forget(levelIndex);
        ReloadLevel(); // LoadLevel starts the tutorial when tutorials are on
        if (!Session.showTutorials) tutorial.TryStart(levelIndex, this);
    }

    // the level from the start again, as it first loaded. unlike Restart it reports nothing.
    // TODO: decide what analytics should get for a coach's reset (a Restart row, or nothing as now)
    public void ReloadLevel() => LoadLevel(levelIndex);
}
