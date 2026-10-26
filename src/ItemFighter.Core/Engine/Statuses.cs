using ItemFighter.Core.Content;

namespace ItemFighter.Core.Engine;

public enum StatusType { Burn, Poison, Chill, Freeze, Haste, Pain, Nail, Power, Vulnerable, Heat, NoHeatGain }

public static class StatusTypes
{
    /// <summary>Burn, poison, chill, pain, nail, power, vulnerable and heat sit on a combatant; freeze and haste are timers on one item or ability.</summary>
    public static bool IsCombatantStatus(this StatusType s) => s is StatusType.Burn or StatusType.Poison or StatusType.Chill or StatusType.Pain or StatusType.Nail or StatusType.Power or StatusType.Vulnerable or StatusType.Heat or StatusType.NoHeatGain;

    /// <summary>Combatant statuses applied as a duration in seconds rather than as stacks.</summary>
    public static bool IsTimed(this StatusType s) => s is StatusType.Vulnerable;
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
    public int Power { get; set; }
    /// <summary>Ticks of Vulnerable left (0 = not vulnerable), and how long the latest application was, for the UI.</summary>
    public int Vulnerable { get; set; }
    public int VulnerableFull { get; set; }
    /// <summary>Heat, 0 up to rules overheatAt, where it overheats and goes back to 0.</summary>
    public int Heat { get; set; }
    /// <summary>1 while No Heat Gain is on (Stabilizer, Luka 2026-10-26): Heat can't go up. On or off, never more.</summary>
    public int NoHeatGain { get; set; }

    /// <summary>Stacks of a combatant status.</summary>
    public int Get(StatusType s) => s switch
    {
        StatusType.Burn => Burn,
        StatusType.Poison => Poison,
        StatusType.Chill => Chill,
        StatusType.Pain => Pain,
        StatusType.Nail => Nail,
        StatusType.Power => Power,
        StatusType.Vulnerable => Vulnerable,
        StatusType.Heat => Heat,
        StatusType.NoHeatGain => NoHeatGain,
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
            case StatusType.Power: Power = 0; break;
            case StatusType.Vulnerable: Vulnerable = 0; break;
            case StatusType.Heat: Heat = 0; break;
            case StatusType.NoHeatGain: NoHeatGain = 0; break;
        }
    }

    /// <summary>The whole combatant is frozen through this tick (from 5 chill).</summary>
    public int FrozenUntilTick { get; set; } = -1;

    /// <summary>Every item and ability is stalled through this tick (from overheating).</summary>
    public int StalledUntilTick { get; set; } = -1;
    /// <summary>How long the latest stall was, in ticks, for the UI.</summary>
    public int StallFull { get; set; }
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

/// <summary>Builds up silently; each time it reaches the threshold it bursts for a flat poisonBurstDamage (shield blocks it like any hit) and keeps what went past the threshold (numbers in rules.json).</summary>
public sealed class PoisonRule : IStatusRule
{
    public StatusType Type => StatusType.Poison;

    public void OnApply(Battle battle, Combatant target, int amount, SourceInstance? source, int depth)
    {
        target.Statuses.Poison += amount;
        int threshold = Math.Max(1, battle.Modify("PoisonThreshold", battle.Rules.PoisonThreshold, source));
        // Each full threshold bursts once for the flat burst damage; poison past it stays for the next burst.
        while (target.Statuses.Poison >= threshold && target.Hp > 0)
        {
            target.Statuses.Poison -= threshold;
            int damage = battle.Rules.PoisonBurstDamage;
            battle.Emit(new PoisonBurst(battle.Tick, target.Side, damage), depth, source);
            battle.DealDamage(target.Side, damage, DamageKind.Poison, source, depth);
        }
    }

    public void OnTick(Battle battle, Combatant target) { }
}

/// <summary>
/// Each stack makes all the target's cooldowns 5% longer while it lasts. New stacks refresh the 5s timer;
/// 5 stacks freeze the target for 1s (numbers in rules.json). At heatFreezeImmunityAt Heat or more the target
/// can't freeze (Luka 2026-10-19): chill stays at 5 and keeps slowing, and the freeze lands as soon as Heat drops below.
/// </summary>
public sealed class ChillRule : IStatusRule
{
    public StatusType Type => StatusType.Chill;

    public void OnApply(Battle battle, Combatant target, int amount, SourceInstance? source, int depth)
    {
        var rules = battle.Rules;
        target.Statuses.Chill += amount;
        target.Statuses.ChillExpiresAtTick = battle.Tick + rules.ChillTicks;
        TryFreeze(battle, target, source, depth);
    }

    public void OnTick(Battle battle, Combatant target)
    {
        if (target.Statuses.Chill > 0 && battle.Tick >= target.Statuses.ChillExpiresAtTick)
            target.Statuses.Chill = 0;
        TryFreeze(battle, target, null, 0);
    }

    private static void TryFreeze(Battle battle, Combatant target, SourceInstance? source, int depth)
    {
        var rules = battle.Rules;
        if (target.Statuses.Chill < rules.ChillFreezeAtStacks) return;
        if (target.Statuses.Heat >= rules.HeatFreezeImmunityAt)
        {
            target.Statuses.Chill = rules.ChillFreezeAtStacks;
            return;
        }
        target.Statuses.Chill = 0;
        target.Statuses.FrozenUntilTick = battle.Tick + rules.FreezeTicks;
        battle.Emit(new ChillFroze(battle.Tick, target.Side), depth, source);
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

/// <summary>Stays all fight. Each stack adds powerDamagePerStack (rules.json) to every hit its owner deals.</summary>
public sealed class PowerRule : IStatusRule
{
    public StatusType Type => StatusType.Power;

    public void OnApply(Battle battle, Combatant target, int amount, SourceInstance? source, int depth) =>
        target.Statuses.Power += amount;

    public void OnTick(Battle battle, Combatant target) { }
}

/// <summary>
/// A timer, not stacks (Luka 2026-10-17): while it lasts, every hit and pain tick the target takes does
/// vulnerableDamagePercent (rules.json) more damage; burn and poison are not boosted. A new application sets the timer to
/// the longer of what is left and the new duration.
/// </summary>
public sealed class VulnerableRule : IStatusRule
{
    public StatusType Type => StatusType.Vulnerable;

    public void OnApply(Battle battle, Combatant target, int ticks, SourceInstance? source, int depth)
    {
        if (ticks <= target.Statuses.Vulnerable) return;
        target.Statuses.Vulnerable = ticks;
        target.Statuses.VulnerableFull = ticks;
    }

    public void OnTick(Battle battle, Combatant target)
    {
        if (target.Statuses.Vulnerable > 0) target.Statuses.Vulnerable--;
    }
}

/// <summary>
/// Builds up from items that add it (Luka 2026-10-19) and cools by heatCoolingPerSecond. At overheatAt the side
/// overheats: Heat goes back to 0, it takes overheatDamage and every item and ability stalls for overheatStallSeconds
/// (numbers in rules.json). OverheatHitsEnemy modifiers send the damage to the opponent instead; OverheatStallPercent
/// modifiers lengthen or shorten the stall.
/// </summary>
public sealed class HeatRule : IStatusRule
{
    public StatusType Type => StatusType.Heat;

    public void OnApply(Battle battle, Combatant target, int amount, SourceInstance? source, int depth)
    {
        target.Statuses.Heat += amount;
        if (target.Statuses.Heat >= battle.MaxHeat(target.Side)) battle.Overheat(target.Side, source, depth);
    }

    public void OnTick(Battle battle, Combatant target)
    {
        int cooling = battle.Rules.HeatCoolingPerSecond;
        if (cooling <= 0 || target.Statuses.Heat <= 0 || battle.Tick % ContentLoader.TicksPerSecond != 0) return;
        target.Statuses.Heat = Math.Max(0, target.Statuses.Heat - cooling);
    }
}

/// <summary>On or off (Luka 2026-10-26): while on, the side can't gain Heat. Stabilizer toggles it.</summary>
public sealed class NoHeatGainRule : IStatusRule
{
    public StatusType Type => StatusType.NoHeatGain;

    public void OnApply(Battle battle, Combatant target, int amount, SourceInstance? source, int depth) =>
        target.Statuses.NoHeatGain = 1;

    public void OnTick(Battle battle, Combatant target) { }
}
