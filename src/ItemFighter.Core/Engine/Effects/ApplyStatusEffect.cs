using ItemFighter.Core.Content;

namespace ItemFighter.Core.Engine.Effects;

/// <summary>
/// Applies burn/poison/chill/pain/nail stacks to a combatant, or freeze/haste (amount = ticks) to items.
/// For combatant statuses, "percentOfCurrent" adds that % of the stacks the target already has (100 = doubles them).
/// </summary>
public sealed class ApplyStatusEffect(StatusType status, ValueRef amount, TargetSpec target, ValueRef? percentOfCurrent = null) : IEffect
{
    public static ApplyStatusEffect Parse(ParseContext ctx)
    {
        var status = ctx.ReadEnum<StatusType>("status");
        if (status.IsCombatantStatus())
        {
            if (status.IsTimed()) return new(status, ctx.ReadDuration(), CombatantTarget.Read(ctx, TargetSpec.Enemy));
            var percent = ctx.Has("percentOfCurrent") ? ctx.ReadValue("percentOfCurrent") : (ValueRef?)null;
            return new(status, ctx.ReadValue("amount", percent is null ? null : 0), CombatantTarget.Read(ctx, TargetSpec.Enemy), percent);
        }
        var amount = ctx.ReadDuration();
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
            var side = CombatantTarget.Resolve(target, ctx.Source);
            if (percentOfCurrent is ValueRef p)
                value += ctx.Battle.Get(side).Statuses.Get(status) * p.Resolve(ctx.Source) / 100;
            if (!status.IsTimed()) value += ctx.Source.BonusFor(status.ToString());
            ctx.Battle.ApplyStatus(side, status, value, ctx.Source, ctx.Depth);
            return;
        }
        foreach (var item in Targeting.ResolveItems(target, ctx))
            ctx.Battle.ApplyItemStatus(item, status, value, ctx.Source, ctx.Depth);
    }

    public string Describe(DescribeContext ctx)
    {
        if (status.IsCombatantStatus())
        {
            string who = ctx.Combatant(target);
            string whom = who == "you" && !ctx.IsEnemy ? "yourself" : who;
            if (percentOfCurrent is ValueRef p)
            {
                int pct = ctx.Value(p), flat = ctx.Value(amount);
                string how = pct == 100 ? $"{status} equal to the {status} already on {whom}" : $"{pct}% of the {status} already on {whom} as more {status}";
                return flat == 0 ? $"apply {how}" : $"apply {flat} {status} plus {how}";
            }
            if (status.IsTimed()) return $"make {whom} {status} for {ctx.Seconds(amount)}";
            return $"apply {ctx.Value(amount)} {status} to {whom}";
        }
        return $"{status.ToString().ToLowerInvariant()} {ctx.Items(target)} for {ctx.Seconds(amount)}";
    }
}
