using ItemFighter.UI;
using Microsoft.Xna.Framework;

namespace ItemFighter.Screens;

/// <summary>Shown when the JSON content has mistakes: lists every one, and F5 reloads after fixing them.</summary>
public sealed class ContentErrorScreen(Game1 game) : Screen(game)
{
    public override void Draw(Ui ui)
    {
        ui.Text("Content has mistakes", new Vector2(40, 30), 36, Palette.Damage, bold: true);
        ui.Text("Fix the files below, then press F5 (or the button) to reload.", new Vector2(40, 80), 18, Palette.TextDim);
        float y = 120;
        foreach (var e in Game.Data.Errors.Take(14))
        {
            y += ui.Wrapped($"{Path.GetFileName(e.File)}  {e.Id}: {e.Message}", new Vector2(40, y), 1200, 16, Palette.Text) + 6;
            if (y > 620) break;
        }
        if (Game.Data.Errors.Count > 14) ui.Text($"...and {Game.Data.Errors.Count - 14} more", new Vector2(40, y), 16, Palette.TextDim);
        if (ui.Button(new Rectangle(40, 640, 220, 50), "Reload (F5)")) Game.ReloadContent();
    }
}
