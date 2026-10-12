// 7 pre-oriented tiles: one cross, four L corners, two I straights
public enum TileType { Cross, CornerNE, CornerES, CornerSW, CornerWN, StraightH, StraightV }

public enum TileCategory { Cross, Corner, Straight }

public static class Tiles
{
    // inventory bar: + | four L | two I
    public static readonly TileType[] All =
    {
        TileType.Cross,
        TileType.CornerNE, TileType.CornerES, TileType.CornerSW, TileType.CornerWN,
        TileType.StraightH, TileType.StraightV,
    };

    static int Bit(Dir d) => 1 << (int)d;

    static int Mask(TileType t) => t switch
    {
        TileType.Cross => Bit(Dir.N) | Bit(Dir.E) | Bit(Dir.S) | Bit(Dir.W),
        TileType.CornerNE => Bit(Dir.N) | Bit(Dir.E),
        TileType.CornerES => Bit(Dir.E) | Bit(Dir.S),
        TileType.CornerSW => Bit(Dir.S) | Bit(Dir.W),
        TileType.CornerWN => Bit(Dir.W) | Bit(Dir.N),
        TileType.StraightH => Bit(Dir.E) | Bit(Dir.W),
        _ => Bit(Dir.N) | Bit(Dir.S),
    };

    // asks is this side open, true - open
    public static bool Has(TileType t, Dir side) => (Mask(t) & Bit(side)) != 0;

    // L and I tiles: came in here and where to leave
    public static Dir OtherExit(TileType t, Dir entrySide)
    {
        foreach (var d in DirUtil.All)
            if (d != entrySide && Has(t, d)) return d;
        return entrySide;
    }

    public static TileCategory Category(TileType t) => t switch
    {
        TileType.Cross => TileCategory.Cross,
        TileType.StraightH or TileType.StraightV => TileCategory.Straight,
        _ => TileCategory.Corner,
    };

    public static string Name(TileType t) => t switch
    {
        TileType.Cross => "+ Cross",
        TileType.CornerNE => "L (N-E)",
        TileType.CornerES => "L (E-S)",
        TileType.CornerSW => "L (S-W)",
        TileType.CornerWN => "L (W-N)",
        TileType.StraightH => "I (horizontal)",
        _ => "I (vertical)",
    };
}
