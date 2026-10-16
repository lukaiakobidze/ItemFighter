using ItemFighter.Core.Content;
using ItemFighter.Core.Run;
using ItemFighter.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ItemFighter.Screens.Run;

/// <summary>
/// Shared layout for run screens: the screen's own content on top, your hotbar and backpack below, and a side panel
/// with the run's progress, gold and passives.
/// </summary>
public abstract class RunScreen(Game1 game, RunSession session) : Screen(game)
{
    public static readonly Rectangle SidePanel = new(892, 16, 372, 688);
    public static readonly Rectangle MenuButton = new(908, 640, 340, 48);

    public RunSession Session => session;
    protected RunState Run => session.Run;
    protected ContentDatabase Db => Game.Data.Database;
    protected InventoryPanel Inventory { get; } = new();

    protected abstract string Title { get; }

    public override void Draw(Ui ui)
    {
        ui.Text(Title, new Vector2(24, 16), 30, Palette.Accent, bold: true);
        DrawContent(ui);
        if (Inventory.Draw(ui, Run, Db)) session.Commit();
        DrawSide(ui);
        if (ui.Input.KeyPressed(Keys.Escape))
        {
            if (Inventory.Held is not null) Inventory.Held = null;
            else ToMenu();
        }
    }

    protected abstract void DrawContent(Ui ui);

    /// <summary>Extra controls for the side panel, drawn under the run info. Returns the next free y.</summary>
    protected virtual int DrawSideExtras(Ui ui, int y) => y;

    private void DrawSide(Ui ui)
    {
        ui.Panel(SidePanel);
        int x = SidePanel.X + 16;
        int y = SidePanel.Y + 14;
        string character = Run.CharacterId is string c && Db.TryGet(c, out var ch) ? ch.Name : "No character";
        ui.Text(character, new Vector2(x, y), 22, Palette.Text, bold: true);
        y += 34;
        string where = Run.IsBossNext ? "boss day" : $"day {Run.Encounter + 1} of {Run.Config.EncountersPerMission}";
        ui.Text($"Mission {Run.Mission} of {Run.Config.Missions}  ·  {where}", new Vector2(x, y), 16, Palette.TextDim);
        y += 26;
        var boss = Run.Boss(Run.Mission);
        if (Db.TryGet(boss.EnemyId!, out var bossDef))
        {
            var r = new Rectangle(x, y, 340, 22);
            ui.Text($"Mission boss: {bossDef.Name}", new Vector2(x, y), 16, Palette.Damage);
            if (ui.Hover(r)) ui.Tooltip(bossDef.Name, [$"{bossDef.MaxHp} HP", "Waits at the end of this mission."], Palette.Damage);
            y += 30;
        }
        ui.IconText("gold", "Gold: ", $"{Run.Gold}", new Vector2(x, y), 24, Palette.Accent, bold: true);
        y += 40;
        // Passives come from passive shops (not built yet), so the list only shows once there are some.
        if (Run.Passives.Count > 0)
        {
            ui.Text("Passives", new Vector2(x, y), 18, Palette.Text, bold: true);
            y += 26;
        }
        foreach (string id in Run.Passives)
        {
            if (!Db.TryGet(id, out var def)) continue;
            var r = new Rectangle(x, y, 340, 20);
            ui.Text("• " + def.Name, new Vector2(x, y), 15, Palette.Text);
            if (ui.Hover(r))
            {
                var (title, lines) = Tooltips.For(def, 1, Db, ownedByPlayer: true);
                ui.Tooltip(title, lines, Palette.For(def.Rarity));
            }
            y += 22;
        }
        DrawSideExtras(ui, y + 16);
        if (ui.Button(MenuButton, "Save and quit to menu", 18)) ToMenu();
    }

    private void ToMenu()
    {
        session.Commit();
        Game.Show(new MainMenuScreen(Game));
    }

    /// <summary>A big card for an item or passive: art, name, rarity, tags and its tooltip.</summary>
    protected void DrawCard(Ui ui, SourceDefinition def, Rectangle r, int level = 1, bool highlight = false)
    {
        bool hover = ui.Hover(r);
        ui.Rect(r, hover ? Palette.PanelLight : Palette.Panel);
        string kind = def.Kind == SourceKind.Item ? "Items" : def.Kind == SourceKind.Enemy ? "Enemies" : "Passives";
        ui.Art(kind, def.Id, def.Name, new Rectangle(r.X + 6, r.Y + 6, r.Width - 12, r.Height - 70));
        ui.TextIn(def.Name, new Rectangle(r.X, r.Bottom - 62, r.Width, 24), 17, Palette.Text, bold: true);
        string sub = def.Kind == SourceKind.Item ? $"{def.Rarity} item" + (def.Tags.Count > 0 ? " · " + string.Join(", ", def.Tags) : "")
            : def.Kind == SourceKind.Enemy ? $"{def.MaxHp} HP" : $"{def.Rarity} passive";
        ui.TextIn(sub, new Rectangle(r.X, r.Bottom - 38, r.Width, 20), 13, Palette.TextDim);
        ui.Rect(new Rectangle(r.X, r.Bottom - 4, r.Width, 4), def.Kind == SourceKind.Enemy ? Palette.Border : Palette.For(def.Rarity));
        ui.Border(r, highlight ? Palette.Accent : hover ? Palette.Text : Palette.Border, highlight ? 3 : 1);
        if (hover && def.Kind != SourceKind.Enemy)
        {
            var (title, lines) = Tooltips.For(def, level, Db, ownedByPlayer: true);
            ui.Tooltip(title, lines, Palette.For(def.Rarity));
        }
    }
}
