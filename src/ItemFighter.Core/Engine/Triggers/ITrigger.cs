namespace ItemFighter.Core.Engine.Triggers;

/// <summary>When an ability reacts. Cooldown and activation triggers are markers the battle loop handles directly.</summary>
public interface ITrigger
{
    bool Matches(BattleEvent e, SourceInstance owner, Battle battle);

    /// <summary>Whether this trigger may react to events its own source produced.</summary>
    bool AllowSelf => false;

    /// <summary>A capitalised clause for tooltips, e.g. "When you take damage".</summary>
    string Describe(Content.DescribeContext ctx) => GetType().Name;
}
