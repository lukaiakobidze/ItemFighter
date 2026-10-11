using ItemFighter.Core.Content;
using ItemFighter.Core.Run;
using Microsoft.Xna.Framework;

namespace ItemFighter.UI;

/// <summary>
/// Your hotbar and backpack during a run. Click an item to pick it up, click any slot to put it there (swapping),
/// right-click an item (or use the Sell button while holding it) to sell it.
/// </summary>
public sealed class InventoryPanel
{
    public const int Top = 386;
    public static readonly Rectangle SellButton = new(686, 672, 180, 38);

    public ItemRef? Held { get; set; }

    public static Rectangle HotbarSlot(int i) => new(24 + i * 142, Top + 30, 132, 150);

    public static Rectangle BackpackSlot(int i) => new(24 + i * 70, Top + 216, 64, 64);

    /// <summary>Draws and handles clicks. Returns true when the run changed (so it can be saved).</summary>
    public bool Draw(Ui ui, RunState run, ContentDatabase db)
    {
        bool changed = false;
        if (Held is not null && run.At(Held) is null) Held = null;

        ui.Text("Hotbar: these items fight", new Vector2(24, Top), 18, Palette.Text, bold: true);
        for (int i = 0; i < run.Hotbar.Length; i++)
            changed |= Slot(ui, run, db, new ItemRef(true, i), HotbarSlot(i), big: true);

        int capacity = run.Config.BackpackSize;
        ui.Text($"Backpack {run.Backpack.Count}/{capacity}", new Vector2(24, Top + 190), 18, Palette.Text, bold: true);
        int cells = Math.Max(capacity, run.Backpack.Count);
        for (int i = 0; i < cells && i < 12; i++)
        {
            // Empty backpack cells all mean "the end of the backpack".
            var where = new ItemRef(false, i < run.Backpack.Count ? i : run.Backpack.Count);
            changed |= Slot(ui, run, db, where, BackpackSlot(i), big: false, empty: i >= run.Backpack.Count);
        }
        if (run.Backpack.Count > 12)
            ui.Text($"+{run.Backpack.Count - 12} more", new Vector2(24, Top + 286), 14, Palette.TextDim);

        if (Held is not null && run.At(Held) is OwnedItem held)
        {
            ui.Text("Click a slot to put it there. Esc to cancel.", new Vector2(24, 682), 15, Palette.Accent);
            if (ui.Button(SellButton, $"Sell for {run.SellValue(held)} gold", 16) && run.Sell(Held))
            {
                Held = null;
                changed = true;
            }
        }
        else
        {
            ui.Text("Click an item to move it.  Right-click an item to sell it.", new Vector2(24, 682), 15, Palette.TextDim);
        }
        return changed;
    }

    private bool Slot(Ui ui, RunState run, ContentDatabase db, ItemRef where, Rectangle r, bool big, bool empty = false)
    {
        var item = empty ? null : run.At(where);
        bool held = Held is not null && Held == where && !empty;
        bool hover = ui.Hover(r);
        ui.Rect(r, hover ? Palette.PanelLight : new Color(30, 33, 45));

        SourceDefinition? def = item is not null && db.TryGet(item.ItemId, out var d) ? d : null;
        if (item is not null && def is not null)
        {
            var art = big ? new Rectangle(r.X + 4, r.Y + 4, r.Width - 8, 100) : new Rectangle(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6);
            ui.Art("Items", def.Id, def.Name, art);
            if (big)
            {
                ui.TextIn(def.Name, new Rectangle(r.X, r.Y + 106, r.Width, 20), 14, Palette.Text, bold: true);
                for (int p = 0; p < ContentLoader.LevelCount; p++)
                    ui.Rect(new Rectangle(r.X + 8 + p * 14, r.Y + 132, 10, 10), p < item.Level ? Palette.Accent : new Color(60, 64, 80));
            }
            else
            {
                ui.Text($"{item.Level}", new Vector2(r.Right - 5, r.Bottom - 19), 14, Palette.Accent, bold: true, align: Align.Right);
            }
            ui.Rect(new Rectangle(r.X, r.Bottom - 3, r.Width, 3), Palette.For(def.Rarity));
            if (hover)
            {
                var (title, lines) = Tooltips.For(def, item.Level, db);
                lines = [.. lines, $"Sells for {run.SellValue(item)} gold (right-click)."];
                ui.Tooltip(title, lines, Palette.For(def.Rarity));
            }
        }
        else if (big) ui.TextIn("empty", r, 15, Palette.TextDim);

        ui.Border(r, held ? Palette.Accent : hover && Held is not null ? Palette.Valid : Palette.Border, held ? 3 : 1);

        if (ui.RightClicked(r) && item is not null && run.Sell(where))
        {
            Held = null;
            return true;
        }
        if (!ui.Clicked(r)) return false;
        if (Held is null)
        {
            if (item is not null) Held = where;
            return false;
        }
        var from = Held;
        Held = null;
        return from != where && run.Move(from, where);
    }
}
