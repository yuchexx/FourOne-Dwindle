using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// draws the board and animates what happens on it
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
    public float boardSize = 700f;
    public float moveSecondsPerCell = 0.2f;

    [Header("Colors")]
    public Color cellColor = new Color(0.86f, 0.87f, 0.89f);
    public Color obstacleColor = new Color(0.22f, 0.22f, 0.25f);
    public Color tileColor = new Color(0.28f, 0.31f, 0.38f);
    public Color crossHighlightColor = new Color(0.98f, 0.88f, 0.55f);
    public Color flashColor = new Color(1f, 0.45f, 0.45f);
    public float flashHoldSeconds = 0.4f;
    public float flashFadeSeconds = 1.2f;

    public Cell[,] Cells { get; private set; }
    public float CellSize { get; private set; }

    LevelData level;
    Dir? pickedDirection;

    // builds the grid, the destination rings and the objects, and hands back the objects.
    public List<MovingObject> Build(LevelData levelData, Action<Vector2Int> onCellClicked)
    {
        level = levelData;
        StopAllCoroutines(); // a Flash left over from the last level would touch a destroyed cell
        Clear(cellsRoot);
        Clear(markersRoot);
        Clear(objectsRoot);
        Clear(choicesRoot);

        int w = level.width, h = level.height;
        // leave room for one ring of cells around the grid, where objects and destinations sit
        CellSize = Mathf.Floor(boardSize / (Mathf.Max(w, h) + 2));

        Cells = new Cell[w, h];
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            var p = new Vector2Int(x, y);
            var view = Instantiate(cellPrefab, cellsRoot);
            var rt = (RectTransform)view.transform;
            rt.anchoredPosition = CellPos(p);
            rt.sizeDelta = Vector2.one * (CellSize - 4); // 4px gap between cells
            view.background.color = cellColor;
            view.Init(p, onCellClicked);
            Cells[x, y] = new Cell { view = view };
        }

        foreach (var o in level.obstacles)
        {
            Cells[o.x, o.y].obstacle = true;
            Cells[o.x, o.y].view.background.color = obstacleColor;
        }

        var objects = new List<MovingObject>();
        foreach (var spec in level.objects)
        {
            var dest = Instantiate(destinationPrefab, markersRoot);
            dest.Setup(spec.color, CellSize * 0.62f);
            ((RectTransform)dest.transform).anchoredPosition = CellPos(spec.dest);

            var view = Instantiate(objectPrefab, objectsRoot);
            view.Setup(spec.color, CellSize * 0.55f);
            view.Face(spec.heading);

            objects.Add(new MovingObject { spec = spec, pos = spec.start, heading = spec.heading, view = view, dest = dest });
        }
        LayoutWaitingObjects(objects);
        return objects;
    }

    // board position (relative to the board's center) of a grid coordinate
    public Vector2 CellPos(Vector2Int c) =>
        new Vector2((c.x - (level.width - 1) / 2f) * CellSize, (c.y - (level.height - 1) / 2f) * CellSize);

    public bool InGrid(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < level.width && c.y < level.height;

    public void SetTile(Vector2Int p, TileType t)
    {
        var cell = Cells[p.x, p.y];
        cell.tile = t;
        if (cell.tileView != null) Destroy(cell.tileView.gameObject);
        cell.tileView = Instantiate(tilePrefab, cell.view.transform);
        cell.tileView.Show(t, tileColor);
    }

    public void ClearTile(Vector2Int p)
    {
        var cell = Cells[p.x, p.y];
        cell.tile = null;
        if (cell.tileView != null) Destroy(cell.tileView.gameObject);
        cell.tileView = null;
        StartCoroutine(Flash(cell.view.background));
    }

    // where an object rests
    public Vector2 WaitPos(MovingObject o) =>
        InGrid(o.pos) ? CellPos(o.pos) + (Vector2)DirUtil.Offset(o.heading) * CellSize * 0.5f : CellPos(o.pos);

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

    public void MarkDelivered(MovingObject o)
    {
        o.view.gameObject.SetActive(false);
        o.dest.MarkDelivered();
    }

    // the object waits at the center of the + tile, clicking one sends it out that way
    public IEnumerator AskDirection(MovingObject o)
    {
        var cellBg = Cells[o.pos.x, o.pos.y].view.background;
        cellBg.color = crossHighlightColor;
        Clear(choicesRoot);

        pickedDirection = null;
        foreach (var d in DirUtil.All)
        {
            if (d == DirUtil.Opposite(o.heading)) continue; // can't turn back
            var dir = d; // copy for the click handler below
            var button = Instantiate(arrowButtonPrefab, choicesRoot);
            var rt = (RectTransform)button.transform;
            rt.sizeDelta = Vector2.one * CellSize * 0.25f;
            rt.anchoredPosition = CellPos(o.pos) + (Vector2)DirUtil.Offset(d) * CellSize * 0.39f; // near that edge
            rt.localEulerAngles = new Vector3(0, 0, DirUtil.Angle(d)); // the prefab's arrow points up
            button.GetComponent<Image>().color = o.spec.color;
            button.onClick.AddListener(() => pickedDirection = dir);
        }

        while (pickedDirection == null) yield return null; // wait for a click

        Clear(choicesRoot);
        cellBg.color = cellColor;
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
