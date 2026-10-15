using ItemFighter.Core.Content;

namespace ItemFighter.Core.Engine.Effects;

/// <summary>Maps an effect "type" in JSON to the code that builds it. New mechanics register here.</summary>
public sealed class EffectRegistry
{
    private readonly Dictionary<string, Func<ParseContext, IEffect>> _factories = new(StringComparer.Ordinal);

    public void Register(string type, Func<ParseContext, IEffect> factory) => _factories[type] = factory;

    public IEffect Create(string type, ParseContext ctx) =>
        _factories.TryGetValue(type, out var f) ? f(ctx) : throw new ContentException($"unknown effect type '{type}'");

    public static EffectRegistry CreateDefault()
    {
        var r = new EffectRegistry();
        r.Register("Damage", DamageEffect.Parse);
        r.Register("Shield", ShieldEffect.Parse);
        r.Register("Heal", HealEffect.Parse);
        r.Register("ApplyStatus", ApplyStatusEffect.Parse);
        r.Register("RemoveStatus", RemoveStatusEffect.Parse);
        r.Register("Uproot", UprootEffect.Parse);
        r.Register("Charge", ChargeEffect.Parse);
        r.Register("Slow", SlowEffect.Parse);
        r.Register("ResetCooldown", ResetCooldownEffect.Parse);
        r.Register("ReduceCooldown", ReduceCooldownEffect.Parse);
        r.Register("ModifyStat", ModifyStatEffect.Parse);
        r.Register("CopyEffect", CopyEffect.Parse);
        return r;
    }
}
