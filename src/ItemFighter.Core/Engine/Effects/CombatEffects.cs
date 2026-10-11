using ItemFighter.Core.Content;

namespace ItemFighter.Core.Engine.Effects;

/// <summary>Shared parsing for effects that hit a combatant (Self or Enemy).</summary>
internal static class CombatantTarget
{
    public static TargetSpec Read(ParseContext ctx, TargetSpec defaultValue)
    {
        var t = ctx.ReadTarget("target", defaultValue);
        if (!t.IsCombatant) throw new ContentException($"'{ctx.ReadString("type")}' must target Self or Enemy, not {t}");
        return t;
    }

    public static Side Resolve(TargetSpec t, SourceInstance source) =>
        t.Kind == TargetKind.Self ? source.Owner : source.Owner == Side.Player ? Side.Enemy : Side.Player;
}

public sealed class DamageEffect(ValueRef amount, TargetSpec target) : IEffect
{
    public static DamageEffect Parse(ParseContext ctx) =>
        new(ctx.ReadValue("amount"), CombatantTarget.Read(ctx, TargetSpec.Enemy));

    public void Apply(EffectContext ctx) =>
        ctx.Battle.DealDamage(CombatantTarget.Resolve(target, ctx.Source), amount.Resolve(ctx.Source) + ctx.Source.BonusFor("Damage"), DamageKind.Direct, ctx.Source, ctx.Depth);
}

public sealed class ShieldEffect(ValueRef amount, TargetSpec target) : IEffect
{
    public static ShieldEffect Parse(ParseContext ctx) =>
        new(ctx.ReadValue("amount"), CombatantTarget.Read(ctx, TargetSpec.Self));

    public void Apply(EffectContext ctx) =>
        ctx.Battle.GainShield(CombatantTarget.Resolve(target, ctx.Source), amount.Resolve(ctx.Source) + ctx.Source.BonusFor("Shield"), ctx.Source, ctx.Depth);
}

public sealed class HealEffect(ValueRef amount, TargetSpec target) : IEffect
{
    public static HealEffect Parse(ParseContext ctx) =>
        new(ctx.ReadValue("amount"), CombatantTarget.Read(ctx, TargetSpec.Self));

    public void Apply(EffectContext ctx) =>
        ctx.Battle.Heal(CombatantTarget.Resolve(target, ctx.Source), amount.Resolve(ctx.Source) + ctx.Source.BonusFor("Heal"), ctx.Source, ctx.Depth);
}
