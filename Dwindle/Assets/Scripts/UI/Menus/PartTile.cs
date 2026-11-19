using TMPro;
using UnityEngine;
using UnityEngine.UI;

// one part on the Levels page's part map; set up once on the template tile, PartMapView clones it
public class PartTile : MonoBehaviour
{
    public Button button;
    public TMP_Text title;     // the part's name
    public TMP_Text stars;     // e.g. "41/96 stars", or the locked text
    public GameObject lockIcon; // optional, shown while locked
}
