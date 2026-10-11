using ItemFighter.Core.Engine;

namespace ItemFighter.Tests;

/// <summary>Interactions between Luka's starter content. Update the numbers when the content is rebalanced.</summary>
public class ComboTests
{
    private static Battle Fight(string enemy, string? character, params string[] items)
    {
        var content = ContentIntegrityTests.WithDummy();
        var board = items.Select((id, slot) => new BoardItem(id, slot)).ToList();
        return new Battle(new BattleSetup(new PlayerSetup(character, board, []), enemy, 1), content.Database, content.Scripts);
    }

    private static List<int> Fires(Battle b, string defId) =>
        b.Events.OfType<SourceFired>().Where(f => !f.Reaction && b.FindSource(f.SourceId)!.Def.Id == defId).Select(f => f.Tick).ToList();

    [Fact]
    public void Apple_ChargesTheSwordOneSecond()
    {
        var b = Fight("test_dummy", null, "sword", "apple");
        b.Steps(90);
        Assert.Equal(new[] { 40, 70 }, Fires(b, "sword")); // the apple at 6s takes 1s off the sword's second cooldown
    }

    [Fact]
    public void Weights_PushesTheZombiesBiteBack()
    {
        var b = Fight("squire_zombie", null, "weights");
        var weights = b.Player.Items.Single();
        b.Steps(80);
        b.Enqueue(new ActivateCommand(b.Tick + 1, weights.Id, null));
        b.Steps(50);
        Assert.Equal(new[] { 50, 120 }, Fires(b, "bite"));
    }

    [Fact]
    public void GrizzlyBear_ThickSkin_TakesOneLessFromDaggers()
    {
        var b = Fight("grizzly_bear", null, "dagger");
        b.Steps(20);
        Assert.Equal(99, b.Enemy.Hp);
    }

    [Fact]
    public void GreenSnake_Venomous_PoisonsOnEachStrike()
    {
        var b = Fight("green_snake", null, "metal_plating");
        b.Steps(30);
        Assert.Equal(2, b.Player.Statuses.Poison);
    }

    [Fact]
    public void FlamingRock_Explodes_OnEveryFifthHit()
    {
        var b = Fight("flaming_rock", null, "dagger");
        b.Steps(100); // 5 dagger hits by 10s
        Assert.Contains(b.Events.OfType<StatusApplied>(), s => s.Status == StatusType.Burn && s.Amount == 3 && s.TargetSide == Side.Player);
    }

    [Fact]
    public void SilverKnight_ShieldsAfterFiveHits()
    {
        var b = Fight("archer_skeleton", "silver_knight", "metal_plating");
        b.Steps(150); // the bow shot hits every 3s: 5 hits by 15s
        Assert.Contains(b.Events.OfType<ShieldGained>(), g => g.Target == Side.Player && g.Amount == 5);
    }

    [Fact]
    public void IceBow_ChillsTheEnemy()
    {
        var b = Fight("test_dummy", null, "ice_bow");
        b.Steps(61);
        Assert.Equal(1, b.Enemy.Statuses.Chill);
    }
}
