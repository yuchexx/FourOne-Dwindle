using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Settings' show tile breaks: a dry run of a round, nothing drawn. the objects move as in the game (GameManager.Rules)
// and each delivery breaks its tile by BreakRule.For. objects never block each other, so each one's walk is tried on
// its own with every arrow (a + or T, or a door with two ways out), then every mix of how the walks end is played to
// the breaks. always: tiles that break whatever arrows are picked; maybe: tiles that break only for some
public static class BreakPreview
{
    public class Obj
    {
        public Vector2Int pos, dest;
        public Dir heading;
        public bool viaDoor, completed;
        public List<Vector2Int> path;
        public Obj Copy() => new Obj { pos = pos, dest = dest, heading = heading, viaDoor = viaDoor, completed = completed, path = new List<Vector2Int>(path) };
    }

    public class Result
    {
        public readonly HashSet<Vector2Int> always = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> maybe = new HashSet<Vector2Int>();
        public bool cut; // too many ways: only the first ones were tried
    }

    const int MaxEnds = 64;      // different ways one object's walk can end
    const int MaxMixes = 4096;   // mixes of those played to the breaks
    const int MaxVisits = 2;     // the same arrow question met again in one walk (a loop): asked at most this often
    const int MaxSteps = 20000;  // squares walked trying one object's arrows

    // the round after a placement. tiles: every tile on the board with the new one; objs: as in GameManager.objects
    public static Result Round(LevelData l, Dictionary<Vector2Int, TileType> tiles, Func<Vector2Int, bool> obstacle, List<Obj> objs)
    {
        var w = new Walker(l, tiles);
        var ends = objs.Select(o => o.completed ? new List<Obj> { o.Copy() } : w.Ends(o, null, default)).ToList();
        return Mix(l, tiles, obstacle, ends, objs.Select(o => o.completed).ToArray(), new List<int>(), w.cut);
    }

    // mid-round, at an arrow question: what follows if `way` is picked. obj: the asking object; door: the door it came
    // through (null at a + or T); delivered: objects this round has delivered so far (all before obj)
    public static Result Arrow(LevelData l, Dictionary<Vector2Int, TileType> tiles, Func<Vector2Int, bool> obstacle, List<Obj> objs,
        int obj, DoorSpec door, Dir way, IEnumerable<int> delivered)
    {
        var w = new Walker(l, tiles);
        var ends = objs.Select((o, i) =>
            i < obj || o.completed ? new List<Obj> { o.Copy() } // moved already this round, or delivered
            : i == obj ? w.Ends(o, new Question { door = door }, way)
            : w.Ends(o, null, default)).ToList();
        var wasDone = objs.Select((o, i) => i < obj || o.completed).ToArray();
        return Mix(l, tiles, obstacle, ends, wasDone, delivered.ToList(), w.cut);
    }

    // for the checks (Editor/Tests): one round with the arrows given, in the order they're asked. objs and tiles are left
    // as the round leaves them; returns the tiles it broke, in order
    internal static List<Vector2Int> Play(LevelData l, Dictionary<Vector2Int, TileType> tiles, Func<Vector2Int, bool> obstacle,
        List<Obj> objs, Queue<Dir> arrows)
    {
        var w = new Walker(l, tiles);
        var delivered = new List<int>();
        for (int i = 0; i < objs.Count; i++)
        {
            var o = objs[i];
            if (o.completed) continue;
            for (var q = w.Walk(o); q != null; q = w.Answer(o, q, arrows.Dequeue()) ?? w.Walk(o)) { }
            if (o.completed) delivered.Add(i);
        }
        var broken = Breaks(l, tiles, obstacle, objs, delivered);
        foreach (var c in broken) tiles.Remove(c);
        return broken;
    }

    // every mix of the objects' ends; each mix delivers in object order (after `before`) and breaks its tiles
    static Result Mix(LevelData l, Dictionary<Vector2Int, TileType> tiles, Func<Vector2Int, bool> obstacle,
        List<List<Obj>> ends, bool[] wasDone, List<int> before, bool cut)
    {
        var r = new Result { cut = cut };
        if (ends.Any(e => e.Count == 0)) return r;
        int n = ends.Count, mixes = 0;
        var pick = new int[n];
        bool first = true;
        var cache = new Dictionary<(Vector2Int, Dir, bool, bool, string), Dictionary<Vector2Int, int>>(); // Ahead, per waiting state and tiles gone
        while (true)
        {
            var objs = ends.Select((e, i) => e[pick[i]]).ToList();
            var delivered = new List<int>(before);
            for (int i = 0; i < n; i++) if (!wasDone[i] && objs[i].completed) delivered.Add(i);
            var broken = Breaks(l, tiles, obstacle, objs, delivered, cache);
            if (first) { r.always.UnionWith(broken); first = false; }
            else r.always.IntersectWith(broken);
            r.maybe.UnionWith(broken);

            if (++mixes >= MaxMixes) { r.cut = true; break; }
            int k = 0; // the next mix, like an odometer
            while (k < n && ++pick[k] == ends[k].Count) pick[k++] = 0;
            if (k == n) break;
        }
        r.maybe.ExceptWith(r.always);
        return r;
    }

    // GameManager.HandleDeliveries: each delivered object breaks one tile, in order. objs aren't changed
    static List<Vector2Int> Breaks(LevelData l, Dictionary<Vector2Int, TileType> tiles, Func<Vector2Int, bool> obstacle,
        List<Obj> objs, List<int> delivered, Dictionary<(Vector2Int, Dir, bool, bool, string), Dictionary<Vector2Int, int>> cache = null)
    {
        var broken = new List<Vector2Int>();
        if (delivered.Count == 0) return broken;
        var left = new Dictionary<Vector2Int, TileType>(tiles);
        TileType? TileAt(Vector2Int c) => left.TryGetValue(c, out var t) ? t : (TileType?)null;
        bool Blocked(Vector2Int c) => l.BoardOf(c) < 0 || obstacle(c);
        var waiting = objs.Where(w => !w.completed).Select(w => (w.pos, w.heading, w.viaDoor)).ToList();
        foreach (var i in delivered)
        {
            string gone = string.Join(";", broken.OrderBy(c => c.x).ThenBy(c => c.y));
            Dictionary<Vector2Int, int> Ahead(int w, bool throughEmpty)
            {
                var (pos, heading, viaDoor) = waiting[w];
                var key = (pos, heading, viaDoor, throughEmpty, gone);
                if (cache == null || !cache.TryGetValue(key, out var found))
                {
                    found = BreakRule.Ahead(l, pos, heading, viaDoor, TileAt, Blocked, throughEmpty);
                    if (cache != null) cache[key] = found;
                }
                return found;
            }
            var pick = BreakRule.For(l, objs[i].path, waiting, TileAt, Blocked, out _, Ahead);
            if (pick == null) continue;
            left.Remove(pick.Value);
            broken.Add(pick.Value);
        }
        return broken;
    }

    class Question { public List<Dir> ways; public DoorSpec door; }

    // one object's walk, as GameManager.MoveObject, LeaveSquare and ComeOut move it
    class Walker
    {
        readonly LevelData l;
        readonly Dictionary<Vector2Int, TileType> tiles;
        public bool cut;
        int steps;
        Dictionary<string, Obj> found; // how the walk being tried can end, told apart by what the breaks look at

        public Walker(LevelData l, Dictionary<Vector2Int, TileType> tiles) { this.l = l; this.tiles = tiles; }

        // every way o's walk can end (o isn't changed). first: a question it's at now, answered with way
        public List<Obj> Ends(Obj o, Question first, Dir way)
        {
            found = new Dictionary<string, Obj>();
            steps = 0;
            Go(o.Copy(), first, way, new Dictionary<(Vector2Int, Dir, bool), int>());
            if (found.Count == 0) { cut = true; return new List<Obj> { o.Copy() }; } // only loops: as if it stayed put
            return found.Values.ToList();
        }

        void Go(Obj o, Question q, Dir way, Dictionary<(Vector2Int, Dir, bool), int> visits)
        {
            var next = q != null ? Answer(o, q, way) ?? Walk(o) : Walk(o);
            if (next == null) { if (steps < MaxSteps) Keep(o); else cut = true; return; } // out of steps: not a real end
            var key = (o.pos, o.heading, next.door != null);
            visits.TryGetValue(key, out int seen);
            if (seen >= MaxVisits) return; // round a loop again: nothing new
            visits[key] = seen + 1;
            foreach (var w in next.ways)
            {
                if (found.Count >= MaxEnds || steps >= MaxSteps) { cut = true; return; }
                Go(o.Copy(), next, w, new Dictionary<(Vector2Int, Dir, bool), int>(visits));
            }
        }

        // a delivered object breaks by its path; a waiting one counts by where it stands and faces
        void Keep(Obj o)
        {
            var key = o.completed ? "D" + string.Join(";", o.path) : $"W{o.pos}{o.heading}{o.viaDoor}";
            if (!found.ContainsKey(key)) found[key] = o;
        }

        // on until the road stops, it reaches its ring, or an arrow question comes
        public Question Walk(Obj o)
        {
            for (int guard = 0; guard < 500 && steps < MaxSteps; guard++, steps++)
            {
                if (!o.viaDoor && l.DoorOnEdge(o.pos, o.heading) != null) return null; // a door is a wall
                var next = o.pos + DirUtil.Offset(o.heading);
                if (l.BoardOf(next) < 0)
                {
                    if (next == o.dest) { o.pos = next; o.completed = true; }
                    return null;
                }
                var entry = DirUtil.Opposite(o.heading);
                if (!tiles.TryGetValue(next, out var t) || !Tiles.Has(t, entry)) return null;
                o.viaDoor = false;
                o.pos = next;
                o.path.Add(next);
                var ways = Tiles.Exits(t, entry);
                if (ways.Count > 1) return new Question { ways = ways };
                o.heading = ways[0];
                var q = ThroughDoor(o);
                if (q != null) return q;
            }
            return null;
        }

        // the arrow picked: at a + or T it may lead onto a door (another question coming out); at a door it steps out
        public Question Answer(Obj o, Question q, Dir way)
        {
            o.heading = way;
            if (q.door == null) return ThroughDoor(o);
            StepOut(o, l.Partner(q.door));
            return null;
        }

        // a door on the side it faces takes it through; two ways out of the far one is a question
        Question ThroughDoor(Obj o)
        {
            var door = l.DoorOnEdge(o.pos, o.heading);
            if (door == null) return null;
            var partner = l.Partner(door);
            var into = new List<Dir>();
            if (l.BoardOf(partner.cell) >= 0) into.Add(DirUtil.Opposite(partner.side));
            if (l.BoardOf(partner.Neighbor) >= 0) into.Add(partner.side);
            if (into.Count > 1) return new Question { ways = into, door = door };
            o.heading = into[0];
            StepOut(o, partner);
            return null;
        }

        static void StepOut(Obj o, DoorSpec partner)
        {
            o.pos = o.heading == partner.side ? partner.cell : partner.Neighbor;
            o.viaDoor = true;
        }
    }
}
