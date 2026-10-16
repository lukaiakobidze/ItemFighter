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

    // These read Luka's numbers from the data, so rebalancing the JSON doesn't break them.
    private static int Level1(string defId, string key) => ContentIntegrityTests.WithDummy().Database.Get(defId).Levels[key][0];

    [Fact]
    public void Weights_PushesTheZombiesBiteBack()
    {
        var control = Fight("squire_zombie", null, "weights");
        control.Steps(300);
        var b = Fight("squire_zombie", null, "weights");
        var weights = b.Player.Items.Single();
        b.Steps(weights.Def.CooldownTicks!.Value + 1); // Weights is Active: ready once its cooldown has charged
        int activatedAt = b.Tick + 1;
        b.Enqueue(new ActivateCommand(activatedAt, weights.Id, null));
        b.Steps(300 - b.Tick);
        int nextControl = Fires(control, "bite").First(t => t > activatedAt), next = Fires(b, "bite").First(t => t > activatedAt);
        Assert.Equal(nextControl + Level1("weights", "slow"), next); // level 1 Weights delays the next bite by its slow
    }

    [Fact]
    public void GrizzlyBear_ThickSkin_TakesOneLessFromDaggers()
    {
        var b = Fight("grizzly_bear", null, "dagger");
        int maxHp = b.Enemy.MaxHp;
        b.Steps(20); // the dagger's first hit
        Assert.Equal(maxHp - (Level1("dagger", "damage") - 1), b.Enemy.Hp);
    }

    [Fact]
    public void GreenSnake_Venomous_PoisonsOnEachStrike()
    {
        var b = Fight("green_snake", null, "metal_plating");
        b.Steps(100);
        var hits = b.Events.OfType<StatusApplied>().Where(s => s.Status == StatusType.Poison && s.TargetSide == Side.Player).ToList();
        int strikes = b.Events.OfType<DamageDealt>().Count(d => d.Target == Side.Player && d.Kind == DamageKind.Direct);
        Assert.NotEmpty(hits);
        Assert.Equal(strikes, hits.Count);
    }

    [Fact]
    public void FlamingRock_Explodes_OnEveryFifthHit()
    {
        var b = Fight("flaming_rock", null, "dagger");
        b.Steps(100); // 5 dagger hits by 10s
        Assert.Single(b.Events.OfType<StatusApplied>(), s => s.SourceId is int id && b.FindSource(id)!.Def.Id == "explosive");
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
