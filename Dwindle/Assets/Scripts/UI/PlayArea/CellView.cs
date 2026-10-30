using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// for Cell prefab
// left click (the button) places a tile, right click returns one, and hovering shows the selected tile's preview.
// dragging its tile off the board returns it too
public class CellView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
                        IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Image background;
    public Button button;
    [HideInInspector] public BoardView board;

    Vector2Int coord;
    System.Action<Vector2Int> onClick, onRightClick, onEnter, onExit;
    bool draggingTile;

    public Vector2Int Coord => coord;

    // keep which grid coordinate this cell is and report it when clicked
    public void Init(Vector2Int coord, System.Action<Vector2Int> onClick,
                     System.Action<Vector2Int> onRightClick = null,
                     System.Action<Vector2Int> onEnter = null, System.Action<Vector2Int> onExit = null)
    {
        this.coord = coord;
        this.onClick = onClick;
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

    public void OnBeginDrag(PointerEventData eventData)
    {
        draggingTile = eventData.button == PointerEventData.InputButton.Left && board != null && board.StartBoardDrag(coord, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (draggingTile) board.MoveDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (draggingTile) board.EndBoardDrag(eventData, onRightClick);
        // a click that moved a little still counts as a click, if it ends on this square
        else if (eventData.button == PointerEventData.InputButton.Left && BoardView.SquareUnder(eventData) == coord) onClick?.Invoke(coord);
        draggingTile = false;
    }
}
