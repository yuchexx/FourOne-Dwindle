using System;
using System.Collections.Generic;
using UnityEngine;

// the plan's guided levels: "FirstLevel" (Level 1: tiles, move, take back, score) and "PlusCombo" (Level 3: the + and
// a combo, with what happens if one arrives alone or picks the wrong way). "do it" steps (TutorialStep.doneWhen) have
// no button: the player does the thing and the guide moves on. think guides are in Tutorials.For
public static partial class Tutorials
{
    // HUD parts the old Target list doesn't have
    static Func<RectTransform> Score(GameManager game) => () => game.hud.scoreText != null ? game.hud.scoreText.rectTransform : null;
    static Func<RectTransform> Redo(GameManager game) => () => game.hud.redoButton != null ? (RectTransform)game.hud.redoButton.transform : null;
    // the level title's text only (its object is wider than the words): a helper rect kept over the text's bounds
    static RectTransform titleBounds;
    static Func<RectTransform> TitleText(GameManager game) => () =>
    {
        var t = game.hud != null ? game.hud.levelTitleText : null;
        if (t == null) return null;
        if (titleBounds == null || titleBounds.parent != t.transform)
        {
            titleBounds = (RectTransform)new GameObject("TitleBounds", typeof(RectTransform)).transform;
            titleBounds.SetParent(t.transform, false);
        }
        var b = t.textBounds;
        titleBounds.anchorMin = titleBounds.anchorMax = t.rectTransform.pivot; // textBounds are around the pivot
        titleBounds.pivot = new Vector2(0.5f, 0.5f);
        titleBounds.anchoredPosition = b.center; titleBounds.sizeDelta = b.size;
        return titleBounds;
    };

    // a box around something with room on each side (x across, y up and down): a helper rect kept over it
    static readonly Dictionary<string, RectTransform> pads = new();
    static Func<RectTransform> Padded(Func<RectTransform> of, float x, float y) => () =>
    {
        var t = of();
        if (t == null) return null;
        string key = t.GetInstanceID() + "/" + x + "/" + y;
        if (!pads.TryGetValue(key, out var p) || p == null)
        {
            p = (RectTransform)new GameObject("PaddedBox", typeof(RectTransform)).transform;
            p.SetParent(t, false);
            pads[key] = p;
        }
        p.anchorMin = Vector2.zero; p.anchorMax = Vector2.one; p.offsetMin = new Vector2(-x, -y); p.offsetMax = new Vector2(x, y);
        return p;
    };

    // a game screen object and everything shown inside it (the More menu with its items open)
    static Func<RectTransform>[] HudAll(GameManager game, string path)
    {
        var parts = new List<Func<RectTransform>>();
        for (int i = 0; i < 24; i++)
        {
            int k = i;
            parts.Add(() =>
            {
                var root = Hud(game, path)();
                if (root == null) return null;
                var all = root.GetComponentsInChildren<UnityEngine.UI.Selectable>(false); // its buttons only (the menu's own object is wider)
                return k < all.Length ? (RectTransform)all[k].transform : null;
            });
        }
        return parts.ToArray();
    }

    // a game screen object by name (a path under GameScreen, as in the scene)
    static Func<RectTransform> Hud(GameManager game, string path) => () =>
    {
        var screen = game.hud != null && game.hud.levelTitleText != null ? game.hud.levelTitleText.transform.parent : null;
        var t = screen != null ? screen.Find(path) : null;
        return t != null && t.gameObject.activeInHierarchy ? (RectTransform)t : null;
    };

    static bool Is(GameAction a, GameActionKind kind, TileType tile) => a.kind == kind && a.tile == tile;
    static bool At(GameAction a, int x, int y) => a.square == new Vector2Int(x, y);

    // ---- Level 1: Level5's board, its straight starting free in the top row ----

    // the score history panel (shown while the score is hovered, or held open at the level's end)
    static Func<RectTransform> History(GameManager game) => () => game.hud != null && game.hud.scoreHistory != null && game.hud.scoreHistory.gameObject.activeInHierarchy ? game.hud.scoreHistory : null;

    // the Settings window (its Box), or a part of it by name (e.g. CloseButton)
    static Func<RectTransform> SettingsPart(GameManager game, string name) => () =>
    {
        var panel = game.menu?.settings?.panel;
        var box = panel != null ? panel.transform.Find("Box") : null;
        if (box == null) return null;
        var t = name == null ? box : box.Find(name);
        return t != null && t.gameObject.activeInHierarchy ? (RectTransform)t : null;
    };

    // the Show tile removal row: the row and each of its children (its title, description and switch)
    static Func<RectTransform>[] BreaksRow(GameManager game)
    {
        RectTransform Row() => game.menu?.settings?.showBreaksToggle != null ? (RectTransform)game.menu.settings.showBreaksToggle.transform : null;
        var parts = new List<Func<RectTransform>> { Row };
        for (int i = 0; i < 6; i++) { int k = i; parts.Add(() => Row() != null && k < Row().childCount ? Row().GetChild(k) as RectTransform : null); }
        return parts.ToArray();
    }

    static bool SettingsOpen(GameManager game) =>
        game.menu != null && game.menu.settings != null && game.menu.settings.panel != null && game.menu.settings.panel.activeInHierarchy;

    // the screen tour's boxes
    static (Func<RectTransform>[] parts, string text)[] TourBoxes(GameManager game) => new[]
    {
        (new[] { Hud(game, "HomeButton"), TitleText(game) }, "Pause menu"),
        (new[] { Padded(Score(game), 14f, 4f) }, "Hover score\nto see details"),
        (HudAll(game, "MoreMenu"), "Rules\n& settings"),
        (new[] { Target.Hint(game) }, "Hints\nfor help"),
        (new[] { Target.Supply(game) }, "Tiles provided"),
        (new[] { Target.Undo(game), Redo(game), Hud(game, "Restart") }, "Controls\nfor placing"),
    };

    static List<TutorialStep> FirstLevel(GameManager game) => new()
    {
        // read: the screen (the user's sketch): a box around each HUD group, a short line outside it
        new TutorialStep
        {
            text = "Check the game screen.",
            dim = true, // only this step darkens the rest
            labels = TourBoxes(game),
            bubble = BubbleSide.TopLeft,
            bubbleUnderLabel = 0, // under "PAUSE MENU", lined up with its box
            align = new[] { (0, 'L', 2), (2, 'B', 4), (3, 'B', 4), (5, 'B', 4), (1, 'R', 5) }, // pause box left = Rules box left; bottom boxes level with the tiles box; score box right = controls box right
            onShow = () => game.hud.HoldMoreMenu(true), // the More menu open, so its box shows what's inside
            onDone = () => game.hud.HoldMoreMenu(false),
            buttonText = "Start",
        },
        // read: Red's start and its ring, each boxed and named
        new TutorialStep
        {
            text = "Look at Red and the ring\nof the same colour:\nRed starts here,\nand the ring is where it goes.\nBring Red to its ring.",
            labels = new[]
            {
                (new[] { Target.Object(game, "Red") }, "Start"),
                (new[] { Target.Ring(game, "Red") }, "Destination"),
            },
            labelSides = "AA", // both above their boxes
            bubble = BubbleSide.TopLeft,
            onShow = () => game.PauseControls(true), // Undo, Redo, Restart and Hint paused from here till the score
        },
        // do: move the free straight next to the start (a right-click on it is undone too)
        new TutorialStep
        {
            text = "Move (click or drag)\nthe tile on the board\nnext to Red.",
            targets = new[] { Target.Cell(game, 0, 0, 1), Target.Cell(game, 0, 0, 0) },
            doneWhen = a => Is(a, GameActionKind.PlaceTile, TileType.StraightH) && At(a, 0, 0),
            wrong = a => a.kind == GameActionKind.PlaceTile || Is(a, GameActionKind.TakeBack, TileType.StraightH) ? "Put it right next to Red." : null,
        },
        // read: locked
        new TutorialStep
        {
            text = "Look at this tile:\nRed has gone over it.\nThat means it's locked,\nso you can't move it anymore.",
            targets = new[] { Target.Cell(game, 0, 0, 0) },
            delay = 1.2f, // Red moves first
            onDone = () => game.TutorialPlace(0, 1, 0, TileType.CornerES), // a tile with no opening toward Red
        },
        // do: take the blocking tile back
        new TutorialStep
        {
            text = "This tile blocks Red.\nPut it back in your tiles:\nright-click it, or drag or click it back.",
            targets = new[] { Target.Cell(game, 0, 1, 0), Target.Supply(game) },
            delay = 0.5f,
            until = () => game.TilesLeft(TileType.CornerES) > 0, // back in your tiles (a move elsewhere doesn't count)
            wrong = a => a.kind == GameActionKind.PlaceTile ? "Put it back in your tiles." : null,
        },
        // read: the locked tile can't be taken back
        new TutorialStep
        {
            text = "But a tile Red has gone over\ncan't be taken back, like this one.",
            targets = new[] { Target.Cell(game, 0, 0, 0) },
            bubble = BubbleSide.Left, // left of the tile, level with it
            bubbleBy = new[] { Target.Cell(game, 0, 0, 0) }, // the tile alone, not the slot too
            labels = new[] { (new[] { Target.Slot(game, TileType.CornerES) }, "Back in your tiles!") },
            labelsAsBoxes = true, // its own guide box
            labelSides = "A",     // above the returned tile's slot
        },
        // do: finish Red's road (the coach takes over once both squares are filled, so it can pause on the removal)
        new TutorialStep
        {
            text = "Now finish Red's road.",
            until = () => game.TileAt(0, 1, 0) != null && game.TileAt(0, 1, 1) != null,
            // a tile off Red's road, or one that doesn't fit it: undone, "try again" (so Red can't end up stuck)
            wrong = a => a.kind == GameActionKind.PlaceTile && !((At(a, 1, 0) && a.tile == TileType.CornerWN) || (At(a, 1, 1) && a.tile == TileType.StraightV))
                ? "That won't get Red to its ring.\nTry again!" : null,
        },
    };

    // after the road: the game pauses as Red's tile is removed, boxes that square, then Settings, before the level ends
    static TutorialCoach FirstLevelCoach(GameManager game) => new()
    {
        onAction = a => a.kind == GameActionKind.Delivered ? CoachReply.Done : CoachReply.None,
        removalSteps = (arrival, together) => !arrival.broke ? null : new List<TutorialStep>
        {
            new TutorialStep
            {
                text = "Look at Red's road:\none tile is gone.\nThat's because Red was delivered,\nand each delivery removes one tile it used.",
                bubble = BubbleSide.Right, // right of the tile, off the board
                targets = new[] { Target.Cell(game, arrival.board, arrival.square.x, arrival.square.y) },
                delay = 0.3f, // the tile is seen to go first
            },
            new TutorialStep
            {
                text = "Turn this on to see which tile\nwill go, or off if you don't want to.",
                targets = BreaksRow(game),
                labels = new[] { (new[] { SettingsPart(game, "CloseButton") }, "Close Settings when done,\nor click outside it.") },
                labelsAsBoxes = true, // its own guide box, left of the close button
                bubble = BubbleSide.Right,
                bubbleBy = new[] { SettingsPart(game, null) }, // right of the Settings window
                keepSide = true,
                onShow = () => { game.menu?.ShowSettings(); game.menu?.settings?.ShowPageOf(game.menu.settings.showBreaksToggle); }, // Settings opened on its Help page
                until = () => !SettingsOpen(game),
                hideWhen = () => game.menu?.settings?.showBreaksToggle != null && !game.menu.settings.showBreaksToggle.gameObject.activeInHierarchy, // another tab: hidden till Help is back
            },
        },
    };

    // the unused row, and each tile type that still has tiles in the dock
    static Func<RectTransform>[] UnusedTargets(GameManager game, Func<RectTransform> row)
    {
        var list = new List<Func<RectTransform>> { row };
        foreach (var t in Tiles.SupplyOrder) if (game.TilesLeft(t) > 0) { var tt = t; list.Add(Target.Slot(game, tt)); }
        return list.ToArray();
    }

    // at the level's end: the score, each step boxing its own row in the history (Back and Next), then a well done
    static List<TutorialStep> FirstLevelEnd(GameManager game)
    {
        var log = game.hud.GetScoreLog(); // newest first, as the history shows it
        int delivered = log.FindIndex(e => e.objects != null && !e.combo);
        int unused = log.FindIndex(e => e.label != null && e.label.Contains("unused"));
        var deliveredRow = Target.ScoreRow(game, Mathf.Max(0, delivered));
        var unusedRow = Target.ScoreRow(game, Mathf.Max(0, unused));
        var steps = new List<TutorialStep>
        {
            new TutorialStep
            {
                text = "Let's look at the score!",
                targets = new[] { Score(game), History(game) }, // the score and its history, boxed together
                bubbleBy = new[] { History(game) },
                alignTop = Score(game), // its top level with the score's
                bubble = BubbleSide.Left,
                onShow = () => game.PauseControls(false), // the guide's pause is over
            },
            new TutorialStep
            {
                text = $"+{game.pointsPerDelivery} for the delivery.",
                backable = true,
                targets = new[] { Target.Ring(game, "Red"), deliveredRow },
                bubbleBy = new[] { History(game) }, // left of the whole history, not over it
                bubble = BubbleSide.Left,
                pops = new[] { new TutorialPop { text = $"+{game.pointsPerDelivery}", near = new[] { Target.Ring(game, "Red") } } },
            },
        };
        if (unused >= 0)
            steps.Add(new TutorialStep
            {
                text = "Look at the tiles you didn't use:\neach one is worth +75\nonce everyone is delivered.",
                targets = UnusedTargets(game, unusedRow), // the row, and each tile still in the dock
                bubbleBy = new[] { History(game) },
                bubble = BubbleSide.Left,
                backable = true,
            });
        steps.Add(new TutorialStep { text = "Well done!\nYou finished Level 1.", buttonText = "Next level", buttonIcon = "nextlvl", onDone = game.MarkGuideDone }); // the guide ran to its end
        return steps;
    }


    // ---- Level 3: the + on 2x2, Red up the left column turns W, Blue along the top row turns N; the + is at (0,1) ----

    static readonly Dictionary<string, Dir> PlusWays = new() { { "Red", Dir.W }, { "Blue", Dir.N } };
    static bool plusBreakShown; // this run: the + break was explained

    // do: the box goes as soon as the first tile is placed (nothing boxed, as Level 1's do steps)
    static List<TutorialStep> PlusCombo(GameManager game)
    {
        plusBreakShown = false;
        return new()
        {
            new TutorialStep
            {
                text = "Try it on your own:\nget Red and Blue to their rings.",
                onShow = () => { game.PauseStuckNotice(true); game.HoldEarnedFlash(true); game.PauseHint(true); }, // no stuck notice; the + flash waits for the score; Hint paused (the guide says what to do)
                doneWhen = a => a.kind == GameActionKind.PlaceTile,
            },
        };
    }

    // as Level 1's slips: a wrong arrow, or one delivered alone, is undone for the player with a red box
    // (a lone delivery's first time has the break step before it); the undo jumps back, no rewind shown
    static TutorialCoach PlusComboCoach(GameManager game) => new()
    {
        undoNoteFor = a => a.kind == GameActionKind.ChooseDirection ? "Wrong way: each object heads to its own ring."
            : "Delivering only one object first removes\nthe + tile needed by the other object.\nPlace the + last to deliver both at the same time.",
        onAction = a =>
        {
            if (a.kind == GameActionKind.ChooseDirection && a.objectName != null && PlusWays.TryGetValue(a.objectName, out var way) && a.direction != way)
                return CoachReply.Undo;
            if (a.kind != GameActionKind.Delivered || a.arrivals == null) return CoachReply.None;
            if (a.arrivals.Length >= 2) return CoachReply.Done;
            return CoachReply.Undo; // the other can't get there now: the placement is undone
        },
        // as Level 1's: the game pauses as the + breaks, its square boxed, the box right of the board. the first lone
        // delivery explains the break; the combo says good call (the break part only if not explained yet)
        removalSteps = (arrival, together) =>
        {
            if (!arrival.broke || arrival.brokenTile != TileType.Cross) return null;
            bool both = together != null && together.Length >= 2;
            if (!both && plusBreakShown) return null; // later lone deliveries: the red box only
            string text = both
                ? (plusBreakShown
                    ? "Good call: you delivered both at the same time!\nIf only one arrives, the + breaks for the other."
                    : "Good call: you delivered both at the same time!\nEach delivery removes one tile it used.\nThe + is needed by both, so it goes first.\nIf only one arrives, the + breaks for the other.")
                : $"{arrival.objectName} is delivered, so one tile it used is removed.\nThe + is needed by both, so it goes first:\nnow {(arrival.objectName == "Red" ? "Blue" : "Red")} can't get through.\nTry again: place the + last\nto deliver both at the same time.";
            plusBreakShown = true;
            return new List<TutorialStep>
            {
                new TutorialStep
                {
                    text = text,
                    bubble = BubbleSide.Right,
                    targets = new[] { Target.Cell(game, arrival.board, arrival.square.x, arrival.square.y) },
                    delay = 0.3f, // the tile is seen to go first
                },
            };
        },
    };

    // at the level's end, as Level 1's: the score, the deliveries, the combo, the earned + as the unused tile, well done
    static List<TutorialStep> PlusComboEnd(GameManager game)
    {
        var log = game.hud.GetScoreLog(); // newest first
        var deliveredRows = new List<Func<RectTransform>>();
        for (int i = 0; i < log.Count; i++) if (log[i].objects != null && !log[i].combo) deliveredRows.Add(Target.ScoreRow(game, i));
        int combo = log.FindIndex(e => e.combo && e.objects != null);
        int unused = log.FindIndex(e => e.label != null && e.label.Contains("unused"));
        Func<RectTransform> red = Target.Ring(game, "Red"), blue = Target.Ring(game, "Blue");
        deliveredRows.InsertRange(0, new[] { red, blue });
        var steps = new List<TutorialStep>
        {
            new TutorialStep
            {
                text = "Let's look at the score!",
                targets = new[] { Score(game), History(game) },
                bubbleBy = new[] { History(game) },
                alignTop = Score(game),
                bubble = BubbleSide.Left,
            },
            new TutorialStep
            {
                text = $"+{game.pointsPerDelivery} for each delivery.",
                backable = true,
                targets = deliveredRows.ToArray(),
                bubbleBy = new[] { History(game) },
                bubble = BubbleSide.Left,
                pops = new[] { new TutorialPop { text = $"+{game.pointsPerDelivery}", near = new[] { red } }, new TutorialPop { text = $"+{game.pointsPerDelivery}", near = new[] { blue } } },
            },
        };
        if (combo >= 0)
            steps.Add(new TutorialStep
            {
                text = $"Look at this line:\nRed and Blue arrived together.\nThat's a combo,\nworth +{game.comboPointsPerObject} more each.",
                backable = true,
                targets = new[] { red, blue, Target.ScoreRow(game, combo) },
                bubbleBy = new[] { History(game) },
                bubble = BubbleSide.Left,
                pops = new[] { new TutorialPop { text = $"+{game.comboPointsPerObject}", near = new[] { red } }, new TutorialPop { text = $"+{game.comboPointsPerObject}", near = new[] { blue } } },
            });
        // one step, two boxes: the earned + in the dock (its own box above it, no buttons) and its +75 line (the
        // main box beside the score history, with Back and Next)
        const string plusText = "Look at your tiles left:\nthere's an extra +.\nThe combo earned it.";
        if (unused >= 0)
            steps.Add(new TutorialStep
            {
                text = "Look at this line:\nthe extra + is unused,\nso it's worth +75.",
                backable = true,
                targets = new[] { Target.ScoreRow(game, unused), Target.Slot(game, TileType.Cross) },
                bubbleBy = new[] { History(game) },
                bubble = BubbleSide.Left,
                labels = new[] { (new[] { Target.Slot(game, TileType.Cross) }, plusText) },
                labelsAsBoxes = true,
                labelSides = "A", // above the + slot
                onShow = () => game.FlashEarnedPlus(0.4f),
            });
        else
            steps.Add(new TutorialStep
            {
                text = plusText,
                backable = true,
                targets = new[] { Target.Slot(game, TileType.Cross) },
                bubble = BubbleSide.Above,
                onShow = () => game.FlashEarnedPlus(0.4f),
            });
        steps.Add(new TutorialStep
        {
            text = "Well done!\nYou finished Level 3.", buttonText = "Next level", buttonIcon = "nextlvl",
            onDone = () => { game.MarkGuideDone(); game.PauseStuckNotice(false); game.PauseHint(false); }, // the guide ran to its end
        });
        return steps;
    }

    // ---- Level 24: doors across boards. one guide box (top left); the linked doors framed, one pair at a time, in turn ----

    // every board's frame (the boards together)
    static Func<RectTransform>[] Frames(GameManager game)
    {
        var list = new List<Func<RectTransform>>();
        for (int i = 0; i < game.BoardCount; i++) { int k = i; list.Add(() => game.BoardFrameRect(k)); }
        return list.ToArray();
    }

    static List<TutorialStep> DoorsAcross(GameManager game)
    {
        var labels = game.DoorLabels();
        string Pair() => labels.Count == 0 ? null : labels[(int)(Time.unscaledTime / 1.6f) % labels.Count]; // the pair shown now
        Func<RectTransform> End(int which, bool badge) => () => Pair() == null ? null : game.DoorRect(Pair(), which, badge);
        return new()
        {
            new TutorialStep
            {
                text = "Now linked doors are on different boards.\nNot every door helps: trace and try!",
                targets = new[] { End(0, false), End(0, true), End(1, false), End(1, true) }, // each end's bar and badge: one frame per door
                bubble = BubbleSide.Above, // at the top, over the boards (the boards keep their size; kept on screen)
                bubbleBy = Frames(game),
                bubbleWidth = 520f, // two short lines
                buttonText = "Got it",
                onDone = game.MarkGuideDone,
            },
        };
    }

    // ---- the hint guide: what each hint stage shows. plays by itself the first time a stage opens (saved), and again
    // from the Hint help button. each step frames what it talks about; a step with nothing to frame now is left out ----

    public static List<TutorialStep> HintGuide(GameManager game, HintButton hb, int stage)
    {
        var steps = new List<TutorialStep>();
        var board = game.board;
        if (stage == 1)
        {
            // as stage 2's: every box beside the boards (left, level with their middle), 420 wide, no Back; only what
            // the step is about framed; an example drawn in the box when the real thing isn't on the board
            const float wide1 = 420f;
            TutorialStep Side(string text, params Func<RectTransform>[] targets)
            {
                var st = new TutorialStep { text = text, targets = targets, bubbleWidth = wide1, bubble = BubbleSide.TopLeft, bubbleBy = Frames(game), keepSide = true };
                steps.Add(st);
                return st;
            }
            Side("Hints come in two stages.\n1st press: the delivery order and where objects meet.\n2nd press: the answer, tile by tile.\nBoth follow one of the best plans, and update with your board.");
            if (hb != null && hb.OrderRect != null)
            {
                var order = Side("Look at the line under your tiles:\nit's the order to deliver in.\nEach object dims once it's delivered.", () => hb.OrderRect);
                order.bubble = BubbleSide.Above; order.bubbleBy = null; // right above the order line
            }
            // the meeting square, decided when the step comes up (a delivery can finish it meanwhile): the real one framed,
            // with who meets there; otherwise an example
            string Who(int number)
            {
                var names = new List<string>(); foreach (var bd in game.HintBadges) if (bd.number == number) names.Add(bd.objectName);
                return names.Count <= 1 ? string.Join("", names) : string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
            }
            var meet = Side("Look at this square's border:\nit's made of the colours of Red and Blue.\nThis is where they meet in the delivery step,\nso they're delivered together.");
            meet.pictureHeight = 110f; meet.drawPicture = box => JunctionPicture(game, box, false);
            meet.chooseWhenShown = () => board.GuideJunction == null ? null : new TutorialStep
            {
                text = $"Look at this square's border:\nit's made of the colours of {Who(board.GuideJunctionNumber)}.\nThis is where they meet in the delivery step,\nso they're delivered together.",
                targets = new Func<RectTransform>[] { () => board.GuideJunction },
                bubbleWidth = wide1, bubble = BubbleSide.TopLeft, bubbleBy = Frames(game), keepSide = true,
            };
            var tile = Side("Look at the faint tile changing on this square, like this:\na +, a T, a bridge.\nThat means a junction tile goes there;\nwhich one, and which way it turns, is up to you.");
            tile.pictureHeight = 110f; tile.drawPicture = box => JunctionPicture(game, box, true);
            tile.chooseWhenShown = () => board.GuideJunction == null ? null : new TutorialStep
            {
                text = "Look at the faint tile changing on this square:\na +, a T, a bridge.\nThat means a junction tile goes here;\nwhich one, and which way it turns, is up to you.",
                targets = new Func<RectTransform>[] { () => board.GuideJunction },
                bubbleWidth = wide1, bubble = BubbleSide.TopLeft, bubbleBy = Frames(game), keepSide = true,
            };
            if (board.GuideRing != null) // the ring and the junction both lit, and hovering them shows their tips
                Side("Hover a ring or a junction for details.", () => board.GuideRing, () => board.GuideJunction).hoverHints = true;
            ButtonSteps(steps, hb);
        }
        else
        {
            // every stage 2 box beside the boards (left, level with their middle), wider than stage 1's
            const float wide = 420f;
            void Beside(string text, params Func<RectTransform>[] targets) =>
                steps.Add(new TutorialStep { text = text, targets = targets, bubbleWidth = wide, bubble = BubbleSide.TopLeft, bubbleBy = Frames(game), keepSide = true }); // no Back: one of these steps is done on the board
            const string Badge = "<link=\"badge\"><color=#FFFFFF>accent</color></link>"; // drawn like a number badge: white on an accent box
            Beside("The answer updates with your board:\nit switches to another best plan as you play.\nFeel free to try your own way, with the steps shown as ideas.");
            // the tile part, by what the board shows now (so a replay never waits for something that can't happen):
            //   a ghost shown and not placed: place it (do it); none shown but more to reveal: press Hint first (do it),
            //   then place it; every step shown and placed: an example picture instead (read)
            bool ghostShown = board.GuideGhostCell != null;
            if (ghostShown || !game.HintAnswerDone)
            {
                if (!ghostShown)
                    steps.Add(new TutorialStep
                    {
                        text = "Press Hint to show the next tile of the plan.",
                        targets = hb != null ? new Func<RectTransform>[] { () => (RectTransform)hb.transform } : null,
                        bubble = BubbleSide.TopLeft, bubbleBy = Frames(game), keepSide = true, bubbleWidth = wide,
                        doneWhen = a => a.kind == GameActionKind.Hint,
                    });
                Vector2Int? c = null; TileType? want = null; // the ghost to place, taken as the step shows (it may only just have appeared)
                steps.Add(new TutorialStep
                {
                    text = "Look at the faint tile on this square:\nit's the next tile of the plan.\nPlace it here.",
                    targets = new Func<RectTransform>[] { () => board.GuideGhost },
                    bubble = BubbleSide.TopLeft, bubbleBy = Frames(game), keepSide = true, bubbleWidth = wide,
                    onShow = () => { c = board.GuideGhostCell; want = board.GuideGhostTile; },
                    doneWhen = a => a.kind == GameActionKind.PlaceTile && c != null && game.GridCell(a.board, a.square) == c.Value && a.tile == want,
                    // any other placement is taken back (it could knock the board off the plan, and the ghosts would go)
                    wrong = a => a.kind != GameActionKind.PlaceTile || c == null || (game.GridCell(a.board, a.square) == c.Value && a.tile == want) ? null
                        : game.GridCell(a.board, a.square) == c.Value ? "That's a different tile, so it's taken back.\nPlace the one shown on this square."
                        : "That's taken back for now.\nPlace the shown tile on this square first.",
                });
                // read: its number now, filled in the accent colour (the frame shows the look)
                steps.Add(new TutorialStep
                {
                    text = $"Look at its number: it's now a filled {Badge} square.\nThat means it's placed right.\nIf it pulses instead, it's the right tile at the wrong moment.\nThe numbers suggest an order; any order that still works is fine.",
                    targets = new Func<RectTransform>[] { () => c != null ? board.GuideBadgeAt(c.Value) : null },
                    bubble = BubbleSide.TopLeft, bubbleBy = Frames(game), keepSide = true, bubbleWidth = wide,
                    delay = 0.6f, // the tile is placed and the objects move first
                });
            }
            else
            {
                Beside($"Look at the faint tile with a number, like this:\nit's the next tile of the plan, shown with each press.\nPlaced right, its number turns into a filled {Badge} square.\nIf it pulses instead, it's the right tile at the wrong moment.\nThe numbers suggest an order; any order that still works is fine.");
                steps[steps.Count - 1].pictureHeight = 110f;
                steps[steps.Count - 1].drawPicture = box => GhostPicture(game, box);
            }
            // the arrow, decided when the step comes up (a tile placed earlier in the guide can bring arrows up): a do-it step
            // on the real arrows when they're up; otherwise an example drawn in the box
            Beside($"Look at the arrows at a junction, like this: one pulses in {Badge}.\nThat's the way the plan goes,\nso pick it to stay on the plan.");
            steps[steps.Count - 1].pictureHeight = 120f;
            steps[steps.Count - 1].drawPicture = box => ArrowPicture(game, box);
            steps[steps.Count - 1].chooseWhenShown = () => board.HintedArrowRect != null ? ArrowStep(game) : null;
            const string Star = "<link=\"pstar\"><color=#00000000>MMMM</color></link>"; // drawn as the purple star itself (the letters only hold its place)
            Beside($"Using the answer gives up the {Star} on this try.\nReplay without the answer to earn the {Star}.");
            ButtonSteps(steps, hb);
        }
        if (steps.Count > 0) steps[steps.Count - 1].buttonText = "Got it";
        return steps;
    }

    // the example: a square with a + on it, an object in the middle and its three arrows, the right one pulsing between
    // the object's colour and the accent, the square lit as when the game asks (as the board draws them)
    static void ArrowPicture(GameManager game, RectTransform box)
    {
        var board = game.board;
        const float cell = 100f;
        var sq = new GameObject("Square", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
        sq.transform.SetParent(box, false);
        sq.rectTransform.sizeDelta = Vector2.one * (cell - 4f);
        sq.color = board.crossHighlightColor;
        sq.raycastTarget = false;
        if (board.tilePrefab != null)
        {
            var tile = UnityEngine.Object.Instantiate(board.tilePrefab, sq.transform);
            var trt = (RectTransform)tile.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            tile.Show(TileType.Cross, board.tileColor);
            foreach (var g in tile.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.raycastTarget = false;
        }
        var red = new Color(0.92f, 0.30f, 0.30f);
        if (board.objectPrefab != null)
        {
            var o = UnityEngine.Object.Instantiate(board.objectPrefab, box);
            o.Setup(red, 60f); o.Rect.localScale = Vector3.one * (cell * board.objectSize / 60f);
            o.Face(Dir.N);
            foreach (var g in o.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.raycastTarget = false;
        }
        if (board.arrowButtonPrefab != null)
            foreach (var d in new[] { Dir.N, Dir.E, Dir.W })
            {
                var b = UnityEngine.Object.Instantiate(board.arrowButtonPrefab, box);
                b.enabled = false;
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = Vector2.one * cell * 0.25f;
                var off = DirUtil.Offset(d);
                rt.anchoredPosition = new Vector2(off.x, off.y) * cell * 0.39f;
                rt.localEulerAngles = new Vector3(0, 0, DirUtil.Angle(d));
                var g = b.GetComponent<UnityEngine.UI.Graphic>();
                g.color = red;
                foreach (var gr in b.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) gr.raycastTarget = false;
                if (d == Dir.E) { var p = b.gameObject.AddComponent<GuidePulse>(); p.target = g; p.from = red; p.to = UIPalette.Current.accent; } // the one to pick
            }
    }

    // the example meeting square: an empty square with a frame half red, half blue (as the board splits it), and with
    // cycling on, the faint junction ghost turning through +, T and bridge
    static void JunctionPicture(GameManager game, RectTransform box, bool cycling)
    {
        var board = game.board;
        const float cell = 96f, w = 7f;
        var sq = new GameObject("Square", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
        sq.transform.SetParent(box, false);
        sq.rectTransform.sizeDelta = Vector2.one * cell;
        sq.color = board.cellColor; sq.raycastTarget = false;
        if (cycling && board.tilePrefab != null)
        {
            var t = UnityEngine.Object.Instantiate(board.tilePrefab, sq.transform);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            foreach (var g in t.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.raycastTarget = false;
            var cyc = t.gameObject.AddComponent<GuideTileCycle>();
            cyc.view = t; cyc.color = Color.Lerp(board.cellColor, UIPalette.Current.hint, board.junctionGhostStrength); cyc.seconds = board.junctionCycleSeconds;
        }
        var red = new Color(0.92f, 0.30f, 0.30f); var blue = new Color(0.30f, 0.50f, 1.00f);
        Color Soft(Color c) => Color.Lerp(board.cellColor, c, board.junctionBorderStrength);
        void Bar(Vector2 pos, Vector2 size, Color c)
        {
            var b = new GameObject("Bar", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            b.transform.SetParent(sq.transform, false);
            b.rectTransform.anchoredPosition = pos; b.rectTransform.sizeDelta = size; b.color = Soft(c); b.raycastTarget = false;
        }
        float h = cell / 2f;
        Bar(new Vector2(cell / 4f, h - w / 2f), new Vector2(h, w), blue);   // top, right half
        Bar(new Vector2(h - w / 2f, 0f), new Vector2(w, cell - 2 * w), blue); // right side
        Bar(new Vector2(cell / 4f, -h + w / 2f), new Vector2(h, w), blue);  // bottom, right half
        Bar(new Vector2(-cell / 4f, h - w / 2f), new Vector2(h, w), red);   // top, left half
        Bar(new Vector2(-h + w / 2f, 0f), new Vector2(w, cell - 2 * w), red); // left side
        Bar(new Vector2(-cell / 4f, -h + w / 2f), new Vector2(h, w), red);  // bottom, left half
    }

    // both stages' last steps: Hide, then Hint guide, one button at a time, each box right beside its button, the
    // drop-down held open
    static void ButtonSteps(List<TutorialStep> steps, HintButton hb)
    {
        if (hb == null) return;
        foreach (var (say, button) in new (string, Func<RectTransform>)[] {
            ("Hide or show hints any time.", () => hb.HideRect),
            ("Press Hint guide to see this guide again if needed.\nHave fun!", () => hb.GuideRect) })
        {
            if (button() == null) continue;
            steps.Add(new TutorialStep { text = say, targets = new[] { button }, bubbleWidth = 420f, bubble = BubbleSide.Right, keepSide = true, onShow = () => hb.HoldOpen(true) });
        }
    }

    // the answer's arrow, on the board (do it): pick the pulsing arrow; another way is taken back to the arrows, with a note
    static TutorialStep ArrowStep(GameManager game)
    {
        Dir? way = null; // the pulsing arrow's way, taken as the step shows
        bool gone = false; // no arrows by the time it shows (it waited behind another guide): it ends at once
        return new TutorialStep
        {
            text = "Look at the arrows: one pulses in <link=\"badge\"><color=#FFFFFF>accent</color></link>.\nThat's the way the plan goes,\nso pick it to stay on the plan.",
            targets = new Func<RectTransform>[] { () => game.board.HintedArrowRect },
            bubble = BubbleSide.TopLeft, bubbleBy = Frames(game), keepSide = true, bubbleWidth = 420f,
            onShow = () => { way = game.board.HintedWay; gone = game.board.HintedArrowRect == null; },
            until = () => gone,
            doneWhen = a => a.kind == GameActionKind.ChooseDirection && (way == null || a.direction == way.Value),
            wrong = a => a.kind == GameActionKind.ChooseDirection && way != null && a.direction != way.Value ? "That's not the pulsing arrow, so it's taken back.\nPick the pulsing one." : null,
            wrongBackToArrows = true,
        };
    }

    // the answer's arrow step on its own: the first time an arrow question comes up with the answer open
    public static List<TutorialStep> ArrowGuide(GameManager game) => new() { ArrowStep(game) };

    // the example for a replay with every step placed: a ghost tile with its number (Hint colour), then the same tile placed
    // right with its number filled in the accent colour
    static void GhostPicture(GameManager game, RectTransform box)
    {
        var board = game.board;
        const float cell = 90f;
        void Square(float x, Color tileColor, Color badgeColor)
        {
            var sq = new GameObject("Square", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            sq.transform.SetParent(box, false);
            sq.rectTransform.sizeDelta = Vector2.one * (cell - 4f);
            sq.rectTransform.anchoredPosition = new Vector2(x, 0f);
            sq.color = board.cellColor; sq.raycastTarget = false;
            if (board.tilePrefab != null)
            {
                var tile = UnityEngine.Object.Instantiate(board.tilePrefab, sq.transform);
                var trt = (RectTransform)tile.transform;
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
                tile.Show(TileType.CornerES, tileColor);
                foreach (var g in tile.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.raycastTarget = false;
            }
            var badge = new GameObject("Badge", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            badge.transform.SetParent(sq.transform, false);
            float b = cell * board.hintBadgeSize;
            badge.rectTransform.sizeDelta = Vector2.one * b;
            badge.rectTransform.anchoredPosition = new Vector2(-(cell - 4f) / 2f + b / 2f + 4f, (cell - 4f) / 2f - b / 2f - 4f);
            badge.color = badgeColor; badge.raycastTarget = false;
            UIBuilder.BadgeLabel(badge.rectTransform, "1", b * 0.7f);
        }
        var ghost = Color.Lerp(board.cellColor, UIPalette.Current.hint, board.hintGhostStrength);
        Square(-cell * 0.75f, ghost, UIPalette.Current.hint);
        Square(cell * 0.75f, board.tileColor, UIPalette.Current.hintAccent);
    }
}
