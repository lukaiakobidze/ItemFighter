using ItemFighter.Core.Run;
using ItemFighter.Screens.Run;
using ItemFighter.UI;
using Microsoft.Xna.Framework;

namespace ItemFighter.Screens;

public sealed class MainMenuScreen : Screen
{
    public static readonly Rectangle ContinueButton = new(490, 300, 300, 60);
    public static readonly Rectangle NewRunButton = new(490, 374, 300, 60);
    public static readonly Rectangle FightLabButton = new(490, 448, 300, 60);
    public static readonly Rectangle QuitButton = new(490, 522, 300, 60);

    private readonly RunState? _saved;
    private bool _confirmNewRun;

    public MainMenuScreen(Game1 game) : base(game)
    {
        _saved = RunSave.Load(RunSession.DefaultSavePath, game.Data.Run, game.Data.Database);
    }

    public bool HasSavedRun => _saved is not null;

    public override void Draw(Ui ui)
    {
        ui.TextIn("ItemFighter", new Rectangle(0, 140, 1280, 90), 72, Palette.Accent, bold: true);
        ui.TextIn("test build", new Rectangle(0, 230, 1280, 40), 22, Palette.TextDim);
        if (_saved is not null)
        {
            if (ui.Button(ContinueButton, $"Continue run (mission {_saved.Mission})", 22)) new RunSession(_saved, RunSession.DefaultSavePath).ShowCurrent(Game);
        }
        string newLabel = _confirmNewRun ? "Abandon saved run?" : "New run";
        if (ui.Button(NewRunButton, newLabel, 24, selected: _confirmNewRun))
        {
            if (_saved is not null && !_confirmNewRun) _confirmNewRun = true;
            else Game.Show(new CharacterSelectScreen(Game));
        }
        else if (ui.Input.LeftClicked) _confirmNewRun = false;
        if (ui.Button(FightLabButton, "Fight Lab", 24)) Game.Show(new FightLabScreen(Game));
        if (ui.Button(QuitButton, "Quit", 24)) Game.Exit();
        ui.Text($"{Game.Data.Database.All.Count} things loaded from {Platform.Paths.Data}", new Vector2(16, 690), 14, Palette.TextDim);
    }
}
