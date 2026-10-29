using System.Collections.Generic;
using System.Linq;

// the hint button, this session's progress and the settings. part of GameManager
public partial class GameManager
{
    readonly Hints hints = new Hints();
    bool keepHintsOnLoad; // set by Restart before the level ended (see Hints)

    public event System.Action HintsChanged; // the hint button and its texts should redraw
    public int HintStage => hints.Stage;     // 0 to 3
    public bool PurpleLost => hints.PurpleLost;
    public string HintTilesText => Hints.TilesText(level);         // stage 1, shown under the score
    public List<string> HintOrderText => Hints.OrderText(level);   // stage 2
    public List<GameAction[]> HintAnswerShown => Hints.AnswerPresses(level).Take(hints.AnswerSteps).ToList(); // stage 3
    public bool HintAnswerDone => hints.AnswerSteps >= Hints.AnswerPresses(level).Count;

    // the hint button: the next stage, or one more step of the answer
    public void UseHint()
    {
        if (level == null || levelOver || inputLocked) return;
        if (!hints.Advance(Hints.AnswerPresses(level).Count)) return;
        Report(new GameAction { kind = GameActionKind.Hint, hintStage = hints.Stage, answerStep = hints.AnswerSteps }); // for analytics
        HintsChanged?.Invoke();
    }

    // LoadLevel: a restart before the level ended keeps stage 3; anything else starts with no hints
    void ResetHintsForLoad()
    {
        if (keepHintsOnLoad) hints.OnRestart(); else hints.Clear();
        keepHintsOnLoad = false;
        HintsChanged?.Invoke();
    }

    // FinishLevel: keeps the level's best this session. purple = 3 stars without the answer hint
    bool SaveResult(int stars)
    {
        bool purple = stars == 3 && !hints.PurpleLost;
        Session.Save(levelIndex, stars, purple);
        return purple;
    }

    // Settings
    public bool AnimateUndo { get => board.animateUndo; set => board.animateUndo = value; }
    public bool UnlockAll { get => Session.unlockAll; set => Session.unlockAll = value; }
}
