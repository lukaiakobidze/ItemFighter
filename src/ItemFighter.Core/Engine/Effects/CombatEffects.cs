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

    public string Describe(DescribeContext ctx)
    {
        string who = ctx.Combatant(target);
        return who == "the enemy" ? $"deal {ctx.Value(amount)} damage" : $"deal {ctx.Value(amount)} damage to {(who == "you" && !ctx.IsEnemy ? "yourself" : who)}";
    }
}

/// <summary>Gains shield: a flat "amount", and/or "percentOfMaxHp" of the shielded side's max HP.</summary>
public sealed class ShieldEffect(ValueRef amount, ValueRef percentOfMaxHp, TargetSpec target) : IEffect
{
    public static ShieldEffect Parse(ParseContext ctx)
    {
        if (!ctx.Has("amount") && !ctx.Has("percentOfMaxHp")) throw new ContentException("'Shield' needs 'amount' or 'percentOfMaxHp'");
        return new(ctx.ReadValue("amount", 0), ctx.ReadValue("percentOfMaxHp", 0), CombatantTarget.Read(ctx, TargetSpec.Self));
    }

    public void Apply(EffectContext ctx)
    {
        var side = CombatantTarget.Resolve(target, ctx.Source);
        int percent = percentOfMaxHp.Resolve(ctx.Source);
        int value = amount.Resolve(ctx.Source) + (int)Math.Round(ctx.Battle.Get(side).MaxHp * percent / 100.0, MidpointRounding.AwayFromZero);
        ctx.Battle.GainShield(side, value + ctx.Source.BonusFor("Shield"), ctx.Source, ctx.Depth);
    }

    public string Describe(DescribeContext ctx)
    {
        int flat = ctx.Value(amount), percent = ctx.Value(percentOfMaxHp);
        string how = (flat, percent) switch
        {
            (_, 0) => $"{flat} shield",
            (0, _) => $"shield equal to {percent}% of max HP",
            _ => $"{flat} shield plus {percent}% of max HP",
        };
        return target.Kind == TargetKind.Self ? $"gain {how}" : $"give {ctx.Combatant(target)} {how}";
    }
}

public sealed class HealEffect(ValueRef amount, TargetSpec target) : IEffect
{
    public static HealEffect Parse(ParseContext ctx) =>
        new(ctx.ReadValue("amount"), CombatantTarget.Read(ctx, TargetSpec.Self));

    public void Apply(EffectContext ctx) =>
        ctx.Battle.Heal(CombatantTarget.Resolve(target, ctx.Source), amount.Resolve(ctx.Source) + ctx.Source.BonusFor("Heal"), ctx.Source, ctx.Depth);

    public string Describe(DescribeContext ctx) =>
        target.Kind == TargetKind.Self ? $"heal {ctx.Value(amount)}" : $"heal {ctx.Combatant(target)} for {ctx.Value(amount)}";
}
