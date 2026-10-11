using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;

namespace ItemFighter.FightView;

/// <summary>
/// Turns clicks on board items into commands. A targeted active is clicked first (selected), then a valid
/// target; clicking it again, Esc or right-click cancels. Anything else is ignored.
/// </summary>
public sealed class TargetingState
{
    private readonly Dictionary<int, int> _sentForTick = [];

    public SourceInstance? Selecting { get; private set; }

    public bool CanActivate(SourceInstance item, Battle battle) =>
        battle.Result == BattleResult.Ongoing && item.Owner == Side.Player && item.Def.Activation == Activation.Active
        && item.Ready && item.FreezeTicks == 0 && !battle.Player.IsFrozenAt(battle.Tick)
        && !(_sentForTick.TryGetValue(item.Id, out int tick) && tick > battle.Tick);

    public bool IsValidTarget(SourceInstance target) => Selecting is { } s && Battle.IsValidTarget(s, target);

    public ICommand? Click(SourceInstance item, Battle battle)
    {
        if (battle.Result != BattleResult.Ongoing)
        {
            Selecting = null;
            return null;
        }
        if (Selecting is { } selected)
        {
            if (item == selected)
            {
                Selecting = null;
                return null;
            }
            if (!Battle.IsValidTarget(selected, item)) return null;
            Selecting = null;
            return Send(selected, item, battle);
        }
        if (!CanActivate(item, battle)) return null;
        if (item.Def.RequiresTarget)
        {
            Selecting = item;
            return null;
        }
        return Send(item, null, battle);
    }

    public void Cancel() => Selecting = null;

    /// <summary>Call every frame: drops the selection when the fight ends or the item can't be used any more.</summary>
    public void Update(Battle battle)
    {
        if (Selecting is { } s && !CanActivate(s, battle)) Selecting = null;
    }

    private ActivateCommand Send(SourceInstance item, SourceInstance? target, Battle battle)
    {
        var command = new ActivateCommand(battle.Tick + 1, item.Id, target?.Id);
        _sentForTick[item.Id] = command.Tick;
        return command;
    }
}
