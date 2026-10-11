using ItemFighter.Core.Engine;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests;

public class ModifierTests
{
    private static Battle Setup(string? character, string[] passives, params (string Id, int Slot)[] board)
    {
        var db = TestContent.Db(
            ("items/buckler.json", Item("buckler", 1, """{ "type": "Shield", "amount": 10 }""")),
            ("items/flask.json", Item("flask", 1, """{ "type": "ApplyStatus", "status": "Burn", "amount": 2 }""", extra: "\"tags\": [\"Fire\"],")),
            ("items/rock.json", Item("rock", 1, """{ "type": "ApplyStatus", "status": "Burn", "amount": 2 }""")),
            ("items/fast.json", Item("fast", 2, """{ "type": "Damage", "amount": 1 }""", extra: "\"tags\": [\"Fire\"],")),
            ("passives/iron_skin.json", """{ "id": "iron_skin", "modifiers": [ { "stat": "ShieldGained", "amount": 5, "filter": { "kind": "Item" } } ] }"""),
            ("passives/kindling.json", """{ "id": "kindling", "modifiers": [ { "stat": "BurnApplied", "amount": 1, "filter": { "tag": "Fire" } } ] }"""),
            ("passives/haste_fire.json", """{ "id": "haste_fire", "modifiers": [ { "stat": "CooldownPercent", "amount": -50, "filter": { "tag": "Fire" } } ] }"""),
            ("passives/ultra.json", """{ "id": "ultra", "modifiers": [ { "stat": "CooldownPercent", "amount": -1000 } ] }"""),
            ("characters/knight.json", """
             { "id": "knight", "maxHp": 150,
               "abilities": [ { "trigger": "OnFightStart", "effects": [ { "type": "Shield", "amount": 20 } ] } ] }
             """),
            ("enemies/dummy.json", Enemy()));
        return new Battle(new BattleSetup(new PlayerSetup(character, board.Select(b => new BoardItem(b.Id, b.Slot)).ToList(), passives), "dummy", 1), db);
    }

    [Fact]
    public void Character_SetsMaxHp_AndRunsAbilities()
    {
        var b = Setup("knight", []);
        Assert.Equal(150, b.Player.MaxHp);
        b.Steps(1);
        Assert.Equal(20, b.Player.Shield);
    }

    [Fact]
    public void Passive_PlusFiveShieldFromItems_AppliesToItemShieldOnly()
    {
        var b = Setup("knight", ["iron_skin"], ("buckler", 0));
        b.Steps(10);
        Assert.Equal(20 + 15, b.Player.Shield);
    }

    [Fact]
    public void Modifier_TagFilter()
    {
        var b = Setup(null, ["kindling"], ("flask", 0), ("rock", 1));
        b.Steps(10);
        Assert.Equal(3 + 2, b.Enemy.Statuses.Burn);
    }

    [Fact]
    public void CooldownPercent_Minus50_Halves()
    {
        var b = Setup(null, ["haste_fire"], ("fast", 0), ("buckler", 1));
        Assert.Equal(10, b.Player.Items.Single(i => i.Def.Id == "fast").CooldownTicks);
        Assert.Equal(10, b.Player.Items.Single(i => i.Def.Id == "buckler").CooldownTicks);
    }

    [Fact]
    public void CooldownPercent_NeverBelowOneTick()
    {
        var b = Setup(null, ["ultra"], ("fast", 0));
        Assert.Equal(1, b.Player.Items.Single().CooldownTicks);
    }

    [Fact]
    public void NoProducer_NoModifier()
    {
        var b = Setup(null, ["iron_skin"]);
        b.GainShield(Side.Player, 10, null, 0);
        Assert.Equal(10, b.Player.Shield);
    }
}

public class EnemyPhaseTests
{
    private static Battle Boss()
    {
        var db = TestContent.Db(
            ("items/axe.json", Item("axe", 1, """{ "type": "Damage", "amount": 20 }""")),
            ("enemies/king.json", """
             { "id": "king", "maxHp": 100, "abilityIds": ["slash"],
               "phases": [ { "hpBelowPercent": 50, "addAbilities": ["rage"] } ] }
             """),
            ("enemies/slash.json", Item("slash", 5, """{ "type": "Damage", "amount": 1 }""", extra: "\"kind\": \"EnemyAbility\",")),
            ("enemies/rage.json", Item("rage", 1, """{ "type": "ApplyStatus", "status": "Burn", "amount": 2 }""", extra: "\"kind\": \"EnemyAbility\",")));
        return Fight(db, "king", ("axe", 0));
    }

    [Fact]
    public void Boss_AddsAbilitiesBelow50Percent_Once()
    {
        var b = Boss();
        b.Steps(20);
        Assert.Empty(b.Of<PhaseStarted>());
        b.Steps(10);
        var phase = Assert.Single(b.Of<PhaseStarted>());
        Assert.Equal((30, 0), (phase.Tick, phase.Index));
        Assert.Contains(b.Enemy.Sources, s => s.Def.Id == "rage");
        b.Steps(20);
        Assert.Single(b.Of<PhaseStarted>());
        Assert.Single(b.Enemy.Sources, s => s.Def.Id == "rage");
    }

    [Fact]
    public void EnemyAbility_UsesSameEffects()
    {
        var b = Boss();
        b.Steps(40);
        Assert.Equal(2, b.Player.Statuses.Burn);
        b.Steps(5);
        Assert.Contains(b.Of<DamageDealt>(), d => d.Target == Side.Player && d.Kind == DamageKind.Burn);
    }
}
