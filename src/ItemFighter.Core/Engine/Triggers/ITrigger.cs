namespace ItemFighter.Core.Engine.Triggers;

/// <summary>When an ability reacts. Cooldown and activation triggers are markers the battle loop handles directly.</summary>
public interface ITrigger
{
    bool Matches(BattleEvent e, SourceInstance owner, Battle battle);

    /// <summary>Whether this trigger may react to events its own source produced.</summary>
    bool AllowSelf => false;
}
