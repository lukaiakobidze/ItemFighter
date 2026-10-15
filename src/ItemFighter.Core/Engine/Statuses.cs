namespace ItemFighter.Core.Engine;

public enum StatusType { Burn, Poison, Chill, Freeze, Haste, Pain, Nail }

public static class StatusTypes
{
    /// <summary>Burn, poison, chill, pain and nail stack on a combatant; freeze and haste are timers on one item or ability.</summary>
    public static bool IsCombatantStatus(this StatusType s) => s is StatusType.Burn or StatusType.Poison or StatusType.Chill or StatusType.Pain or StatusType.Nail;
}

/// <summary>Stacking statuses on a combatant.</summary>
public sealed class StatusSet
{
    public int Burn { get; set; }
    public int Poison { get; set; }
    public int Chill { get; set; }
    public int ChillExpiresAtTick { get; set; }
    public int Pain { get; set; }
    public int Nail { get; set; }

    /// <summary>Stacks of a combatant status.</summary>
    public int Get(StatusType s) => s switch
    {
        StatusType.Burn => Burn,
        StatusType.Poison => Poison,
        StatusType.Chill => Chill,
        StatusType.Pain => Pain,
        StatusType.Nail => Nail,
        _ => 0,
    };

    /// <summary>Removes every stack of a combatant status.</summary>
    public void Clear(StatusType s)
    {
        switch (s)
        {
            case StatusType.Burn: Burn = 0; break;
            case StatusType.Poison: Poison = 0; break;
            case StatusType.Chill: Chill = 0; break;
            case StatusType.Pain: Pain = 0; break;
            case StatusType.Nail: Nail = 0; break;
        }
    }

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

/// <summary>Stays all fight. Each time the target's item or ability fires on its own or by click, the target takes damage equal to its pain.</summary>
public sealed class PainRule : IStatusRule
{
    public StatusType Type => StatusType.Pain;

    public void OnApply(Battle battle, Combatant target, int amount, SourceInstance? source, int depth) =>
        target.Statuses.Pain += amount;

    public void OnTick(Battle battle, Combatant target) { }
}

/// <summary>Does nothing on its own. Uproot pulls every nail out for nailDamage each (rules.json), plus NailDamage modifiers.</summary>
public sealed class NailRule : IStatusRule
{
    public StatusType Type => StatusType.Nail;

    public void OnApply(Battle battle, Combatant target, int amount, SourceInstance? source, int depth) =>
        target.Statuses.Nail += amount;

    public void OnTick(Battle battle, Combatant target) { }
}
