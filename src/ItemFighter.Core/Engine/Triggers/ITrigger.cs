namespace ItemFighter.Core.Engine.Triggers;

/// <summary>When an ability reacts. Cooldown and activation triggers are markers the battle loop handles directly.</summary>
public interface ITrigger
{
    bool Matches(BattleEvent e, SourceInstance owner, Battle battle);

    /// <summary>Whether this trigger may react to events its own source produced.</summary>
    bool AllowSelf => false;

    /// <summary>How many times the ability runs for a matched event (e.g. once per 2 nails uprooted). 0 skips it.</summary>
    int Repeats(BattleEvent e) => 1;

    /// <summary>A capitalised clause for tooltips, e.g. "When you take damage".</summary>
    string Describe(Content.DescribeContext ctx) => GetType().Name;
}
