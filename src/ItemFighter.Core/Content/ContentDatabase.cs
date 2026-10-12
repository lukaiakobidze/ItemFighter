using System.Diagnostics.CodeAnalysis;

namespace ItemFighter.Core.Content;

public sealed class ContentDatabase
{
    private readonly Dictionary<string, SourceDefinition> _byId = new(StringComparer.Ordinal);
    private readonly List<SourceDefinition> _all = [];

    /// <summary>The fight's status and time-limit numbers (Data/rules.json, or the defaults).</summary>
    public Engine.CombatRules Rules { get; set; } = new();

    /// <summary>All definitions, sorted by id (ordinal), so iteration order never depends on file order.</summary>
    public IReadOnlyList<SourceDefinition> All => _all;

    internal bool TryAdd(SourceDefinition def)
    {
        if (!_byId.TryAdd(def.Id, def)) return false;
        int index = _all.BinarySearch(def, Comparer<SourceDefinition>.Create((a, b) => string.CompareOrdinal(a.Id, b.Id)));
        _all.Insert(~index, def);
        return true;
    }

    public SourceDefinition Get(string id) =>
        _byId.TryGetValue(id, out var def) ? def : throw new KeyNotFoundException($"no content with id '{id}'");

    public bool TryGet(string id, [NotNullWhen(true)] out SourceDefinition? def) => _byId.TryGetValue(id, out def);

    public IEnumerable<SourceDefinition> OfKind(SourceKind kind) => _all.Where(d => d.Kind == kind);
}
