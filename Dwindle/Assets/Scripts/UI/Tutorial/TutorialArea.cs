using UnityEngine;

// a box for Level 1's screen tour, placed by hand: put it on an empty object under GameScreen, size its Rect Transform
// over the area, and type its line. with any of these in the scene, the tour uses them (in order) instead of its own
[RequireComponent(typeof(RectTransform))]
public class TutorialArea : MonoBehaviour
{
    [TextArea] public string text = "Pause menu";
    public int order;
}
