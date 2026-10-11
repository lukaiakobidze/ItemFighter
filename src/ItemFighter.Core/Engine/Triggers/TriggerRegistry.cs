using ItemFighter.Core.Content;

namespace ItemFighter.Core.Engine.Triggers;

/// <summary>Maps a trigger "type" in JSON to the code that builds it. New mechanics register here.</summary>
public sealed class TriggerRegistry
{
    private readonly Dictionary<string, Func<ParseContext, ITrigger>> _factories = new(StringComparer.Ordinal);

    public void Register(string type, Func<ParseContext, ITrigger> factory) => _factories[type] = factory;

    public ITrigger Create(string type, ParseContext ctx) =>
        _factories.TryGetValue(type, out var f) ? f(ctx) : throw new ContentException($"unknown trigger type '{type}'");

    public static TriggerRegistry CreateDefault()
    {
        var r = new TriggerRegistry();
        r.Register("OnCooldown", _ => new OnCooldownTrigger());
        r.Register("OnActivate", _ => new OnActivateTrigger());
        return r;
    }
}
