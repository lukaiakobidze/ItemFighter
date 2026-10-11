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
