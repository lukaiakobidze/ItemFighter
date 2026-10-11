namespace ItemFighter.Core.Engine;

public enum StatusType { Burn, Poison, Chill, Freeze, Haste }

public static class StatusTypes
{
    /// <summary>Burn, poison and chill stack on a combatant; freeze and haste are timers on one item or ability.</summary>
    public static bool IsCombatantStatus(this StatusType s) => s is StatusType.Burn or StatusType.Poison or StatusType.Chill;
}

/// <summary>Stacking statuses on a combatant.</summary>
public sealed class StatusSet
{
    public int Burn { get; set; }
    public int Poison { get; set; }
    public int Chill { get; set; }
    public int ChillExpiresAtTick { get; set; }

    /// <summary>The whole combatant is frozen through this tick (from 5 chill).</summary>
    public int FrozenUntilTick { get; set; } = -1;
}

/// <summary>
/// The rules of one combatant status. Each status is its own class so new ones (water, shock)
/// and reactions between them can be added without touching the others.
/// </summary>
public interface IStatusRule
{
    StatusType Type { get; }
    void OnApply(Battle battle, Combatant target, int amount, SourceInstance? source, int depth);
    void OnTick(Battle battle, Combatant target);
}

/// <summary>Fast and anti-armour: ticks every 0.5s for its stacks, loses a stack each tick, double damage to shield.</summary>
public sealed class BurnRule : IStatusRule
{
    public const int Interval = 5;

    public StatusType Type => StatusType.Burn;

    public void OnApply(Battle battle, Combatant target, int amount, SourceInstance? source, int depth) =>
        target.Statuses.Burn += amount;

    public void OnTick(Battle battle, Combatant target)
    {
        int burn = target.Statuses.Burn;
        if (burn <= 0 || battle.Tick % Interval != 0) return;
        int shieldDamage = Math.Min(target.Shield, 2 * burn);
        int burnSpentOnShield = (shieldDamage + 1) / 2;
        int hpDamage = burn - burnSpentOnShield;
        target.Statuses.Burn = burn - 1;
        battle.ApplyRawDamage(target.Side, hpDamage, shieldDamage, DamageKind.Burn, null, 0);
    }
}

/// <summary>Builds up silently; at the threshold it bursts for 3 per stack, ignoring shield, and resets.</summary>
public sealed class PoisonRule : IStatusRule
{
    public const int DefaultThreshold = 10;
    public const int BurstPerStack = 3;

    public StatusType Type => StatusType.Poison;

    public void OnApply(Battle battle, Combatant target, int amount, SourceInstance? source, int depth)
    {
        target.Statuses.Poison += amount;
        int threshold = Math.Max(1, battle.Modify("PoisonThreshold", DefaultThreshold, source));
        if (target.Statuses.Poison < threshold) return;
        int damage = target.Statuses.Poison * BurstPerStack;
        target.Statuses.Poison = 0;
        battle.Emit(new PoisonBurst(battle.Tick, target.Side, damage), depth, source);
        battle.ApplyRawDamage(target.Side, damage, 0, DamageKind.Poison, source, depth);
    }

    public void OnTick(Battle battle, Combatant target) { }
}

/// <summary>
/// Each stack makes all the target's cooldowns 5% longer while it lasts. New stacks refresh the 5s timer;
/// 5 stacks freeze the target for 1s.
/// </summary>
public sealed class ChillRule : IStatusRule
{
    public const int Duration = 50;
    public const int CooldownPercentPerStack = 5;
    public const int FreezeAt = 5;
    public const int FreezeTicks = 10;

    public StatusType Type => StatusType.Chill;

    public void OnApply(Battle battle, Combatant target, int amount, SourceInstance? source, int depth)
    {
        target.Statuses.Chill += amount;
        target.Statuses.ChillExpiresAtTick = battle.Tick + Duration;
        if (target.Statuses.Chill < FreezeAt) return;
        target.Statuses.Chill = 0;
        target.Statuses.FrozenUntilTick = battle.Tick + FreezeTicks;
        battle.Emit(new ChillFroze(battle.Tick, target.Side), depth, source);
    }

    public void OnTick(Battle battle, Combatant target)
    {
        if (target.Statuses.Chill > 0 && battle.Tick >= target.Statuses.ChillExpiresAtTick)
            target.Statuses.Chill = 0;
    }
}
