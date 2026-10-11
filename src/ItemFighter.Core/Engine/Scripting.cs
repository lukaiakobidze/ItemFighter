using System.Reflection;

namespace ItemFighter.Core.Engine;

/// <summary>
/// Custom C# behaviour for unique sources, referenced from JSON by "script". A script sees every event
/// after the source's data abilities, one chain level deeper, and acts through the public Battle API.
/// Each source gets its own instance, so fields are per-battle state.
/// </summary>
public interface ISourceScript
{
    void OnEvent(BattleEvent e, SourceInstance self, Battle battle, int depth);
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class SourceScriptAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

public sealed class ScriptRegistry
{
    private readonly SortedDictionary<string, Func<ISourceScript>> _factories = new(StringComparer.Ordinal);

    public void Register(string name, Func<ISourceScript> factory) => _factories[name] = factory;

    public bool Contains(string name) => _factories.ContainsKey(name);

    public ISourceScript Create(string name) =>
        _factories.TryGetValue(name, out var f) ? f() : throw new ArgumentException($"unknown script '{name}'");

    /// <summary>Finds every class marked [SourceScript] with a parameterless constructor.</summary>
    public static ScriptRegistry FromAssemblies(params Assembly[] assemblies)
    {
        var registry = new ScriptRegistry();
        foreach (var type in assemblies.SelectMany(a => a.GetTypes()).OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            var attr = type.GetCustomAttribute<SourceScriptAttribute>();
            if (attr is null || !typeof(ISourceScript).IsAssignableFrom(type) || type.IsAbstract) continue;
            if (registry.Contains(attr.Name)) throw new InvalidOperationException($"two scripts named '{attr.Name}'");
            registry.Register(attr.Name, () => (ISourceScript)Activator.CreateInstance(type)!);
        }
        return registry;
    }
}
