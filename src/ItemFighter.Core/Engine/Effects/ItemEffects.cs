using ItemFighter.Core.Content;

namespace ItemFighter.Core.Engine.Effects;

internal static class ItemTarget
{
    public static TargetSpec Read(ParseContext ctx)
    {
        var t = ctx.ReadTarget("target", TargetSpec.Chosen);
        if (t.IsCombatant) throw new ContentException($"'{ctx.ReadString("type")}' must target items, not {t}");
        return t;
    }
}

/// <summary>Advances target items' cooldowns by an amount of ticks.</summary>
public sealed class ChargeEffect(ValueRef ticks, TargetSpec target) : IEffect
{
    public static ChargeEffect Parse(ParseContext ctx) => new(ctx.ReadDuration(), ItemTarget.Read(ctx));

    public void Apply(EffectContext ctx)
    {
        int value = ticks.Resolve(ctx.Source);
        foreach (var item in Targeting.ResolveItems(target, ctx))
            ctx.Battle.Charge(item, value, ctx.Source, ctx.Depth);
    }

    public string Describe(DescribeContext ctx) => $"charge {ctx.Items(target)} {ctx.Seconds(ticks)}";
}

/// <summary>Adds time to target items' current cooldowns. By default hits every enemy item or ability.</summary>
public sealed class SlowEffect(ValueRef ticks, TargetSpec target) : IEffect
{
    public static SlowEffect Parse(ParseContext ctx)
    {
        var t = ctx.ReadTarget("target", new TargetSpec(TargetKind.AllEnemy));
        if (t.IsCombatant) throw new ContentException($"'Slow' must target items or abilities, not {t}");
        return new(ctx.ReadDuration(), t);
    }

    public void Apply(EffectContext ctx)
    {
        int value = ticks.Resolve(ctx.Source);
        foreach (var item in Targeting.ResolveItems(target, ctx))
            ctx.Battle.Delay(item, value, ctx.Source, ctx.Depth);
    }

    public string Describe(DescribeContext ctx) => $"add {ctx.Seconds(ticks)} to the cooldown of {ctx.Items(target)}";
}

/// <summary>Finishes target items' cooldowns: actives become ready, passives fire as soon as they can.</summary>
public sealed class ResetCooldownEffect(TargetSpec target) : IEffect
{
    public static ResetCooldownEffect Parse(ParseContext ctx) => new(ItemTarget.Read(ctx));

    public void Apply(EffectContext ctx)
    {
        foreach (var item in Targeting.ResolveItems(target, ctx))
            ctx.Battle.ResetCooldown(item, ctx.Source, ctx.Depth);
    }

    public string Describe(DescribeContext ctx) => $"finish {Words.Possessive(ctx.Items(target))} cooldown";
}

/// <summary>Permanently (for this fight) raises a target item's Damage, Shield, Heal, Burn, Poison or Chill.</summary>
public sealed class ModifyStatEffect(string stat, ValueRef amount, TargetSpec target) : IEffect
{
    public static readonly IReadOnlySet<string> Stats = new HashSet<string>(StringComparer.Ordinal)
        { "Damage", "Shield", "Heal", "Burn", "Poison", "Chill" };

    public static ModifyStatEffect Parse(ParseContext ctx)
    {
        string stat = ctx.ReadString("stat");
        if (!Stats.Contains(stat)) throw new ContentException($"unknown stat '{stat}' (use {string.Join(", ", Stats)})");
        return new(stat, ctx.ReadValue("amount"), ItemTarget.Read(ctx));
    }

    public void Apply(EffectContext ctx)
    {
        int value = amount.Resolve(ctx.Source);
        foreach (var item in Targeting.ResolveItems(target, ctx))
            ctx.Battle.ModifyStat(item, stat, value, ctx.Source, ctx.Depth);
    }

    public string Describe(DescribeContext ctx) => $"+{ctx.Value(amount)} {stat} to {ctx.Items(target)} this fight";
}

/// <summary>
/// Runs the target items' own cooldown/activation effects once more, one chain level deeper.
/// Never copies its own item, and copies of copies stop at the chain cap.
/// </summary>
public sealed class CopyEffect(TargetSpec target) : IEffect
{
    public static CopyEffect Parse(ParseContext ctx) => new(ItemTarget.Read(ctx));

    public void Apply(EffectContext ctx)
    {
        foreach (var item in Targeting.ResolveItems(target, ctx))
        {
            if (item == ctx.Source) continue;
            foreach (var ability in item.Def.Abilities)
                if (ability.Trigger is Triggers.OnCooldownTrigger or Triggers.OnActivateTrigger
                    && ctx.Battle.TryReact(item, ctx.Depth + 1))
                    ctx.Battle.RunEffects(ability, item, null, ctx.Depth + 1);
        }
    }

    public string Describe(DescribeContext ctx) => $"repeat {Words.Possessive(ctx.Items(target))} effects";
}
