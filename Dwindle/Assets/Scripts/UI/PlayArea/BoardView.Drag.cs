using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// dragging tiles: from the supply onto a square (GameManager starts it), or a board tile to another square (a move)
// or off the board (back to the supply). the dragged tile follows the mouse; over a square it can go on, the usual preview shows too. part of BoardView
public partial class BoardView
{
    [Header("Dragging")]
    [Range(0f, 1f)] public float draggedFromOpacity = 0.35f; // the board tile being dragged, left on its square
    [Range(0.2f, 1f)] public float draggedTileOpacity = 0.9f;

    // set by GameManager: whether the tile on this square may be dragged off (no object has used it, nothing moving)
    public Func<Vector2Int, bool> canDragTile;
    public Action<Vector2Int, Vector2Int> onTileMoved; // set by GameManager: a board tile dropped on another square

    TileType draggedTile;

    RectTransform dragIcon;
    CanvasGroup dragIconGroup;
    Vector2Int? draggedFrom; // the square a board tile is being dragged from

    public bool Dragging => dragIcon != null;

    // the tile under the mouse: as big as sizeLike (a supply card's icon), or as a square
    public void StartDrag(TileType t, PointerEventData e, RectTransform sizeLike = null)
    {
        EndDrag();
        var canvas = cellsRoot.GetComponentInParent<Canvas>().rootCanvas; // BoardView itself may sit outside the Canvas
        var go = new GameObject("DraggedTile", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        dragIcon = (RectTransform)go.transform;
        dragIcon.SetParent(canvas.transform, false);
        dragIcon.SetAsLastSibling();
        dragIcon.anchorMin = dragIcon.anchorMax = dragIcon.pivot = new Vector2(0.5f, 0.5f);
        float toCanvas = 1f / canvas.transform.lossyScale.x; // the board and the dock may be scaled in the canvas
        dragIcon.sizeDelta = sizeLike != null ? sizeLike.rect.size * sizeLike.lossyScale.x * toCanvas
                                              : Vector2.one * (CellSize - CellGap) * cellsRoot.lossyScale.x * toCanvas;
        go.GetComponent<Image>().color = cellColor;
        dragIconGroup = go.GetComponent<CanvasGroup>();
        dragIconGroup.blocksRaycasts = false; // the squares under it still get the mouse
        dragIconGroup.alpha = draggedTileOpacity;
        var tile = Instantiate(tilePrefab, dragIcon);
        var rt = (RectTransform)tile.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        tile.Show(t, tileColor);
        MoveDrag(e);
    }

    public void MoveDrag(PointerEventData e)
    {
        if (dragIcon == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)dragIcon.parent, e.position, e.pressEventCamera, out var local);
        dragIcon.anchoredPosition = local;
    }

    public void EndDrag()
    {
        if (draggedFrom is Vector2Int p && Cells != null && Cells[p.x, p.y]?.tileView != null)
            SetTileOpacity(Cells[p.x, p.y], 1f);
        draggedFrom = null;
        if (dragIcon != null) Destroy(dragIcon.gameObject);
        dragIcon = null;
    }

    // the square under the mouse, if any
    public static Vector2Int? SquareUnder(PointerEventData e)
    {
        var hit = e.pointerCurrentRaycast.gameObject;
        var cell = hit != null ? hit.GetComponentInParent<CellView>() : null;
        return cell != null ? cell.Coord : (Vector2Int?)null;
    }

    // CellView: a drag starting on a square. false when there's no tile it may take
    public bool StartBoardDrag(Vector2Int p, PointerEventData e)
    {
        var cell = Cells[p.x, p.y];
        if (cell?.tile == null || canDragTile == null || !canDragTile(p)) return false;
        HideGhost();
        StartDrag(cell.tile.Value, e);
        draggedFrom = p;
        draggedTile = cell.tile.Value;
        SetTileOpacity(cell, draggedFromOpacity);
        return true;
    }

    // CellView: dropped. off the board it goes back to the supply (like right-click); on another square it moves there
    public void EndBoardDrag(PointerEventData e, Action<Vector2Int> takeBack)
    {
        var from = draggedFrom;
        EndDrag();
        if (from == null) return;
        var to = SquareUnder(e);
        if (to == null) takeBack?.Invoke(from.Value);
        else if (to != from) onTileMoved?.Invoke(from.Value, to.Value);
    }

    static void SetTileOpacity(Cell cell, float alpha)
    {
        var group = cell.tileView.GetComponent<CanvasGroup>();
        if (group == null) group = cell.tileView.gameObject.AddComponent<CanvasGroup>();
        group.alpha = alpha;
    }
}
