using ItemFighter.Core.Content;

namespace ItemFighter.Core.Engine.Effects;

/// <summary>Applies burn/poison/chill stacks to a combatant, or freeze/haste/slow (amount = ticks) to items.</summary>
public sealed class ApplyStatusEffect(StatusType status, ValueRef amount, TargetSpec target) : IEffect
{
    public static ApplyStatusEffect Parse(ParseContext ctx)
    {
        var status = ctx.ReadEnum<StatusType>("status");
        var amount = ctx.ReadValue("amount");
        if (status.IsCombatantStatus())
            return new(status, amount, CombatantTarget.Read(ctx, TargetSpec.Enemy));
        var defaultTarget = status == StatusType.Haste ? TargetSpec.SelfItem : new TargetSpec(TargetKind.RandomEnemyAbility);
        var target = ctx.ReadTarget("target", defaultTarget);
        if (target.IsCombatant) throw new ContentException($"{status} must target items or abilities, not {target}");
        return new(status, amount, target);
    }

    public void Apply(EffectContext ctx)
    {
        int value = amount.Resolve(ctx.Source);
        if (status.IsCombatantStatus())
        {
            value += ctx.Source.BonusFor(status.ToString());
            ctx.Battle.ApplyStatus(CombatantTarget.Resolve(target, ctx.Source), status, value, ctx.Source, ctx.Depth);
            return;
        }
        foreach (var item in Targeting.ResolveItems(target, ctx.Source, ctx.ChosenTarget, ctx.Battle))
            ctx.Battle.ApplyItemStatus(item, status, value, ctx.Source, ctx.Depth);
    }
}
