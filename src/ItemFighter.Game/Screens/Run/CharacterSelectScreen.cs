using ItemFighter.Core.Content;
using ItemFighter.Core.Run;
using ItemFighter.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ItemFighter.Screens.Run;

/// <summary>Start a new run: pick a character.</summary>
public sealed class CharacterSelectScreen(Game1 game) : Screen(game)
{
    public static readonly Rectangle BackButton = new(1410, 32, 150, 40);

    public static Rectangle CardRect(int i) => new(Layout.Margin + i * 320, 120, 280, 420);

    /// <summary>Automation sets this so scripted runs are repeatable.</summary>
    public static ulong? SeedOverride { get; set; }

    public override void Draw(Ui ui)
    {
        ui.Text("Choose your character", new Vector2(Layout.Margin, 32), 30, Palette.Accent, bold: true);
        if (ui.Button(BackButton, "Back") || ui.Input.KeyPressed(Keys.Escape))
        {
            Game.Show(new MainMenuScreen(Game));
            return;
        }
        var characters = Game.Data.Database.OfKind(SourceKind.Character).OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
        for (int i = 0; i < characters.Count && i < 4; i++)
        {
            var def = characters[i];
            var r = CardRect(i);
            bool hover = ui.Hover(r);
            ui.Rect(r, hover ? Palette.PanelLight : Palette.Panel);
            ui.Art("Characters", def.Id, def.Name, new Rectangle(r.X + 10, r.Y + 10, r.Width - 20, 220));
            ui.TextIn(def.Name, new Rectangle(r.X, r.Y + 236, r.Width, 32), 24, Palette.Text, bold: true);
            var (_, lines) = Tooltips.For(def, 1, Game.Data.Database);
            float y = r.Y + 276;
            foreach (string line in lines) y += ui.Wrapped(line, new Vector2(r.X + 14, y), r.Width - 28, 15, Palette.TextDim) + 4;
            ui.Border(r, hover ? Palette.Accent : Palette.Border, hover ? 3 : 1);
            if (ui.Clicked(r))
            {
                StartRun(Game, def.Id);
                return;
            }
        }
        if (characters.Count == 0) ui.Text("No characters in the content. Add one under Data/characters.", new Vector2(60, 140), 20, Palette.Damage);
    }

    public static void StartRun(Game1 game, string characterId)
    {
        ulong seed = SeedOverride ?? (ulong)DateTime.UtcNow.Ticks;
        var run = RunState.New(seed, characterId, game.Data.Run, game.Data.Database);
        var session = new RunSession(run, RunSession.DefaultSavePath);
        session.Commit();
        session.ShowCurrent(game);
    }
}
