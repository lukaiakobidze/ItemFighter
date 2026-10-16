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
    public const int Top = 520;
    public static readonly Rectangle SellButton = new(980, 836, 220, 38);

    public ItemRef? Held { get; set; }

    public static Rectangle HotbarSlot(int i) => new(Layout.Margin + i * 112, Top + 32, 96, 96);

    public static Rectangle BackpackSlot(int i) => new(Layout.Margin + i * 66, Top + 196, 56, 56);

    /// <summary>Draws and handles clicks. Returns true when the run changed (so it can be saved).</summary>
    public bool Draw(Ui ui, RunState run, ContentDatabase db)
    {
        bool changed = false;
        if (Held is not null && run.At(Held) is null) Held = null;

        ui.Text("Hotbar: these items fight", new Vector2(Layout.Margin, Top), 16, Palette.Text, bold: true);
        for (int i = 0; i < run.Hotbar.Length; i++)
            changed |= Slot(ui, run, db, new ItemRef(true, i), HotbarSlot(i), big: true);

        int capacity = run.Config.BackpackSize;
        ui.Text($"Backpack {run.Backpack.Count}/{capacity}", new Vector2(Layout.Margin, Top + 166), 16, Palette.Text, bold: true);
        int cells = Math.Max(capacity, run.Backpack.Count);
        for (int i = 0; i < cells && i < 12; i++)
        {
            // Empty backpack cells all mean "the end of the backpack".
            var where = new ItemRef(false, i < run.Backpack.Count ? i : run.Backpack.Count);
            changed |= Slot(ui, run, db, where, BackpackSlot(i), big: false, empty: i >= run.Backpack.Count);
        }
        if (run.Backpack.Count > 12)
            ui.Text($"+{run.Backpack.Count - 12} more", new Vector2(Layout.Margin, Top + 258), 14, Palette.TextDim);

        if (Held is not null && run.At(Held) is OwnedItem held)
        {
            ui.Text("Click a slot to put it there. Esc to cancel.", new Vector2(Layout.Margin, 844), 14, Palette.Accent);
            if (ui.Button(SellButton, $"Sell for {run.SellValue(held)} gold", 16) && run.Sell(Held))
            {
                Held = null;
                changed = true;
            }
        }
        else
        {
            ui.Text("Click an item to move it.  Right-click an item to sell it.", new Vector2(Layout.Margin, 844), 14, Palette.TextDim);
        }
        return changed;
    }

    private bool Slot(Ui ui, RunState run, ContentDatabase db, ItemRef where, Rectangle r, bool big, bool empty = false)
    {
        var item = empty ? null : run.At(where);
        bool held = Held is not null && Held == where && !empty;
        bool hover = ui.Hover(r);
        SourceDefinition? def = item is not null && db.TryGet(item.ItemId, out var d) ? d : null;
        if (item is not null && def is not null)
        {
            Color border = held ? Palette.Accent : hover && Held is not null ? Palette.Valid : hover ? Palette.Text : Palette.For(def.Rarity);
            ui.Square("Items", def.Id, def.Name, r, border, held || hover ? 3 : 2);
            if (big)
                for (int p = 0; p < ContentLoader.LevelCount; p++)
                    ui.Rect(new Rectangle(r.X + p * 12, r.Bottom + 6, 9, 9), p < item.Level ? Palette.Accent : new Color(60, 64, 80));
            else
                ui.Text($"{item.Level}", new Vector2(r.Right - 5, r.Bottom - 18), 13, Palette.Accent, bold: true, align: Align.Right);
            if (hover)
            {
                var (title, lines) = Tooltips.For(def, item.Level, db);
                lines = [.. lines, $"Sells for {run.SellValue(item)} gold (right-click)."];
                ui.Tooltip(title, lines, Palette.For(def.Rarity));
            }
        }
        else
        {
            ui.Rect(r, hover ? new Color(38, 42, 56) : new Color(22, 24, 33));
            ui.Border(r, hover && Held is not null ? Palette.Valid : new Color(52, 56, 72), hover && Held is not null ? 2 : 1);
        }

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
