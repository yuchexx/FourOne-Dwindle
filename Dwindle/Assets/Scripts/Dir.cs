using UnityEngine;

// grid directions
public enum Dir { N = 0, E = 1, S = 2, W = 3 }

public static class DirUtil
{
    public static readonly Dir[] All = { Dir.N, Dir.E, Dir.S, Dir.W };

    public static Vector2Int Offset(Dir d) => d switch
    {
        Dir.N => new Vector2Int(0, 1),
        Dir.E => new Vector2Int(1, 0),
        Dir.S => new Vector2Int(0, -1),
        _ => new Vector2Int(-1, 0),
    };

    // relative turns from the point of view of something facing d
    public static Dir Opposite(Dir d) => (Dir)(((int)d + 2) % 4);
    public static Dir Right(Dir d) => (Dir)(((int)d + 1) % 4);
    public static Dir Left(Dir d) => (Dir)(((int)d + 3) % 4);

    // Z rotation for a sprite/prefab that points up (north) by default
    public static float Angle(Dir d) => -90f * (int)d;
}
