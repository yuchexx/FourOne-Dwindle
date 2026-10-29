using TMPro;
using UnityEngine;
using UnityEngine.UI;

// one level on the Levels page: its number and best stars this session, or "Locked" while it can't be played yet.
// set up once on the template card; LevelSelectView clones it for every level
public class LevelCard : MonoBehaviour
{
    public Button button;
    public TMP_Text label;         // "Level #" or "Locked"
    public Graphic[] stars;        // three, left to right (StarGraphics), hidden while locked
    public Graphic[] tutorialFrame; // edges drawn around a locked level that has a tutorial
}
