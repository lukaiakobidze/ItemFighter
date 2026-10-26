using ItemFighter.Content;
using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests;

/// <summary>Luka 2026-10-26: Stabilizer, Hot Plate, Flaming Coal, three Heat passives and Heat-boosted Uproot.</summary>
public class HeatContentTests
{
    private static ContentDatabase Db => GameContent.Load().Database;

    private static Battle Fight(string[] passives, params (string Id, int Slot)[] board) =>
        new(new BattleSetup(new PlayerSetup(null, board.Select(b => new BoardItem(b.Id, b.Slot)).ToList(), passives), "squire_zombie", 1), Db);

    [Fact]
    public void Stabilizer_TogglesNoHeatGain_WhichBlocksHeat()
    {
        var b = Fight([], ("stabilizer", 0));
        var stabilizer = b.Player.Items.Single();
        b.Steps(100);
        b.Enqueue(new ActivateCommand(b.Tick + 1, stabilizer.Id, null));
        b.Steps(1);
        Assert.Equal(1, b.Player.Statuses.NoHeatGain);
        b.ApplyStatus(Side.Player, StatusType.Heat, 30, null, 0);
        Assert.Equal(0, b.Player.Statuses.Heat);

        b.Steps(100);
        b.Enqueue(new ActivateCommand(b.Tick + 1, stabilizer.Id, null));
        b.Steps(1);
        Assert.Equal(0, b.Player.Statuses.NoHeatGain);
        b.ApplyStatus(Side.Player, StatusType.Heat, 30, null, 0);
        Assert.Equal(30, b.Player.Statuses.Heat);
    }

    [Fact]
    public void HotPlate_Shields15_AndAdds10Heat()
    {
        var b = Fight([], ("hot_plate", 0));
        b.Steps(60);
        Assert.Equal(10, b.Player.Statuses.Heat);
        Assert.Contains(b.Of<ShieldGained>(), g => g.Amount == 15);
    }

    [Fact]
    public void FlamingCoal_TurnsTenPercentOfHeat_IntoBurn()
    {
        var b = Fight([], ("flaming_coal", 0));
        b.ApplyStatus(Side.Player, StatusType.Heat, 60, null, 0);
        b.Steps(90);
        Assert.Equal(54, b.Player.Statuses.Heat);
        Assert.Contains(b.Of<StatusApplied>(), a => a.Status == StatusType.Burn && a.TargetSide == Side.Enemy && a.Amount == 6);
    }

    [Fact]
    public void Stoker_BurnsEveryFifthHeatGain()
    {
        var b = Fight(["stoker"]);
        for (int i = 0; i < 4; i++) b.ApplyStatus(Side.Player, StatusType.Heat, 1, null, 0);
        Assert.Equal(0, b.Enemy.Statuses.Burn);
        b.ApplyStatus(Side.Player, StatusType.Heat, 1, null, 0);
        Assert.Equal(5, b.Enemy.Statuses.Burn);
    }

    [Fact]
    public void Firebox_AddsOneToEveryHeatGain()
    {
        var b = Fight(["firebox"], ("shovel_of_coal", 0));
        b.Steps(50);
        Assert.Equal(6, b.Player.Statuses.Heat);
    }

    [Fact]
    public void BigBoiler_RaisesMaxHeatByTen()
    {
        var b = Fight(["big_boiler"]);
        Assert.Equal(110, b.MaxHeat(Side.Player));
        b.ApplyStatus(Side.Player, StatusType.Heat, 105, null, 0);
        Assert.Empty(b.Of<Overheated>());
        b.ApplyStatus(Side.Player, StatusType.Heat, 5, null, 0);
        Assert.Single(b.Of<Overheated>());
    }

    [Fact]
    public void Uproot_IgnoresHeat_WithoutHotNails() => Assert.Equal(5 * 4, UprootWith50Heat([]));

    [Fact]
    public void HotNails_UprootDoesOnePercentMorePerHeat() => Assert.Equal(5 * 4 * 3 / 2, UprootWith50Heat(["hot_nails"]));

    private static int UprootWith50Heat(string[] passives)
    {
        var b = Fight(passives, ("magnetized_glove", 0));
        var glove = b.Player.Items.Single();
        b.ApplyStatus(Side.Enemy, StatusType.Nail, 5, null, 0);
        b.ApplyStatus(Side.Player, StatusType.Heat, 50, null, 0);
        b.Steps(40);
        b.Enqueue(new ActivateCommand(b.Tick + 1, glove.Id, null));
        b.Steps(1);
        return Assert.Single(b.Of<NailsUprooted>()).Damage;
    }
}
