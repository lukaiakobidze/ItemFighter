using ItemFighter.Core.Content;

namespace ItemFighter.Core.Engine;

/// <summary>
/// Sums standing modifiers ("+5 shield gained by items") from every source on the producer's side
/// whose filter matches the producer.
/// </summary>
public static class ModifierPipeline
{
    public static int Sum(Battle battle, string stat, SourceInstance producer)
    {
        int total = 0;
        var side = battle.Get(producer.Owner);
        foreach (var holder in side.Sources)
            foreach (var m in holder.Def.Modifiers)
                if (m.Stat == stat && Matches(m.Filter, producer, battle))
                    total += m.PerItemTag is string tag ? m.Amount * Targeting.Targetable(side).Count(i => i.Def.HasTag(tag)) : m.Amount;
        return total;
    }

    private static bool Matches(ModifierFilter filter, SourceInstance producer, Battle battle) =>
        (filter.Kind is null || filter.Kind == producer.Def.Kind) && (filter.Tag is null || producer.Def.HasTag(filter.Tag))
        && (filter.Slot is null || Targeting.SlotNumber(battle, producer) == filter.Slot);

    /// <summary>Cooldown after CooldownPercent modifiers: ticks * (100 + sum) / 100, at least 1.</summary>
    public static int ModifiedCooldown(int ticks, int percent) => Math.Max(1, ticks * (100 + percent) / 100);
}
