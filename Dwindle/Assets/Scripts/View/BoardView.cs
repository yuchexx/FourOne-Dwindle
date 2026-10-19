using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// draws the boards and animates what happens on them
public class BoardView : MonoBehaviour
{
    [Header("Prefabs")]
    public TileView tilePrefab;
    public CellView cellPrefab;
    public ObjectView objectPrefab;
    public DestinationView destinationPrefab;
    public Button arrowButtonPrefab;

    [Header("Layers")]
    public RectTransform cellsRoot;
    public RectTransform markersRoot;
    public RectTransform objectsRoot;
    public RectTransform choicesRoot;

    [Header("Board")]
    public float boardSize = 700f; // a single board fits a square this size
    [Range(0.2f, 1f)] public float objectSize = 0.55f; // an object's circle, as a fraction of a cell
    [Range(0.2f, 1f)] public float ringSize = 0.62f;   // a ring, as a fraction of a cell
    [Tooltip("The board area also takes the strip on the right where the Removed panel was, so the boards center in the space next to the info panel")]
    public bool useRemovedPanelSpace = true;
    public float areaRightMargin = 15f; // gap between the board area and the screen's right edge

    void Awake()
    {
        if (useRemovedPanelSpace && cellsRoot.parent != null && cellsRoot.parent.parent is RectTransform area)
            area.offsetMax = new Vector2(-areaRightMargin, area.offsetMax.y);
    }
    public float moveSecondsPerCell = 0.2f;
    public float teleportSeconds = 0.25f; // shrinking into one door then growing out of the other

    [Header("Colors")]
    public Color cellColor = new Color(0.86f, 0.87f, 0.89f);
    public Color obstacleColor = new Color(0.22f, 0.22f, 0.25f);
    public Color tileColor = new Color(0.28f, 0.31f, 0.38f);
    public Color crossHighlightColor = new Color(0.98f, 0.88f, 0.55f);
    public Color lockedCellColor = new Color(0.62f, 0.64f, 0.69f); // a tile an object has gone through: it can no longer be returned
    [Range(0f, 1f)] public float ghostFade = 0.55f; // how far the hover preview fades toward the cell color
    public Color boardFrameColor = new Color(0.19f, 0.20f, 0.24f); // behind each board, so boards read as separate
    public Color doorColor = new Color(0.62f, 0.64f, 0.68f); // every door, so none looks tied to one object
    public Color doorLabelColor = new Color(0.08f, 0.08f, 0.10f);
    public Color doorHoverColor = new Color(0.98f, 0.82f, 0.35f); // both ends of a door pair while the mouse is over one
    public Color flashColor = new Color(1f, 0.45f, 0.45f);
    public float flashHoldSeconds = 0.4f;
    public float flashFadeSeconds = 1.2f;

    public Cell[,] Cells { get; private set; } // the shared grid, null outside every board
    public float CellSize { get; private set; }

    LevelData level;
    Dir? pickedDirection;
    Action<DoorSpec> onDoorClicked;

    // builds the boards, the doors, the destination rings and the objects, and hands back the objects.
    public List<MovingObject> Build(LevelData levelData, Action<Vector2Int> onCellClicked,
                                    Action<DoorSpec> onDoorClicked = null, Action<Vector2Int> onCellRightClicked = null,
                                    Func<TileType?> previewTile = null)
    {
        level = levelData;
        this.onDoorClicked = onDoorClicked;
        this.previewTile = previewTile;
        ghost = null;
        StopAllCoroutines(); // a Flash left over from the last level would touch a destroyed cell
        Clear(cellsRoot);
        Clear(markersRoot);
        Clear(objectsRoot);
        Clear(choicesRoot);

        // leave room for one ring of cells around each board, where objects and destinations sit
        int cols = level.width + 2, rows = level.height + 2;
        if (level.boards.Count == 1)
            CellSize = Mathf.Floor(boardSize / Mathf.Max(cols, rows));
        else
        {
            // several boards side by side use the whole board area
            var area = cellsRoot.parent != null && cellsRoot.parent.parent is RectTransform areaRect
                ? areaRect.rect.size : new Vector2(boardSize, boardSize);
            CellSize = Mathf.Floor(Mathf.Min(area.x / cols, area.y / rows));
        }

        foreach (var b in level.boards) MakeFrame(b);

        Cells = new Cell[level.width, level.height];
        foreach (var b in level.boards)
            for (int x = 0; x < b.width; x++)
            for (int y = 0; y < b.height; y++)
            {
                var p = b.origin + new Vector2Int(x, y);
                var view = Instantiate(cellPrefab, cellsRoot);
                var rt = (RectTransform)view.transform;
                rt.anchoredPosition = CellPos(p);
                rt.sizeDelta = Vector2.one * (CellSize - 4); // 4px gap between cells
                view.background.color = cellColor;
                view.Init(p, onCellClicked, onCellRightClicked, ShowGhost, HideGhost);
                // no hover or click tint on the squares: the faded preview of the selected tile is the only hover
                var colors = view.button.colors;
                colors.highlightedColor = colors.normalColor;
                colors.selectedColor = colors.normalColor;
                colors.pressedColor = colors.normalColor;
                view.button.colors = colors;
                Cells[p.x, p.y] = new Cell { view = view };
            }

        foreach (var o in level.obstacles)
        {
            Cells[o.x, o.y].obstacle = true;
            Cells[o.x, o.y].view.background.color = obstacleColor;
        }

        doorHighlights.Clear();
        MakeDoorHighlightRoot();
        foreach (var door in level.doors) MakeDoor(door);

        var objects = new List<MovingObject>();
        foreach (var spec in level.objects)
        {
            var dest = Instantiate(destinationPrefab, markersRoot);
            dest.Setup(spec.color, CellSize * ringSize);
            ((RectTransform)dest.transform).anchoredPosition = CellPos(spec.dest);

            var view = Instantiate(objectPrefab, objectsRoot);
            view.Setup(spec.color, CellSize * objectSize);
            view.Face(spec.heading);

            // objects and rings don't take the mouse, so a door under one can still be hovered and clicked
            foreach (var g in view.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            foreach (var g in dest.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

            objects.Add(new MovingObject { spec = spec, pos = spec.start, heading = spec.heading, view = view, dest = dest });
        }
        LayoutWaitingObjects(objects);
        return objects;
    }

    // a dark panel behind a board and its ring, so side-by-side boards read as separate
    void MakeFrame(BoardSpec b)
    {
        var frame = MakeImage("BoardFrame", cellsRoot);
        var rt = frame.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        var low = CellPos(b.origin + new Vector2Int(-1, -1));
        var high = CellPos(b.origin + new Vector2Int(b.width, b.height));
        rt.anchoredPosition = (low + high) / 2f;
        rt.sizeDelta = high - low + Vector2.one * CellSize * 0.9f;
        frame.color = boardFrameColor;
    }

    // a door: a gray bar along the edge, and a badge with its letter in the middle
    void MakeDoor(DoorSpec door)
    {
        var mid = EdgePos(door.cell, door.side);
        bool horizontalEdge = door.side == Dir.N || door.side == Dir.S;

        var bar = MakeImage("Door " + door.label, markersRoot);
        bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        bar.rectTransform.anchoredPosition = mid;
        float length = CellSize * 0.7f, thickness = Mathf.Max(6f, CellSize * 0.1f);
        bar.rectTransform.sizeDelta = horizontalEdge ? new Vector2(length, thickness) : new Vector2(thickness, length);
        bar.color = doorColor;

        var badge = MakeImage("Badge", bar.rectTransform);
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        badge.rectTransform.anchoredPosition = Vector2.zero;
        badge.rectTransform.sizeDelta = Vector2.one * CellSize * 0.3f;
        badge.color = doorColor;

        if (onDoorClicked != null)
        {
            // the bar and the badge both take clicks; the button on the bar gets the badge's too
            bar.raycastTarget = badge.raycastTarget = true;
            var button = bar.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onDoorClicked(door));
            // hovering either end lights up both ends of the pair, so it's clear which doors are linked
            var hover = bar.gameObject.AddComponent<HoverRelay>();
            hover.onEnter = () => SetDoorHighlight(door.label, true);
            hover.onExit = () => SetDoorHighlight(door.label, false);
        }

        var letter = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var lrt = (RectTransform)letter.transform;
        lrt.SetParent(badge.rectTransform, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var text = letter.GetComponent<TextMeshProUGUI>();
        text.text = door.label;
        text.fontSize = CellSize * 0.22f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = doorLabelColor;
        text.raycastTarget = false;

        // the hover highlight: a copy of this door in the highlight color, on a layer above the objects so an
        // object standing at the door doesn't hide it. it never takes clicks, and shows only while hovered
        var copy = Instantiate(bar.gameObject, doorHighlightRoot);
        foreach (var c in copy.GetComponents<Button>()) Destroy(c);
        foreach (var c in copy.GetComponents<HoverRelay>()) Destroy(c);
        foreach (var g in copy.GetComponentsInChildren<Graphic>(true))
        {
            g.raycastTarget = false;
            if (g is Image) g.color = doorHoverColor;
        }
        copy.transform.position = bar.transform.position;
        copy.SetActive(false);
        if (!doorHighlights.TryGetValue(door.label, out var copies)) doorHighlights[door.label] = copies = new List<GameObject>();
        copies.Add(copy);
    }

    // the highlight copies of both ends of each door pair, by letter, on a layer drawn after the objects
    readonly Dictionary<string, List<GameObject>> doorHighlights = new Dictionary<string, List<GameObject>>();
    RectTransform doorHighlightRoot;

    void SetDoorHighlight(string label, bool on)
    {
        if (!doorHighlights.TryGetValue(label, out var copies)) return;
        foreach (var c in copies) if (c != null) c.SetActive(on);
    }

    // a layer just above the objects, lined up with them
    void MakeDoorHighlightRoot()
    {
        if (doorHighlightRoot != null) { Clear(doorHighlightRoot); return; }
        var go = new GameObject("DoorHighlights", typeof(RectTransform));
        doorHighlightRoot = (RectTransform)go.transform;
        doorHighlightRoot.SetParent(objectsRoot.parent, false);
        doorHighlightRoot.anchorMin = objectsRoot.anchorMin;
        doorHighlightRoot.anchorMax = objectsRoot.anchorMax;
        doorHighlightRoot.pivot = objectsRoot.pivot;
        doorHighlightRoot.anchoredPosition = objectsRoot.anchoredPosition;
        doorHighlightRoot.sizeDelta = objectsRoot.sizeDelta;
        doorHighlightRoot.localScale = objectsRoot.localScale;
        doorHighlightRoot.SetSiblingIndex(objectsRoot.GetSiblingIndex() + 1);
    }

    static Image MakeImage(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    // board position relative to the board's center of a grid coordinate
    public Vector2 CellPos(Vector2Int c) =>
        new Vector2((c.x - (level.width - 1) / 2f) * CellSize, (c.y - (level.height - 1) / 2f) * CellSize);

    // the middle of a cell's edge
    public Vector2 EdgePos(Vector2Int c, Dir side) => CellPos(c) + (Vector2)DirUtil.Offset(side) * CellSize * 0.5f;

    // inside one of the boards
    public bool InGrid(Vector2Int c) => level.BoardOf(c) >= 0;

    public void SetTile(Vector2Int p, TileType t)
    {
        HideGhost();
        var cell = Cells[p.x, p.y];
        cell.tile = t;
        if (cell.tileView != null) Destroy(cell.tileView.gameObject);
        cell.tileView = Instantiate(tilePrefab, cell.view.transform);
        cell.tileView.Show(t, tileColor);
    }

    public void ClearTile(Vector2Int p)
    {
        var cell = Cells[p.x, p.y];
        RemoveTile(cell);
        StartCoroutine(Flash(cell.view.background));
    }

    // a cell's own color: dark for an obstacle, darker for a tile an object has used
    Color CellColor(Cell cell) => cell.obstacle ? obstacleColor : cell.locked ? lockedCellColor : cellColor;

    // an object went through this tile: from now on it can't be returned to the supply
    public void SetLocked(Vector2Int p, bool locked)
    {
        var cell = Cells[p.x, p.y];
        cell.locked = locked;
        cell.view.background.color = CellColor(cell);
    }

    // for undo: which tiles were locked, after RestoreTiles has put the tiles back
    public void RestoreLocks(bool[,] locked)
    {
        for (int x = 0; x < Cells.GetLength(0); x++)
        for (int y = 0; y < Cells.GetLength(1); y++)
        {
            var cell = Cells[x, y];
            if (cell == null) continue;
            cell.locked = locked[x, y];
            cell.view.background.color = CellColor(cell);
        }
    }

    // right-click returned the tile to the supply: it just goes, without the red flash of a lost tile
    public void TakeBackTile(Vector2Int p)
    {
        var cell = Cells[p.x, p.y];
        RemoveTile(cell);
        cell.view.background.color = CellColor(cell);
    }

    // the hover preview: a faded copy of the selected tile on an empty square.
    // with no tile selected (or on a square that's taken) nothing happens, since clicking wouldn't place anything
    Func<TileType?> previewTile;
    TileView ghost;
    Vector2Int ghostCell;

    void ShowGhost(Vector2Int p)
    {
        HideGhost(p);
        var cell = Cells[p.x, p.y];
        var t = previewTile?.Invoke();
        if (t == null || cell == null || cell.obstacle || cell.tile != null) return;
        ghost = Instantiate(tilePrefab, cell.view.transform);
        ghost.Show(t.Value, Color.Lerp(tileColor, cellColor, ghostFade));
        ghostCell = p;
    }

    void HideGhost(Vector2Int p)
    {
        if (ghost == null || p != ghostCell) return;
        HideGhost();
    }

    public void HideGhost()
    {
        if (ghost == null) return;
        Destroy(ghost.gameObject);
        ghost = null;
        var cell = Cells != null && ghostCell.x < Cells.GetLength(0) && ghostCell.y < Cells.GetLength(1) ? Cells[ghostCell.x, ghostCell.y] : null;
        if (cell != null && cell.view != null) cell.view.background.color = CellColor(cell);
    }

    static void RemoveTile(Cell cell)
    {
        cell.locked = false; // a new tile here would start free
        cell.tile = null;
        if (cell.tileView != null) Destroy(cell.tileView.gameObject);
        cell.tileView = null;
    }

    // for undo: puts every cell's tile back without the removal flash, and gives every cell its own color again
    public void RestoreTiles(TileType?[,] tiles)
    {
        for (int x = 0; x < Cells.GetLength(0); x++)
        for (int y = 0; y < Cells.GetLength(1); y++)
        {
            var cell = Cells[x, y];
            if (cell == null) continue;
            cell.view.background.color = CellColor(cell); // a flash or junction highlight cut short
            if (cell.tile == tiles[x, y]) continue;
            if (tiles[x, y] is TileType t) SetTile(new Vector2Int(x, y), t);
            else RemoveTile(cell);
        }
    }

    public void StopFlashes() => StopAllCoroutines();

    // where an object rests
    public Vector2 WaitPos(MovingObject o) =>
        InGrid(o.pos) || o.viaDoor ? EdgePos(o.pos, o.heading) : CellPos(o.pos);

    // several spread each group out along its edge
    public void LayoutWaitingObjects(List<MovingObject> objects)
    {
        var groups = objects.Where(o => !o.completed)
            .GroupBy(o => Vector2Int.RoundToInt(WaitPos(o) * 2f / CellSize));
        foreach (var group in groups)
        {
            var list = group.ToList();
            Vector2 center = WaitPos(list[0]);
            // East/West spread vertically and vice versa
            bool movesHorizontally = list[0].heading == Dir.E || list[0].heading == Dir.W;
            Vector2 along = movesHorizontally ? Vector2.up : Vector2.right;
            float spacing = Mathf.Min(0.36f, 0.9f / list.Count) * CellSize;
            for (int i = 0; i < list.Count; i++)
                list[i].view.Rect.anchoredPosition = center + along * ((i - (list.Count - 1) / 2f) * spacing);
        }
    }

    // animates the object to a board position
    public IEnumerator Slide(MovingObject o, Vector2 target)
    {
        Vector2 start = o.view.Rect.anchoredPosition;
        float seconds = moveSecondsPerCell * Vector2.Distance(start, target) / CellSize;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            o.view.Rect.anchoredPosition = Vector2.Lerp(start, target, t / seconds);
            yield return null;
        }
        o.view.Rect.anchoredPosition = target;
    }

    // the object shrinks into one door and grows out of its partner
    public IEnumerator Teleport(MovingObject o, Vector2 to)
    {
        float half = teleportSeconds / 2f;
        for (float t = 0; t < half; t += Time.deltaTime)
        {
            o.view.Rect.localScale = Vector3.one * (1f - t / half);
            yield return null;
        }
        o.view.Rect.anchoredPosition = to;
        for (float t = 0; t < half; t += Time.deltaTime)
        {
            o.view.Rect.localScale = Vector3.one * (t / half);
            yield return null;
        }
        o.view.Rect.localScale = Vector3.one;
    }

    public void MarkDelivered(MovingObject o)
    {
        o.view.gameObject.SetActive(false);
        o.dest.SetDelivered(true);
    }

    // for undo: matches an object's view to its state - shown or delivered, facing, full size, crossed out if blocked.
    // where it stands is set by LayoutWaitingObjects
    public void SyncObject(MovingObject o)
    {
        o.view.gameObject.SetActive(!o.completed);
        o.dest.SetDelivered(o.completed);
        o.view.Face(o.heading);
        o.view.Rect.localScale = Vector3.one; // a teleport cut short
        o.view.SetBlocked(o.blocked);
    }

    // the object waits at the center of a + or T tile or a door, with one arrow per way it may go
    public IEnumerator AskDirection(MovingObject o, List<Dir> options)
    {
        var cellBg = Cells[o.pos.x, o.pos.y].view.background;
        cellBg.color = crossHighlightColor;
        yield return AskDirectionAt(o, CellPos(o.pos), options);
        cellBg.color = CellColor(Cells[o.pos.x, o.pos.y]);
    }

    // arrows around a point
    public IEnumerator AskDirectionAt(MovingObject o, Vector2 center, List<Dir> options)
    {
        Clear(choicesRoot);
        pickedDirection = null;
        foreach (var d in options)
        {
            var dir = d; // copy for the click handler below
            var button = Instantiate(arrowButtonPrefab, choicesRoot);
            var rt = (RectTransform)button.transform;
            rt.sizeDelta = Vector2.one * CellSize * 0.25f;
            rt.anchoredPosition = center + (Vector2)DirUtil.Offset(d) * CellSize * 0.39f; // that way
            rt.localEulerAngles = new Vector3(0, 0, DirUtil.Angle(d)); // the prefab's arrow points up
            button.GetComponent<Graphic>().color = o.spec.color;
            button.onClick.AddListener(() => pickedDirection = dir);
        }

        while (pickedDirection == null) yield return null; // wait for a click

        Clear(choicesRoot);
        o.heading = pickedDirection.Value;
    }

    public void ClearChoices() => Clear(choicesRoot);

    // briefly tints a cell red so the player sees where a tile was removed
    IEnumerator Flash(Image img)
    {
        img.color = flashColor;
        yield return new WaitForSeconds(flashHoldSeconds);
        for (float t = 0; t < flashFadeSeconds; t += Time.deltaTime)
        {
            img.color = Color.Lerp(flashColor, cellColor, t / flashFadeSeconds);
            yield return null;
        }
        img.color = cellColor;
    }

    static void Clear(Transform root)
    {
        foreach (Transform child in root) Destroy(child.gameObject);
    }
}
