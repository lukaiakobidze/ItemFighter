using ItemFighter.Core.Engine;
using ItemFighter.Core.Run;
using ItemFighter.Platform;

namespace ItemFighter.Screens.Run;

public enum RunScreenKind { Map, Prep, Shop, Drop, End }

/// <summary>The run being played: its state plus its save file. Every change is saved straight away.</summary>
public sealed class RunSession(RunState run, string savePath)
{
    public static string DefaultSavePath => Path.Combine(Paths.Saves, "run.json");

    public RunState Run { get; } = run;

    /// <summary>Saves the run, or deletes the save once the run is over.</summary>
    public void Commit()
    {
        if (Run.IsOver) RunSave.Delete(savePath);
        else RunSave.Save(savePath, Run);
    }

    /// <summary>Starts the current encounter's fight. Saved as Fighting, so quitting now replays this fight.</summary>
    public BattleSetup BeginFight()
    {
        var setup = Run.StartFight();
        Commit();
        return setup;
    }

    public void EndFight(BattleResult result)
    {
        Run.FinishFight(result == BattleResult.PlayerWon);
        Commit();
    }

    /// <summary>The screen for a phase. Fighting maps to Prep: a fight is only ever entered from Prep.</summary>
    public static RunScreenKind ScreenFor(RunPhase phase) => phase switch
    {
        RunPhase.ChoosingEncounter => RunScreenKind.Map,
        RunPhase.Preparing or RunPhase.Fighting => RunScreenKind.Prep,
        RunPhase.Shopping => RunScreenKind.Shop,
        RunPhase.Dropping => RunScreenKind.Drop,
        _ => RunScreenKind.End,
    };

    /// <summary>Shows the screen for the run's current phase.</summary>
    public void ShowCurrent(Game1 game)
    {
        if (Run.Phase == RunPhase.Fighting) Run.Phase = RunPhase.Preparing; // never left mid-fight on screen
        game.Show(ScreenFor(Run.Phase) switch
        {
            RunScreenKind.Map => new MapScreen(game, this),
            RunScreenKind.Prep => new PrepScreen(game, this),
            RunScreenKind.Shop => new ShopScreen(game, this),
            RunScreenKind.Drop => new DropScreen(game, this),
            _ => new RunEndScreen(game, this),
        });
    }

    public void StartFight(Game1 game)
    {
        var setup = BeginFight();
        game.Show(new FightScreen(game, setup, result =>
        {
            EndFight(result);
            ShowCurrent(game);
        }));
    }
}
