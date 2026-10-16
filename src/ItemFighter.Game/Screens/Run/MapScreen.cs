using ItemFighter.Core.Run;
using ItemFighter.UI;
using Microsoft.Xna.Framework;

namespace ItemFighter.Screens.Run;

/// <summary>Pick the next encounter: 1 of 3, or the mission boss once the mission's encounters are done.</summary>
public sealed class MapScreen(Game1 game, RunSession session) : RunScreen(game, session)
{
    private readonly IReadOnlyList<Encounter> _choices = session.Run.EncounterChoices();

    public IReadOnlyList<Encounter> Choices => _choices;

    protected override string Title => Run.IsBossNext ? "Boss day"
        : _choices.Count > 0 && _choices[0].Kind == EncounterKind.Shop ? $"Day {Run.Encounter + 1}: shop day"
        : $"Day {Run.Encounter + 1}: fight day";

    public static Rectangle ChoiceRect(int index, int count)
    {
        const int width = 200, gap = 80;
        int total = count * width + (count - 1) * gap;
        return new Rectangle(Layout.Margin + (1152 - total) / 2 + index * (width + gap), 110, width, 290);
    }

    protected override void DrawContent(Ui ui)
    {
        for (int i = 0; i < _choices.Count; i++)
        {
            var e = _choices[i];
            var r = ChoiceRect(i, _choices.Count);
            DrawEncounter(ui, e, r);
            if (ui.Clicked(r))
            {
                Run.Choose(e);
                Session.Commit();
                Session.ShowCurrent(Game);
                return;
            }
        }
    }

    /// <summary>Luka 2026-10-16: just the picture, the name and the gold it pays (details on hover).</summary>
    private void DrawEncounter(Ui ui, Encounter e, Rectangle r)
    {
        bool hover = ui.Hover(r);
        var square = new Rectangle(r.X, r.Y, r.Width, r.Width);
        var nameRect = new Rectangle(r.X, square.Bottom + 8, r.Width, 26);
        Color border = hover ? Palette.Text : e.Kind == EncounterKind.Boss ? Palette.Damage : Palette.Border;
        if (e.Kind == EncounterKind.Shop)
        {
            var shop = Run.Config.Shops.FirstOrDefault(s => s.Id == e.ShopId);
            string name = shop?.Name ?? "Shop";
            ui.Square("Encounters", e.ShopId ?? "shop", name, square, border, hover ? 3 : 2);
            ui.TextIn(name, nameRect, 20, Palette.Text, bold: true);
            if (hover)
            {
                string sells = shop?.Sells == ShopSells.Passives ? "Sells passives you don't have yet."
                    : shop?.Tags is { Count: > 0 } tags ? "Sells " + string.Join(", ", tags) + " items." : "Sells any item.";
                ui.Tooltip(name, [sells], Palette.Accent);
            }
        }
        else if (Db.TryGet(e.EnemyId!, out var enemy))
        {
            ui.Square("Enemies", enemy.Id, enemy.Name, square, border, hover ? 3 : 2);
            ui.TextIn(enemy.Name, nameRect, 20, e.Kind == EncounterKind.Boss ? Palette.Damage : Palette.Text, bold: true);
            string gold = $"{Run.GoldFor(e) + Run.GoldFromItems}";
            float gw = ui.Measure(gold, 20, bold: true).X + 30;
            ui.IconText("gold", "Gold: ", gold, new Vector2(r.Center.X - gw / 2, nameRect.Bottom + 4), 20, Palette.Accent, bold: true);
            if (hover)
            {
                var lines = Tooltips.For(enemy, 1, Db).Lines;
                if (e.Kind == EncounterKind.Boss) lines = ["Mission boss", .. lines];
                ui.Tooltip(enemy.Name, lines, border);
            }
        }
    }
}
