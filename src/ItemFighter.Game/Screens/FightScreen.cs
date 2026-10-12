using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using ItemFighter.FightView;
using ItemFighter.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ItemFighter.Screens;

/// <summary>Plays one fight in real time. The battle runs in Core; this screen only shows it and sends clicks.</summary>
public sealed class FightScreen : Screen
{
    public static readonly Rectangle EnemyPanel = new(16, 12, 878, 312);
    public static readonly Rectangle PlayerPanel = new(16, 336, 878, 372);
    public static readonly Rectangle LogPanel = new(906, 60, 358, 648);
    private static readonly Rectangle EnemyHpBar = new(206, 68, 672, 28);
    private static readonly Rectangle PlayerHpBar = new(246, 350, 632, 28);
    private static readonly int[] Speeds = [1, 2, 4];

    private readonly LabSetup? _lab;
    private readonly BattleSetup _battleSetup;
    private readonly Action<BattleResult>? _onFinished;
    private bool _confirmGiveUp;
    private readonly FightClock _clock;
    private readonly EventCursor _cursor;
    private readonly TargetingState _targeting = new();
    private readonly FloatingTexts _floats = new();
    private readonly CombatLog _log = new();
    private readonly Dictionary<int, double> _firedAt = [];
    private readonly HashSet<int> _revealed = [];
    private double _time;
    private double _endedAt = -1;
    private bool _wasActive;

    /// <summary>A Fight Lab fight: the result offers a new seed, the same seed, or back to the lab.</summary>
    public FightScreen(Game1 game, LabSetup setup) : this(game, setup.ToBattleSetup(), null, setup) { }

    /// <summary>A run fight: the result offers only Continue, which reports the result.</summary>
    public FightScreen(Game1 game, BattleSetup setup, Action<BattleResult> onFinished) : this(game, setup, onFinished, null) { }

    private FightScreen(Game1 game, BattleSetup setup, Action<BattleResult>? onFinished, LabSetup? lab) : base(game)
    {
        _lab = lab;
        _battleSetup = setup;
        _onFinished = onFinished;
        Battle = new Battle(setup, game.Data.Database, game.Data.Scripts);
        _clock = new FightClock(Battle);
        _cursor = new EventCursor(Battle);
        _wasActive = game.IsActive;
    }

    public Battle Battle { get; }
    public TargetingState Targeting => _targeting;

    public bool Paused
    {
        get => _clock.Paused;
        set => _clock.Paused = value;
    }

    public int Speed
    {
        get => _clock.Speed;
        set => _clock.Speed = value;
    }

    public static Rectangle CardRect(int slot) => new(32 + slot * 142, 420, 132, 196);

    private static Rectangle AbilityRow(int index) => new(206, 146 + index * 34, 672, 30);

    public override void Update(double seconds)
    {
        if (_wasActive && !Game.IsActive && Battle.Result == BattleResult.Ongoing) Paused = true; // alt-tab pauses the fight
        _wasActive = Game.IsActive;
        if (Paused && Battle.Result == BattleResult.Ongoing) seconds = 0; // freeze floating numbers and flashes too
        _time += seconds;
        _clock.Advance(seconds);
        var events = _cursor.TakeNew();
        foreach (var e in events)
        {
            if (e is SourceFired f)
            {
                _firedAt[f.SourceId] = _time;
                _revealed.Add(f.SourceId);
            }
            // Always-on passives (like Thick Skin) never fire; they show themselves the first time their side is hit.
            if (e is DamageDealt { Target: Side.Enemy })
                foreach (var s in Battle.Enemy.Sources.Where(s => s.Def.Abilities.Count == 0 && s.Def.Modifiers.Count > 0)) _revealed.Add(s.Id);
            int? by = e switch { DamageDealt d => d.SourceId, ShieldGained g => g.SourceId, StatusApplied a => a.SourceId, _ => null };
            if (by is int id) _revealed.Add(id);
        }
        _floats.Consume(events, Battle);
        _log.Consume(events, Battle);
        _floats.Update(seconds);
        _targeting.Update(Battle);
        if (Battle.Result != BattleResult.Ongoing && _endedAt < 0) _endedAt = _time;
    }

    public override void Draw(Ui ui)
    {
        bool showingResult = _endedAt >= 0 && _time - _endedAt > 0.8;
        HandleKeys(ui);
        ui.InputBlocked = showingResult; // the result overlay is modal
        DrawEnemy(ui);
        DrawPlayer(ui);
        DrawSidebar(ui);
        DrawFloats(ui);
        if (Paused && Battle.Result == BattleResult.Ongoing) DrawPausedBanner(ui);
        ui.InputBlocked = false;
        if (showingResult) DrawResult(ui);
        else if (ui.Input.LeftClicked && _targeting.Selecting is not null)
        {
            _targeting.Cancel(); // a click that hit nothing cancels the selection
            ui.Input.ConsumeClicks();
        }
    }

    private void HandleKeys(Ui ui)
    {
        if (ui.Input.KeyPressed(Keys.Escape) || ui.Input.RightClicked)
        {
            if (_targeting.Selecting is not null) _targeting.Cancel();
            else if (ui.Input.KeyPressed(Keys.Escape) && Battle.Result != BattleResult.Ongoing) Leave(Battle.Result);
            ui.Input.ConsumeClicks();
        }
        if ((ui.Input.KeyPressed(Keys.Space) || ui.Input.KeyPressed(Keys.P)) && Battle.Result == BattleResult.Ongoing) Paused = !Paused;
        if (ui.Input.KeyPressed(Keys.D1)) Speed = 1;
        if (ui.Input.KeyPressed(Keys.D2)) Speed = 2;
        if (ui.Input.KeyPressed(Keys.D3)) Speed = 4;
    }

    // ---- enemy -------------------------------------------------------------------------------------------------

    private void DrawEnemy(Ui ui)
    {
        ui.Panel(EnemyPanel);
        var def = Battle.EnemyDefinition;
        var portrait = new Rectangle(32, 28, 158, 158);
        ui.Art("Enemies", def.Id, def.Name, portrait, Battle.Enemy.IsFrozenAt(Battle.Tick) ? new Color(120, 200, 255) * 0.35f : null);
        ui.Border(portrait, Palette.Border, 1);
        if (ui.Hover(portrait)) ui.Tooltip(def.Name, [$"{def.MaxHp} HP", "Its abilities show up as it uses them."], Palette.Damage);

        ui.Text(def.Name, new Vector2(206, 24), 28, Palette.Text, bold: true);
        DrawHp(ui, Battle.Enemy, EnemyHpBar);
        DrawStatuses(ui, Battle.Enemy, new Vector2(206, 104));

        var abilities = Battle.Enemy.Sources.Where(s => s.Def.Kind == SourceKind.EnemyAbility).ToList();
        for (int i = 0; i < abilities.Count && i < 5; i++) DrawAbility(ui, abilities[i], AbilityRow(i));
        DrawTimer(ui, new Vector2(32, 200));
    }

    private void DrawAbility(Ui ui, SourceInstance a, Rectangle row)
    {
        bool known = _revealed.Contains(a.Id);
        bool flash = _firedAt.TryGetValue(a.Id, out double t) && _time - t < 0.3;
        ui.Rect(row, flash ? new Color(110, 60, 60) : Palette.PanelLight);
        ui.Text(known ? a.Def.Name : "???", new Vector2(row.X + 10, row.Y + 5), 16, known ? Palette.Text : Palette.TextDim, bold: true);
        var bar = new Rectangle(row.X + 250, row.Y + 9, 300, 12);
        if (a.HasCooldown)
        {
            int speed = Battle.CooldownSpeed(a);
            ui.Bar(bar, (float)CooldownFraction(a), speed == 0 ? Palette.Freeze : new Color(200, 90, 80), new Color(40, 30, 36));
            ui.Text(speed == 0 ? "frozen" : $"{SecondsLeft(a):0.0}s", new Vector2(bar.X - 12, row.Y + 5), 15, speed == 0 ? Palette.Freeze : Palette.TextDim, align: Align.Right);
            DrawItemTimers(ui, a, new Vector2(bar.Right + 12, row.Y + 6));
        }
        else
        {
            ui.Text("passive", new Vector2(bar.X, row.Y + 5), 15, Palette.TextDim);
        }
        if (ui.Hover(row))
            ui.Tooltip(known ? a.Def.Name : "Unknown ability", known ? Tooltips.For(a.Def, a.Level, Game.Data.Database).Lines : ["You'll learn what this does when it happens."], Palette.Damage);
    }

    private static void DrawPausedBanner(Ui ui)
    {
        var r = new Rectangle(287, 304, 336, 48);
        ui.Panel(r, new Color(20, 22, 32) * 0.92f, Palette.Accent);
        ui.TextIn("PAUSED  ·  Space to resume", r, 18, Palette.Accent, bold: true);
    }

    private void DrawTimer(Ui ui, Vector2 pos)
    {
        int ticksLeft = Math.Max(0, Battle.TimeLimitTicks - Battle.Tick);
        int secondsLeft = (ticksLeft + 9) / 10;
        var color = secondsLeft <= 15 ? Palette.Damage : Palette.TextDim;
        ui.Text($"Time left {secondsLeft / 60}:{secondsLeft % 60:00}", pos, 17, color, bold: secondsLeft <= 15);
        ui.Text("Out of time = defeat", pos + new Vector2(0, 22), 14, Palette.TextDim);
    }

    // ---- player ------------------------------------------------------------------------------------------------

    private void DrawPlayer(Ui ui)
    {
        ui.Panel(PlayerPanel);
        string title = _battleSetup.Player.CharacterId is string c && Game.Data.Database.TryGet(c, out var ch) ? $"You ({ch.Name})" : "You";
        ui.Text(title, new Vector2(32, 350), 24, Palette.Text, bold: true);
        DrawHp(ui, Battle.Player, PlayerHpBar);
        DrawStatuses(ui, Battle.Player, new Vector2(246, 384));
        if (Battle.Player.IsFrozenAt(Battle.Tick)) ui.Text("You are frozen!", new Vector2(32, 388), 16, Palette.Freeze, bold: true);

        foreach (var item in Battle.Player.Items) DrawCard(ui, item, CardRect(item.Slot!.Value));
        DrawHint(ui);
        DrawExtras(ui);
    }

    private void DrawCard(Ui ui, SourceInstance item, Rectangle r)
    {
        var def = item.Def;
        bool canClick = _targeting.CanActivate(item, Battle);
        bool selected = _targeting.Selecting == item;
        bool validTarget = _targeting.IsValidTarget(item);
        bool fired = _firedAt.TryGetValue(item.Id, out double t) && _time - t < 0.25;
        bool hover = ui.Hover(r);

        ui.Rect(r, hover ? Palette.PanelLight : new Color(40, 44, 60));
        var art = new Rectangle(r.X + 4, r.Y + 4, r.Width - 8, 112);
        Color? tint = item.FreezeTicks > 0 ? new Color(120, 200, 255) * 0.45f : null;
        ui.Art("Items", def.Id, def.Name, art, tint);

        if (item.HasCooldown && !item.Ready)
        {
            int covered = (int)(art.Height * (1 - CooldownFraction(item)));
            if (covered > 0) ui.Rect(new Rectangle(art.X, art.Y, art.Width, covered), new Color(0, 0, 0, 130));
        }
        if (fired) ui.Rect(art, Color.White * 0.3f);

        ui.TextIn(def.Name, new Rectangle(r.X, r.Y + 118, r.Width, 22), 14, Palette.Text, bold: true);
        string status = !item.HasCooldown ? (item.Def.Abilities.Count == 0 ? "after the fight" : "reacts") : item.Ready ? "READY" : Battle.CooldownSpeed(item) == 0 ? "frozen" : $"{SecondsLeft(item):0.0}s";
        var statusColor = item.Ready ? Palette.Ready : status == "frozen" ? Palette.Freeze : Palette.TextDim;
        ui.TextIn(status, new Rectangle(r.X, r.Y + 140, r.Width, 20), 15, statusColor, bold: item.Ready);
        if (item.HasCooldown)
            ui.Bar(new Rectangle(r.X + 8, r.Y + 164, r.Width - 16, 8), item.Ready ? 1 : (float)CooldownFraction(item),
                item.Ready ? Palette.Ready : def.Activation == Activation.Active ? new Color(220, 180, 70) : new Color(90, 160, 220), new Color(24, 26, 36));
        DrawPips(ui, item.Level, new Vector2(r.X + 8, r.Y + 178));
        if (def.Activation == Activation.Active)
            ui.Text("click", new Vector2(r.Right - 8, r.Y + 175), 13, canClick ? Palette.Ready : Palette.TextDim, bold: canClick, align: Align.Right);
        DrawItemTimers(ui, item, new Vector2(r.X + 6, r.Y + 6));
        ui.Rect(new Rectangle(r.X, r.Bottom - 3, r.Width, 3), Palette.For(def.Rarity));

        if (selected) ui.Border(r, Palette.Accent, 4);
        else if (validTarget) ui.Border(r, Palette.Valid, (int)(3 + Math.Sin(_time * 8) * 1.5));
        else if (canClick) ui.Border(r, Palette.Ready, (int)(2 + (Math.Sin(_time * 6) + 1) * 1.2));
        else ui.Border(r, Palette.Border, 1);

        if (hover)
        {
            var (title, lines) = Tooltips.For(def, item.Level, Game.Data.Database);
            var bonuses = new[] { "Damage", "Shield", "Heal", "Burn", "Poison", "Chill" }.Where(s => item.BonusFor(s) != 0).Select(s => $"+{item.BonusFor(s)} {s}").ToList();
            if (bonuses.Count > 0) lines.Add("This fight: " + string.Join(", ", bonuses));
            ui.Tooltip(title, lines, Palette.For(def.Rarity));
        }
        if (ui.Clicked(r) && _targeting.Click(item, Battle) is { } command) Battle.Enqueue(command);
    }

    private static void DrawPips(Ui ui, int level, Vector2 pos)
    {
        for (int i = 0; i < ContentLoader.LevelCount; i++)
            ui.Rect(new Rectangle((int)pos.X + i * 14, (int)pos.Y, 10, 10), i < level ? Palette.Accent : new Color(70, 74, 90));
    }

    /// <summary>Small freeze/haste/slow labels on an item or ability.</summary>
    private static void DrawItemTimers(Ui ui, SourceInstance s, Vector2 pos)
    {
        float x = pos.X;
        void Chip(string text, Color c, int ticks)
        {
            string label = $"{text} {ticks / 10.0:0.0}";
            var size = ui.Measure(label, 12, true);
            ui.Rect(new Rectangle((int)x, (int)pos.Y, (int)size.X + 8, 17), new Color(10, 12, 18, 220));
            ui.Text(label, new Vector2(x + 4, pos.Y + 1), 12, c, bold: true);
            x += size.X + 12;
        }
        if (s.FreezeTicks > 0) Chip("FROZEN", Palette.Freeze, s.FreezeTicks);
        if (s.HasteTicks > 0) Chip("HASTE", Palette.Haste, s.HasteTicks);
    }

    private void DrawHint(Ui ui)
    {
        string? hint = null;
        if (_targeting.Selecting is { } s)
        {
            const string tag = "OwnItemTag:";
            string what = s.Def.TargetFilter.StartsWith(tag, StringComparison.Ordinal) ? Words.Article(s.Def.TargetFilter[tag.Length..]) : "one of your items";
            hint = $"{s.Def.Name}: pick {what}.  Right-click or Esc to cancel.";
        }
        else if (Battle.Player.Items.Any(i => _targeting.CanActivate(i, Battle)))
            hint = "Glowing items are ready: click to use them.";
        if (hint is not null) ui.TextIn(hint, new Rectangle(PlayerPanel.X, 622, PlayerPanel.Width, 24), 17, Palette.Accent, bold: true);
    }

    private void DrawExtras(Ui ui)
    {
        var passives = Battle.Player.Sources.Where(s => s.Def.Kind == SourceKind.Passive || s.Def.IsPassiveAbility).ToList();
        float x = 32;
        if (passives.Count > 0) ui.Text("Passives:", new Vector2(x, 670), 15, Palette.TextDim);
        x += 80;
        foreach (var p in passives)
        {
            var size = ui.Measure(p.Def.Name, 15, true);
            var r = new Rectangle((int)x, 666, (int)size.X + 16, 26);
            ui.Rect(r, Palette.PanelLight);
            ui.Text(p.Def.Name, new Vector2(x + 8, 670), 15, Palette.For(p.Def.Rarity), bold: true);
            if (ui.Hover(r))
            {
                var (title, lines) = Tooltips.For(p.Def, 1, Game.Data.Database, ownedByPlayer: true);
                ui.Tooltip(title, lines, Palette.For(p.Def.Rarity));
            }
            x += r.Width + 8;
        }
    }

    // ---- shared ------------------------------------------------------------------------------------------------

    private static void DrawHp(Ui ui, Combatant c, Rectangle bar)
    {
        ui.Bar(bar, c.Hp / (float)c.MaxHp, Palette.Hp, Palette.HpBack);
        if (c.Shield > 0)
        {
            int w = (int)(bar.Width * Math.Min(1f, c.Shield / (float)c.MaxHp));
            ui.Rect(new Rectangle(bar.X, bar.Y, w, 8), Palette.Shield);
        }
        ui.Border(bar, Palette.Border, 1);
        string text = $"{Math.Max(0, c.Hp)} / {c.MaxHp}" + (c.Shield > 0 ? $"   +{c.Shield} shield" : "");
        ui.TextIn(text, bar, 17, Color.White, bold: true);
    }

    private void DrawStatuses(Ui ui, Combatant c, Vector2 pos)
    {
        float x = pos.X;
        void Chip(string text, Color color, string tip, string body)
        {
            var size = ui.Measure(text, 15, true);
            var r = new Rectangle((int)x, (int)pos.Y, (int)size.X + 18, 28);
            ui.Rect(r, color * 0.18f);
            ui.Border(r, color, 1);
            ui.Text(text, new Vector2(x + 9, pos.Y + 5), 15, color, bold: true);
            if (ui.Hover(r)) ui.Tooltip(tip, [body], color);
            x += r.Width + 8;
        }
        var s = c.Statuses;
        var rules = Battle.Rules;
        if (s.Burn > 0) Chip($"Burn {s.Burn}", Palette.Burn, "Burn", $"Every {rules.BurnTickSeconds:0.##}s deals its stacks as damage (x{rules.BurnShieldMultiplier} against shield), then loses {rules.BurnLossPerTick} {(rules.BurnLossPerTick == 1 ? "stack" : "stacks")}.");
        if (s.Poison > 0) Chip($"Poison {s.Poison}/{rules.PoisonThreshold}", Palette.Poison, "Poison", $"At {rules.PoisonThreshold} stacks it bursts for {rules.PoisonBurstPerStack} damage per stack, ignoring shield, then resets.");
        if (s.Chill > 0)
            Chip($"Chill {s.Chill}/{rules.ChillFreezeAtStacks}  {Math.Max(0, s.ChillExpiresAtTick - Battle.Tick) / 10.0:0.0}s", Palette.Chill, "Chill",
                $"Each stack makes every cooldown {rules.ChillCooldownPercentPerStack}% longer. Stacks last {rules.ChillSeconds:0.#}s, refreshed by each new stack. At {rules.ChillFreezeAtStacks} stacks: frozen for {rules.FreezeSeconds:0.#}s.");
        if (c.IsFrozenAt(Battle.Tick)) Chip($"FROZEN {(s.FrozenUntilTick - Battle.Tick + 1) / 10.0:0.0}s", Palette.Freeze, "Frozen", "Nothing charges or fires until it thaws.");
    }

    private double CooldownFraction(SourceInstance s)
    {
        if (!s.HasCooldown || s.FullProgress == 0) return 0;
        double progress = s.Progress + _clock.Fraction * Battle.CooldownSpeed(s);
        return Math.Clamp(progress / s.FullProgress, 0, 1);
    }

    private double SecondsLeft(SourceInstance s)
    {
        int speed = Battle.CooldownSpeed(s);
        if (speed <= 0) return 0;
        return Math.Max(0, (s.FullProgress - s.Progress) / (double)speed / 10.0);
    }

    private Vector2 AnchorPosition(FloatAnchor a)
    {
        if (a.Side is Side side) return side == Side.Enemy ? new Vector2(EnemyHpBar.Center.X, EnemyHpBar.Y - 16) : new Vector2(PlayerHpBar.Center.X, PlayerHpBar.Y - 16);
        var source = a.SourceId is int id ? Battle.FindSource(id) : null;
        if (source?.Slot is int slot) return new Vector2(CardRect(slot).Center.X, CardRect(slot).Y + 40);
        if (source is not null)
        {
            var abilities = Battle.Enemy.Sources.Where(s => s.Def.Kind == SourceKind.EnemyAbility).ToList();
            int index = abilities.IndexOf(source);
            if (index >= 0) return new Vector2(AbilityRow(index).X + 380, AbilityRow(index).Y);
        }
        return new Vector2(EnemyHpBar.Center.X, EnemyHpBar.Y);
    }

    private void DrawFloats(Ui ui)
    {
        foreach (var f in _floats.Active)
        {
            float age = (float)(f.Age / FloatingTexts.Lifetime);
            float dx = (f.Stack % 3) switch { 0 => 0, 1 => -110, _ => 110 };
            var pos = AnchorPosition(f.Anchor) + new Vector2(dx, -age * 30 - f.Stack / 3 % 2 * 22);
            var color = f.Color * (age < 0.7f ? 1f : (1 - age) / 0.3f);
            ui.Text(f.Text, pos + new Vector2(2, 2), 22, Color.Black * (color.A / 255f), bold: true, align: Align.Center);
            ui.Text(f.Text, pos, 22, color, bold: true, align: Align.Center);
        }
    }

    // ---- sidebar & result --------------------------------------------------------------------------------------

    private void DrawSidebar(Ui ui)
    {
        for (int i = 0; i < Speeds.Length; i++)
            if (ui.Button(SpeedButton(i), $"{Speeds[i]}×", 18, selected: Speed == Speeds[i])) Speed = Speeds[i];
        if (ui.Button(PauseButton, Paused ? "Resume" : "Pause", 16, Battle.Result == BattleResult.Ongoing, selected: Paused)) Paused = !Paused;
        if (ui.Button(GiveUpButton, _confirmGiveUp ? "Sure?" : "Give up", 16, Battle.Result == BattleResult.Ongoing, selected: _confirmGiveUp))
        {
            // In a run giving up loses the run, so it takes a second click.
            if (_lab is null && !_confirmGiveUp) _confirmGiveUp = true;
            else Leave(BattleResult.EnemyWon);
        }
        else if (ui.Input.LeftClicked) _confirmGiveUp = false;

        ui.Panel(LogPanel);
        ui.Text("Combat log", new Vector2(LogPanel.X + 10, LogPanel.Y + 8), 16, Palette.TextDim, bold: true);
        float bottom = LogPanel.Bottom - 8, width = LogPanel.Width - 20;
        float y = bottom;
        for (int i = _log.Entries.Count - 1; i >= 0; i--)
        {
            var entry = _log.Entries[i];
            int h = ui.WrappedHeight(entry.Text, width, 14);
            if (y - h < LogPanel.Y + 34) break;
            y -= h + 2;
            ui.Wrapped(entry.Text, new Vector2(LogPanel.X + 10, y), width, 14, entry.Color);
        }
    }

    public static readonly Rectangle GiveUpButton = new(1172, 14, 92, 38);

    public static Rectangle SpeedButton(int index) => new(906 + index * 60, 14, 54, 38);
    public static readonly Rectangle PauseButton = new(1086, 14, 80, 38);

    public static readonly Rectangle AgainButton = new(390, 400, 160, 48);
    public static readonly Rectangle ReplayButton = new(560, 400, 160, 48);
    public static readonly Rectangle BackButton = new(730, 400, 160, 48);

    private void DrawResult(Ui ui)
    {
        ui.Rect(new Rectangle(0, 0, 1280, 720), new Color(0, 0, 0, 160));
        var panel = new Rectangle(370, 230, 540, 240);
        ui.Panel(panel, new Color(28, 30, 42), Palette.Accent);
        (string title, Color color) = Battle.Result switch
        {
            BattleResult.PlayerWon => ("Victory!", Palette.Heal),
            BattleResult.EnemyWon when Battle.TimedOut => ("Out of time", Palette.Damage),
            BattleResult.EnemyWon => ("Defeat", Palette.Damage),
            _ => ("Draw", Palette.TextDim),
        };
        ui.TextIn(title, new Rectangle(panel.X, panel.Y + 20, panel.Width, 70), 52, color, bold: true);
        string seed = _lab is null ? "" : $"  ·  seed {_lab.Seed}";
        ui.TextIn($"{Battle.Tick / 10.0:0.0}s  ·  you {Math.Max(0, Battle.Player.Hp)}/{Battle.Player.MaxHp} HP{seed}",
            new Rectangle(panel.X, panel.Y + 96, panel.Width, 30), 17, Palette.TextDim);
        if (_lab is null)
        {
            if (ui.Button(ReplayButton, Battle.Result == BattleResult.PlayerWon ? "Continue" : "End run", 18)) Leave(Battle.Result);
        }
        else
        {
            if (ui.Button(AgainButton, "New seed", 18))
            {
                _lab.Seed++;
                Game.Show(new FightScreen(Game, _lab));
            }
            if (ui.Button(ReplayButton, "Same seed", 18)) Game.Show(new FightScreen(Game, _lab));
            if (ui.Button(BackButton, "Back to lab", 18)) Leave(Battle.Result);
        }
        ui.Input.ConsumeClicks();
    }

    /// <summary>Leaves the fight: back to the lab, or hands the result to the run.</summary>
    private void Leave(BattleResult result)
    {
        if (_onFinished is not null)
        {
            _onFinished(result);
            return;
        }
        _lab!.Save(FightLabScreen.SavePath);
        Game.Show(new FightLabScreen(Game));
    }
}
