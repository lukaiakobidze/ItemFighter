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

/// <summary>Fast and anti-armour: ticks every 0.5s for its stacks, loses a stack each tick, double damage to shield (numbers in rules.json).</summary>
public sealed class BurnRule : IStatusRule
{
    public StatusType Type => StatusType.Burn;

    public void OnApply(Battle battle, Combatant target, int amount, SourceInstance? source, int depth) =>
        target.Statuses.Burn += amount;

    public void OnTick(Battle battle, Combatant target)
    {
        var rules = battle.Rules;
        int burn = target.Statuses.Burn;
        if (burn <= 0 || battle.Tick % rules.BurnTickInterval != 0) return;
        int multiplier = rules.BurnShieldMultiplier;
        int shieldDamage = Math.Min(target.Shield, multiplier * burn);
        int burnSpentOnShield = (shieldDamage + multiplier - 1) / multiplier;
        int hpDamage = burn - burnSpentOnShield;
        target.Statuses.Burn = Math.Max(0, burn - rules.BurnLossPerTick);
        battle.ApplyRawDamage(target.Side, hpDamage, shieldDamage, DamageKind.Burn, null, 0);
    }
}

/// <summary>Builds up silently; at the threshold it bursts for 3 per stack, ignoring shield, and resets (numbers in rules.json).</summary>
public sealed class PoisonRule : IStatusRule
{
    public StatusType Type => StatusType.Poison;

    public void OnApply(Battle battle, Combatant target, int amount, SourceInstance? source, int depth)
    {
        target.Statuses.Poison += amount;
        int threshold = Math.Max(1, battle.Modify("PoisonThreshold", battle.Rules.PoisonThreshold, source));
        if (target.Statuses.Poison < threshold) return;
        int damage = target.Statuses.Poison * battle.Rules.PoisonBurstPerStack;
        target.Statuses.Poison = 0;
        battle.Emit(new PoisonBurst(battle.Tick, target.Side, damage), depth, source);
        battle.ApplyRawDamage(target.Side, damage, 0, DamageKind.Poison, source, depth);
    }

    public void OnTick(Battle battle, Combatant target) { }
}

/// <summary>
/// Each stack makes all the target's cooldowns 5% longer while it lasts. New stacks refresh the 5s timer;
/// 5 stacks freeze the target for 1s (numbers in rules.json).
/// </summary>
public sealed class ChillRule : IStatusRule
{
    public StatusType Type => StatusType.Chill;

    public void OnApply(Battle battle, Combatant target, int amount, SourceInstance? source, int depth)
    {
        var rules = battle.Rules;
        target.Statuses.Chill += amount;
        target.Statuses.ChillExpiresAtTick = battle.Tick + rules.ChillTicks;
        if (target.Statuses.Chill < rules.ChillFreezeAtStacks) return;
        target.Statuses.Chill = 0;
        target.Statuses.FrozenUntilTick = battle.Tick + rules.FreezeTicks;
        battle.Emit(new ChillFroze(battle.Tick, target.Side), depth, source);
    }

    public void OnTick(Battle battle, Combatant target)
    {
        if (target.Statuses.Chill > 0 && battle.Tick >= target.Statuses.ChillExpiresAtTick)
            target.Statuses.Chill = 0;
    }
}
