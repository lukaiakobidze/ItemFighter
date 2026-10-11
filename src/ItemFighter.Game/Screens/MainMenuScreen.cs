using ItemFighter.UI;
using Microsoft.Xna.Framework;

namespace ItemFighter.Screens;

public sealed class MainMenuScreen(Game1 game) : Screen(game)
{
    public static readonly Rectangle FightLabButton = new(490, 330, 300, 60);
    public static readonly Rectangle QuitButton = new(490, 410, 300, 60);

    public override void Draw(Ui ui)
    {
        ui.TextIn("ItemFighter", new Rectangle(0, 140, 1280, 90), 72, Palette.Accent, bold: true);
        ui.TextIn("test build: combat lab", new Rectangle(0, 230, 1280, 40), 22, Palette.TextDim);
        if (ui.Button(FightLabButton, "Fight Lab", 26)) Game.Show(new FightLabScreen(Game));
        if (ui.Button(QuitButton, "Quit", 26)) Game.Exit();
        ui.Text($"{Game.Data.Database.All.Count} things loaded from {Platform.Paths.Data}", new Vector2(16, 690), 14, Palette.TextDim);
    }
}
