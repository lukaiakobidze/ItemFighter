using ItemFighter.Core.Engine.Effects;
using ItemFighter.Core.Engine.Triggers;

namespace ItemFighter.Core.Content;

public enum SourceKind { Item, Passive, Character, EnemyAbility, Enemy }

public enum Activation { Passive, Active, None }

public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

/// <summary>One thing that "does something": an item, passive, character, enemy or enemy ability. Loaded from JSON.</summary>
public sealed class SourceDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required SourceKind Kind { get; init; }
    public string File { get; init; } = "";
    public Rarity Rarity { get; init; } = Rarity.Common;
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Cooldown in ticks, or null when the source never fires on its own.</summary>
    public int? CooldownTicks { get; init; }
    public Activation Activation { get; init; } = Activation.None;
    public bool RequiresTarget { get; init; }
    public string TargetFilter { get; init; } = "OwnItem";

    /// <summary>Per-level values, each exactly 4 long, referenced from effects as "{name}".</summary>
    public IReadOnlyDictionary<string, int[]> Levels { get; init; } = new Dictionary<string, int[]>();
    public IReadOnlyList<AbilityDefinition> Abilities { get; init; } = [];
    public IReadOnlyList<ModifierDefinition> Modifiers { get; init; } = [];
    public string? Script { get; init; }

    /// <summary>Hand-written tooltip text; replaces the generated one. "{key}" shows a level value, "\n" starts a new line.</summary>
    public string? Description { get; init; }

    public int MaxHp { get; init; }

    public IReadOnlyList<string> AbilityIds { get; init; } = [];
    public IReadOnlyList<PhaseDefinition> Phases { get; init; } = [];
    public IReadOnlyList<string> StartingItems { get; init; } = [];

    /// <summary>Enemies only, never shown: 1 to 10. Harder enemies are met later in a run.</summary>
    public int Difficulty { get; init; } = 1;

    /// <summary>Items only: gold earned for each fight won with this item on the board (run loop, not the fight).</summary>
    public ValueRef? GoldOnWin { get; init; }

    /// <summary>An enemy ability with no cooldown (always on or reacting), which the player can also take as a passive.</summary>
    public bool IsPassiveAbility => Kind == SourceKind.EnemyAbility && CooldownTicks is null;

    /// <summary>Shown as a passive in fights: a passive from passives/, or an enemy ability without a cooldown.</summary>
    public bool IsPassive => Kind == SourceKind.Passive || IsPassiveAbility;

    public bool HasTag(string tag) => Tags.Contains(tag, StringComparer.Ordinal);

    public override string ToString() => Id;
}

public sealed record AbilityDefinition(ITrigger Trigger, IReadOnlyList<IEffect> Effects);

public sealed record PhaseDefinition(int HpBelowPercent, IReadOnlyList<string> AddAbilityIds);

public sealed record ModifierFilter(SourceKind? Kind, string? Tag);

/// <summary>A standing bonus such as "+5 shield gained by items". Summed by the modifier pipeline.</summary>
public sealed record ModifierDefinition(string Stat, int Amount, ModifierFilter Filter)
{
    public static readonly IReadOnlySet<string> KnownStats = new HashSet<string>(StringComparer.Ordinal)
    {
        "ShieldGained", "DamageDealt", "HealGained", "BurnApplied", "PoisonApplied", "ChillApplied",
        "PainApplied", "NailApplied", "PowerApplied", "NailDamage", "PoisonThreshold", "CooldownPercent", "DamageTaken",
    };
}
