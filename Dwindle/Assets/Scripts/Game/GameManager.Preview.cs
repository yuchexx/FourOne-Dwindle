using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Settings' show tile breaks (on by default): the tiles a placement would break are tinted light red (sure or only for
// some arrows) while it's hovered, and stay tinted through its round: at an arrow question, everything its arrows could
// break (hovering one: just that arrow's); once picked, what that arrow breaks. a tint goes when its tile breaks; the
// rest when the round ends. a dry run each time (BreakPreview). part of GameManager
public partial class GameManager
{
    (MovingObject o, DoorSpec door, List<Dir> ways)? asking; // the arrow question showing now (door: the one it came through)
    BreakPreview.Result roundMarks; // what the round shows when no arrow is hovered

    // the hover preview moved, changed tile or went (at null). mid-round the round's own tints stay
    void OnGhostChanged(Vector2Int? at, TileType? t)
    {
        if (busy) return;
        if (!Session.showBreaks || at == null || t == null || levelOver) { board.ClearBreakMarks(); return; }
        var tiles = BoardTiles();
        if ((board.DraggingFrom ?? pickedUp) is Vector2Int from) tiles.Remove(from); // a board tile being moved
        tiles[at.Value] = t.Value;
        board.ShowBreakMarks(BreakPreview.Round(level, tiles, IsObstacle, PreviewObjects()));
    }

    // an arrow hovered (null: left or picked): that arrow's breaks, or back to the round's
    void OnArrowHovered(Dir? way)
    {
        if (!Session.showBreaks) return;
        board.ShowBreakMarks(way != null && asking != null ? ArrowMarks(way.Value) : roundMarks);
    }

    // the round starts: what the placement breaks
    void MarkRound()
    {
        roundMarks = Session.showBreaks ? BreakPreview.Round(level, BoardTiles(), IsObstacle, PreviewObjects()) : null;
        board.ShowBreakMarks(roundMarks);
    }

    // an arrow question shows: everything its arrows could break
    void MarkQuestion(MovingObject o, DoorSpec door, List<Dir> ways)
    {
        asking = (o, door, ways);
        if (!Session.showBreaks) return;
        var each = ways.Select(ArrowMarks).ToList();
        roundMarks = new BreakPreview.Result();
        roundMarks.always.UnionWith(each[0].always);
        foreach (var r in each)
        {
            roundMarks.always.IntersectWith(r.always);
            roundMarks.maybe.UnionWith(r.always);
            roundMarks.maybe.UnionWith(r.maybe);
        }
        roundMarks.maybe.ExceptWith(roundMarks.always);
        board.ShowBreakMarks(roundMarks);
    }

    // the arrow was picked (o.heading): what it breaks, until the next question or the breaks
    void MarkPicked(MovingObject o)
    {
        if (Session.showBreaks && asking != null) { roundMarks = ArrowMarks(o.heading); board.ShowBreakMarks(roundMarks); }
        asking = null;
    }

    // the round is over (or undone): no tints left
    void ClearRoundMarks()
    {
        asking = null;
        roundMarks = null;
        board.ClearBreakMarks();
    }

    BreakPreview.Result ArrowMarks(Dir way)
    {
        var (o, door, _) = asking.Value;
        return BreakPreview.Arrow(level, BoardTiles(), IsObstacle, PreviewObjects(),
            objects.IndexOf(o), door, way, roundDelivered.Select(x => objects.IndexOf(x)));
    }

    Dictionary<Vector2Int, TileType> BoardTiles()
    {
        var tiles = new Dictionary<Vector2Int, TileType>();
        for (int x = 0; x < Cells.GetLength(0); x++)
            for (int y = 0; y < Cells.GetLength(1); y++)
                if (Cells[x, y]?.tile is TileType t) tiles[new Vector2Int(x, y)] = t;
        return tiles;
    }

    bool IsObstacle(Vector2Int c) => Cells[c.x, c.y] == null || Cells[c.x, c.y].obstacle;

    List<BreakPreview.Obj> PreviewObjects() => objects.Select(o => new BreakPreview.Obj
    {
        pos = o.pos, dest = o.spec.dest, heading = o.heading, viaDoor = o.viaDoor, completed = o.completed, path = o.path,
    }).ToList(); // BreakPreview copies them
}
