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
    public static ChargeEffect Parse(ParseContext ctx) => new(ctx.ReadValue("amount"), ItemTarget.Read(ctx));

    public void Apply(EffectContext ctx)
    {
        int value = ticks.Resolve(ctx.Source);
        foreach (var item in Targeting.ResolveItems(target, ctx.Source, ctx.ChosenTarget, ctx.Battle))
            ctx.Battle.Charge(item, value, ctx.Source, ctx.Depth);
    }
}

/// <summary>Finishes target items' cooldowns: actives become ready, passives fire as soon as they can.</summary>
public sealed class ResetCooldownEffect(TargetSpec target) : IEffect
{
    public static ResetCooldownEffect Parse(ParseContext ctx) => new(ItemTarget.Read(ctx));

    public void Apply(EffectContext ctx)
    {
        foreach (var item in Targeting.ResolveItems(target, ctx.Source, ctx.ChosenTarget, ctx.Battle))
            ctx.Battle.ResetCooldown(item, ctx.Source, ctx.Depth);
    }
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
        foreach (var item in Targeting.ResolveItems(target, ctx.Source, ctx.ChosenTarget, ctx.Battle))
            ctx.Battle.ModifyStat(item, stat, value, ctx.Source, ctx.Depth);
    }
}

/// <summary>Runs the target items' own cooldown/activation effects once more.</summary>
public sealed class CopyEffect(TargetSpec target) : IEffect
{
    public static CopyEffect Parse(ParseContext ctx) => new(ItemTarget.Read(ctx));

    public void Apply(EffectContext ctx)
    {
        foreach (var item in Targeting.ResolveItems(target, ctx.Source, ctx.ChosenTarget, ctx.Battle))
            foreach (var ability in item.Def.Abilities)
                if (ability.Trigger is Triggers.OnCooldownTrigger or Triggers.OnActivateTrigger)
                    ctx.Battle.RunEffects(ability, item, null, ctx.Depth);
    }
}
