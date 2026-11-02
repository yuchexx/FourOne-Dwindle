using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
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

    // a board's square (its cell, with or without a tile), as (board, x, y) as in Levels.cs; null off the boards
    public RectTransform CellRect(int boardIndex, int x, int y)
    {
        var p = level.At(boardIndex, x, y);
        var cell = board.Cells != null && board.InGrid(p) ? board.Cells[p.x, p.y] : null;
        return cell?.view != null ? (RectTransform)cell.view.transform : null;
    }

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

    // called as each object a placement delivered breaks its tile (gone from the board already), one at a time, with
    // the names of everyone that placement delivered; true holds the deliveries there (the next tile, the combo, the
    // Delivered report) until ReleaseRemoval, so a step can show that tile. LoadLevel lets go of a hold
    public System.Func<Arrival, string[], bool> onRemoval;
    bool removalHeld;
    public void ReleaseRemoval() => removalHeld = false;

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

    // made from code the first time it's needed, beside the title: a rectangle like the tutorial's own Next button
    // (the Style's Button Size, the palette's Tutorial yellow, a dark bold label at Next's size), so it reads as the
    // tutorial's. its click plays the tutorial again (ReplayTutorial)
    Button tutorialButton;
    const float TutorialButtonGap = 16f; // from the end of the title's text

    bool MakeTutorialButton()
    {
        if (tutorial == null || hud == null || hud.levelTitleText == null) return false;
        var style = tutorial.style;

        var go = new GameObject("TutorialButton", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(hud.levelTitleText.transform.parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = style.buttonSize;
        var background = go.GetComponent<Image>();
        background.color = UIPalette.Current.tutorial;
        tutorialButton = go.GetComponent<Button>();
        tutorialButton.targetGraphic = background;
        tutorialButton.onClick.AddListener(ReplayTutorial);

        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(rt, false);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.text = "Tutorial";
        label.fontSize = style.fontSize * 0.85f; // as Next's
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = TutorialOverlay.NextTextColor;
        label.raycastTarget = false;

        go.SetActive(false);
        return true;
    }

    // levels with a tutorial whose button shows: once one's end window has come up (after its score recap, if any) or
    // it has been restarted (the Restart button, not a coach's reset) this launch, its button stays
    readonly HashSet<int> tutorialButtonEarned = new HashSet<int>();

    // the level's end window just came up, or it's being restarted: from now on its Tutorial button shows (see LoadLevel)
    void EarnTutorialButton()
    {
        if (tutorial == null || level == null || string.IsNullOrEmpty(level.tutorial)) return;
        tutorialButtonEarned.Add(levelIndex);
        ShowTutorialButton(true);
    }

    // whether the level that's on gets the button: it has a tutorial, and has ended or been restarted before
    bool TutorialButtonFor(int index) =>
        tutorial != null && !string.IsNullOrEmpty(Levels.All[index].tutorial) && tutorialButtonEarned.Contains(index);

    // called on every level load (LoadLevel), and when a level earns its button
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
