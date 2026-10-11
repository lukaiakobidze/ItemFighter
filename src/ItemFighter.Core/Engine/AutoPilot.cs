using ItemFighter.Core.Content;

namespace ItemFighter.Core.Engine;

/// <summary>Plays the player's active items automatically: clicks each one as soon as it is ready. For tests and the simulator.</summary>
public static class AutoPilot
{
    /// <summary>Commands for the next tick: every ready, unfrozen active, aimed at its first legal target.</summary>
    public static IEnumerable<ICommand> ReadyActives(Battle battle)
    {
        foreach (var item in battle.Player.Items)
        {
            if (item.Def.Activation != Activation.Active || !item.Ready || item.FreezeTicks > 0) continue;
            int? target = null;
            if (item.Def.RequiresTarget)
            {
                var pick = battle.Player.Items.FirstOrDefault(t => Battle.IsValidTarget(item, t));
                if (pick is null) continue;
                target = pick.Id;
            }
            yield return new ActivateCommand(battle.Tick + 1, item.Id, target);
        }
    }

    public static BattleResult Run(Battle battle, int untilTick = int.MaxValue)
    {
        while (battle.Result == BattleResult.Ongoing && battle.Tick < untilTick)
        {
            foreach (var command in ReadyActives(battle).ToList()) battle.Enqueue(command);
            battle.Step();
        }
        return battle.Result;
    }
}
