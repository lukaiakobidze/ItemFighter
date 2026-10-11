using ItemFighter.UI;
using Microsoft.Xna.Framework;

namespace ItemFighter.Screens.Run;

/// <summary>Buy items for gold, reroll the stock (the cost rises each time), sell what you don't want.</summary>
public sealed class ShopScreen(Game1 game, RunSession session) : RunScreen(game, session)
{
    public static readonly Rectangle RerollButton = new(908, 500, 340, 50);
    public static readonly Rectangle LeaveButton = new(908, 566, 340, 56);

    protected override string Title => "Shop";

    public static Rectangle StockRect(int i) => new(24 + i * 170, 64, 160, 262);

    public static Rectangle BuyRect(int i) => new(24 + i * 170, 332, 160, 38);

    protected override void DrawContent(Ui ui)
    {
        var stock = Run.ShopStock ?? [];
        if (stock.Count == 0) ui.Text("Sold out. Reroll for new stock.", new Vector2(24, 80), 20, Palette.TextDim);
        for (int i = 0; i < stock.Count; i++)
        {
            if (!Db.TryGet(stock[i], out var def)) continue;
            var owned = Run.AllOwned.FirstOrDefault(o => o.ItemId == def.Id);
            int level = owned is null ? 1 : owned.Level + 1;
            DrawCard(ui, def, StockRect(i), level);
            if (owned is not null)
            {
                ui.Rect(new Rectangle(StockRect(i).X + 6, StockRect(i).Y + 6, StockRect(i).Width - 12, 20), new Color(0, 0, 0, 150));
                ui.Text($"merges: level {level}", new Vector2(StockRect(i).Center.X, StockRect(i).Y + 8), 13, Palette.Accent, bold: true, align: Align.Center);
            }
            int price = Run.Price(def.Id);
            bool affordable = Run.Gold >= price;
            bool room = owned is not null || Run.HasRoom;
            string label = !room ? "No room" : $"Buy: {price} gold";
            if (ui.Button(BuyRect(i), label, 16, affordable && room) && Run.Buy(i))
            {
                Session.Commit();
                return;
            }
        }
    }

    protected override int DrawSideExtras(Ui ui, int y)
    {
        if (ui.Button(RerollButton, $"Reroll: {Run.RerollCost} gold", 20, Run.Gold >= Run.RerollCost) && Run.Reroll()) Session.Commit();
        if (ui.Button(LeaveButton, "Leave shop", 22))
        {
            Run.LeaveShop();
            Session.Commit();
            Session.ShowCurrent(Game);
        }
        return y;
    }
}
