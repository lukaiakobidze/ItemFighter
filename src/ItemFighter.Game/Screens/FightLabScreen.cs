using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using ItemFighter.Platform;
using ItemFighter.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ItemFighter.Screens;

/// <summary>Build any board against any enemy, then fight it. The test bench for items and balance.</summary>
public sealed class FightLabScreen : Screen
{
    public static string SavePath => Path.Combine(Paths.Saves, "lab.json");

    private const int GridColumns = 11, GridRows = 5;
    public static readonly Rectangle FightButton = new(1220, 800, 340, 64);
    public static readonly Rectangle BackButton = new(1410, 32, 150, 40);

    private readonly LabSetup _setup;
    private int _selectedSlot;
    private int _gridScroll;

    public FightLabScreen(Game1 game) : base(game)
    {
        _setup = LabSetup.Load(SavePath, game.Data.Database);
        _selectedSlot = Array.FindIndex(_setup.Items, i => i is null) is int free and >= 0 ? free : 0;
    }

    public LabSetup Setup => _setup;

    public static Rectangle SlotRect(int slot) => new(30 + slot * 142, 100, 130, 150);

    public static Rectangle GridCell(int index) => new(30 + index % GridColumns * 106, 330 + index / GridColumns * 100, 100, 94);

    private ContentDatabase Db => Game.Data.Database;

    private List<SourceDefinition> PickableItems() =>
        Db.OfKind(SourceKind.Item).OrderBy(d => d.Rarity).ThenBy(d => d.Name, StringComparer.Ordinal).ToList();

    public override void Draw(Ui ui)
    {
        ui.Text("Fight Lab", new Vector2(30, 18), 34, Palette.Accent, bold: true);
        ui.Text("Build any board, pick an enemy, and fight. Everything here is test content.", new Vector2(250, 32), 16, Palette.TextDim);
        if (ui.Button(BackButton, "Back")) Game.Show(new MainMenuScreen(Game));

        DrawBoard(ui);
        DrawGrid(ui);
        DrawSidebar(ui);

        ui.Text("Click a slot, then an item.  Right-click a slot to empty it.  Mouse wheel over a slot changes its level.  F5 reloads content and art.",
            new Vector2(30, 866), 14, Palette.TextDim);
        if (ui.Input.KeyPressed(Keys.Escape)) Game.Show(new MainMenuScreen(Game));
    }

    private void DrawBoard(Ui ui)
    {
        ui.Text("Your board", new Vector2(30, 72), 20, Palette.Text, bold: true);
        for (int slot = 0; slot < Board.SlotCount; slot++)
        {
            var r = SlotRect(slot);
            string? id = _setup.Items[slot];
            bool selected = slot == _selectedSlot;
            ui.Rect(r, Palette.Panel);
            if (id is not null && Db.TryGet(id, out var def))
            {
                ui.Art("Items", def.Id, def.Name, new Rectangle(r.X + 4, r.Y + 4, r.Width - 8, 100));
                ui.TextIn(def.Name, new Rectangle(r.X, r.Y + 104, r.Width, 22), 14, Palette.Text, bold: true);
                ui.Rect(new Rectangle(r.X, r.Bottom - 4, r.Width, 4), Palette.For(def.Rarity));
                if (ui.Hover(r))
                {
                    var (title, lines) = Tooltips.For(def, _setup.Levels[slot], Db);
                    ui.Tooltip(title, lines, Palette.For(def.Rarity));
                    if (ui.Input.Wheel != 0) SetLevel(slot, _setup.Levels[slot] + ui.Input.Wheel);
                }
                if (ui.RightClicked(r)) _setup.Items[slot] = null;
            }
            else
            {
                ui.TextIn("empty", r, 16, Palette.TextDim);
            }
            ui.Border(r, selected ? Palette.Accent : Palette.Border, selected ? 3 : 1);
            if (ui.Clicked(r)) _selectedSlot = slot;

            var minus = new Rectangle(r.X, r.Bottom + 6, 36, 28);
            var plus = new Rectangle(r.Right - 36, r.Bottom + 6, 36, 28);
            if (id is not null)
            {
                if (ui.Button(minus, "−", 18, _setup.Levels[slot] > 1)) SetLevel(slot, _setup.Levels[slot] - 1);
                if (ui.Button(plus, "+", 18, _setup.Levels[slot] < ContentLoader.LevelCount)) SetLevel(slot, _setup.Levels[slot] + 1);
                ui.TextIn($"Lv {_setup.Levels[slot]}", new Rectangle(minus.Right, minus.Y, plus.X - minus.Right, 28), 16, Palette.Text, bold: true);
            }
        }
    }

    private void SetLevel(int slot, int level) => _setup.Levels[slot] = Math.Clamp(level, 1, ContentLoader.LevelCount);

    private void DrawGrid(Ui ui)
    {
        var items = PickableItems();
        int rows = (items.Count + GridColumns - 1) / GridColumns;
        int maxScroll = Math.Max(0, rows - GridRows);
        ui.Text($"Items ({items.Count})  →  slot {_selectedSlot + 1}", new Vector2(30, 300), 20, Palette.Text, bold: true);
        var area = new Rectangle(24, 326, GridColumns * 106 + 4, GridRows * 100 + 4);
        if (ui.Hover(area) && ui.Input.Wheel != 0) _gridScroll = Math.Clamp(_gridScroll - ui.Input.Wheel, 0, maxScroll);
        _gridScroll = Math.Clamp(_gridScroll, 0, maxScroll);
        if (maxScroll > 0) ui.Text($"scroll for more ({_gridScroll + 1}/{maxScroll + 1})", new Vector2(1000, 304), 14, Palette.TextDim);

        int first = _gridScroll * GridColumns;
        for (int i = first; i < Math.Min(items.Count, first + GridRows * GridColumns); i++)
        {
            var def = items[i];
            var r = GridCell(i - first);
            bool hover = ui.Hover(r);
            ui.Rect(r, hover ? Palette.PanelLight : Palette.Panel);
            ui.Art("Items", def.Id, def.Name, new Rectangle(r.X + 3, r.Y + 3, r.Width - 6, 62));
            ui.TextIn(Shorten(ui, def.Name, r.Width - 6, 13), new Rectangle(r.X, r.Y + 66, r.Width, 22), 13, Palette.Text);
            ui.Rect(new Rectangle(r.X, r.Bottom - 3, r.Width, 3), Palette.For(def.Rarity));
            ui.Border(r, hover ? Palette.Text : Palette.Border, 1);
            if (hover)
            {
                var (title, lines) = Tooltips.For(def, 1, Db);
                ui.Tooltip(title, lines, Palette.For(def.Rarity));
            }
            if (ui.Clicked(r)) Place(def.Id);
        }
    }

    /// <summary>Puts an item in the selected slot and moves the selection to the next empty slot.</summary>
    public void Place(string itemId)
    {
        _setup.Items[_selectedSlot] = itemId;
        _setup.Levels[_selectedSlot] = 1;
        for (int k = 1; k <= Board.SlotCount; k++)
        {
            int next = (_selectedSlot + k) % Board.SlotCount;
            if (_setup.Items[next] is null)
            {
                _selectedSlot = next;
                return;
            }
        }
    }

    private static string Shorten(Ui ui, string text, int width, int size)
    {
        if (ui.Measure(text, size).X <= width) return text;
        while (text.Length > 1 && ui.Measure(text + "…", size).X > width) text = text[..^1];
        return text + "…";
    }

    private void DrawSidebar(Ui ui)
    {
        const int x = 1220, w = 340, cw = 166; // two columns of short buttons, details on mouse-over
        Rectangle Cell(int i, float top) => new(x + i % 2 * (cw + 8), (int)top + i / 2 * 32, cw, 28);
        float y = 72;
        ui.Text("Enemy", new Vector2(x, y), 18, Palette.Text, bold: true);
        y += 26;
        var enemies = Db.OfKind(SourceKind.Enemy).ToList();
        for (int i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            var r = Cell(i, y);
            if (ui.Button(r, Shorten(ui, $"{enemy.Name} (d{enemy.Difficulty})", cw - 10, 14), 14, selected: _setup.EnemyId == enemy.Id)) _setup.EnemyId = enemy.Id;
            if (ui.Hover(r))
            {
                var (title, lines) = Tooltips.For(enemy, 1, Db);
                ui.Tooltip(title, [$"Difficulty {enemy.Difficulty}", .. lines], Palette.Damage);
            }
        }
        y += (enemies.Count + 1) / 2 * 32 + 12;

        ui.Text("Character", new Vector2(x, y), 18, Palette.Text, bold: true);
        y += 26;
        var characters = Db.OfKind(SourceKind.Character).ToList();
        if (ui.Button(Cell(0, y), "None (100 HP)", 14, selected: _setup.CharacterId is null)) _setup.CharacterId = null;
        for (int i = 0; i < characters.Count; i++)
        {
            var c = characters[i];
            var r = Cell(i + 1, y);
            if (ui.Button(r, Shorten(ui, c.Name, cw - 10, 14), 14, selected: _setup.CharacterId == c.Id)) _setup.CharacterId = c.Id;
            if (ui.Hover(r))
            {
                var (title, lines) = Tooltips.For(c, 1, Db);
                ui.Tooltip(title, lines);
            }
        }
        y += (characters.Count + 2) / 2 * 32 + 12;

        ui.Text("Passives", new Vector2(x, y), 18, Palette.Text, bold: true);
        y += 26;
        var passives = Db.OfKind(SourceKind.Passive).OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
        if (passives.Count == 0) ui.Text("none yet", new Vector2(x, y), 16, Palette.TextDim);
        for (int i = 0; i < passives.Count; i++)
        {
            var p = passives[i];
            var r = Cell(i, y);
            bool on = _setup.Passives.Contains(p.Id);
            if (ui.Button(r, Shorten(ui, (on ? "✓ " : "") + p.Name, cw - 10, 14), 14, selected: on))
            {
                if (on) _setup.Passives.Remove(p.Id);
                else _setup.Passives.Add(p.Id);
            }
            if (ui.Hover(r))
            {
                var (title, lines) = Tooltips.For(p, 1, Db);
                ui.Tooltip(title, lines, Palette.For(p.Rarity));
            }
        }

        int seedY = FightButton.Y - 44;
        ui.Text($"Seed {_setup.Seed}", new Vector2(x, seedY + 6), 18, Palette.TextDim);
        if (ui.Button(new Rectangle(x + w - 80, seedY, 36, 30), "−", 18, _setup.Seed > 1)) _setup.Seed--;
        if (ui.Button(new Rectangle(x + w - 40, seedY, 36, 30), "+", 18)) _setup.Seed++;

        bool canFight = _setup.EnemyId.Length > 0 && _setup.Items.Any(i => i is not null);
        if (ui.Button(FightButton, "FIGHT", 30, canFight)) StartFight();
    }

    /// <summary>The setup is saved whenever the lab is left (fight, back, Esc, F5), so edits are never lost.</summary>
    public override void OnLeave() => _setup.Save(SavePath);

    public void StartFight()
    {
        Game.Show(new FightScreen(Game, _setup));
    }
}
