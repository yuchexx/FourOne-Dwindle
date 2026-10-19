using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// for Cell prefab
// left click (the button) places a tile, right click returns one, and hovering shows the selected tile's preview
public class CellView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Image background;
    public Button button;

    Vector2Int coord;
    System.Action<Vector2Int> onRightClick, onEnter, onExit;

    // keep which grid coordinate this cell is and report it when clicked
    public void Init(Vector2Int coord, System.Action<Vector2Int> onClick,
                     System.Action<Vector2Int> onRightClick = null,
                     System.Action<Vector2Int> onEnter = null, System.Action<Vector2Int> onExit = null)
    {
        this.coord = coord;
        this.onRightClick = onRightClick;
        this.onEnter = onEnter;
        this.onExit = onExit;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick(coord));
    }

    // the Button only answers the left button, so the right one is caught here
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right) onRightClick?.Invoke(coord);
    }

    public void OnPointerEnter(PointerEventData eventData) => onEnter?.Invoke(coord);
    public void OnPointerExit(PointerEventData eventData) => onExit?.Invoke(coord);
}
