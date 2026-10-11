using ItemFighter.Core.Engine;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests;

/// <summary>Luka's rules from 2026-10-11: slow adds seconds, chill lengthens cooldowns, charge overflows, plus enemy passives.</summary>
public class NewRulesTests
{
    private static string EnemyAbility(string id, decimal? cooldown, string effects, string trigger = "\"OnCooldown\"") =>
        Item(id, cooldown, effects, trigger, extra: "\"kind\": \"EnemyAbility\",");

    private static List<int> Fires(Battle b, string defId) =>
        b.Of<SourceFired>().Where(f => !f.Reaction && b.FindSource(f.SourceId)!.Def.Id == defId).Select(f => f.Tick).ToList();

    [Fact]
    public void Slow_AddsSecondsToTheCurrentCooldown_OfEveryEnemyAbility()
    {
        var weights = Item("weights", null, """{ "type": "Slow", "seconds": 2 }""", trigger: "\"OnFightStart\"");
        var db = TestContent.Db(("items/weights.json", weights),
            ("enemies/dummy.json", Enemy(abilityIds: "\"bite\"")), ("enemies/bite.json", EnemyAbility("bite", 3, """{ "type": "Damage", "amount": 1 }""")));
        var b = Fight(db, "dummy", ("weights", 0));
        b.Steps(80);
        Assert.Equal(new[] { 50, 80 }, Fires(b, "bite"));
    }

    [Fact]
    public void Slow_WorksOnThePlayerToo()
    {
        var sword = Item("sword", 2, """{ "type": "Damage", "amount": 1 }""");
        var db = TestContent.Db(("items/sword.json", sword),
            ("enemies/dummy.json", Enemy(abilityIds: "\"drag\"")), ("enemies/drag.json", EnemyAbility("drag", null, """{ "type": "Slow", "seconds": 1.5 }""", "\"OnFightStart\"")));
        var b = Fight(db, "dummy", ("sword", 0));
        b.Steps(40);
        Assert.Equal(new[] { 35 }, Fires(b, "sword"));
    }

    [Fact]
    public void Charge_Overflow_CarriesIntoTheNextCooldown()
    {
        var slowSword = Item("big", 2, """{ "type": "Damage", "amount": 1 }""");
        var charger = Item("charger", 1.5m, """{ "type": "Charge", "seconds": 1, "target": "LeftItem" }""");
        var db = TestContent.Db(("items/big.json", slowSword), ("items/charger.json", charger), ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("big", 0), ("charger", 1));
        b.Steps(31);
        // Tick 15: 1.5s done + 1s charge = fires with 0.5s left over, so the next fire is 1.5s later.
        Assert.Equal(new[] { 15, 30 }, Fires(b, "big"));
    }

    [Fact]
    public void OnDamageDealt_Venomous_PoisonsOnEveryHit()
    {
        var db = TestContent.Db(("items/rock.json", Item("rock", 9, """{ "type": "Shield", "amount": 1 }""")),
            ("enemies/snake.json", Enemy("snake", abilityIds: "\"strike\", \"venomous\"")),
            ("enemies/strike.json", EnemyAbility("strike", 1, """{ "type": "Damage", "amount": 3 }""")),
            ("enemies/venomous.json", EnemyAbility("venomous", null, """{ "type": "ApplyStatus", "status": "Poison", "amount": 2 }""", "\"OnDamageDealt\"")));
        var b = Fight(db, "snake", ("rock", 0));
        b.Steps(30);
        Assert.Equal(6, b.Player.Statuses.Poison);
    }

    [Fact]
    public void OnDamageTaken_Every5th()
    {
        var dagger = Item("dagger", 1, """{ "type": "Damage", "amount": 1 }""");
        var db = TestContent.Db(("items/dagger.json", dagger),
            ("enemies/rock.json", Enemy("rock", abilityIds: "\"explosive\"")),
            ("enemies/explosive.json", EnemyAbility("explosive", null, """{ "type": "ApplyStatus", "status": "Burn", "amount": 3 }""",
                """{ "type": "OnDamageTaken", "every": 5 }""")));
        var b = Fight(db, "rock", ("dagger", 0));
        b.Steps(100);
        Assert.Equal(2, b.Of<StatusApplied>().Count(s => s.Status == StatusType.Burn));
    }

    [Fact]
    public void DamageTaken_Modifier_ReducesEveryHit_IncludingBurn()
    {
        var sword = Item("sword", 1, """{ "type": "Damage", "amount": 5 }""");
        var db = TestContent.Db(("items/sword.json", sword),
            ("enemies/bear.json", Enemy("bear", maxHp: 100, abilityIds: "\"thick_skin\"")),
            ("enemies/thick_skin.json", """{ "id": "thick_skin", "kind": "EnemyAbility", "modifiers": [ { "stat": "DamageTaken", "amount": -1 } ] }"""));
        var b = Fight(db, "bear", ("sword", 0));
        b.Steps(10);
        Assert.Equal(96, b.Enemy.Hp);
        b.ApplyStatus(Side.Enemy, StatusType.Burn, 3, null, 0);
        b.Steps(5);
        Assert.Equal(94, b.Enemy.Hp);
    }

    [Fact]
    public void Shield_PercentOfMaxHp()
    {
        var knight = """
            { "id": "knight", "maxHp": 140, "abilities": [
              { "trigger": "OnFightStart", "effects": [ { "type": "Shield", "percentOfMaxHp": 5 } ] } ] }
            """;
        var db = TestContent.Db(("characters/knight.json", knight), ("enemies/dummy.json", Enemy()));
        var b = new Battle(new BattleSetup(new PlayerSetup("knight", [], []), "dummy", 1), db);
        b.Step();
        Assert.Equal(7, b.Player.Shield);
    }

    [Fact]
    public void Difficulty_IsRead()
    {
        var db = TestContent.Db(("enemies/dummy.json", """{ "id": "dummy", "maxHp": 10, "difficulty": "Easy" }"""));
        Assert.Equal("Easy", db.Get("dummy").Difficulty);
    }
}
