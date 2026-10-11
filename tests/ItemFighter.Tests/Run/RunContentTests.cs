using ItemFighter.Content;
using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using ItemFighter.Core.Run;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests.Run;

/// <summary>Content the run loop needs: enemy drop items, run.json, enemy passives for the player, HP scaling.</summary>
public class RunContentTests
{
    private static readonly string Sword = Item("sword", 2, """{ "type": "Damage", "amount": 10 }""");

    [Fact]
    public void Enemy_DropItem_Loads_AndMustBeAnItem()
    {
        var db = TestContent.Db(("items/sword.json", Sword),
            ("enemies/zombie.json", """{ "id": "zombie", "maxHp": 10, "dropItem": "sword" }"""));
        Assert.Equal("sword", db.Get("zombie").DropItemId);

        var bad = TestContent.Load(("items/sword.json", Sword),
            ("enemies/zombie.json", """{ "id": "zombie", "maxHp": 10, "dropItem": "zombie" }"""));
        var errors = ContentValidator.Validate(bad.Database, new ScriptRegistry());
        Assert.Contains(errors, e => e.Message.Contains("dropItem"));
        var missing = TestContent.Load(("enemies/zombie.json", """{ "id": "zombie", "maxHp": 10, "dropItem": "nope" }"""));
        Assert.Contains(ContentValidator.Validate(missing.Database, new ScriptRegistry()), e => e.Message.Contains("dropItem"));
    }

    [Theory]
    [InlineData("squire_zombie", "sword")]
    [InlineData("archer_skeleton", "ice_bow")]
    [InlineData("green_snake", "venom_fang")]
    [InlineData("flaming_rock", "cinder")]
    [InlineData("grizzly_bear", "tower_shield")]
    public void AllShippedEnemies_HaveDropItems_MatchingLuka(string enemy, string item)
    {
        var content = GameContent.Load();
        Assert.Empty(content.Errors);
        Assert.Equal(item, content.Database.Get(enemy).DropItemId);
    }

    [Fact]
    public void SilverKnight_StartsWithSwordAndMetalPlating()
    {
        var content = GameContent.Load();
        Assert.Equal(new[] { "sword", "metal_plating" }, content.Database.Get("silver_knight").StartingItems);
    }

    [Fact]
    public void BattleSetup_EnemyHpPercent_ScalesMaxHp()
    {
        var db = TestContent.Db(("enemies/dummy.json", Enemy(maxHp: 40)));
        var b = new Battle(new BattleSetup(new PlayerSetup(null, [], []), "dummy", 1, EnemyHpPercent: 150), db);
        Assert.Equal(60, b.Enemy.MaxHp);
        Assert.Equal(60, b.Enemy.Hp);
    }

    [Fact]
    public void Player_CanTakeEnemyPassive_Venomous_PoisonsEnemyOnHit()
    {
        var venomous = Item("venomous", null, """{ "type": "ApplyStatus", "status": "Poison", "amount": 2 }""", "\"OnDamageDealt\"",
            extra: "\"kind\": \"EnemyAbility\",");
        var db = TestContent.Db(("items/sword.json", Sword), ("enemies/venomous.json", venomous), ("enemies/dummy.json", Enemy()));
        Assert.True(db.Get("venomous").IsPassiveAbility);
        var b = new Battle(new BattleSetup(new PlayerSetup(null, [new BoardItem("sword", 0)], ["venomous"]), "dummy", 1), db);
        b.Steps(20);
        Assert.Equal(2, b.Enemy.Statuses.Poison);
    }

    [Fact]
    public void Player_CannotTakeCooldownAbility()
    {
        var bite = Item("bite", 3, """{ "type": "Damage", "amount": 1 }""", extra: "\"kind\": \"EnemyAbility\",");
        var db = TestContent.Db(("enemies/bite.json", bite), ("enemies/dummy.json", Enemy()));
        Assert.False(db.Get("bite").IsPassiveAbility);
        Assert.ThrowsAny<Exception>(() => new Battle(new BattleSetup(new PlayerSetup(null, [], ["bite"]), "dummy", 1), db));
    }

    [Fact]
    public void RunConfig_ShippedFile_ParsesWithoutErrors()
    {
        var content = GameContent.Load();
        Assert.Empty(content.Errors);
        var run = content.Run;
        Assert.Equal(5, run.Missions);
        Assert.Equal(10, run.EncountersPerMission);
        Assert.Equal(12, run.BackpackSize);
        Assert.Equal(50, run.SellPercent);
        Assert.Equal(3, run.CharacterPoolMultiplier);
        Assert.Equal(15, run.TagBonusPercent);
        Assert.Equal(5, run.Bosses.Count);
    }

    [Fact]
    public void RunConfig_MissingKeys_UseDefaults()
    {
        var db = TestContent.Db(("enemies/dummy.json", Enemy()));
        var config = RunConfig.Parse("""{ "backpackSize": 8 }""", db, out var errors);
        Assert.Empty(errors);
        Assert.Equal(8, config.BackpackSize);
        Assert.Equal(6, config.HotbarSize);
        Assert.Equal(50, config.ItemDropChancePercent);
    }

    [Fact]
    public void RunConfig_UnknownBoss_IsError()
    {
        var db = TestContent.Db(("enemies/dummy.json", Enemy()));
        RunConfig.Parse("""{ "bosses": [ { "enemy": "nope" } ] }""", db, out var errors);
        Assert.Contains(errors, e => e.Contains("nope"));
        RunConfig.Parse("""{ "backpackSize": "lots" """, db, out var broken);
        Assert.NotEmpty(broken);
    }
}

public class EnemyPassiveWordingTests
{
    [Fact]
    public void EnemyPassive_TakenByPlayer_IsWordedForThePlayer()
    {
        var content = GameContent.Load();
        var venomous = content.Database.Get("venomous");
        string enemyView = string.Join(" ", SourceDescriber.Lines(venomous, 1));
        string playerView = string.Join(" ", SourceDescriber.Lines(venomous, 1, ownedByPlayer: true));
        Assert.Contains("you", enemyView);
        Assert.Contains("the enemy", playerView);
        Assert.DoesNotContain(" you", playerView.Replace("When you", ""));
    }
}
