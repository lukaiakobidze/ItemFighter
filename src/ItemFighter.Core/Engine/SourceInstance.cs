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
    public int UseCount { get; internal set; }
    internal int LastFiredTick { get; set; } = -1;

    public bool IsItem => Slot is not null;

    public override string ToString() => $"{Def.Id}#{Id}";
}
