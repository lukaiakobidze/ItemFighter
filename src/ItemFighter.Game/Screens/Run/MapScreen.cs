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
        const int width = 270, gap = 16;
        int total = count * width + (count - 1) * gap;
        return new Rectangle(24 + (842 - total) / 2 + index * (width + gap), 64, width, 306);
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

    private void DrawEncounter(Ui ui, Encounter e, Rectangle r)
    {
        bool hover = ui.Hover(r);
        var (label, color) = e.Kind switch
        {
            EncounterKind.Boss => ("BOSS", Palette.Damage),
            EncounterKind.Shop => ("Shop", Palette.Accent),
            _ => ("Fight", Palette.Text),
        };
        ui.Rect(r, hover ? Palette.PanelLight : Palette.Panel);
        ui.TextIn(label, new Rectangle(r.X, r.Y + 6, r.Width, 30), 22, color, bold: true);
        var art = new Rectangle(r.X + 20, r.Y + 42, r.Width - 40, 150);
        if (e.Kind == EncounterKind.Shop)
        {
            var shop = Run.Config.Shops.FirstOrDefault(s => s.Id == e.ShopId);
            string name = shop?.Name ?? "Shop";
            ui.Art("Encounters", e.ShopId ?? "shop", name, art);
            ui.TextIn(name, new Rectangle(r.X, r.Y + 198, r.Width, 26), 19, Palette.Text, bold: true);
            string sells = shop?.Tags is { Count: > 0 } tags ? "Sells " + string.Join(", ", tags) + " items" : "Sells any item";
            ui.TextIn(sells, new Rectangle(r.X, r.Y + 226, r.Width, 22), 15, Palette.TextDim);
        }
        else if (Db.TryGet(e.EnemyId!, out var enemy))
        {
            ui.Art("Enemies", enemy.Id, enemy.Name, art);
            ui.TextIn(enemy.Name, new Rectangle(r.X, r.Y + 198, r.Width, 26), 19, Palette.Text, bold: true);
            int hp = enemy.MaxHp;
            ui.TextIn($"{hp} HP", new Rectangle(r.X, r.Y + 226, r.Width, 22), 15, Palette.Hp);
            int itemGold = Run.GoldFromItems;
            string win = itemGold > 0 ? $"Win: +{Run.GoldFor(e)} gold (+{itemGold} from items)" : $"Win: +{Run.GoldFor(e)} gold";
            ui.TextIn(win, new Rectangle(r.X, r.Y + 250, r.Width, 22), 15, Palette.Accent);
            if (hover) ui.Tooltip(enemy.Name, [$"{hp} HP", "You learn its abilities by fighting it."], color);
        }
        ui.Text("click to choose", new Vector2(r.Center.X, r.Bottom - 28), 13, hover ? Palette.Text : Palette.TextDim, align: Align.Center);
        ui.Border(r, hover ? color : Palette.Border, hover ? 3 : 1);
    }
}
