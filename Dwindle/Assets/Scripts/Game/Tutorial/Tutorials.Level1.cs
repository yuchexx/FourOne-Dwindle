using System.Collections.Generic;
using UnityEngine;

// level 1's tutorial: the board and the top bar, a first delivery, undo, right-click take-back,
// a + junction delivering both objects together, and the end window
public static partial class Tutorials
{
    static List<TutorialStep> Level1(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Welcome!\nBuild paths with tiles to guide each object to the ring of its own color.",
            // no area: everything is dimmed and the bubble sits in the middle of the screen
        },
        new TutorialStep
        {
            text = "These are the tiles you can use.\nThe number under each one is how many you have left.",
            area = new Rect(14, 10, 1165, 110), // the tile slots
        },
        new TutorialStep
        {
            text = "These buttons let you undo your last tile placement, restart the level,\nread the rules, or open the menu.",
            area = new Rect(1193, 17, 398, 96), // the top bar's Undo, Restart, Rules and Menu buttons
        },
        new TutorialStep
        {
            text = "These are the objects you need to deliver.",
            areas = new[]
            {
                new Rect(831, 724, 108, 108),  // Red, below the board's bottom-left square
                new Rect(1181, 373, 108, 108), // Blue, right of the board's top-right square
            },
        },
        new TutorialStep
        {
            text = "These are the rings.\nDeliver each object to the ring that matches its color.",
            areas = new[]
            {
                new Rect(650, 368, 120, 120),  // Red's ring, left of the board's top-left square
                new Rect(825, 193, 120, 120),  // Blue's ring, above the board's top-left square
            },
        },
        new TutorialStep
        {
            text = "Your goal is to deliver as many objects as you can,\nusing as few tiles as possible.",
            // the info panel's PROGRESS (GoalText, its two lines centered in its 180-high box) and SCORE with the
            // "best plans use N tiles" line (BonusText, from the top of its 90-high box), worked out from the panel's
            // column layout; check them with TutorialController's Log Selected Area if the panel changes again
            areas = new[]
            {
                new Rect(37, 284, 264, 66),  // PROGRESS, 0 / 2 delivered
                new Rect(37, 419, 264, 84),  // SCORE, 0 / 675, 3rd star: best plans use 3 tiles
            },
        },
        new TutorialStep
        {
            text = "Click the tile to pick it up.",
            area = new Rect(703, 7, 85, 116), // the vertical I slot
            clickable = true,
            advance = Advance.Action,
            action = Did.Select(TileType.StraightV),
        },
        new TutorialStep
        {
            text = "Click this spot on the board to place the tile.",
            area = new Rect(800, 517, 171, 171), // the board's bottom-left square (the board area takes the Removed panel's space at runtime)
            clickable = true,
            advance = Advance.Action,
            action = Did.Place(TileType.StraightV, 0, 0, 0),
        },
        new TutorialStep
        {
            text = "The object moves along the path by itself,\nand stops where the path ends.",
            area = new Rect(800, 431, 171, 257), // the same square plus half a square above it, where the object stops
        },
        new TutorialStep
        {
            text = "Now click this tile to pick it up.",
            area = new Rect(402, 7, 85, 116), // the L slot open to the left and down (the 5th slot)
            clickable = true,
            advance = Advance.Action,
            action = Did.Select(TileType.CornerSW),
        },
        new TutorialStep
        {
            text = "Now click this spot to place the tile.",
            area = new Rect(800, 342, 171, 171), // the board's top-left square, one square above the bottom-left one
            clickable = true,
            advance = Advance.Action,
            action = Did.Place(TileType.CornerSW, 0, 0, 1),
            // Red's delivery must break this L, not the I below it: a used L here would leave Blue no way to its
            // ring and end the level mid-tutorial. once, so after the undo lesson removals are random again
            onShow = () => game.ForceNextRemoval(0, 0, 1),
        },
        new TutorialStep
        {
            text = "Each object heads for the ring of its own color.\nWhen it reaches the ring, it's delivered!",
            area = new Rect(625, 342, 346, 171), // the top-left square plus one square to its left, where Red's ring is
        },
        new TutorialStep
        {
            text = "When an object is delivered,\none random tile from its path is removed.",
            area = new Rect(800, 342, 171, 346), // the board's top-left and bottom-left squares, Red's path
        },
        new TutorialStep
        {
            // the second line in the frames' yellow (TutorialStyle's frameColor)
            text = "But you can actually deliver both objects at the same time!\n" +
                   "<color=#F2C740>Deliver two or more together, and you get an extra + tile in your supply!</color>",
            // no area: everything is dimmed and the bubble sits in the middle of the screen
        },
        new TutorialStep
        {
            text = "Click Undo.",
            area = new Rect(1193, 17, 96, 96), // the top bar's Undo button
            clickable = true,
            advance = Advance.Action,
            action = Did.Undo(),
        },
        new TutorialStep
        {
            text = "Undo takes back the last tile you placed,\nand everything that happened because of it.",
            area = new Rect(800, 342, 171, 346), // the board's top-left and bottom-left squares
        },
        // the better plan: Blue's straight first, then a + where both paths meet, so both arrive together
        new TutorialStep
        {
            text = "Now click this tile to pick it up.",
            area = new Rect(613, 7, 85, 116), // the horizontal I slot
            clickable = true,
            advance = Advance.Action,
            action = Did.Select(TileType.StraightH),
        },
        // a placement on purpose in the wrong spot, to show right-click taking it back. no object can reach the
        // bottom-right square from where they wait, so the tile stays unused and can always go back
        new TutorialStep
        {
            text = "Now click this spot to place the tile.",
            area = new Rect(975, 517, 171, 171), // the board's bottom-right square
            clickable = true,
            advance = Advance.Action,
            action = Did.Place(TileType.StraightH, 0, 1, 0),
        },
        new TutorialStep
        {
            text = "Oops! That's the wrong spot.",
            area = new Rect(975, 517, 171, 171), // the board's bottom-right square
        },
        new TutorialStep
        {
            text = "Right-click this tile to put it back.\nYou can do this with any tile that no object has passed through yet.",
            area = new Rect(975, 517, 171, 171), // the board's bottom-right square
            clickable = true,
            advance = Advance.Action,
            action = Did.TakeBack(0, 1, 0),
        },
        new TutorialStep
        {
            // the only straight went back, but nothing is selected any more
            text = "Now pick it up again.",
            area = new Rect(613, 7, 85, 116), // the horizontal I slot
            clickable = true,
            advance = Advance.Action,
            action = Did.Select(TileType.StraightH),
        },
        new TutorialStep
        {
            text = "Now click this spot to place the tile.",
            area = new Rect(975, 342, 171, 171), // the board's top-right square
            clickable = true,
            advance = Advance.Action,
            action = Did.Place(TileType.StraightH, 0, 1, 1),
        },
        new TutorialStep
        {
            text = "Now pick up the + tile.",
            area = new Rect(11, 7, 85, 116), // the + slot (the 1st slot)
            clickable = true,
            advance = Advance.Action,
            action = Did.Select(TileType.Cross),
        },
        new TutorialStep
        {
            text = "Place it on this spot, where both paths meet.",
            area = new Rect(800, 342, 171, 171), // the board's top-left square
            clickable = true,
            advance = Advance.Action,
            action = Did.Place(TileType.Cross, 0, 0, 1),
        },
        new TutorialStep
        {
            text = "At a + tile, each object stops and waits for you to choose its way.",
            // the top-left square plus about half a square on every side: Blue waits past its right edge,
            // and the junction's arrows reach a little outside it
            area = new Rect(713, 255, 345, 345),
        },
        // the junction's arrows sit 0.39 of a square from its middle, each a quarter of a square wide
        new TutorialStep
        {
            text = "Click this arrow to send Red left, to its ring.",
            area = new Rect(791, 401, 52, 52), // Red's left arrow, at the top-left square's left edge
            clickable = true,
            advance = Advance.Action,
            action = Did.Choose("Red", Dir.W),
        },
        new TutorialStep
        {
            text = "Now click this arrow to send Blue up, to its ring.",
            area = new Rect(860, 333, 52, 52), // Blue's up arrow, at the top-left square's top edge
            clickable = true,
            advance = Advance.Action,
            action = Did.Choose("Blue", Dir.N),
            // Red walks to its ring and Blue into the + first, so the area doesn't open on a spot with no arrow yet
            delay = 0.5f,
            // this choice ends the level: its end window waits until the next step has shown the + it earned
            onShow = () => game.HoldEndWindow(true),
        },
        new TutorialStep
        {
            text = "Delivering two or more objects at once earned you an extra + tile!",
            area = new Rect(11, 7, 85, 116), // the + slot (the 1st slot), now x1: the + used, plus the one earned
            delay = 0.5f, // Blue's walk to its ring, so the + is in the supply when this shows
            onDone = () => game.HoldEndWindow(false), // the end window shows now
        },
        new TutorialStep
        {
            text = "The level ends when every object is delivered,\nor when the rest can no longer reach their rings.",
            area = new Rect(450, 150, 700, 600), // the end window (its size is MenuView's Menu Panel Size)
            delay = 0.1f, // the end window shows the frame after the last step lets it go
            bubble = BubbleSide.Right,
            bubbleWidth = 400, // narrower, to fit between the end window and the screen's right edge
        },
        new TutorialStep
        {
            text = "Your final score depends on several things.\nLet's look at each one.",
            // the four score labels in the end window's summary (Deliveries to Unused tiles), estimated from its
            // text layout: font size 20, about 23 per line, these are lines 3 to 6
            area = new Rect(492, 452, 210, 100), // from here on the bubble may cover the end window, just not the area
        },
        new TutorialStep
        {
            text = "Deliveries is the number of objects you delivered.",
            area = new Rect(492, 452, 110, 31), // "Deliveries", line 3 of the summary
        },
        new TutorialStep
        {
            text = "Delivered together is the number of objects delivered at the same time by placing one tile.",
            area = new Rect(492, 475, 200, 31), // "Delivered together", line 4 of the summary
        },
        new TutorialStep
        {
            // counted once per delivered object that went through any door, not once per door
            text = "Used a door is the number of delivered objects that went through a door on the way.\n" +
                   "(You'll learn more about this in a later level.)",
            area = new Rect(492, 498, 127, 31), // "Used a door", line 5 of the summary
        },
        new TutorialStep
        {
            // GameManager.FinishLevel: the tiles left in the supply, scored only when everyone was delivered
            text = "Unused tiles is the number of tiles left in your supply.\n" +
                   "They only count if every object is delivered.",
            area = new Rect(492, 521, 130, 31), // "Unused tiles", line 6 of the summary
        },
        new TutorialStep
        {
            text = "That's the end of the tutorial!\nNow try the next level on your own.",
            // no area: everything is dimmed and the bubble sits in the middle of the screen
            buttonText = "End",
        },
    };
}
