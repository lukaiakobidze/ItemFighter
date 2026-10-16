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
    public static readonly Rectangle EnemyPanel = new(40, 32, 1160, 340);
    public static readonly Rectangle PlayerPanel = new(40, 396, 1160, 472);
    public static readonly Rectangle LogPanel = new(1240, 88, 320, 780);
    private static readonly Rectangle EnemyHpBar = new(244, 90, 560, 24);
    private static readonly Rectangle PlayerHpBar = new(300, 418, 560, 24);
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

    public static Rectangle CardRect(int slot) => new(64 + slot * 128, 540, 112, 112);

    public static Rectangle AbilityCard(int index) => new(244 + index * 112, 236, 96, 96);

    public static Rectangle PassiveIcon(Vector2 rowStart, int index) => new((int)rowStart.X + index * 38, (int)rowStart.Y, 32, 32);

    public static readonly Vector2 EnemyPassiveRow = new(244, 128);
    private static readonly Vector2 PlayerPassiveRow = new(140, 740);

    public override void Update(double seconds)
    {
        if (_wasActive && !Game.IsActive && Battle.Result == BattleResult.Ongoing) Paused = true; // alt-tab pauses the fight
        _wasActive = Game.IsActive;
        if (Paused && Battle.Result == BattleResult.Ongoing) seconds = 0; // freeze floating numbers and flashes too
        _time += seconds;
        _clock.Advance(seconds);
        var events = _cursor.TakeNew();
        foreach (var e in events.OfType<SourceFired>()) _firedAt[e.SourceId] = _time;
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
        var portrait = new Rectangle(64, 56, 156, 156);
        ui.Square("Enemies", def.Id, def.Name, portrait, Palette.Damage * 0.8f);
        if (Battle.Enemy.IsFrozenAt(Battle.Tick)) ui.Rect(portrait, new Color(120, 200, 255) * 0.35f);
        if (ui.Hover(portrait)) ui.Tooltip(def.Name, [$"{def.MaxHp} HP", "Its abilities and passives are listed beside it."], Palette.Damage);

        ui.Text(def.Name, new Vector2(244, 52), 24, Palette.Text, bold: true);
        DrawHp(ui, Battle.Enemy, EnemyHpBar);
        var passives = Battle.Enemy.Sources.Where(s => s.Def.IsPassive).ToList();
        for (int i = 0; i < passives.Count && i < 16; i++) DrawPassive(ui, passives[i], PassiveIcon(EnemyPassiveRow, i), enemy: true);
        DrawStatuses(ui, Battle.Enemy, new Vector2(244, 176));

        var abilities = Battle.Enemy.Sources.Where(s => s.Def.Kind == SourceKind.EnemyAbility && s.HasCooldown).ToList();
        for (int i = 0; i < abilities.Count && i < 5; i++) DrawAbility(ui, abilities[i], AbilityCard(i));
        DrawTimer(ui, new Vector2(64, 232));
    }

    /// <summary>An enemy ability as a square like the player's items: picture, cooldown shade and seconds left.</summary>
    private void DrawAbility(Ui ui, SourceInstance a, Rectangle r)
    {
        bool flash = _firedAt.TryGetValue(a.Id, out double t) && _time - t < 0.3;
        bool hover = ui.Hover(r);
        ui.Square("EnemyAbilities", a.Def.Id, a.Def.Name, r, flash ? Palette.Damage : hover ? Palette.Text : new Color(150, 70, 64), flash || hover ? 3 : 2);
        DrawCooldownShade(ui, a, r, flash ? new Color(255, 120, 110) * 0.45f : null);
        DrawItemTimers(ui, a, new Vector2(r.X + 4, r.Y + 4));
        if (hover)
            ui.Tooltip(a.Def.Name, Tooltips.For(a.Def, a.Level, Game.Data.Database).Lines, Palette.Damage);
    }

    /// <summary>
    /// The shade over a square that shrinks as it charges, and the seconds left in the middle (Luka 2026-10-16: no bars,
    /// the shade and the timer are enough).
    /// </summary>
    private void DrawCooldownShade(Ui ui, SourceInstance s, Rectangle r, Color? flash, string? readyText = null)
    {
        var inner = new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4);
        if (s.FreezeTicks > 0) ui.Rect(inner, new Color(120, 200, 255) * 0.40f);
        if (s.HasCooldown && !s.Ready)
        {
            int covered = (int)(inner.Height * (1 - CooldownFraction(s)));
            if (covered > 0) ui.Rect(new Rectangle(inner.X, inner.Y, inner.Width, covered), new Color(0, 0, 0, 140));
        }
        if (flash is Color f) ui.Rect(inner, f);
        string? label = !s.HasCooldown ? null : s.Ready ? readyText : Battle.CooldownSpeed(s) == 0 ? "frozen" : $"{SecondsLeft(s):0.0}";
        if (label is null) return;
        var color = s.Ready ? Palette.Ready : label == "frozen" ? Palette.Freeze : Color.White;
        int size = r.Width >= 100 ? 22 : 19;
        ui.TextIn(label, new Rectangle(inner.X + 2, inner.Y + 2, inner.Width, inner.Height), size, Color.Black * 0.8f, bold: true);
        ui.TextIn(label, inner, size, color, bold: true);
    }

    /// <summary>A passive as a small square icon; details on mouse-over.</summary>
    private void DrawPassive(Ui ui, SourceInstance p, Rectangle r, bool enemy)
    {
        bool flash = _firedAt.TryGetValue(p.Id, out double t) && _time - t < 0.3;
        bool hover = ui.Hover(r);
        var border = hover || flash ? Palette.Accent : enemy ? Palette.Damage : Palette.Accent * 0.8f;
        ui.FramedIcon("Passives", p.Def.Id, Initials(p.Def.Name), r, border, hover || flash ? 2 : 1);
        if (flash) ui.Rect(r, Color.White * 0.25f);
        if (!hover) return;
        var (title, lines) = Tooltips.For(p.Def, p.Level, Game.Data.Database, ownedByPlayer: !enemy, ownedByEnemy: enemy);
        ui.Tooltip(title, ["Passive", .. lines], enemy ? Palette.Damage : Palette.For(p.Def.Rarity));
    }

    private static string Initials(string name) => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));

    private static void DrawPausedBanner(Ui ui)
    {
        var r = new Rectangle(452, 360, 336, 48);
        ui.Panel(r, new Color(20, 22, 32) * 0.92f, Palette.Accent);
        ui.TextIn("PAUSED  ·  Space to resume", r, 18, Palette.Accent, bold: true);
    }

    private void DrawTimer(Ui ui, Vector2 pos)
    {
        int ticksLeft = Math.Max(0, Battle.TimeLimitTicks - Battle.Tick);
        int secondsLeft = (ticksLeft + 9) / 10;
        var color = secondsLeft <= 15 ? Palette.Damage : Palette.TextDim;
        ui.IconText("clock", "Time left ", $"{secondsLeft / 60}:{secondsLeft % 60:00}", pos, 17, color, bold: secondsLeft <= 15);
        ui.Text("Out of time = defeat", pos + new Vector2(0, 22), 14, Palette.TextDim);
    }

    // ---- player ------------------------------------------------------------------------------------------------

    private void DrawPlayer(Ui ui)
    {
        ui.Panel(PlayerPanel);
        string title = _battleSetup.Player.CharacterId is string c && Game.Data.Database.TryGet(c, out var ch) ? $"You ({ch.Name})" : "You";
        ui.Text(title, new Vector2(64, 416), 20, Palette.Text, bold: true);
        DrawHp(ui, Battle.Player, PlayerHpBar);
        DrawStatuses(ui, Battle.Player, new Vector2(300, 456));
        if (Battle.Player.IsFrozenAt(Battle.Tick)) ui.Text("You are frozen!", new Vector2(64, 456), 16, Palette.Freeze, bold: true);

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

        Color border = selected ? Palette.Accent
            : validTarget ? Palette.Valid
            : canClick ? Palette.Ready
            : hover ? Palette.Text : Palette.For(def.Rarity);
        int thickness = selected ? 4 : validTarget ? (int)(3 + Math.Sin(_time * 8) * 1.5) : canClick ? (int)(2 + (Math.Sin(_time * 6) + 1) * 1.2) : hover ? 3 : 2;
        ui.Square("Items", def.Id, def.Name, r, border, Math.Max(2, thickness));
        DrawCooldownShade(ui, item, r, fired ? Color.White * 0.3f : null, def.Activation == Activation.Active ? (canClick ? "click" : null) : null);
        DrawPips(ui, item.Level, new Vector2(r.X + 6, r.Bottom - 12));
        DrawItemTimers(ui, item, new Vector2(r.X + 4, r.Y + 4));

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
            ui.Rect(new Rectangle((int)pos.X + i * 10, (int)pos.Y, 7, 7), i < level ? Palette.Accent : new Color(40, 42, 54, 200));
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
        if (hint is not null) ui.TextIn(hint, new Rectangle(PlayerPanel.X, 680, PlayerPanel.Width, 24), 16, Palette.Accent, bold: true);
    }

    private void DrawExtras(Ui ui)
    {
        var passives = Battle.Player.Sources
            .Where(s => s.Def.Kind is SourceKind.Passive or SourceKind.Character || s.Def.IsPassiveAbility)
            .Where(s => s.Def.Abilities.Count > 0 || s.Def.Modifiers.Count > 0 || s.Script is not null).ToList();
        if (passives.Count == 0) return;
        ui.Text("Passives", new Vector2(64, PlayerPassiveRow.Y + 7), 15, Palette.TextDim);
        for (int i = 0; i < passives.Count && i < 18; i++) DrawPassive(ui, passives[i], PassiveIcon(PlayerPassiveRow, i), enemy: false);
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
        int icon = bar.Height - 4;
        ui.Icon("heart", new Rectangle(bar.X + 4, bar.Y + 2, icon, icon));
        bool shieldIcon = c.Shield > 0 && ui.Icon("shield", new Rectangle(bar.Right - icon - 52, bar.Y + 2, icon, icon));
        if (shieldIcon) ui.Text($"{c.Shield}", new Vector2(bar.Right - 50, bar.Y + 4), 17, Color.White, bold: true);
        string text = $"{Math.Max(0, c.Hp)} / {c.MaxHp}" + (c.Shield > 0 && !shieldIcon ? $"   +{c.Shield} shield" : "");
        ui.TextIn(text, bar, 17, Color.White, bold: true);
    }

    private void DrawStatuses(Ui ui, Combatant c, Vector2 pos)
    {
        float x = pos.X;
        // Each status: its icon on the shared dark frame with a border in the status colour, then the numbers.
        // Without an icon PNG the status name is written out instead.
        void Chip(string text, Color color, string tip, string body)
        {
            string id = tip == "Frozen" ? "freeze" : tip.ToLowerInvariant();
            bool hasIcon = ui.Assets.Texture("Statuses", id) is not null;
            string numbers = hasIcon && text.StartsWith(tip, StringComparison.OrdinalIgnoreCase) ? text[tip.Length..].Trim() : text;
            var icon = new Rectangle((int)x, (int)pos.Y - 1, 30, 30);
            var size = ui.Measure(numbers, 15, true);
            var r = new Rectangle(icon.X, icon.Y, icon.Width + (int)size.X + 10, icon.Height);
            if (hasIcon) ui.FramedIcon("Statuses", id, tip, icon, color);
            else r = icon = new Rectangle((int)x, (int)pos.Y, 0, 28);
            ui.Text(numbers, new Vector2(icon.Right + 6, pos.Y + 5), 15, color, bold: true);
            if (!hasIcon) r.Width = (int)size.X + 12;
            if (ui.Hover(r)) ui.Tooltip(tip, [body], color);
            x += r.Width + 10;
        }
        var s = c.Statuses;
        var rules = Battle.Rules;
        if (s.Burn > 0) Chip($"Burn {s.Burn}", Palette.Burn, "Burn", $"Every {rules.BurnTickSeconds:0.##}s deals its stacks as damage (x{rules.BurnShieldMultiplier} against shield), then loses {rules.BurnLossPerTick} {(rules.BurnLossPerTick == 1 ? "stack" : "stacks")}.");
        if (s.Poison > 0) Chip($"Poison {s.Poison}/{rules.PoisonThreshold}", Palette.Poison, "Poison", $"At {rules.PoisonThreshold} stacks it bursts for {rules.PoisonBurstPerStack} damage per stack (shield blocks it), then resets.");
        if (s.Chill > 0)
            Chip($"Chill {s.Chill}/{rules.ChillFreezeAtStacks}  {Math.Max(0, s.ChillExpiresAtTick - Battle.Tick) / 10.0:0.0}s", Palette.Chill, "Chill",
                $"Each stack makes every cooldown {rules.ChillCooldownPercentPerStack}% longer. Stacks last {rules.ChillSeconds:0.#}s, refreshed by each new stack. At {rules.ChillFreezeAtStacks} stacks: frozen for {rules.FreezeSeconds:0.#}s.");
        if (s.Pain > 0) Chip($"Pain {s.Pain}", Palette.Pain, "Pain", $"Each time one of {(c.Side == Side.Player ? "your items" : "its abilities")} fires, {(c.Side == Side.Player ? "you take" : "it takes")} {s.Pain} damage. Lasts all fight.");
        if (s.Nail > 0) Chip($"Nail {s.Nail}", Palette.Nail, "Nail", $"Does nothing alone. Uproot pulls every Nail out for {rules.NailDamage} damage each (more with bonuses).");
        if (s.Power > 0) Chip($"Power {s.Power}", Palette.Power, "Power", $"Every hit {(c.Side == Side.Player ? "you deal" : "it deals")} does {s.Power * rules.PowerDamagePerStack} more damage. Lasts all fight.");
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
        // Damage to a side floats in the open space right of its HP bar.
        if (a.Side is Side side) return side == Side.Enemy ? new Vector2(EnemyHpBar.Right + 170, EnemyHpBar.Y + 90) : new Vector2(PlayerHpBar.Right + 170, PlayerHpBar.Y + 90);
        var source = a.SourceId is int id ? Battle.FindSource(id) : null;
        if (source?.Slot is int slot) return new Vector2(CardRect(slot).Center.X, CardRect(slot).Y + 40);
        if (source is not null)
        {
            var abilities = Battle.Enemy.Sources.Where(s => s.Def.Kind == SourceKind.EnemyAbility && s.HasCooldown).ToList();
            int index = abilities.IndexOf(source);
            if (index >= 0) return new Vector2(AbilityCard(index).Center.X, AbilityCard(index).Y + 30);
            var passives = Battle.Enemy.Sources.Where(s => s.Def.IsPassive).ToList();
            index = passives.IndexOf(source);
            if (index >= 0) return PassiveIcon(EnemyPassiveRow, index).Center.ToVector2();
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
            if (ui.Button(SpeedButton(i), $"{Speeds[i]}×", 16, selected: Speed == Speeds[i])) Speed = Speeds[i];
        if (ui.Button(PauseButton, Paused ? "Resume" : "Pause", 14, Battle.Result == BattleResult.Ongoing, selected: Paused)) Paused = !Paused;
        if (ui.Button(GiveUpButton, _confirmGiveUp ? "Sure?" : "Give up", 14, Battle.Result == BattleResult.Ongoing, selected: _confirmGiveUp))
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

    public static readonly Rectangle GiveUpButton = new(1484, 32, 76, 40);

    public static Rectangle SpeedButton(int index) => new(1240 + index * 56, 32, 50, 40);
    public static readonly Rectangle PauseButton = new(1408, 32, 70, 40);

    public static readonly Rectangle AgainButton = new(550, 500, 160, 48);
    public static readonly Rectangle ReplayButton = new(720, 500, 160, 48);
    public static readonly Rectangle BackButton = new(890, 500, 160, 48);

    private void DrawResult(Ui ui)
    {
        ui.Rect(Layout.Full, new Color(0, 0, 0, 160));
        var panel = new Rectangle(530, 330, 540, 240);
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
