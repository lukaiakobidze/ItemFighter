namespace ItemFighter.Core.Engine.Triggers;

/// <summary>Runs when the source's cooldown completes. Handled by the tick loop, never by events.</summary>
public sealed class OnCooldownTrigger : ITrigger
{
    public bool Matches(BattleEvent e, SourceInstance owner, Battle battle) => false;
}

/// <summary>Runs when the player clicks a ready active item. Handled by commands, never by events.</summary>
public sealed class OnActivateTrigger : ITrigger
{
    public bool Matches(BattleEvent e, SourceInstance owner, Battle battle) => false;
}
