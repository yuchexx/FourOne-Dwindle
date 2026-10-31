using System.Collections.Generic;
using System.Linq;

// 12 pre-oriented tiles
public enum TileType
{
    Cross, CornerNE, CornerES, CornerSW, CornerWN, StraightH, StraightV,
    Bridge, TeeNES, TeeESW, TeeSWN, TeeWNE,
}

// the groups of the supply bar
public enum TileCategory { Cross, Corner, Straight, Tee }

public static class Tiles
{
    // every tile type (the solver and the level tools go through them in this order)
    public static readonly TileType[] All =
    {
        TileType.Cross, TileType.Bridge,
        TileType.CornerNE, TileType.CornerES, TileType.CornerSW, TileType.CornerWN,
        TileType.StraightH, TileType.StraightV,
        TileType.TeeNES, TileType.TeeESW, TileType.TeeSWN, TileType.TeeWNE,
    };

    // the supply dock's order: + and bridge | L | I | T, each group turning clockwise
    public static readonly TileType[] SupplyOrder =
    {
        TileType.Cross, TileType.Bridge,
        TileType.CornerNE, TileType.CornerES, TileType.CornerSW, TileType.CornerWN,
        TileType.StraightV, TileType.StraightH,
        TileType.TeeWNE, TileType.TeeNES, TileType.TeeESW, TileType.TeeSWN,
    };

    static int Bit(Dir d) => 1 << (int)d;
    const int AllSides = 0b1111;

    static int Mask(TileType t) => t switch
    {
        TileType.Cross or TileType.Bridge => AllSides,
        TileType.CornerNE => Bit(Dir.N) | Bit(Dir.E),
        TileType.CornerES => Bit(Dir.E) | Bit(Dir.S),
        TileType.CornerSW => Bit(Dir.S) | Bit(Dir.W),
        TileType.CornerWN => Bit(Dir.W) | Bit(Dir.N),
        TileType.StraightH => Bit(Dir.E) | Bit(Dir.W),
        TileType.StraightV => Bit(Dir.N) | Bit(Dir.S),
        TileType.TeeNES => AllSides & ~Bit(Dir.W),
        TileType.TeeESW => AllSides & ~Bit(Dir.N),
        TileType.TeeSWN => AllSides & ~Bit(Dir.E),
        _ => AllSides & ~Bit(Dir.S), // TeeWNE
    };

    public static bool Has(TileType t, Dir side) => (Mask(t) & Bit(side)) != 0;

    public static List<Dir> Exits(TileType t, Dir entrySide)
    {
        if (t == TileType.Bridge) return new List<Dir> { DirUtil.Opposite(entrySide) };
        return DirUtil.All.Where(d => d != entrySide && Has(t, d)).ToList();
    }

    public static TileCategory Category(TileType t) => t switch
    {
        TileType.Cross or TileType.Bridge => TileCategory.Cross,
        TileType.TeeNES or TileType.TeeESW or TileType.TeeSWN or TileType.TeeWNE => TileCategory.Tee,
        TileType.StraightH or TileType.StraightV => TileCategory.Straight,
        _ => TileCategory.Corner,
    };
}
