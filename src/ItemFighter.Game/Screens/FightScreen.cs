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
    // Luka 2026-10-16: both sides use the same layout at the same size, so everything sits in the same place in each
    // panel. The fight timer sits between them on the left.
    public static readonly Rectangle EnemyPanel = new(40, 32, 1160, 380);
    public static readonly Rectangle PlayerPanel = new(40, 488, 1160, 380);
    public static readonly Rectangle LogPanel = new(1240, 88, 320, 780);
    private static readonly Rectangle EnemyHpBar = HpBarIn(EnemyPanel);
    private static readonly Rectangle PlayerHpBar = HpBarIn(PlayerPanel);

    // Positions inside a side's panel, the same for both sides.
    private static Rectangle PortraitIn(Rectangle panel) => new(panel.X + 24, panel.Y + 24, 156, 156);
    // Luka 2026-10-16: a thick HP bar running to the panel's right edge, statuses under it, passives in a grid under the
    // portrait.
    private static Rectangle HpBarIn(Rectangle panel) => new(panel.X + 204, panel.Y + 54, panel.Width - 228, 40);
    private static Vector2 PassiveGridIn(Rectangle panel) => new(panel.X + 24, panel.Y + 196);
    private static Vector2 StatusRowIn(Rectangle panel) => new(panel.X + 204, panel.Y + 108);
    // Luka 2026-10-19: a vertical Heat bar right of the item squares, with its number underneath; 2026-10-20: taller.
    private static Rectangle HeatBarIn(Rectangle panel) => new(panel.X + 990, panel.Y + 112, 44, 222);
    private static Rectangle SquareIn(Rectangle panel, int index) => new(panel.X + 204 + index * 128, panel.Y + 190, 112, 112);
    private static readonly Color FrostTint = new Color(120, 200, 255) * 0.35f;
    private const int PassiveSize = 34, PassiveGap = 7, PassiveColumns = 4, PassiveMax = 16;
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
    private readonly BarAnimator _playerBars = new(), _enemyBars = new();
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

    public static Rectangle CardRect(int slot) => SquareIn(PlayerPanel, slot);

    public static Rectangle AbilityCard(int index) => SquareIn(EnemyPanel, index);

    /// <summary>A passive's square in the grid under a portrait: left to right, then the next row down.</summary>
    public static Rectangle PassiveIcon(Vector2 gridStart, int index) => new(
        (int)gridStart.X + index % PassiveColumns * (PassiveSize + PassiveGap),
        (int)gridStart.Y + index / PassiveColumns * (PassiveSize + PassiveGap), PassiveSize, PassiveSize);

    public static readonly Vector2 EnemyPassiveGrid = PassiveGridIn(EnemyPanel);
    private static readonly Vector2 PlayerPassiveGrid = PassiveGridIn(PlayerPanel);

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
        _playerBars.Update(Battle.Player, Battle.MaxHeat(Side.Player), seconds);
        _enemyBars.Update(Battle.Enemy, Battle.MaxHeat(Side.Enemy), seconds);
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
        ui.Panel(EnemyPanel, skin: "panel_enemy");
        var def = Battle.EnemyDefinition;
        var portrait = PortraitIn(EnemyPanel);
        ui.Square("Enemies", def.Id, def.Name, portrait, Palette.Damage * 0.8f,
            underFrame: () => { if (Battle.Enemy.IsFrozenAt(Battle.Tick)) ui.Rect(portrait, FrostTint); });
        if (ui.Hover(portrait)) ui.Tooltip(def.Name, [$"{def.MaxHp} HP", "Its abilities and passives are listed beside it."], Palette.Damage);

        ui.Text(def.Name, new Vector2(EnemyHpBar.X, EnemyPanel.Y + 18), 22, Palette.Text, bold: true);
        DrawHp(ui, Battle.Enemy, EnemyHpBar, _enemyBars);
        DrawHeat(ui, Battle.Enemy, HeatBarIn(EnemyPanel), _enemyBars);
        var passives = Battle.Enemy.Sources.Where(s => s.Def.IsPassive).ToList();
        for (int i = 0; i < passives.Count && i < PassiveMax; i++) DrawPassive(ui, passives[i], PassiveIcon(EnemyPassiveGrid, i), enemy: true);
        DrawStatuses(ui, Battle.Enemy, StatusRowIn(EnemyPanel));

        var abilities = Battle.Enemy.Sources.Where(s => s.Def.Kind == SourceKind.EnemyAbility && s.HasCooldown).ToList();
        for (int i = 0; i < abilities.Count && i < 5; i++) DrawAbility(ui, abilities[i], AbilityCard(i));
        DrawTimer(ui, new Vector2(EnemyPanel.X + 24, EnemyPanel.Bottom + 26));
    }

    /// <summary>An enemy ability as a square like the player's items: picture, cooldown shade and seconds left.</summary>
    private void DrawAbility(Ui ui, SourceInstance a, Rectangle r)
    {
        bool flash = _firedAt.TryGetValue(a.Id, out double t) && _time - t < 0.3;
        bool hover = ui.Hover(r);
        ui.Square("EnemyAbilities", a.Def.Id, a.Def.Name, r, flash ? Palette.Damage : hover ? Palette.Text : new Color(150, 70, 64), flash || hover ? 3 : 2,
            underFrame: () => DrawCooldownShade(ui, a, r, flash ? new Color(255, 120, 110) * 0.45f : null));
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
        bool stalled = s.FreezeTicks == 0 && Battle.Get(s.Owner) is var owner && owner.IsStalledAt(Battle.Tick) && !owner.IsFrozenAt(Battle.Tick);
        string? label = !s.HasCooldown ? null : s.Ready ? readyText : Battle.CooldownSpeed(s) == 0 ? (stalled ? "stalled" : "frozen") : $"{SecondsLeft(s):0.0}";
        if (label is null) return;
        var color = s.Ready ? Palette.Ready : label == "frozen" ? Palette.Freeze : label == "stalled" ? Palette.Heat : Color.White;
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
        ui.FramedIcon("Passives", p.Def.Id, Initials(p.Def.Name), r, border, hover || flash ? 3 : 2,
            underFrame: () => { if (flash) ui.Rect(r, Color.White * 0.25f); });
        if (!hover) return;
        var (title, lines) = Tooltips.For(p.Def, p.Level, Game.Data.Database, ownedByPlayer: !enemy, ownedByEnemy: enemy);
        ui.Tooltip(title, ["Passive", .. lines], enemy ? Palette.Damage : Palette.For(p.Def.Rarity));
    }

    private static string Initials(string name) => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));

    private static void DrawPausedBanner(Ui ui)
    {
        var r = new Rectangle(452, 426, 336, 48);
        ui.Panel(r, new Color(20, 22, 32) * 0.92f, Palette.Accent);
        ui.TextIn("PAUSED  ·  Space to resume", r, 18, Palette.Accent, bold: true);
    }

    private void DrawTimer(Ui ui, Vector2 pos)
    {
        int ticksLeft = Math.Max(0, Battle.TimeLimitTicks - Battle.Tick);
        int secondsLeft = (ticksLeft + 9) / 10;
        var color = secondsLeft <= 15 ? Palette.Damage : Palette.TextDim;
        ui.IconText("clock", "Time left ", $"{secondsLeft / 60}:{secondsLeft % 60:00}", pos, 20, color, bold: true);
        ui.Text("Out of time = defeat", pos + new Vector2(100, 4), 14, Palette.TextDim);
    }

    // ---- player ------------------------------------------------------------------------------------------------

    private void DrawPlayer(Ui ui)
    {
        ui.Panel(PlayerPanel, skin: "panel_player");
        // The character's portrait where the enemy's is; no name needed.
        var portrait = PortraitIn(PlayerPanel);
        var character = _battleSetup.Player.CharacterId is string c && Game.Data.Database.TryGet(c, out var ch) ? ch : null;
        ui.Square("Characters", character?.Id ?? "none", character?.Name ?? "You", portrait, Palette.Accent * 0.8f,
            underFrame: () => { if (Battle.Player.IsFrozenAt(Battle.Tick)) ui.Rect(portrait, FrostTint); });
        if (character is not null && ui.Hover(portrait))
        {
            var (title, lines) = Tooltips.For(character, 1, Game.Data.Database);
            ui.Tooltip(title, lines, Palette.Accent);
        }
        DrawHp(ui, Battle.Player, PlayerHpBar, _playerBars);
        DrawHeat(ui, Battle.Player, HeatBarIn(PlayerPanel), _playerBars);
        DrawStatuses(ui, Battle.Player, StatusRowIn(PlayerPanel));

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
        ui.Square("Items", def.Id, def.Name, r, border, Math.Max(2, thickness),
            underFrame: () => DrawCooldownShade(ui, item, r, fired ? Color.White * 0.3f : null, def.Activation == Activation.Active ? (canClick ? "click" : null) : null));
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
        if (hint is not null) ui.Text(hint, new Vector2(PlayerHpBar.X, PlayerPanel.Y + 316), 15, Palette.Accent, bold: true);
    }

    private void DrawExtras(Ui ui)
    {
        var passives = Battle.Player.Sources
            .Where(s => s.Def.Kind is SourceKind.Passive or SourceKind.Character || s.Def.IsPassiveAbility)
            .Where(s => s.Def.Abilities.Count > 0 || s.Def.Modifiers.Count > 0 || s.Script is not null).ToList();
        if (passives.Count == 0) return;
        for (int i = 0; i < passives.Count && i < PassiveMax; i++) DrawPassive(ui, passives[i], PassiveIcon(PlayerPassiveGrid, i), enemy: false);
    }

    // ---- shared ------------------------------------------------------------------------------------------------

    /// <summary>
    /// The HP bar (Luka 2026-10-16): shield as a see-through layer over the bar, a thin line every
    /// <see cref="HpTickEvery"/> HP so a bigger health pool shows as more segments, the shield number with its icon on
    /// the left and the HP number with the heart on the right.
    /// </summary>
    private static void DrawHp(Ui ui, Combatant c, Rectangle bar, BarAnimator shown)
    {
        // Luka 2026-10-20: textured when UI/hp_bar_back, hp_bar_fill, shield_bar_fill and hp_bar_frame exist. HP drops at
        // once and leaves a white trail that drains; shield glides.
        float hp = Math.Max(0, c.Hp) / (float)c.MaxHp, shieldPart = Math.Min(1f, shown.Shield / c.MaxHp);
        if (!ui.NineSlice("hp_bar_back", bar, HpBarCorner)) ui.Rect(bar, Palette.HpBack);
        float trail = Math.Min(1f, shown.Trail / c.MaxHp);
        if (trail > hp)
        {
            int from = bar.X + (int)(bar.Width * hp), to = bar.X + (int)Math.Ceiling(bar.Width * trail);
            ui.Rect(new Rectangle(from, bar.Y, to - from, bar.Height), Color.White * 0.9f);
        }
        if (!ui.BarFill("hp_bar_fill", bar, hp)) ui.Bar(bar, hp, Palette.Hp, Color.Transparent);
        if (shieldPart > 0.001f)
        {
            int w = (int)(bar.Width * shieldPart);
            if (!ui.BarFill("shield_bar_fill", bar, shieldPart, tint: Color.White * 0.8f))
                ui.Rect(new Rectangle(bar.X, bar.Y, w, bar.Height), Palette.Shield * 0.6f);
            ui.Rect(new Rectangle(bar.X + w - 2, bar.Y, 3, bar.Height), Palette.Shield); // where the shield ends
        }
        // Luka 2026-10-20: every line the same, one screen pixel thin, across the whole bar.
        for (int t = HpTickEvery; t < c.MaxHp; t += HpTickEvery)
            ui.HairlineV(bar.X + bar.Width * t / (float)c.MaxHp, bar.Y, bar.Height, Color.Black * 0.5f);
        if (!ui.NineSlice("hp_bar_frame", Outset(bar, HpFrameOutset), HpBarCorner)) ui.Border(bar, Palette.Border, 1);

        int icon = 26, iconY = bar.Center.Y - icon / 2, textY = bar.Center.Y - 11;
        string hpText = $"{Math.Max(0, c.Hp)} / {c.MaxHp}";
        float hpWidth = ui.Measure(hpText, 20, true).X;
        var heart = new Rectangle(bar.Right - 10 - icon, iconY, icon, icon);
        Shadowed(ui, hpText, new Vector2(heart.X - 8 - hpWidth, textY));
        if (!ui.Icon("heart", heart)) Shadowed(ui, "HP", new Vector2(heart.X, textY));
        if (c.Shield <= 0) return;
        string shieldText = $"{c.Shield}";
        var shield = new Rectangle(bar.X + 10, iconY, icon, icon);
        if (!ui.Icon("shield", shield)) shieldText += " shield";
        Shadowed(ui, shieldText, new Vector2(shield.Right + 6, textY));
    }

    /// <summary>
    /// The Heat bar (Luka 2026-10-19): vertical, right of the items, filling from the bottom in a green to yellow to red
    /// gradient, a line every <see cref="HeatTickEvery"/> Heat, and the number underneath. Shown for either side once it
    /// has anything tagged Heat or any Heat at all. A light blue line marks where Heat stops chill from freezing.
    /// </summary>
    private void DrawHeat(Ui ui, Combatant c, Rectangle bar, BarAnimator shown)
    {
        var rules = Battle.Rules;
        if (c.Statuses.Heat <= 0 && !c.IsStalledAt(Battle.Tick) && !c.Sources.Any(s => s.Def.HasTag("Heat"))) return;
        int max = Battle.MaxHeat(c.Side);
        float part = Math.Clamp(shown.Heat / max, 0, 1); // glides up and down (Luka 2026-10-20)
        // Luka 2026-10-20: textured when UI/heat_bar_back, heat_bar_fill and heat_bar_frame exist.
        if (!ui.NineSlice("heat_bar_back", bar, HpBarCorner)) ui.Rect(bar, Palette.HeatBack);
        if (!ui.BarFill("heat_bar_fill", bar, part, vertical: true))
        {
            int filled = (int)(bar.Height * part);
            const int slice = 2;
            for (int y = 0; y < filled; y += slice)
            {
                int h = Math.Min(slice, filled - y);
                ui.Rect(new Rectangle(bar.X, bar.Bottom - y - h, bar.Width, h), HeatColor((y + h / 2f) / bar.Height));
            }
        }
        for (int heat = HeatTickEvery; heat < max; heat += HeatTickEvery)
            ui.HairlineH(bar.X, bar.Bottom - bar.Height * heat / (float)max, bar.Width, Color.Black * 0.5f);
        if (rules.HeatFreezeImmunityAt < max)
        {
            int y = bar.Bottom - (int)Math.Round(bar.Height * rules.HeatFreezeImmunityAt / (double)max);
            ui.Rect(new Rectangle(bar.X, y - 1, bar.Width, 2), Palette.Freeze);
        }
        if (!ui.NineSlice("heat_bar_frame", Outset(bar, HeatFrameOutset), HpBarCorner)) ui.Border(bar, Palette.Border, 1);
        string text = c.Statuses.Heat.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var label = new Rectangle(bar.X - 20, bar.Bottom + 8, bar.Width + 40, 24);
        ui.TextIn(text, new Rectangle(label.X + 2, label.Y + 2, label.Width, label.Height), 20, Color.Black * 0.8f, bold: true);
        ui.TextIn(text, label, 20, HeatColor(c.Statuses.Heat / (float)max), bold: true);
        if (ui.Hover(bar) || ui.Hover(label))
            ui.Tooltip("Heat", [
                $"{c.Statuses.Heat}/{max}. At {max} {(c.Side == Side.Player ? "you overheat" : "it overheats")}: {rules.OverheatDamage} damage, every item stalls for {rules.OverheatStallSeconds:0.#}s, and Heat goes back to 0.",
                $"At {rules.HeatFreezeImmunityAt} Heat or more (the blue line), chill still slows but can't freeze.",
                rules.HeatCoolingPerSecond > 0 ? $"Cools {rules.HeatCoolingPerSecond} per second." : "Heat doesn't cool down.",
            ], Palette.Heat);
    }

    /// <summary>Green at 0, yellow halfway, red at the top.</summary>
    private static Color HeatColor(float t)
    {
        t = Math.Clamp(t, 0, 1);
        return t < 0.5f ? Color.Lerp(HeatLow, HeatMid, t * 2) : Color.Lerp(HeatMid, HeatHigh, (t - 0.5f) * 2);
    }

    private static readonly Color HeatLow = new(80, 200, 90), HeatMid = new(250, 220, 60), HeatHigh = new(230, 50, 40);

    /// <summary>A line on the Heat bar every this much Heat.</summary>
    private const int HeatTickEvery = 10;

    private static Rectangle Outset(Rectangle r, int by) => new(r.X - by, r.Y - by, r.Width + 2 * by, r.Height + 2 * by);

    /// <summary>The bar frames are drawn this far outside the bar, so their outline doesn't cover the fill.</summary>
    private const int HpFrameOutset = 4, HeatFrameOutset = 4;
    private const int HpBarCorner = 12;

    /// <summary>A thin line on the HP bar every this many HP.</summary>
    private const int HpTickEvery = 10;

    private static void Shadowed(Ui ui, string text, Vector2 pos)
    {
        ui.Text(text, pos + new Vector2(2, 2), 20, Color.Black * 0.8f, bold: true);
        ui.Text(text, pos, 20, Color.White, bold: true);
    }

    /// <summary>
    /// Statuses as framed squares (Luka 2026-10-16): stacks in the bottom-right corner; a status that runs on a timer
    /// (burn's next tick, chill wearing off, frozen) gets the same shade as a charging item, and its seconds show only on
    /// mouse-over.
    /// </summary>
    private void DrawStatuses(Ui ui, Combatant c, Vector2 pos)
    {
        float x = pos.X;
        void Chip(string id, string name, int? stacks, Color color, string body, int ticksLeft = 0, int ticksTotal = 0)
        {
            var r = new Rectangle((int)x, (int)pos.Y, StatusSize, StatusSize);
            x += StatusSize + 10;
            bool hover = ui.Hover(r);
            bool timed = ticksTotal > 0;
            ui.FramedIcon("Statuses", id, name, r, color, hover ? 3 : 2, underFrame: () =>
            {
                if (!timed) return;
                double left = Math.Clamp((ticksLeft - _clock.Fraction) / ticksTotal, 0, 1);
                int covered = (int)(r.Height * left);
                if (covered > 0) ui.Rect(new Rectangle(r.X, r.Y, r.Width, covered), new Color(0, 0, 0, 140));
                if (!hover) return;
                string seconds = $"{Math.Max(0, ticksLeft - _clock.Fraction) / 10.0:0.0}";
                ui.TextIn(seconds, new Rectangle(r.X + 2, r.Y + 2, r.Width, r.Height), 18, Color.Black * 0.8f, bold: true);
                ui.TextIn(seconds, r, 18, Color.White, bold: true);
            });
            if (stacks is int n)
            {
                string text = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var size = ui.Measure(text, 17, true);
                var at = new Vector2(r.Right - size.X - 5, r.Bottom - size.Y - 3);
                ui.Text(text, at + new Vector2(2, 2), 17, Color.Black * 0.9f, bold: true);
                ui.Text(text, at, 17, Color.White, bold: true);
            }
            if (hover) ui.Tooltip(name, [body], color);
        }
        var s = c.Statuses;
        var rules = Battle.Rules;
        int tick = Battle.Tick;
        if (s.Burn > 0)
            Chip("burn", "Burn", s.Burn, Palette.Burn, $"Every {rules.BurnTickSeconds:0.##}s deals its stacks as damage (x{rules.BurnShieldMultiplier} against shield), then loses {rules.BurnLossPerTick} {(rules.BurnLossPerTick == 1 ? "stack" : "stacks")}.",
                rules.BurnTickInterval - tick % rules.BurnTickInterval, rules.BurnTickInterval);
        if (s.Poison > 0) Chip("poison", "Poison", s.Poison, Palette.Poison, $"{s.Poison}/{rules.PoisonThreshold} stacks. At {rules.PoisonThreshold} it bursts for {rules.PoisonBurstDamage} damage (shield blocks it); stacks past {rules.PoisonThreshold} carry over to the next burst.");
        if (s.Chill > 0)
            Chip("chill", "Chill", s.Chill, Palette.Chill,
                $"{s.Chill}/{rules.ChillFreezeAtStacks} stacks. Each stack makes every cooldown {rules.ChillCooldownPercentPerStack}% longer. Stacks last {rules.ChillSeconds:0.#}s, refreshed by each new stack. At {rules.ChillFreezeAtStacks} stacks: frozen for {rules.FreezeSeconds:0.#}s.",
                Math.Max(0, s.ChillExpiresAtTick - tick), rules.ChillTicks);
        if (s.Pain > 0) Chip("pain", "Pain", s.Pain, Palette.Pain, $"Each time one of {(c.Side == Side.Player ? "your items" : "its abilities")} fires, {(c.Side == Side.Player ? "you take" : "it takes")} {s.Pain} damage. Lasts all fight.");
        if (s.Nail > 0) Chip("nail", "Nail", s.Nail, Palette.Nail, $"Does nothing alone. Uproot pulls every Nail out for {rules.NailDamage} damage each (more with bonuses).");
        if (s.Power > 0) Chip("power", "Power", s.Power, Palette.Power, $"Every hit {(c.Side == Side.Player ? "you deal" : "it deals")} does {s.Power * rules.PowerDamagePerStack} more damage. Lasts all fight.");
        if (s.Vulnerable > 0)
            Chip("vulnerable", "Vulnerable", null, Palette.Vulnerable, $"Hits and pain {(c.Side == Side.Player ? "you take" : "it takes")} do {rules.VulnerableDamagePercent}% more damage (not burn or poison).",
                s.Vulnerable, Math.Max(s.Vulnerable, s.VulnerableFull));
        if (s.NoHeatGain > 0) Chip("no_heat_gain", "No Heat Gain", null, Palette.Freeze, $"{(c.Side == Side.Player ? "You can't" : "It can't")} gain any Heat until it's turned off.");
        if (c.IsStalledAt(tick)) Chip("overheat", "Overheated", null, Palette.Heat, "Every item is stalled until it cools off.", s.StalledUntilTick - tick + 1, Math.Max(1, s.StallFull));
        if (c.IsFrozenAt(tick)) Chip("freeze", "Frozen", null, Palette.Freeze, "Nothing charges or fires until it thaws.", s.FrozenUntilTick - tick + 1, rules.FreezeTicks);
    }

    private const int StatusSize = 60;

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
        // Damage to a side comes out of its HP bar.
        if (a.Side is Side side) return side == Side.Enemy ? new Vector2(EnemyHpBar.Center.X, EnemyHpBar.Center.Y - 12) : new Vector2(PlayerHpBar.Center.X, PlayerHpBar.Center.Y - 12);
        var source = a.SourceId is int id ? Battle.FindSource(id) : null;
        if (source?.Slot is int slot) return new Vector2(CardRect(slot).Center.X, CardRect(slot).Y + 40);
        if (source is not null)
        {
            var abilities = Battle.Enemy.Sources.Where(s => s.Def.Kind == SourceKind.EnemyAbility && s.HasCooldown).ToList();
            int index = abilities.IndexOf(source);
            if (index >= 0) return new Vector2(AbilityCard(index).Center.X, AbilityCard(index).Y + 30);
            var passives = Battle.Enemy.Sources.Where(s => s.Def.IsPassive).ToList();
            index = passives.IndexOf(source);
            if (index >= 0) return PassiveIcon(EnemyPassiveGrid, index).Center.ToVector2();
        }
        return new Vector2(EnemyHpBar.Center.X, EnemyHpBar.Y);
    }

    private void DrawFloats(Ui ui)
    {
        foreach (var f in _floats.Active)
        {
            float age = (float)(f.Age / FloatingTexts.Lifetime);
            float dx = (f.Stack % 3) switch { 0 => 0, 1 => -110, _ => 110 };
            var pos = AnchorPosition(f.Anchor) + new Vector2(dx, -age * f.Speed - f.Stack / 3 % 2 * 22); // each rises at its own speed
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
