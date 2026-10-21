using System;
using System.Collections.Generic;
using UnityEngine;

// how a tutorial step ends
public enum Advance { NextButton, ClickInArea, Condition }

// where a step's text bubble goes, next to its area
public enum BubbleSide { Auto, Below, Above, Left, Right, Center }

// one page of a tutorial
public class TutorialStep
{
    public string text;
    public Rect? area;           // left undimmed, in 1600x900 units from the top-left corner; null = dim everything
    public bool clickable;       // can the player click inside the area
    public Advance advance = Advance.NextButton;
    public Func<bool> condition; // for Advance.Condition: the step ends once this is true
    public BubbleSide bubble = BubbleSide.Auto; // Auto = below the area, or above it if there's no room
    public float delay;          // seconds to wait before the step shows; nothing can be clicked meanwhile
    public float bubbleWidth;    // 0 = the Bubble Width in TutorialController's Style
    public string buttonText;    // the Next button's label; null = "Next"
}

// tutorial pages per level. get an area from TutorialController's "Log Selected Area" (with a 16:9 Game view)
public static class Tutorials
{
    // level index (from 0) -> its tutorial
    static readonly Dictionary<int, Func<List<TutorialStep>>> ByLevel = new()
    {
        { 0, Level1 },
    };

    // null when the level has no tutorial
    public static List<TutorialStep> For(int levelIndex) =>
        ByLevel.TryGetValue(levelIndex, out var make) ? make() : null;

    static List<TutorialStep> Level1() => new()
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
            text = "These buttons let you undo a move, restart the level,\nread the rules, or open the menu.",
            area = new Rect(1193, 17, 398, 96), // the top bar's Undo, Restart, Rules and Menu buttons
        },
        new TutorialStep
        {
            text = "Click the tile to pick it up.",
            area = new Rect(703, 7, 85, 116), // the vertical I slot
            clickable = true,
            advance = Advance.ClickInArea,
        },
        new TutorialStep
        {
            text = "Click this spot on the board to place the tile.",
            area = new Rect(800, 517, 171, 171), // the board's bottom-left square (the board area takes the Removed panel's space at runtime)
            clickable = true,
            advance = Advance.ClickInArea,
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
            advance = Advance.ClickInArea,
        },
        new TutorialStep
        {
            text = "Now click this spot to place the tile.",
            area = new Rect(800, 342, 171, 171), // the board's top-left square, one square above the bottom-left one
            clickable = true,
            advance = Advance.ClickInArea,
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
            text = "Now click this tile to pick it up.",
            area = new Rect(222, 7, 85, 116), // the L slot open up and to the right (the 3rd slot)
            clickable = true,
            advance = Advance.ClickInArea,
        },
        new TutorialStep
        {
            text = "Now click this spot to place the tile.",
            area = new Rect(975, 517, 171, 171), // the board's bottom-right square
            clickable = true,
            advance = Advance.ClickInArea,
        },
        new TutorialStep
        {
            text = "Now click this tile to pick it up.",
            area = new Rect(703, 7, 85, 116), // the vertical I slot
            clickable = true,
            advance = Advance.ClickInArea,
        },
        new TutorialStep
        {
            text = "Now click this spot to place the tile.",
            area = new Rect(975, 342, 171, 171), // the board's top-right square
            clickable = true,
            advance = Advance.ClickInArea,
        },
        new TutorialStep
        {
            text = "The level ends when every object is delivered,\nor when the rest can no longer reach their rings.",
            area = new Rect(450, 150, 700, 600), // the end window (its size is MenuView's Menu Panel Size)
            delay = 0.5f, // Blue's last walk, then GameManager's End Popup Delay (1.2s), until the end window shows
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
            text = "Delivered together is the number of objects delivered at the same time by placing one tile.\n" +
                   "(You'll learn more about this in a later level.)",
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
