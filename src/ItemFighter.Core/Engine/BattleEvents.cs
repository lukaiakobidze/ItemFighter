namespace ItemFighter.Core.Engine;

public enum DamageKind { Direct, Burn, Poison }

/// <summary>Something that happened during a battle. The frontend animates from these, in order.</summary>
public abstract record BattleEvent(int Tick);

public sealed record FightStarted(int Tick) : BattleEvent(Tick);

/// <param name="Reaction">True when the source fired because a trigger matched, not from its cooldown or a click.</param>
public sealed record SourceFired(int Tick, int SourceId, Side Owner, int? TargetSourceId, bool Reaction) : BattleEvent(Tick);

public sealed record DamageDealt(int Tick, Side Target, int HpDamage, int ShieldDamage, DamageKind Kind, int? SourceId) : BattleEvent(Tick)
{
    public int Total => HpDamage + ShieldDamage;
}

public sealed record ShieldGained(int Tick, Side Target, int Amount, int? SourceId) : BattleEvent(Tick);

public sealed record Healed(int Tick, Side Target, int Amount, int? SourceId) : BattleEvent(Tick);

public sealed record ChainCapped(int Tick, int SourceId) : BattleEvent(Tick);

public sealed record BattleEnded(int Tick, BattleResult Result) : BattleEvent(Tick);

/// <summary>A status was applied to a combatant (TargetSide) or to one item/ability (TargetSourceId).</summary>
public sealed record StatusApplied(int Tick, Side? TargetSide, int? TargetSourceId, StatusType Status, int Amount, int? SourceId) : BattleEvent(Tick);

public sealed record PoisonBurst(int Tick, Side Target, int Damage) : BattleEvent(Tick);

public sealed record ChillFroze(int Tick, Side Target) : BattleEvent(Tick);

/// <summary>An active item finished its cooldown and can be clicked.</summary>
public sealed record ItemReady(int Tick, int SourceId) : BattleEvent(Tick);

public sealed record CommandRejected(int Tick, int SourceId, string Reason) : BattleEvent(Tick);

public sealed record Charged(int Tick, int TargetSourceId, int Ticks, int? SourceId) : BattleEvent(Tick);

/// <summary>Slow: time added to a cooldown.</summary>
public sealed record Delayed(int Tick, int TargetSourceId, int Ticks, int? SourceId) : BattleEvent(Tick);

public sealed record CooldownReset(int Tick, int TargetSourceId, int? SourceId) : BattleEvent(Tick);

public sealed record StatModified(int Tick, int TargetSourceId, string Stat, int Amount, int? SourceId) : BattleEvent(Tick);
public sealed record CooldownChanged(int Tick, int TargetSourceId, int FromTicks, int ToTicks, int? SourceId) : BattleEvent(Tick);

/// <summary>A boss crossed an HP threshold; its new abilities follow as <see cref="SourceAdded"/>.</summary>
public sealed record PhaseStarted(int Tick, int Index) : BattleEvent(Tick);

public sealed record SourceAdded(int Tick, int SourceId, Side Owner) : BattleEvent(Tick);
