using System.Collections.Generic;
using UnityEngine;

// level 6's tutorial: doors
public static partial class Tutorials
{
    static List<TutorialStep> Level6(GameManager game) => new()
    {
        new TutorialStep
        {
            text = "Let's take a break!\nThis level shows you how doors work.",
            // no area: everything is dimmed and the bubble sits in the middle of the screen
        },
        // the doors, worked out from this level's layout: squares 136 apart, a door is a bar about 95 long and 14 wide
        // with a 41-wide letter badge, on the middle of the edge between a board's two squares
        new TutorialStep
        {
            text = "These are doors.\nHover over one, and both doors with the same letter light up.",
            areas = new[]
            {
                new Rect(606, 461, 53, 107),  // door A on the left board, between its two squares
                new Rect(1286, 461, 53, 107), // door A on the right board, between its two squares
            },
            clickable = true, // lets the pointer reach the doors, so hovering one lights up both
        },
        new TutorialStep
        {
            text = "Click this tile to pick it up.",
            area = new Rect(613, 7, 85, 116), // the horizontal I slot (the slots are the same in every level)
            clickable = true,
            advance = Advance.Action,
            action = Did.Select(TileType.StraightH),
        },
        new TutorialStep
        {
            text = "Click this spot to place the tile.",
            area = new Rect(498, 449, 132, 132), // the left board's left square
            clickable = true,
            advance = Advance.Action,
            action = Did.Place(TileType.StraightH, 0, 0, 0),
        },
        new TutorialStep
        {
            text = "As you can see, Red went into door A on the left board\nand came out of door A on the right board.",
            areas = new[]
            {
                new Rect(494, 445, 276, 140),  // the left board's two squares
                new Rect(1174, 445, 276, 140), // the right board's two squares
            },
            // Red comes out of the right board's door and waits there for a way to face; the next step picks it
        },
        new TutorialStep
        {
            text = "Red is standing on this door now.\nHover over a door, and it shows on top of whatever covers it.",
            // wider than the doors, to take in all of Red (37 from its middle, on the right door's middle): the
            // arrows' inner ends fall inside too, which is why this step is hover-only
            areas = new[]
            {
                new Rect(589, 461, 87, 108),  // door A on the left board, as wide as the right one
                new Rect(1269, 461, 87, 108), // door A on the right board, with Red on it
            },
            hoverOnly = true, // the door highlight works, clicks do nothing
        },
        new TutorialStep
        {
            text = "Coming out of a door, an object can face either way.\nClick this arrow to send Red right.",
            // the door's arrows sit 0.39 of a square from the door's middle, each a quarter of a square wide
            area = new Rect(1344, 494, 42, 42), // Red's right arrow, next to the right board's door
            clickable = true,
            advance = Advance.Action,
            action = Did.Choose("Red", Dir.E),
        },
        // no step to pick the straight again: the second one stays selected after the first went down
        // (a click on its slot would deselect it)
        new TutorialStep
        {
            text = "Your other straight tile is still selected.\nClick this spot to place it.",
            area = new Rect(1314, 449, 132, 132), // the right board's right square
            clickable = true,
            advance = Advance.Action,
            action = Did.Place(TileType.StraightH, 1, 1, 0),
        },
        new TutorialStep
        {
            text = "That's the end of the tutorial!\nNow try the next level on your own.",
            // no area: everything is dimmed and the bubble sits in the middle of the screen, over the end window too
            buttonText = "End",
            delay = 0.5f, // Red walks to its ring and the end window shows first, so the dim doesn't hide the delivery
        },
    };
}
