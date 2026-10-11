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
    public const int FatigueStartTick = 300;
    public const int FatigueInterval = 10;
    public const int MaxTicks = 1200;
    public const int DefaultPlayerHp = 100;

    private readonly List<BattleEvent> _events = [];
    private readonly ContentDatabase _db;
    private int _nextId = 1;
    private bool _started;

    public Battle(BattleSetup setup, ContentDatabase db, ScriptRegistry? scripts = null)
    {
        _db = db;
        Scripts = scripts ?? new ScriptRegistry();
        Rng = new Rng(setup.Seed).Fork("combat");

        SourceDefinition? character = setup.Player.CharacterId is null ? null : Require(setup.Player.CharacterId, SourceKind.Character);
        Player = new Combatant(Side.Player, character?.MaxHp ?? DefaultPlayerHp);
        if (character is not null) AddSource(Player, character, null, 1);
        foreach (string id in setup.Player.PassiveIds)
            AddSource(Player, Require(id, SourceKind.Passive), null, 1);

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
        Enemy = new Combatant(Side.Enemy, enemy.MaxHp);
        EnemyDefinition = enemy;
        AddSource(Enemy, enemy, null, 1);
        foreach (string id in enemy.AbilityIds)
            AddSource(Enemy, Require(id, SourceKind.EnemyAbility), null, 1);

        foreach (var s in AllSources)
            ApplyCooldownModifier(s);
    }

    private void ApplyCooldownModifier(SourceInstance s)
    {
        if (s.HasCooldown)
            s.CooldownTicks = ModifierPipeline.ModifiedCooldown(s.Def.CooldownTicks!.Value, ModifierPipeline.Sum(this, "CooldownPercent", s));
    }

    public int Tick { get; private set; }
    public BattleResult Result { get; private set; } = BattleResult.Ongoing;
    public Combatant Player { get; }
    public Combatant Enemy { get; }
    public SourceDefinition EnemyDefinition { get; }
    public IReadOnlyList<BattleEvent> Events => _events;
    public Rng Rng { get; }
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

    internal SourceInstance AddSource(Combatant owner, SourceDefinition def, int? slot, int level)
    {
        var source = new SourceInstance(_nextId++, def, owner.Side, slot, level);
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
        ApplyCommands();
        TickStatuses();
        AdvanceCooldowns();
        FireCompletedSources();
        ApplyFatigue();
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
            if (s.HasCooldown && !s.Ready && s.LastFiredTick != Tick)
                s.Progress = Math.Min(s.FullProgress, s.Progress + CooldownSpeed(s));
            if (s.FreezeTicks > 0) s.FreezeTicks--;
            if (s.HasteTicks > 0) s.HasteTicks--;
            if (s.SlowTicks > 0) s.SlowTicks--;
        }
    }

    public const int MinSpeed = 10;

    /// <summary>Progress gained this tick: 100 normally, x2 hasted, x0.5 slowed (both cancel), -10 per chill stack, at least 10; 0 when frozen.</summary>
    public int CooldownSpeed(SourceInstance s)
    {
        var owner = Get(s.Owner);
        if (s.FreezeTicks > 0 || owner.IsFrozenAt(Tick)) return 0;
        int speed = SourceInstance.ProgressPerTick;
        bool haste = s.HasteTicks > 0, slow = s.SlowTicks > 0;
        if (haste && !slow) speed *= 2;
        else if (slow && !haste) speed /= 2;
        speed -= ChillRule.SlowPerStack * owner.Statuses.Chill;
        return Math.Max(MinSpeed, speed);
    }

    /// <summary>Burn, poison and chill rules, in a fixed order.</summary>
    public static readonly IReadOnlyDictionary<StatusType, IStatusRule> StatusRules = new SortedDictionary<StatusType, IStatusRule>
    {
        [StatusType.Burn] = new BurnRule(),
        [StatusType.Poison] = new PoisonRule(),
        [StatusType.Chill] = new ChillRule(),
    };

    /// <summary>Adds burn, poison or chill stacks to a combatant.</summary>
    public void ApplyStatus(Side target, StatusType status, int amount, SourceInstance? source, int depth)
    {
        if (!status.IsCombatantStatus()) throw new ArgumentException($"{status} goes on items; use ApplyItemStatus");
        amount = Modify(status + "Applied", amount, source);
        if (amount <= 0) return;
        var c = Get(target);
        Emit(new StatusApplied(Tick, target, null, status, amount, source?.Id), depth, source);
        StatusRules[status].OnApply(this, c, amount, source, depth);
    }

    /// <summary>Sets freeze, haste or slow on one item or ability for <paramref name="ticks"/> ticks (longest wins).</summary>
    public void ApplyItemStatus(SourceInstance target, StatusType status, int ticks, SourceInstance? source, int depth)
    {
        if (status.IsCombatantStatus()) throw new ArgumentException($"{status} goes on combatants; use ApplyStatus");
        if (ticks <= 0) return;
        switch (status)
        {
            case StatusType.Freeze: target.FreezeTicks = Math.Max(target.FreezeTicks, ticks); break;
            case StatusType.Haste: target.HasteTicks = Math.Max(target.HasteTicks, ticks); break;
            case StatusType.Slow: target.SlowTicks = Math.Max(target.SlowTicks, ticks); break;
        }
        Emit(new StatusApplied(Tick, null, target.Id, status, ticks, source?.Id), depth, source);
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
            var due = AllSources.FirstOrDefault(s => s.HasCooldown && !s.Ready && s.Progress >= s.FullProgress
                && (s.Def.Activation == Activation.Active || s.LastFiredTick != Tick));
            if (due is null) return;
            if (due.Def.Activation == Activation.Active)
            {
                due.Ready = true;
                Emit(new ItemReady(Tick, due.Id), 0, due);
                continue;
            }
            due.Progress = 0;
            Fire(due, 0, null);
        }
    }

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

    public void Charge(SourceInstance target, int ticks, SourceInstance? source, int depth)
    {
        if (!target.HasCooldown || target.Ready || ticks <= 0) return;
        target.Progress = Math.Min(target.FullProgress, target.Progress + ticks * SourceInstance.ProgressPerTick);
        Emit(new Charged(Tick, target.Id, ticks, source?.Id), depth, source);
    }

    public void ResetCooldown(SourceInstance target, SourceInstance? source, int depth)
    {
        if (!target.HasCooldown || target.Ready) return;
        target.Progress = target.FullProgress;
        Emit(new CooldownReset(Tick, target.Id, source?.Id), depth, source);
    }

    public void ModifyStat(SourceInstance target, string stat, int amount, SourceInstance? source, int depth)
    {
        target.AddBonus(stat, amount);
        Emit(new StatModified(Tick, target.Id, stat, amount, source?.Id), depth, source);
    }

    private void ApplyFatigue()
    {
        if (Tick < FatigueStartTick || (Tick - FatigueStartTick) % FatigueInterval != 0) return;
        int amount = (Tick - FatigueStartTick) / FatigueInterval + 1;
        DealDamage(Side.Player, amount, DamageKind.Fatigue, null, 0, ignoreShield: true);
        DealDamage(Side.Enemy, amount, DamageKind.Fatigue, null, 0, ignoreShield: true);
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
                var added = AddSource(Enemy, Require(id, SourceKind.EnemyAbility), null, 1);
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
        else if (Tick >= MaxTicks) Result = BattleResult.Draw;
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
    }

    /// <summary>Runs one ability because its trigger matched. Counts as a use and lets others react in turn.</summary>
    public void React(SourceInstance source, AbilityDefinition ability, int depth)
    {
        source.UseCount++;
        var fired = new SourceFired(Tick, source.Id, source.Owner, null, Reaction: true);
        _events.Add(fired);
        RunEffects(ability, source, null, depth);
        Dispatch(fired, depth, source);
    }

    public void RunEffects(AbilityDefinition ability, SourceInstance source, SourceInstance? chosenTarget, int depth)
    {
        var ctx = new EffectContext(this, source, chosenTarget, depth);
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
        foreach (var source in AllSources.ToList())
        {
            foreach (var ability in source.Def.Abilities)
            {
                var trigger = ability.Trigger;
                if (trigger is OnCooldownTrigger or OnActivateTrigger) continue;
                if (source == producer && !trigger.AllowSelf) continue;
                if (!trigger.Matches(e, source, this)) continue;
                if (depth + 1 > MaxChainDepth)
                {
                    _events.Add(new ChainCapped(Tick, source.Id));
                    continue;
                }
                React(source, ability, depth + 1);
            }
        }
    }

    public void DealDamage(Side target, int amount, DamageKind kind, SourceInstance? source, int depth, bool ignoreShield = false)
    {
        if (kind == DamageKind.Direct) amount = Modify("DamageDealt", amount, source);
        if (amount <= 0) return;
        int shieldDamage = ignoreShield ? 0 : Math.Min(Get(target).Shield, amount);
        ApplyRawDamage(target, amount - shieldDamage, shieldDamage, kind, source, depth);
    }

    /// <summary>Removes exactly the given HP and shield, for statuses with their own shield rules.</summary>
    public void ApplyRawDamage(Side target, int hpDamage, int shieldDamage, DamageKind kind, SourceInstance? source, int depth)
    {
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
        int healed = Math.Min(Modify("HealGained", amount, source), c.MaxHp - c.Hp);
        if (healed <= 0) return;
        c.Hp += healed;
        Emit(new Healed(Tick, target, healed, source?.Id), depth, source);
    }
}
