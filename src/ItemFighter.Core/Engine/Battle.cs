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
        AdvanceCooldowns();
        FireCompletedSources();
        ApplyFatigue();
        ResolveResult();
    }

    private void AdvanceCooldowns()
    {
        foreach (var s in AllSources.ToList())
        {
            if (!s.HasCooldown || s.Ready) continue;
            s.Progress = Math.Min(s.FullProgress, s.Progress + SourceInstance.ProgressPerTick);
        }
    }

    private void FireCompletedSources()
    {
        // Loop because a firing can complete another source's cooldown in the same tick (e.g. charge).
        // Each source fires at most once per tick.
        while (true)
        {
            var due = AllSources.FirstOrDefault(s => s.HasCooldown && !s.Ready && s.Progress >= s.FullProgress && s.LastFiredTick != Tick);
            if (due is null) return;
            due.Progress = 0;
            Fire(due, 0, null, reaction: false);
        }
    }

    private void ApplyFatigue()
    {
        if (Tick < FatigueStartTick || (Tick - FatigueStartTick) % FatigueInterval != 0) return;
        int amount = (Tick - FatigueStartTick) / FatigueInterval + 1;
        DealDamage(Side.Player, amount, DamageKind.Fatigue, null, 0, ignoreShield: true);
        DealDamage(Side.Enemy, amount, DamageKind.Fatigue, null, 0, ignoreShield: true);
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
    public void Fire(SourceInstance source, int depth, SourceInstance? chosenTarget, bool reaction)
    {
        source.UseCount++;
        source.LastFiredTick = Tick;
        var fired = new SourceFired(Tick, source.Id, source.Owner, chosenTarget?.Id, reaction);
        _events.Add(fired);
        if (!reaction)
        {
            foreach (var ability in source.Def.Abilities)
                if (ability.Trigger is OnCooldownTrigger or OnActivateTrigger)
                    RunEffects(ability, source, chosenTarget, depth);
        }
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

    private void Dispatch(BattleEvent e, int depth, SourceInstance? producer)
    {
        // Filled in by the trigger system.
    }

    public void DealDamage(Side target, int amount, DamageKind kind, SourceInstance? source, int depth, bool ignoreShield = false)
    {
        if (amount <= 0) return;
        var c = Get(target);
        int shieldDamage = ignoreShield ? 0 : Math.Min(c.Shield, amount);
        c.Shield -= shieldDamage;
        int hpDamage = amount - shieldDamage;
        c.Hp -= hpDamage;
        Emit(new DamageDealt(Tick, target, hpDamage, shieldDamage, kind, source?.Id), depth, source);
    }

    public void GainShield(Side target, int amount, SourceInstance? source, int depth)
    {
        if (amount <= 0) return;
        Get(target).Shield += amount;
        Emit(new ShieldGained(Tick, target, amount, source?.Id), depth, source);
    }

    public void Heal(Side target, int amount, SourceInstance? source, int depth)
    {
        var c = Get(target);
        int healed = Math.Min(amount, c.MaxHp - c.Hp);
        if (healed <= 0) return;
        c.Hp += healed;
        Emit(new Healed(Tick, target, healed, source?.Id), depth, source);
    }
}
