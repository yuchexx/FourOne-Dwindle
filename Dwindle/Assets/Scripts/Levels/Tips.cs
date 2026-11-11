// the end window's tips (shown after "Tip: "), one per trick. Levels.cs and the Level Maker both use these
public static class Tips
{
    public const string EarnedPlus = "Deliver two together to earn a + tile.";
    public const string TogetherToFinish = "Some objects have to arrive together.";
    public const string TogetherForScore = "Bring objects in together to score more.";
    public const string Bridge = "The bridge lets two roads cross.";
    public const string DetourToFinish = "Take a detour. Go to the other board through a door and come back.";
    public const string DetourForScore = "Going through a door scores +50, even if your ring is on the same board.";
    public const string OneRoute = "Start one route early, before anyone reaches it.";
    public const string TwoRoutes = "Start two routes early, before anyone reaches them.";
    public const string Pairs = "Pairs break a junction. Bring more in together.";
    public const string Routing = "Plan each road so it leaves the tiles the others need.";
    public const string FewerTiles = "Every unused tile scores. Try using fewer.";
    public const string BridgeAndDetour = "The bridge lets two roads cross. \nTake a detour. Go to the other board and come back.";

    // the constant's name for a tip, so the Level Maker writes Tips.Bridge and not the sentence (null: not one of these)
    public static string NameOf(string tip)
    {
        foreach (var f in typeof(Tips).GetFields())
            if (f.IsLiteral && (string)f.GetRawConstantValue() == tip) return f.Name;
        return null;
    }
}
