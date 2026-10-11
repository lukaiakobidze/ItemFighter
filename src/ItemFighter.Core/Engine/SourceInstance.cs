using ItemFighter.Core.Content;

namespace ItemFighter.Core.Engine;

/// <summary>Runtime state of one item, passive, character or enemy ability in a battle.</summary>
public sealed class SourceInstance
{
    /// <summary>Progress needed per tick of cooldown at normal speed.</summary>
    public const int ProgressPerTick = 100;

    public static readonly Comparer<SourceInstance> CanonicalOrder = Comparer<SourceInstance>.Create((a, b) =>
    {
        int c = a.Owner.CompareTo(b.Owner);
        if (c != 0) return c;
        c = (a.Slot ?? int.MaxValue).CompareTo(b.Slot ?? int.MaxValue);
        return c != 0 ? c : a.Id.CompareTo(b.Id);
    });

    public SourceInstance(int id, SourceDefinition def, Side owner, int? slot, int level)
    {
        Id = id;
        Def = def;
        Owner = owner;
        Slot = slot;
        Level = level;
        CooldownTicks = def.CooldownTicks ?? 0;
    }

    public int Id { get; }
    public SourceDefinition Def { get; }
    public Side Owner { get; }
    public int? Slot { get; }
    public int Level { get; }

    /// <summary>Cooldown after modifiers; 0 when the source never fires on its own.</summary>
    public int CooldownTicks { get; internal set; }

    /// <summary>Cooldown progress; the source is ready at <see cref="FullProgress"/>.</summary>
    public int Progress { get; set; }
    public int FullProgress => CooldownTicks * ProgressPerTick;
    public bool HasCooldown => CooldownTicks > 0;

    /// <summary>For active items: cooldown finished, waiting for the player to click.</summary>
    public bool Ready { get; set; }
    /// <summary>Every fire, including reactions.</summary>
    public int UseCount { get; internal set; }

    /// <summary>Fires from its own cooldown or a click (not reactions).</summary>
    public int CycleCount { get; internal set; }

    /// <summary>Once-per-battle triggers that have already gone off for this source.</summary>
    public HashSet<object> FiredOnce { get; } = new(ReferenceEqualityComparer.Instance);
    internal int LastFiredTick { get; set; } = -1;

    public bool IsItem => Slot is not null;

    public ISourceScript? Script { get; internal set; }

    private readonly Dictionary<string, int> _bonus = new(StringComparer.Ordinal);

    /// <summary>Extra Damage/Shield/Heal/Burn/Poison/Chill this source adds for the rest of the fight (from ModifyStat).</summary>
    public int BonusFor(string stat) => _bonus.GetValueOrDefault(stat);

    internal void AddBonus(string stat, int amount) => _bonus[stat] = BonusFor(stat) + amount;

    public int FreezeTicks { get; set; }
    public int HasteTicks { get; set; }
    public int SlowTicks { get; set; }

    public override string ToString() => $"{Def.Id}#{Id}";
}
