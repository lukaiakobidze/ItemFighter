using ItemFighter.Core.Content;

namespace ItemFighter.Core.Engine.Triggers;

public sealed class OnFightStartTrigger : ITrigger
{
    public bool Matches(BattleEvent e, SourceInstance owner, Battle battle) => e is FightStarted;
}

/// <summary>
/// Reacts when one of the owner's sources fires. Filters (all must match): Self, Adjacent, Tag:&lt;tag&gt;, AnyOwn.
/// Without Self, the owner's own fires are ignored.
/// </summary>
public sealed class OnItemFiredTrigger : ITrigger
{
    private readonly bool _self;
    private readonly bool _adjacent;
    private readonly IReadOnlyList<string> _tags;

    public OnItemFiredTrigger(IReadOnlyList<string> filters)
    {
        var tags = new List<string>();
        foreach (string f in filters)
        {
            if (f == "Self") _self = true;
            else if (f == "Adjacent") _adjacent = true;
            else if (f.StartsWith("Tag:", StringComparison.Ordinal) && f.Length > 4) tags.Add(f[4..]);
            else if (f != "AnyOwn") throw new ContentException($"unknown OnItemFired filter '{f}'");
        }
        _tags = tags;
    }

    public static OnItemFiredTrigger Parse(ParseContext ctx) => new(ctx.ReadStrings("filter"));

    public bool AllowSelf => _self;

    public bool Matches(BattleEvent e, SourceInstance owner, Battle battle)
    {
        if (e is not SourceFired f || f.Owner != owner.Owner) return false;
        var fired = battle.FindSource(f.SourceId);
        if (fired is null) return false;
        if (_self != (fired == owner)) return false;
        if (fired == owner && f.Reaction) return false; // never react to your own reaction
        if (_adjacent && !(fired.Slot is int a && owner.Slot is int b && Math.Abs(a - b) == 1)) return false;
        return _tags.All(fired.Def.HasTag);
    }
}

public sealed class OnDamageTakenTrigger : ITrigger
{
    public bool Matches(BattleEvent e, SourceInstance owner, Battle battle) =>
        e is DamageDealt d && d.Target == owner.Owner && d.Total > 0;
}

/// <summary>Reacts the first time the owner's HP drops below a percentage.</summary>
public sealed class OnHpBelowTrigger(int percent) : ITrigger
{
    public static OnHpBelowTrigger Parse(ParseContext ctx)
    {
        int percent = ctx.ReadInt("percent");
        if (percent is <= 0 or >= 100) throw new ContentException("'percent' must be between 1 and 99");
        return new(percent);
    }

    public bool Matches(BattleEvent e, SourceInstance owner, Battle battle)
    {
        if (e is not DamageDealt d || d.Target != owner.Owner || d.HpDamage <= 0) return false;
        var c = battle.Get(owner.Owner);
        if (c.Hp * 100 >= c.MaxHp * percent) return false;
        return owner.FiredOnce.Add(this);
    }
}

/// <summary>Reacts when a status is applied to the owner's side ("Self") or the other side ("Enemy").</summary>
public sealed class OnStatusAppliedTrigger(StatusType status, bool enemySide) : ITrigger
{
    public static OnStatusAppliedTrigger Parse(ParseContext ctx)
    {
        string side = ctx.ReadString("side", "Enemy");
        if (side is not ("Enemy" or "Self")) throw new ContentException($"'side' must be Enemy or Self, not '{side}'");
        return new(ctx.ReadEnum<StatusType>("status"), side == "Enemy");
    }

    public bool Matches(BattleEvent e, SourceInstance owner, Battle battle)
    {
        if (e is not StatusApplied a || a.Status != status) return false;
        Side? targetSide = a.TargetSide ?? (a.TargetSourceId is int id ? battle.FindSource(id)?.Owner : null);
        if (targetSide is null) return false;
        return (targetSide != owner.Owner) == enemySide;
    }
}

public sealed class OnShieldGainedTrigger : ITrigger
{
    public bool Matches(BattleEvent e, SourceInstance owner, Battle battle) =>
        e is ShieldGained g && g.Target == owner.Owner;
}

/// <summary>Reacts on every n-th cooldown or click fire of the owner (reaction fires don't count).</summary>
public sealed class EveryNthUseTrigger(int n) : ITrigger
{
    public static EveryNthUseTrigger Parse(ParseContext ctx)
    {
        int n = ctx.ReadInt("n");
        if (n < 2) throw new ContentException("'n' must be at least 2");
        return new(n);
    }

    public bool AllowSelf => true;

    public bool Matches(BattleEvent e, SourceInstance owner, Battle battle) =>
        e is SourceFired { Reaction: false } f && f.SourceId == owner.Id && owner.CycleCount % n == 0;
}
