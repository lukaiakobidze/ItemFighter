using ItemFighter.Core.Content;

namespace ItemFighter.Core.Engine;

public enum TargetKind
{
    Self,
    Enemy,
    SelfItem,
    AdjacentItems,
    LeftItem,
    RightItem,
    ItemsWithTag,
    AllOwn,
    RandomOwnItem,
    RandomEnemyAbility,
    Chosen,
    TriggeringItem,
}

/// <summary>Who an effect hits. Self/Enemy are combatants; everything else resolves to sources (items or enemy abilities).</summary>
public readonly record struct TargetSpec(TargetKind Kind, string? Tag = null)
{
    public static readonly TargetSpec Self = new(TargetKind.Self);
    public static readonly TargetSpec Enemy = new(TargetKind.Enemy);
    public static readonly TargetSpec SelfItem = new(TargetKind.SelfItem);
    public static readonly TargetSpec Chosen = new(TargetKind.Chosen);

    public bool IsCombatant => Kind is TargetKind.Self or TargetKind.Enemy;

    public static TargetSpec Parse(string s)
    {
        if (s.StartsWith("ItemsWithTag:", StringComparison.Ordinal))
            return new TargetSpec(TargetKind.ItemsWithTag, s["ItemsWithTag:".Length..]);
        if (Enum.TryParse<TargetKind>(s, out var kind) && Enum.IsDefined(kind) && kind != TargetKind.ItemsWithTag)
            return new TargetSpec(kind);
        throw new ContentException($"unknown target '{s}'");
    }

    public override string ToString() => Tag is null ? Kind.ToString() : $"{Kind}:{Tag}";
}

public static class Targeting
{
    /// <summary>The sources on a side that item-targeting effects can hit: board items, or an enemy's cooldown abilities.</summary>
    public static IEnumerable<SourceInstance> Targetable(Combatant c) =>
        c.Side == Side.Player ? c.Items : c.Sources.Where(s => s.HasCooldown);

    /// <summary>Resolves an item target to sources, in canonical order. Random picks use the battle's combat rng.</summary>
    public static IReadOnlyList<SourceInstance> ResolveItems(TargetSpec spec, Effects.EffectContext ctx)
    {
        var (battle, source, chosen) = (ctx.Battle, ctx.Source, ctx.ChosenTarget);
        var own = battle.Get(source.Owner);
        switch (spec.Kind)
        {
            case TargetKind.SelfItem:
                return [source];
            case TargetKind.AdjacentItems:
                return source.Slot is int s ? own.Items.Where(i => i.Slot == s - 1 || i.Slot == s + 1).ToList() : [];
            case TargetKind.LeftItem:
                return source.Slot is int l ? own.Items.Where(i => i.Slot == l - 1).ToList() : [];
            case TargetKind.RightItem:
                return source.Slot is int r ? own.Items.Where(i => i.Slot == r + 1).ToList() : [];
            case TargetKind.ItemsWithTag:
                return Targetable(own).Where(i => i.Def.HasTag(spec.Tag!)).ToList();
            case TargetKind.AllOwn:
                return Targetable(own).ToList();
            case TargetKind.RandomOwnItem:
                return PickRandom(Targetable(own).Where(i => i != source).ToList(), battle);
            case TargetKind.RandomEnemyAbility:
                return PickRandom(Targetable(battle.OpponentOf(source.Owner)).Where(i => i.HasCooldown).ToList(), battle);
            case TargetKind.Chosen:
                return chosen is null ? [] : [chosen];
            case TargetKind.TriggeringItem:
                return ctx.Trigger is null ? [] : [ctx.Trigger];
            default:
                throw new InvalidOperationException($"{spec} is a combatant target, not an item target");
        }
    }

    private static IReadOnlyList<SourceInstance> PickRandom(List<SourceInstance> candidates, Battle battle) =>
        candidates.Count == 0 ? [] : [candidates[battle.Rng.NextInt(candidates.Count)]];
}
