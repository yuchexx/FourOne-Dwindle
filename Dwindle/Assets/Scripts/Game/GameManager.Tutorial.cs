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
    public List<string> DoorLabels() => level == null ? new List<string>() : level.doors.Select(d => d.label).Distinct().ToList(); // each door pair's letter
    public RectTransform BoardFrameRect(int i) => board.BoardFrame(i);
    public int BoardCount => level == null ? 0 : level.boards.Count;

    // how many of a tile type are left in the supply
    public int TilesLeft(TileType t) => inventory != null ? inventory.GetValueOrDefault(t) : 0;

    // for the tutorial: the tile on a board's square (as in Levels.cs), or null
    public TileType? TileAt(int boardIndex, int x, int y) { var c = level.At(boardIndex, x, y); return Cells[c.x, c.y].tile; }

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
    public void HoldLevelEnd(bool hold) { levelEndHeld = hold; if (!hold && !levelOver && !busy && level != null) CheckLevelEnd(); } // a level end skipped while held is checked again
    public void ClearLevelEndHold() => levelEndHeld = false; // let go without checking (the level is about to reload)

    // called as each object a placement delivered breaks its tile (gone from the board already), one at a time, with
    // the names of everyone that placement delivered; true holds the deliveries there (the next tile, the combo, the
    // Delivered report) until ReleaseRemoval, so a step can show that tile. LoadLevel lets go of a hold
    public System.Func<Arrival, string[], bool> onRemoval;
    bool removalHeld;
    public void ReleaseRemoval() => removalHeld = false;

    // called as an object coming out of a door gets its arrows (AskDirectionWithRedo), with its name and the door's
    // letter, so a step can show over them. nothing is held: the arrows wait for a click anyway (not when a redo
    // answers them: then no arrows show, and this isn't called)
    public System.Action<string, string> onDoorQuestion;

    // called as an object comes out of a door with one way to go (the door on the board's edge: no arrows), with its
    // name and the door's letter; true holds it there, facing in, until ReleaseDoorExit, so a step can show it.
    // LoadLevel lets go of a hold
    public System.Func<string, string, bool> onDoorExit;
    bool doorExitHeld;
    public void ReleaseDoorExit() => doorExitHeld = false;

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

    // levels with a tutorial that have earned their button (with tutorials off in Settings it shows without this, see
    // TutorialButtonFor): once one's end window has come up (after its level-end steps, if any), it has been restarted
    // (the Restart button, not a coach's reset), or its tutorial, only one opening step, has been closed (TutorialController) this
    // launch, its button stays
    readonly HashSet<int> tutorialButtonEarned = new HashSet<int>();

    // the level's end window just came up, it's being restarted, or its tutorial (only one opening step) just closed (public
    // for TutorialController): from now on its Tutorial button shows (see LoadLevel)
    public void EarnTutorialButton()
    {
        if (tutorial == null || level == null || !HasGuide(level)) return;
        tutorialButtonEarned.Add(levelIndex);
        ShowTutorialButton(true);
    }

    // for the tutorial: Undo, Redo, Restart and Hint greyed out and not clickable while a guide runs, with a PAUSED tag
    // in the middle of the bottom-right controls and one above Hint
    UnityEngine.UI.Image pausedTag, hintPausedTag;
    public void PauseControls(bool paused)
    {
        var screen = hud != null && hud.levelTitleText != null ? hud.levelTitleText.transform.parent : null;
        if (screen == null) return;
        foreach (var name in new[] { "UndoButton", "RedoButton", "HintButton" }) // Restart stays: it replays an unfinished guide
        {
            var t = screen.Find(name);
            if (t == null) continue;
            var cg = t.GetComponent<CanvasGroup>();
            if (cg == null) { if (!paused) continue; cg = t.gameObject.AddComponent<CanvasGroup>(); }
            cg.alpha = paused ? 0.35f : 1f;
            cg.interactable = cg.blocksRaycasts = !paused;
        }
        // in the screen's own space
        Rect R(RectTransform t) { var c = new Vector3[4]; t.GetWorldCorners(c); var a = screen.InverseTransformPoint(c[0]); var b = screen.InverseTransformPoint(c[2]); return Rect.MinMaxRect(a.x, a.y, b.x, b.y); }
        Rect? Around(params string[] names)
        {
            Rect? u = null;
            foreach (var n in names)
                if (screen.Find(n) is RectTransform t) { var r = R(t); u = u == null ? r : Rect.MinMaxRect(Mathf.Min(u.Value.xMin, r.xMin), Mathf.Min(u.Value.yMin, r.yMin), Mathf.Max(u.Value.xMax, r.xMax), Mathf.Max(u.Value.yMax, r.yMax)); }
            return u;
        }
        var controls = Around("UndoButton", "RedoButton"); // the paused pair: Undo and Redo, centred on them
        if (controls != null) PlaceTag(ref pausedTag, "PausedTag", screen, paused, controls.Value.center, Mathf.Max(90f, controls.Value.width), 0.5f);
        // Hint: over its middle
        var hint = Around("HintButton");
        if (hint != null) PlaceTag(ref hintPausedTag, "HintPausedTag", screen, paused, hint.Value.center, Mathf.Max(90f, hint.Value.width), 0.5f); // over the middle of Hint
    }

    // Hint alone greyed out with its PAUSED tag (Level 3's guide: Undo and Restart stay)
    public void PauseHint(bool paused)
    {
        var screen = hud != null && hud.levelTitleText != null ? hud.levelTitleText.transform.parent : null;
        if (screen == null || !(screen.Find("HintButton") is RectTransform t)) return;
        var cg = t.GetComponent<CanvasGroup>();
        if (cg == null) { if (!paused) return; cg = t.gameObject.AddComponent<CanvasGroup>(); }
        cg.alpha = paused ? 0.35f : 1f;
        cg.interactable = cg.blocksRaycasts = !paused;
        var c = new Vector3[4]; t.GetWorldCorners(c);
        var a = screen.InverseTransformPoint(c[0]); var b = screen.InverseTransformPoint(c[2]);
        var r = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        PlaceTag(ref hintPausedTag, "HintPausedTag", screen, paused, r.center, Mathf.Max(90f, r.width), 0.5f);
    }

    void PlaceTag(ref UnityEngine.UI.Image tag, string name, Transform screen, bool paused, Vector2 at, float width, float pivotY)
    {
        if (paused && tag == null)
        {
            tag = UIBuilder.NewImage(name, screen, UIPalette.Current.tutorial);
            tag.raycastTarget = false;
            var label = UIBuilder.NewText("Text", tag.transform, "PAUSED", 18, new Color(0.08f, 0.08f, 0.1f), hud.levelTitleText.font);
            label.fontStyle = TMPro.FontStyles.Bold;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.raycastTarget = false;
            UIBuilder.Fill(label.rectTransform);
        }
        if (tag == null) return;
        tag.gameObject.SetActive(paused);
        if (!paused) return;
        var rt = tag.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, pivotY);
        rt.sizeDelta = new Vector2(width, 28f);
        rt.localPosition = new Vector3(at.x, at.y, 0f);
        rt.SetAsLastSibling();
    }

    // for the tutorial: the stuck notice held back while a guide runs; off again on every level load
    bool stuckPaused;
    public void PauseStuckNotice(bool paused) => stuckPaused = paused;

    // levels whose guide ran to its very end this launch (a restart before that plays it again from the start)
    public void MarkGuideDone() => Session.MarkGuideDone(levelIndex); // saved
    // for the hint guide: a placement's square in the board's own grid (GameAction gives the board and its local square)
    public Vector2Int GridCell(int board, Vector2Int local) => level.boards[board].origin + local;

    // for the tutorial: a supply tile goes on a square as if the player had put it there (Level 1's blocking tile)
    public void TutorialPlace(int boardIndex, int x, int y, TileType t)
    {
        if (inventory == null || inventory.GetValueOrDefault(t) <= 0) return;
        selected = t;
        PlaceSelected(level.At(boardIndex, x, y));
        selected = null;
        hud.RefreshInventory(inventory, selected);
    }

    // a level with something to replay: a pop-up, a tutorial or a think guide
    static bool HasGuide(LevelData l) => !string.IsNullOrEmpty(l.popup) || !string.IsNullOrEmpty(l.tutorial) || !string.IsNullOrEmpty(l.think);

    // whether the level that's on gets the button: it has a guide, and either tutorials are off in Settings (nothing
    // pops up by itself, so the button is the way in) or it has ended or been restarted before
    bool TutorialButtonFor(int index) =>
        tutorial != null && HasGuide(Levels.All[index])
        && (!Session.showTutorials || tutorialButtonEarned.Contains(index));

    bool openedFromMenu; // this load came from a menu (LoadLevel)
    bool forcePopup; // the Tutorial button: the pop-up shows again, even with tutorials off

    // the pop-ups' canvas and font: the HUD's
    Canvas PopupCanvas => tutorial != null && tutorial.canvas != null ? tutorial.canvas : hud != null && hud.levelTitleText != null ? hud.levelTitleText.canvas : null; // the HUD object itself may sit outside the Canvas
    TMPro.TMP_FontAsset PopupFont => hud != null && hud.levelTitleText != null ? hud.levelTitleText.font : null;

    // on load: the level's pop-up first (Guide me, not seen yet; or the Tutorial button), then its tutorial / think guide
    void StartGuide(int index)
    {
        var lg = Levels.All[index];
        if (openedFromMenu && tutorial != null && !string.IsNullOrEmpty(lg.tutorial) && Session.PopupSeen(index) && !Session.GuideDone(index))
        { tutorial.Forget(index); forcePopup = true; } // left part-way before: the guide starts over, pop-up first
        TutorialOverlay.HideWhile = () => menu != null && ((menu.leaveDialog != null && menu.leaveDialog.panel.activeInHierarchy) || (menu.rulesPopup != null && menu.rulesPopup.activeInHierarchy) || (menu.endWindow != null && menu.endWindow.panel.activeInHierarchy && !endWindowHeld));
        var l = Levels.All[index];
        bool run = Session.showTutorials || forcePopup;
        bool popup = !string.IsNullOrEmpty(l.popup) && (forcePopup || !Session.PopupSeen(index)); // a pop-up shows the first time whatever the Tutorials switch says
        forcePopup = false;
        if (popup && PopupCanvas == null) Debug.LogWarning($"{l.title}: no Canvas found for its \"{l.popup}\" pop-up (set TutorialController's Canvas).");
        if (popup && PopupCanvas != null)
        {
            LevelPopup.Show(PopupCanvas, l.popup, PopupFont, board, () =>
            {
                Session.MarkPopupSeen(index);
                if (levelIndex != index) return; // left meanwhile
                if (run && tutorial != null) tutorial.TryStart(index, this);
                if (string.IsNullOrEmpty(l.tutorial) && string.IsNullOrEmpty(l.think)) EarnTutorialButton(); // only a pop-up: the button can bring it back
            });
            return;
        }
        if (run && tutorial != null) tutorial.TryStart(index, this);
    }

    // Settings' Show tutorials just changed: the level that's on gets or loses its button right away
    void RefreshTutorialButton() { if (level != null) ShowTutorialButton(TutorialButtonFor(levelIndex)); }

    // called on every level load (LoadLevel), when a level earns its button, and when Show tutorials changes
    void ShowTutorialButton(bool show)
    {
        if (show && tutorialButton == null && !MakeTutorialButton()) return;
        if (tutorialButton == null) return;
        tutorialButton.gameObject.SetActive(show);
        PlaceTutorialButton();
    }

    void LateUpdate() { PlaceTutorialButton(); PlayHintSound(); }

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
        if (tutorial == null || level == null || !HasGuide(level)) return;
        tutorial.Forget(levelIndex);
        forcePopup = true; // StartGuide shows the pop-up and runs the guide, even with tutorials off
        ReloadLevel();
    }

    // the level from the start again, as it first loaded. unlike Restart it reports nothing.
    // TODO: decide what analytics should get for a coach's reset (a Restart row, or nothing as now)
    public void ReloadLevel() => LoadLevel(levelIndex);
}
