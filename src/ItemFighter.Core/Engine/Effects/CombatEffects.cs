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

/// <summary>
/// Deals "amount" damage, plus "percentOfShield" of the owner's current shield, plus "perItem" for each of the owner's
/// board items with tag "perItemTag" (this item included), plus "perStatus" for every "perStatusEvery" stacks of the
/// owner's own "perStatusOf" status (Hot Iron: 1 per 5 Heat). "hits" repeats it as separate hits.
/// </summary>
public sealed class DamageEffect(ValueRef amount, TargetSpec target, ValueRef hits, ValueRef percentOfShield, ValueRef perItem, string? perItemTag,
    StatusType? perStatusOf = null, ValueRef perStatus = default, int perStatusEvery = 1) : IEffect
{
    public static DamageEffect Parse(ParseContext ctx)
    {
        string? tag = ctx.Has("perItemTag") ? ctx.ReadString("perItemTag") : null;
        if ((tag is null) != !ctx.Has("perItem")) throw new ContentException("'perItem' and 'perItemTag' go together");
        if (!ctx.Has("amount") && !ctx.Has("percentOfShield") && tag is null)
            throw new ContentException("'Damage' needs 'amount', 'percentOfShield' or 'perItem'");
        var hits = ctx.ReadValue("hits", 1);
        if (hits.Literal is < 1) throw new ContentException("'hits' must be at least 1");
        StatusType? perStatusOf = null;
        if ((ctx.Has("perStatusOf")) != ctx.Has("perStatus")) throw new ContentException("'perStatus' and 'perStatusOf' go together");
        if (ctx.Has("perStatusOf"))
        {
            perStatusOf = ctx.ReadEnum<StatusType>("perStatusOf");
            if (!perStatusOf.Value.IsCombatantStatus()) throw new ContentException($"'perStatusOf' must be a status on a fighter, not {perStatusOf}");
        }
        int every = ctx.ReadInt("perStatusEvery", 1);
        if (every < 1) throw new ContentException("'perStatusEvery' must be at least 1");
        return new(ctx.ReadValue("amount", 0), CombatantTarget.Read(ctx, TargetSpec.Enemy), hits,
            ctx.ReadValue("percentOfShield", 0), ctx.ReadValue("perItem", 0), tag, perStatusOf, ctx.ReadValue("perStatus", 0), every);
    }

    public void Apply(EffectContext ctx)
    {
        var (battle, source) = (ctx.Battle, ctx.Source);
        int value = amount.Resolve(source) + source.BonusFor("Damage");
        int percent = percentOfShield.Resolve(source);
        if (percent != 0) value += (int)Math.Round(battle.Get(source.Owner).Shield * percent / 100.0, MidpointRounding.AwayFromZero);
        if (perItemTag is not null)
            value += perItem.Resolve(source) * Targeting.Targetable(battle.Get(source.Owner)).Count(i => i.Def.HasTag(perItemTag));
        if (perStatusOf is StatusType st)
            value += perStatus.Resolve(source) * (battle.Get(source.Owner).Statuses.Get(st) / perStatusEvery);
        var side = CombatantTarget.Resolve(target, source);
        for (int i = hits.Resolve(source); i > 0; i--)
            battle.DealDamage(side, value, DamageKind.Direct, source, ctx.Depth);
    }

    public string Describe(DescribeContext ctx)
    {
        int flat = ctx.Value(amount), percent = ctx.Value(percentOfShield), per = ctx.Value(perItem), times = ctx.Value(hits);
        string your = ctx.IsEnemy ? "its" : "your";
        string shield = percent == 100 ? $"{your} shield" : $"{percent}% of {your} shield";
        string how = flat == 0 && percent != 0 && perItemTag is null ? $"damage equal to {shield}"
            : $"{flat} damage" + (percent != 0 ? $" plus {shield}" : "")
              + (perItemTag is not null ? $" plus {per} for each {perItemTag} item {(ctx.IsEnemy ? "it has" : "on your board")}" : "")
              + (perStatusOf is StatusType st ? $" plus {ctx.Value(perStatus)} for every {(perStatusEvery == 1 ? "" : perStatusEvery + " ")}{st} {(ctx.IsEnemy ? "it has" : "you have")}" : "");
        string who = ctx.Combatant(target);
        string whom = who == "the enemy" ? "" : $" to {(who == "you" && !ctx.IsEnemy ? "yourself" : who)}";
        string repeat = times switch { 1 => "", 2 => " twice", _ => $" {times} times" };
        return $"deal {how}{whom}{repeat}";
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

/// <summary>Removes every stack of the listed combatant statuses (default target: Self).</summary>
public sealed class RemoveStatusEffect(IReadOnlyList<StatusType> statuses, TargetSpec target) : IEffect
{
    public static RemoveStatusEffect Parse(ParseContext ctx)
    {
        var names = ctx.ReadStrings("statuses");
        if (names.Count == 0) throw new ContentException("'RemoveStatus' needs 'statuses', e.g. [\"Poison\", \"Burn\"]");
        var list = new List<StatusType>();
        foreach (string n in names)
        {
            if (!Enum.TryParse<StatusType>(n, out var s) || !Enum.IsDefined(s) || !s.IsCombatantStatus())
                throw new ContentException($"'{n}' is not a status that sits on a fighter (Burn, Poison, Chill, Pain, Nail, Power, Vulnerable)");
            list.Add(s);
        }
        return new(list, CombatantTarget.Read(ctx, TargetSpec.Self));
    }

    public void Apply(EffectContext ctx) =>
        ctx.Battle.RemoveStatuses(CombatantTarget.Resolve(target, ctx.Source), statuses, ctx.Source, ctx.Depth);

    public string Describe(DescribeContext ctx)
    {
        string list = statuses.Count == 1 ? statuses[0].ToString()
            : string.Join(", ", statuses.Take(statuses.Count - 1)) + " and " + statuses[^1];
        string who = ctx.Combatant(target);
        return $"remove all {list} from {(who == "you" && !ctx.IsEnemy ? "yourself" : who)}";
    }
}

/// <summary>Pulls out every nail on the target (default: Enemy) for nailDamage each, as one hit.</summary>
public sealed class UprootEffect(TargetSpec target) : IEffect
{
    public static UprootEffect Parse(ParseContext ctx) => new(CombatantTarget.Read(ctx, TargetSpec.Enemy));

    public void Apply(EffectContext ctx) => ctx.Battle.Uproot(CombatantTarget.Resolve(target, ctx.Source), ctx.Source, ctx.Depth);

    public string Describe(DescribeContext ctx) => $"uproot every Nail on {ctx.Combatant(target)} (damage for each Nail)";
}
