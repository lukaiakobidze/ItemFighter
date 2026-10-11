namespace ItemFighter.Core.Engine;

/// <summary>Custom C# behaviour for unique sources, referenced from JSON by "script".</summary>
public interface ISourceScript
{
}

public sealed class ScriptRegistry
{
    private readonly SortedDictionary<string, Func<ISourceScript>> _factories = new(StringComparer.Ordinal);

    public void Register(string name, Func<ISourceScript> factory) => _factories[name] = factory;

    public bool Contains(string name) => _factories.ContainsKey(name);

    public ISourceScript Create(string name) => _factories[name]();
}
