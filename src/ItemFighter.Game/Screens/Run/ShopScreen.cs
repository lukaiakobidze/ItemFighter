using ItemFighter.Core.Content;
using ItemFighter.UI;
using Microsoft.Xna.Framework;

namespace ItemFighter.Screens.Run;

/// <summary>Buy items (or passives, in a passive shop) for gold, reroll the stock (the cost rises each time), sell what you don't want.</summary>
public sealed class ShopScreen(Game1 game, RunSession session) : RunScreen(game, session)
{
    public static readonly Rectangle RerollButton = SideButton(1, 44);
    public static readonly Rectangle LeaveButton = SideButton(0, 50);

    protected override string Title => Run.Config.Shops.FirstOrDefault(s => s.Id == Run.Current?.ShopId)?.Name ?? "Shop";

    /// <summary>A stock square. Click it to buy (Luka 2026-10-16: just squares, price in the corner, details on hover).</summary>
    public static Rectangle StockRect(int i) => new(Layout.Margin + i * 148, 110, 120, 120);

    protected override void DrawContent(Ui ui)
    {
        var stock = Run.ShopStock ?? [];
        if (stock.Count == 0) ui.Text("Sold out. Reroll for new stock.", new Vector2(Layout.Margin, 120), 18, Palette.TextDim);
        for (int i = 0; i < stock.Count; i++)
        {
            if (!Db.TryGet(stock[i], out var def)) continue;
            var r = StockRect(i);
            bool passive = def.Kind == SourceKind.Passive; // passives don't level up or take a slot
            var owned = passive ? null : Run.AllOwned.FirstOrDefault(o => o.ItemId == def.Id);
            int level = owned is null ? 1 : owned.Level + 1;
            int price = Run.Price(def.Id);
            bool affordable = Run.Gold >= price;
            bool room = passive || owned is not null || Run.HasRoom;
            DrawSquare(ui, def, r, level);
            if (!affordable || !room) ui.Rect(r, new Color(0, 0, 0, 120));
            if (owned is not null)
            {
                ui.Rect(new Rectangle(r.X + 3, r.Y + 3, 44, 20), new Color(8, 9, 14, 220));
                ui.Text($"Lv {level}", new Vector2(r.X + 8, r.Y + 4), 13, Palette.Accent, bold: true);
            }
            ui.PriceBadge(r, price, affordable);
            if (!room && ui.Hover(r)) ui.Text("No room: sell something first", new Vector2(r.X, r.Bottom + 8), 13, Palette.Damage);
            if (affordable && room && ui.Clicked(r) && Run.Buy(i))
            {
                Session.Commit();
                return;
            }
        }
    }

    protected override int DrawSideExtras(Ui ui, int y)
    {
        if (ui.Button(RerollButton, $"Reroll: {Run.RerollCost} gold", 18, Run.Gold >= Run.RerollCost) && Run.Reroll()) Session.Commit();
        if (ui.Button(LeaveButton, "Leave shop", 20))
        {
            Run.LeaveShop();
            Session.Commit();
            Session.ShowCurrent(Game);
        }
        return y;
    }
}
