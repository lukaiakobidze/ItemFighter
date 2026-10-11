using ItemFighter.Core.Run;
using ItemFighter.UI;
using Microsoft.Xna.Framework;

namespace ItemFighter.Screens.Run;

/// <summary>The run is over, won or lost. The save is already gone.</summary>
public sealed class RunEndScreen : Screen
{
    public static readonly Rectangle MenuButton = new(490, 470, 300, 60);

    private readonly RunSession _session;

    public RunEndScreen(Game1 game, RunSession session) : base(game)
    {
        _session = session;
        session.Commit(); // deletes the save
    }

    public override void Draw(Ui ui)
    {
        var run = _session.Run;
        bool won = run.Phase == RunPhase.Won;
        ui.TextIn(won ? "Run won!" : "Run over", new Rectangle(0, 150, 1280, 90), 72, won ? Palette.Heal : Palette.Damage, bold: true);
        string reached = won ? $"You beat all {run.Config.Missions} missions." : $"You fell in mission {run.Mission}, encounter {Math.Min(run.Encounter + 1, run.Config.EncountersPerMission + 1)}.";
        ui.TextIn(reached, new Rectangle(0, 270, 1280, 36), 24, Palette.Text);
        ui.TextIn($"Fights won: {run.FightsWon}  ·  Gold left: {run.Gold}  ·  Items: {run.AllOwned.Count}  ·  Passives: {run.Passives.Count}",
            new Rectangle(0, 320, 1280, 30), 18, Palette.TextDim);
        if (ui.Button(MenuButton, "Main menu", 26) || ui.Input.KeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
            Game.Show(new MainMenuScreen(Game));
    }
}
