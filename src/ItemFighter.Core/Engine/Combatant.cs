namespace ItemFighter.Core.Engine;

/// <summary>One side of a fight: HP, shield, statuses and its sources.</summary>
public sealed class Combatant
{
    private readonly List<SourceInstance> _sources = [];

    public Combatant(Side side, int maxHp)
    {
        Side = side;
        MaxHp = maxHp;
        Hp = maxHp;
    }

    public Side Side { get; }
    public int MaxHp { get; }
    public int Hp { get; set; }
    public int Shield { get; set; }
    public StatusSet Statuses { get; } = new();

    public bool IsFrozenAt(int tick) => tick <= Statuses.FrozenUntilTick;

    public bool IsStalledAt(int tick) => tick <= Statuses.StalledUntilTick;

    /// <summary>Frozen or stalled: nothing charges, fires or can be clicked.</summary>
    public bool IsHaltedAt(int tick) => IsFrozenAt(tick) || IsStalledAt(tick);

    /// <summary>All sources in canonical order: board items by slot, then slotless sources by id.</summary>
    public IReadOnlyList<SourceInstance> Sources => _sources;

    public IEnumerable<SourceInstance> Items => _sources.Where(s => s.Slot is not null);

    public bool IsDead => Hp <= 0;

    internal void Add(SourceInstance source)
    {
        _sources.Add(source);
        _sources.Sort(SourceInstance.CanonicalOrder);
    }
}
