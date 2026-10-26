using ItemFighter.Core.Content;
using ItemFighter.Core.Determinism;
using ItemFighter.Core.Engine.Effects;
using ItemFighter.Core.Engine.Triggers;

namespace ItemFighter.Core.Engine;

public enum Side { Player, Enemy }

public enum BattleResult { Ongoing, PlayerWon, EnemyWon, Draw }

/// <summary>
/// A deterministic fight on fixed 0.1s ticks. Same setup + same seed + same commands = same events.
/// </summary>
public sealed class Battle
{
    public const int MaxChainDepth = 8;
    /// <summary>Reactions (and copied effects) allowed per tick; later ones are dropped with ChainCapped so wide boards stay fast.</summary>
    public const int MaxReactionsPerTick = 256;
    public const int DefaultPlayerHp = 100;

    /// <summary>True when the fight ended because time ran out (counted as a loss).</summary>
    public bool TimedOut { get; private set; }

    private readonly List<BattleEvent> _events = [];
    private readonly ContentDatabase _db;
    private int _nextId = 1;
    private bool _started;

    public Battle(BattleSetup setup, ContentDatabase db, ScriptRegistry? scripts = null)
    {
        _db = db;
        Rules = db.Rules;
        Scripts = scripts ?? new ScriptRegistry();
        Rng = new Rng(setup.Seed).Fork("combat");

        SourceDefinition? character = setup.Player.CharacterId is null ? null : Require(setup.Player.CharacterId, SourceKind.Character);
        Player = new Combatant(Side.Player, (character?.MaxHp ?? DefaultPlayerHp) + Math.Max(0, setup.Player.BonusMaxHp));
        if (character is not null) AddSource(Player, character, null, 1);
        foreach (string id in setup.Player.PassiveIds)
            AddSource(Player, RequirePassive(id), null, 1);

        var usedSlots = new HashSet<int>();
        foreach (var item in setup.Player.Board.OrderBy(i => i.Slot))
        {
            if (item.Slot < 0 || item.Slot >= Board.SlotCount)
                throw new ArgumentException($"slot {item.Slot} is outside the board (0..{Board.SlotCount - 1})");
            if (!usedSlots.Add(item.Slot)) throw new ArgumentException($"slot {item.Slot} is used twice");
            if (item.Level is < 1 or > ContentLoader.LevelCount) throw new ArgumentException($"level {item.Level} is out of range");
            AddSource(Player, Require(item.ItemId, SourceKind.Item), item.Slot, item.Level);
        }

        var enemy = Require(setup.EnemyId, SourceKind.Enemy);
        if (setup.EnemyHpPercent <= 0) throw new ArgumentException("EnemyHpPercent must be positive");
        Enemy = new Combatant(Side.Enemy, Math.Max(1, (int)((long)enemy.MaxHp * setup.EnemyHpPercent / 100)));
        EnemyDefinition = enemy;
        AddSource(Enemy, enemy, null, 1);
        foreach (string id in enemy.AbilityIds.Concat(enemy.PassiveIds))
            AddSource(Enemy, RequireEnemyAbility(id), null, 1);

        foreach (var s in AllSources)
            ApplyCooldownModifier(s);
    }

    private void ApplyCooldownModifier(SourceInstance s)
    {
        if (!s.HasCooldown) return;
        int baseTicks = s.Def.CooldownTicks!.Value;
        int ticks = ModifierPipeline.ModifiedCooldown(baseTicks, ModifierPipeline.Sum(this, "CooldownPercent", s))
            + ModifierPipeline.Sum(this, "CooldownSeconds", s) * 10;
        s.CooldownTicks = Rules.FloorCooldown(baseTicks, Math.Max(1, ticks));
    }

    public int Tick { get; private set; }
    public BattleResult Result { get; private set; } = BattleResult.Ongoing;
    public Combatant Player { get; }
    public Combatant Enemy { get; }
    public SourceDefinition EnemyDefinition { get; }
    public IReadOnlyList<BattleEvent> Events => _events;
    public Rng Rng { get; }

    /// <summary>Status and time-limit numbers (Data/rules.json).</summary>
    public CombatRules Rules { get; }

    /// <summary>A fight still going after this many ticks (2 minutes by default) is a loss for the player.</summary>
    public int TimeLimitTicks => Rules.TimeLimitTicks;
    public ScriptRegistry Scripts { get; }

    public Combatant Get(Side side) => side == Side.Player ? Player : Enemy;
    public Combatant OpponentOf(Side side) => side == Side.Player ? Enemy : Player;

    /// <summary>Every source in the battle, player first, in canonical order.</summary>
    public IEnumerable<SourceInstance> AllSources => Player.Sources.Concat(Enemy.Sources);

    public SourceInstance? FindSource(int id) => AllSources.FirstOrDefault(s => s.Id == id);

    private SourceDefinition Require(string id, SourceKind kind)
    {
        if (!_db.TryGet(id, out var def)) throw new ArgumentException($"no content with id '{id}'");
        if (def.Kind != kind) throw new ArgumentException($"'{id}' is a {def.Kind}, expected {kind}");
        return def;
    }

    /// <summary>Player passives come from the passive pool (passives/), bought in passive shops.</summary>
    private SourceDefinition RequirePassive(string id) => Require(id, SourceKind.Passive);

    /// <summary>An enemy uses its own abilities (enemies/abilities/) and passives from the shared pool (passives/).</summary>
    private SourceDefinition RequireEnemyAbility(string id)
    {
        if (!_db.TryGet(id, out var def)) throw new ArgumentException($"no content with id '{id}'");
        if (def.Kind is not (SourceKind.EnemyAbility or SourceKind.Passive)) throw new ArgumentException($"'{id}' is not an enemy ability or a passive");
        return def;
    }

    internal SourceInstance AddSource(Combatant owner, SourceDefinition def, int? slot, int level)
    {
        var source = new SourceInstance(_nextId++, def, owner.Side, slot, level);
        if (def.Script is not null) source.Script = Scripts.Create(def.Script);
        owner.Add(source);
        return source;
    }

    public BattleResult RunToEnd()
    {
        while (Result == BattleResult.Ongoing) Step();
        return Result;
    }

    /// <summary>Advances one tick (0.1s).</summary>
    public void Step()
    {
        if (Result != BattleResult.Ongoing) return;
        if (!_started)
        {
            _started = true;
            Emit(new FightStarted(Tick), 0, null);
        }
        Tick++;
        _reactionsThisTick = 0;
        ApplyCommands();
        TickStatuses();
        AdvanceCooldowns();
        FireCompletedSources();
        CheckPhases();
        ResolveResult();
    }

    private void TickStatuses()
    {
        foreach (var c in new[] { Player, Enemy })
            foreach (var rule in StatusRules.Values)
                rule.OnTick(this, c);
    }

    private void AdvanceCooldowns()
    {
        foreach (var s in AllSources.ToList())
        {
            if (s.HasCooldown && !s.Ready && s.LastFiredTick != Tick && s.Progress < s.FullProgress)
                s.Progress = Math.Min(s.FullProgress, s.Progress + CooldownSpeed(s));
            if (s.FreezeTicks > 0) s.FreezeTicks--;
            if (s.HasteTicks > 0) s.HasteTicks--;
        }
    }

    /// <summary>
    /// Progress gained this tick: 100 normally, x2 hasted; each chill stack makes the cooldown 5% longer
    /// (rounded so a 2s cooldown with 2 chill takes exactly 2.2s); 0 when frozen.
    /// </summary>
    public int CooldownSpeed(SourceInstance s)
    {
        var owner = Get(s.Owner);
        if (s.FreezeTicks > 0 || owner.IsHaltedAt(Tick)) return 0;
        int speed = SourceInstance.ProgressPerTick;
        if (s.HasteTicks > 0) speed *= 2;
        int lengthen = 100 + Rules.ChillCooldownPercentPerStack * owner.Statuses.Chill;
        speed = (speed * 100 + lengthen - 1) / lengthen;
        return Math.Max(Rules.SlowestCooldownSpeedPercent * SourceInstance.ProgressPerTick / 100, speed);
    }

    /// <summary>Burn, poison and chill rules, in a fixed order.</summary>
    public static readonly IReadOnlyDictionary<StatusType, IStatusRule> StatusRules = new SortedDictionary<StatusType, IStatusRule>
    {
        [StatusType.Burn] = new BurnRule(),
        [StatusType.Poison] = new PoisonRule(),
        [StatusType.Chill] = new ChillRule(),
        [StatusType.Pain] = new PainRule(),
        [StatusType.Nail] = new NailRule(),
        [StatusType.Power] = new PowerRule(),
        [StatusType.Vulnerable] = new VulnerableRule(),
        [StatusType.Heat] = new HeatRule(),
        [StatusType.NoHeatGain] = new NoHeatGainRule(),
    };

    /// <summary>Heat at which a side overheats: rules overheatAt plus that side's MaxHeat modifiers.</summary>
    public int MaxHeat(Side side) => Math.Max(1, Rules.OverheatAt + SideModifier(side, "MaxHeat"));

    /// <summary>Adds burn, poison or chill stacks to a combatant.</summary>
    public void ApplyStatus(Side target, StatusType status, int amount, SourceInstance? source, int depth)
    {
        if (!status.IsCombatantStatus()) throw new ArgumentException($"{status} goes on items; use ApplyItemStatus");
        amount = Modify(status + "Applied", amount, source);
        if (amount <= 0) return;
        var c = Get(target);
        if (status == StatusType.Heat && c.Statuses.NoHeatGain > 0) return;
        Emit(new StatusApplied(Tick, target, null, status, amount, source?.Id), depth, source);
        StatusRules[status].OnApply(this, c, amount, source, depth);
    }

    /// <summary>Sets freeze or haste on one item or ability for <paramref name="ticks"/> ticks (longest wins).</summary>
    public void ApplyItemStatus(SourceInstance target, StatusType status, int ticks, SourceInstance? source, int depth)
    {
        if (status.IsCombatantStatus()) throw new ArgumentException($"{status} goes on combatants; use ApplyStatus");
        if (ticks <= 0) return;
        switch (status)
        {
            case StatusType.Freeze: target.FreezeTicks = Math.Max(target.FreezeTicks, ticks); break;
            case StatusType.Haste: target.HasteTicks = Math.Max(target.HasteTicks, ticks); break;
        }
        Emit(new StatusApplied(Tick, null, target.Id, status, ticks, source?.Id), depth, source);
    }

    /// <summary>Sums a side-wide modifier such as DamageTaken from all of that side's sources (filters don't apply).</summary>
    public int SideModifier(Side side, string stat)
    {
        int total = 0;
        foreach (var holder in Get(side).Sources)
            foreach (var m in holder.Def.Modifiers)
                if (m.Stat == stat) total += m.Amount;
        return total;
    }

    /// <summary>Applies a modifier stat to a base value for the given producer. Filled in by the modifier pipeline.</summary>
    public int Modify(string stat, int baseValue, SourceInstance? producer) =>
        producer is null ? baseValue : baseValue + ModifierPipeline.Sum(this, stat, producer);

    private void FireCompletedSources()
    {
        // Loop because a firing can complete another source's cooldown in the same tick (e.g. charge).
        // Each source fires at most once per tick.
        while (true)
        {
            // Frozen passives hold a full cooldown and fire once the freeze ends ("freeze wins" over charge/reset).
            var due = AllSources.FirstOrDefault(s => s.HasCooldown && !s.Ready && s.Progress >= s.FullProgress
                && (s.Def.Activation == Activation.Active || (s.LastFiredTick != Tick && !IsFrozen(s))));
            if (due is null) return;
            if (due.Def.Activation == Activation.Active)
            {
                due.Ready = true;
                Emit(new ItemReady(Tick, due.Id), 0, due);
                continue;
            }
            due.Progress = Math.Max(0, due.Progress - due.FullProgress); // charge overflow carries over
            Fire(due, 0, null);
        }
    }

    private int _reactionsThisTick;

    /// <summary>
    /// Claims one reaction slot at the given depth. Past <see cref="MaxChainDepth"/> or the per-tick
    /// <see cref="MaxReactionsPerTick"/> budget it logs <see cref="ChainCapped"/> and returns false.
    /// </summary>
    public bool TryReact(SourceInstance source, int depth)
    {
        if (depth > MaxChainDepth || _reactionsThisTick >= MaxReactionsPerTick)
        {
            _events.Add(new ChainCapped(Tick, source.Id));
            return false;
        }
        _reactionsThisTick++;
        return true;
    }

    private bool IsFrozen(SourceInstance s) => s.FreezeTicks > 0 || Get(s.Owner).IsHaltedAt(Tick);

    private readonly List<ICommand> _pending = [];

    /// <summary>Queues player input. It applies at the start of its tick; a command for a tick already played is rejected.</summary>
    public void Enqueue(ICommand command)
    {
        if (command.Tick <= Tick && _started)
        {
            if (command is ActivateCommand late) Reject(late, RejectReasons.Late);
            return;
        }
        _pending.Add(command);
    }

    public BattleResult RunToEnd(IEnumerable<ICommand> commands)
    {
        foreach (var c in commands) Enqueue(c);
        return RunToEnd();
    }

    private void ApplyCommands()
    {
        var due = _pending.Where(c => c.Tick <= Tick).ToList();
        _pending.RemoveAll(c => c.Tick <= Tick);
        foreach (var command in due)
            if (command is ActivateCommand activate) Activate(activate);
    }

    private void Reject(ActivateCommand c, string reason) =>
        Emit(new CommandRejected(Tick, c.SourceId, reason), 0, null);

    private void Activate(ActivateCommand c)
    {
        var source = FindSource(c.SourceId);
        if (source is null || source.Owner != Side.Player) { Reject(c, RejectReasons.UnknownSource); return; }
        if (source.Def.Activation != Activation.Active) { Reject(c, RejectReasons.NotActive); return; }
        if (source.FreezeTicks > 0 || Player.IsFrozenAt(Tick)) { Reject(c, RejectReasons.Frozen); return; }
        if (Player.IsStalledAt(Tick)) { Reject(c, RejectReasons.Stalled); return; }
        if (!source.Ready) { Reject(c, RejectReasons.NotReady); return; }
        SourceInstance? target = null;
        if (source.Def.RequiresTarget)
        {
            if (c.TargetSourceId is null) { Reject(c, RejectReasons.TargetRequired); return; }
            target = FindSource(c.TargetSourceId.Value);
            if (target is null || !IsValidTarget(source, target)) { Reject(c, RejectReasons.InvalidTarget); return; }
        }
        source.Ready = false;
        source.Progress = 0;
        Fire(source, 0, target);
    }

    /// <summary>Targeted actives pick another of the player's items; "OwnItemTag:X" also requires the tag.</summary>
    public static bool IsValidTarget(SourceInstance source, SourceInstance target)
    {
        if (target == source || target.Owner != source.Owner || !target.IsItem) return false;
        string filter = source.Def.TargetFilter;
        if (filter.StartsWith("OwnItemTag:", StringComparison.Ordinal)) return target.Def.HasTag(filter["OwnItemTag:".Length..]);
        return true;
    }

    /// <summary>
    /// Takes time off the current cooldown. Past zero the item fires and the rest carries into its next
    /// cooldown (actives just become ready).
    /// </summary>
    public void Charge(SourceInstance target, int ticks, SourceInstance? source, int depth)
    {
        if (!target.HasCooldown || target.Ready || ticks <= 0) return;
        int cap = target.Def.Activation == Activation.Active ? target.FullProgress : 2 * target.FullProgress - 1;
        target.Progress = Math.Min(cap, target.Progress + ticks * SourceInstance.ProgressPerTick);
        Emit(new Charged(Tick, target.Id, ticks, source?.Id), depth, source);
    }

    /// <summary>Slow: adds time to the current cooldown (a ready active stops being ready).</summary>
    public void Delay(SourceInstance target, int ticks, SourceInstance? source, int depth)
    {
        if (!target.HasCooldown || ticks <= 0) return;
        if (target.Ready)
        {
            target.Ready = false;
            target.Progress = target.FullProgress;
        }
        target.Progress -= ticks * SourceInstance.ProgressPerTick;
        Emit(new Delayed(Tick, target.Id, ticks, source?.Id), depth, source);
    }

    public void ResetCooldown(SourceInstance target, SourceInstance? source, int depth)
    {
        if (!target.HasCooldown || target.Ready) return;
        target.Progress = target.FullProgress;
        Emit(new CooldownReset(Tick, target.Id, source?.Id), depth, source);
    }

    /// <summary>Shortens a cooldown for the rest of the fight by a percent of its current length, floored by the rules.</summary>
    public void ReduceCooldown(SourceInstance target, int percent, SourceInstance? source, int depth)
    {
        if (!target.HasCooldown || percent <= 0) return;
        int before = target.CooldownTicks;
        int after = Rules.FloorCooldown(target.Def.CooldownTicks ?? before, before * (100 - Math.Min(percent, 100)) / 100);
        if (after >= before) return;
        // Keep the same fraction of the cooldown done, so a cut never hands out a free fire.
        target.Progress = (int)((long)target.Progress * after / before);
        target.CooldownTicks = after;
        Emit(new CooldownChanged(Tick, target.Id, before, after, source?.Id), depth, source);
    }

    public void ModifyStat(SourceInstance target, string stat, int amount, SourceInstance? source, int depth)
    {
        target.AddBonus(stat, amount);
        Emit(new StatModified(Tick, target.Id, stat, amount, source?.Id), depth, source);
    }

    private readonly HashSet<int> _phasesStarted = [];

    private void CheckPhases()
    {
        var phases = EnemyDefinition.Phases;
        for (int i = 0; i < phases.Count; i++)
        {
            if (_phasesStarted.Contains(i) || Enemy.Hp * 100 >= Enemy.MaxHp * phases[i].HpBelowPercent) continue;
            _phasesStarted.Add(i);
            Emit(new PhaseStarted(Tick, i), 0, null);
            foreach (string id in phases[i].AddAbilityIds)
            {
                var added = AddSource(Enemy, RequireEnemyAbility(id), null, 1);
                ApplyCooldownModifier(added);
                Emit(new SourceAdded(Tick, added.Id, Side.Enemy), 0, added);
            }
        }
    }

    private void ResolveResult()
    {
        if (Player.IsDead && Enemy.IsDead) Result = BattleResult.Draw;
        else if (Enemy.IsDead) Result = BattleResult.PlayerWon;
        else if (Player.IsDead) Result = BattleResult.EnemyWon;
        else if (Tick >= TimeLimitTicks)
        {
            Result = BattleResult.EnemyWon;
            TimedOut = true;
        }
        if (Result != BattleResult.Ongoing) Emit(new BattleEnded(Tick, Result), 0, null);
    }

    /// <summary>Fires a source: logs it, runs its cooldown/activation abilities, then lets others react.</summary>
    public void Fire(SourceInstance source, int depth, SourceInstance? chosenTarget)
    {
        source.UseCount++;
        source.CycleCount++;
        source.LastFiredTick = Tick;
        var fired = new SourceFired(Tick, source.Id, source.Owner, chosenTarget?.Id, Reaction: false);
        _events.Add(fired);
        foreach (var ability in source.Def.Abilities)
            if (ability.Trigger is OnCooldownTrigger or OnActivateTrigger)
                RunEffects(ability, source, chosenTarget, depth);
        Dispatch(fired, depth, source);
        int pain = Get(source.Owner).Statuses.Pain;
        if (pain > 0) DealDamage(source.Owner, pain, DamageKind.Pain, null, depth);
    }

    /// <summary>Removes every stack of the given statuses from a combatant.</summary>
    public void RemoveStatuses(Side target, IReadOnlyList<StatusType> statuses, SourceInstance? source, int depth)
    {
        var c = Get(target);
        foreach (var status in statuses)
        {
            int had = c.Statuses.Get(status);
            if (had <= 0) continue;
            c.Statuses.Clear(status);
            Emit(new StatusRemoved(Tick, target, status, had, source?.Id), depth, source);
        }
    }

    /// <summary>Pulls out every nail on a combatant: one hit of nails × (rules nailDamage + NailDamage modifiers).</summary>
    public void Uproot(Side target, SourceInstance? source, int depth)
    {
        var c = Get(target);
        int nails = c.Statuses.Nail;
        if (nails <= 0) return;
        c.Statuses.Nail = 0;
        int each = Math.Max(0, Modify("NailDamage", Rules.NailDamage, source));
        int total = nails * each;
        // Luka 2026-10-26: the uprooting side's Heat adds uprootDamagePercentPerHeat % each (50 Heat = +50%), rounded half up.
        int heat = source is null ? 0 : Get(source.Owner).Statuses.Heat;
        total = (total * (100 + heat * Rules.UprootDamagePercentPerHeat) + 50) / 100;
        Emit(new NailsUprooted(Tick, target, nails, total, source?.Id), depth, source);
        DealDamage(target, total, DamageKind.Direct, source, depth);
    }

    /// <summary>
    /// A side overheats: Heat back to 0, every item and ability stalls, and overheatDamage goes to the side itself or,
    /// with an OverheatHitsEnemy modifier, to its opponent. OverheatStallPercent modifiers change the stall.
    /// </summary>
    public void Overheat(Side side, SourceInstance? source, int depth)
    {
        var c = Get(side);
        c.Statuses.Heat = 0;
        int stall = Rules.OverheatStallTicks * Math.Max(0, 100 + SideModifier(side, "OverheatStallPercent")) / 100;
        if (stall > 0)
        {
            c.Statuses.StalledUntilTick = Math.Max(c.Statuses.StalledUntilTick, Tick + stall);
            c.Statuses.StallFull = stall;
        }
        var damageTarget = SideModifier(side, "OverheatHitsEnemy") > 0 ? OpponentOf(side).Side : side;
        Emit(new Overheated(Tick, side, damageTarget, Rules.OverheatDamage, stall), depth, source);
        DealDamage(damageTarget, Rules.OverheatDamage, DamageKind.Overheat, source, depth);
    }

    /// <summary>Runs one ability because its trigger matched. Counts as a use and lets others react in turn.</summary>
    public void React(SourceInstance source, AbilityDefinition ability, int depth, SourceInstance? trigger = null)
    {
        source.UseCount++;
        var fired = new SourceFired(Tick, source.Id, source.Owner, null, Reaction: true);
        _events.Add(fired);
        RunEffects(ability, source, null, depth, trigger);
        Dispatch(fired, depth, source);
    }

    public void RunEffects(AbilityDefinition ability, SourceInstance source, SourceInstance? chosenTarget, int depth, SourceInstance? trigger = null)
    {
        var ctx = new EffectContext(this, source, chosenTarget, depth, trigger);
        foreach (var effect in ability.Effects) effect.Apply(ctx);
    }

    /// <summary>Logs an event and offers it to every trigger.</summary>
    public void Emit(BattleEvent e, int depth, SourceInstance? producer)
    {
        _events.Add(e);
        Dispatch(e, depth, producer);
    }

    /// <summary>
    /// Offers an event to every triggered ability, in canonical order. Reactions run one level deeper;
    /// beyond <see cref="MaxChainDepth"/> they are dropped with a <see cref="ChainCapped"/> event, so loops always end.
    /// </summary>
    private void Dispatch(BattleEvent e, int depth, SourceInstance? producer)
    {
        var trigger = e is SourceFired f ? FindSource(f.SourceId) : null;
        foreach (var source in AllSources.ToList())
        {
            foreach (var ability in source.Def.Abilities)
            {
                var t = ability.Trigger;
                if (t is OnCooldownTrigger or OnActivateTrigger) continue;
                if (source == producer && !t.AllowSelf) continue;
                if (!t.Matches(e, source, this)) continue;
                for (int n = t.Repeats(e); n > 0; n--)
                {
                    if (!TryReact(source, depth + 1)) break;
                    React(source, ability, depth + 1, trigger);
                }
            }
            if (source.Script is not null && depth + 1 <= MaxChainDepth)
                source.Script.OnEvent(e, source, this, depth + 1);
        }
    }

    public void DealDamage(Side target, int amount, DamageKind kind, SourceInstance? source, int depth, bool ignoreShield = false)
    {
        if (kind == DamageKind.Direct)
        {
            amount = Modify("DamageDealt", amount, source);
            if (source is not null) amount += Get(source.Owner).Statuses.Power * Rules.PowerDamagePerStack;
        }
        if (kind is DamageKind.Direct or DamageKind.Pain && amount > 0 && Get(target).Statuses.Vulnerable > 0)
            amount = (amount * (100 + Rules.VulnerableDamagePercent) + 50) / 100; // rounds halves up
        if (amount <= 0) return;
        int shieldDamage = ignoreShield ? 0 : Math.Min(Get(target).Shield, amount);
        ApplyRawDamage(target, amount - shieldDamage, shieldDamage, kind, source, depth);
    }

    /// <summary>Removes exactly the given HP and shield, for statuses with their own shield rules.</summary>
    public void ApplyRawDamage(Side target, int hpDamage, int shieldDamage, DamageKind kind, SourceInstance? source, int depth)
    {
        int adjust = SideModifier(target, "DamageTaken");
        if (adjust < 0)
        {
            int cut = Math.Min(-adjust, hpDamage);
            hpDamage -= cut;
            shieldDamage = Math.Max(0, shieldDamage - (-adjust - cut));
        }
        else if (adjust > 0 && hpDamage + shieldDamage > 0) hpDamage += adjust;
        if (hpDamage <= 0 && shieldDamage <= 0) return;
        var c = Get(target);
        c.Shield -= shieldDamage;
        c.Hp -= hpDamage;
        Emit(new DamageDealt(Tick, target, hpDamage, shieldDamage, kind, source?.Id), depth, source);
    }

    public void GainShield(Side target, int amount, SourceInstance? source, int depth)
    {
        amount = Modify("ShieldGained", amount, source);
        if (amount <= 0) return;
        Get(target).Shield += amount;
        Emit(new ShieldGained(Tick, target, amount, source?.Id), depth, source);
    }

    public void Heal(Side target, int amount, SourceInstance? source, int depth)
    {
        var c = Get(target);
        if (c.Hp <= 0) return; // lethal damage stays lethal
        int healed = Math.Min(Modify("HealGained", amount, source), c.MaxHp - c.Hp);
        if (healed <= 0) return;
        c.Hp += healed;
        Emit(new Healed(Tick, target, healed, source?.Id), depth, source);
    }
}
