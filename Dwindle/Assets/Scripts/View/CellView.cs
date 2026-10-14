using UnityEngine;
using UnityEngine.UI;

// for Cell prefab
public class CellView : MonoBehaviour
{
    public Image background;
    public Button button;

    // keep which grid coordinate this cell is and report it when clicked
    public void Init(Vector2Int coord, System.Action<Vector2Int> onClick)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick(coord));
    }
}
