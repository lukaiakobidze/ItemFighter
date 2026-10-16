using System.Text.Json;
using System.Text.Json.Serialization;
using ItemFighter.Core.Content;

namespace ItemFighter.Core.Engine;

/// <summary>
/// The fight's own numbers (statuses, time limit), from Data/rules.json. A key missing from the file keeps the
/// default below. Times are in seconds.
/// </summary>
public sealed class CombatRules
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>A fight not won by then is a loss.</summary>
    public decimal FightTimeLimitSeconds { get; init; } = 120;

    /// <summary>Burn deals its stacks as damage this often.</summary>
    public decimal BurnTickSeconds { get; init; } = 0.5m;
    /// <summary>Stacks burn loses after each tick.</summary>
    public int BurnLossPerTick { get; init; } = 1;
    /// <summary>Burn damage to shield is multiplied by this.</summary>
    public int BurnShieldMultiplier { get; init; } = 2;

    /// <summary>Poison bursts when it reaches this many stacks (PoisonThreshold modifiers change it per source).</summary>
    public int PoisonThreshold { get; init; } = 10;
    /// <summary>A burst deals this much per stack (shield blocks it).</summary>
    public int PoisonBurstPerStack { get; init; } = 3;

    /// <summary>Chill stacks last this long; every new stack restarts the timer.</summary>
    public decimal ChillSeconds { get; init; } = 5;
    /// <summary>Each chill stack makes every cooldown this much longer.</summary>
    public int ChillCooldownPercentPerStack { get; init; } = 5;
    /// <summary>At this many chill stacks the combatant freezes and chill resets.</summary>
    public int ChillFreezeAtStacks { get; init; } = 5;
    /// <summary>How long a freeze from chill lasts.</summary>
    public decimal FreezeSeconds { get; init; } = 1;

    /// <summary>Damage per nail when nails are uprooted (NailDamage modifiers add to it).</summary>
    public int NailDamage { get; init; } = 4;

    /// <summary>Each Power stack adds this much to every hit its owner deals.</summary>
    public int PowerDamagePerStack { get; init; } = 1;

    /// <summary>Cooldowns never run slower than this percent of normal speed, however much they are chilled.</summary>
    public int SlowestCooldownSpeedPercent { get; init; } = 10;

    /// <summary>
    /// Cooldown reductions (CooldownPercent modifiers, ReduceCooldown) never take a cooldown below this. A cooldown
    /// written shorter than this in an item's file stays as written.
    /// </summary>
    public decimal MinCooldownSeconds { get; init; } = 0.5m;

    public int MinCooldownTicks => ContentLoader.SecondsToTicks(MinCooldownSeconds);

    /// <summary>A reduced cooldown, floored at the minimum (or at the base, when the base is already shorter).</summary>
    public int FloorCooldown(int baseTicks, int reducedTicks) => Math.Max(Math.Min(baseTicks, MinCooldownTicks), reducedTicks);

    public int TimeLimitTicks => ContentLoader.SecondsToTicks(FightTimeLimitSeconds);
    public int BurnTickInterval => ContentLoader.SecondsToTicks(BurnTickSeconds);
    public int ChillTicks => ContentLoader.SecondsToTicks(ChillSeconds);
    public int FreezeTicks => ContentLoader.SecondsToTicks(FreezeSeconds);

    public static CombatRules Parse(string json, out List<string> errors)
    {
        errors = [];
        CombatRules? rules;
        try
        {
            rules = JsonSerializer.Deserialize<CombatRules>(json, Json);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException)
        {
            errors.Add($"rules.json can't be read: {e.Message}");
            return new CombatRules();
        }
        if (rules is null)
        {
            errors.Add("rules.json is empty");
            return new CombatRules();
        }
        rules.Validate(errors);
        return errors.Count == 0 ? rules : new CombatRules();
    }

    private void Validate(List<string> errors)
    {
        void Check(bool ok, string message)
        {
            if (!ok) errors.Add(message);
        }
        decimal max = ContentLoader.MaxCooldownSeconds;
        Check(new[] { FightTimeLimitSeconds, MinCooldownSeconds, BurnTickSeconds, ChillSeconds, FreezeSeconds }.All(t => t > 0 && t <= max),
            $"times must be above 0 and at most {max} seconds");
        Check(BurnLossPerTick >= 0 && BurnShieldMultiplier >= 1, "'burnLossPerTick' can't be negative and 'burnShieldMultiplier' must be at least 1");
        Check(PoisonThreshold >= 1 && PoisonBurstPerStack >= 0, "'poisonThreshold' must be at least 1 and 'poisonBurstPerStack' can't be negative");
        Check(ChillCooldownPercentPerStack >= 0 && ChillFreezeAtStacks >= 1, "'chillCooldownPercentPerStack' can't be negative and 'chillFreezeAtStacks' must be at least 1");
        Check(NailDamage >= 0 && PowerDamagePerStack >= 0, "'nailDamage' and 'powerDamagePerStack' can't be negative");
        Check(SlowestCooldownSpeedPercent is >= 1 and <= 100, "'slowestCooldownSpeedPercent' must be 1 to 100");
    }
}
