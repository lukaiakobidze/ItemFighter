namespace ItemFighter.Core.Engine.Effects;

/// <summary>What a source does when one of its abilities runs.</summary>
public interface IEffect
{
    void Apply(EffectContext ctx);
}

/// <param name="Source">The source whose ability is running.</param>
/// <param name="ChosenTarget">The item the player picked, for targeted actives.</param>
/// <param name="Depth">Reaction chain depth; 0 for a cooldown or activation.</param>
/// <param name="Trigger">For reactions to an item firing: the item that fired.</param>
public sealed record EffectContext(Battle Battle, SourceInstance Source, SourceInstance? ChosenTarget, int Depth, SourceInstance? Trigger = null);
