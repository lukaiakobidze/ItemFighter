using ItemFighter.Core.Engine;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests;

public class TriggerTests
{
    private static readonly string Sword = Item("sword", 2, """{ "type": "Damage", "amount": 10 }""");

    private static string Reactor(string id, string trigger, string effect = """{ "type": "Damage", "amount": 1 }""") =>
        Item(id, null, effect, trigger: trigger);

    private static List<int> ReactionsBy(Battle b, string defId) =>
        b.Of<SourceFired>().Where(f => f.Reaction && b.FindSource(f.SourceId)!.Def.Id == defId).Select(f => f.Tick).ToList();

    [Fact]
    public void OnFightStart_FiresOnce_AtTickZero()
    {
        var db = TestContent.Db(("items/r.json", Reactor("r", "\"OnFightStart\"", """{ "type": "Shield", "amount": 5 }""")), ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("r", 0));
        b.Steps(50);
        Assert.Equal(new[] { 0 }, ReactionsBy(b, "r"));
        Assert.Equal(5, b.Player.Shield);
    }

    [Fact]
    public void OnItemFired_Adjacent_OnlyNeighboursReact()
    {
        var adjacent = Reactor("adj", """{ "type": "OnItemFired", "filter": "Adjacent" }""");
        var db = TestContent.Db(("items/sword.json", Sword), ("items/adj.json", adjacent), ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("sword", 2), ("adj", 1), ("adj", 3), ("adj", 5));
        b.Steps(20);
        var reacted = b.Of<SourceFired>().Where(f => f.Reaction).Select(f => b.FindSource(f.SourceId)!.Slot).ToList();
        Assert.Equal(new int?[] { 1, 3 }, reacted);
    }

    [Fact]
    public void OnItemFired_TagFilter_And_Adjacent_Combined()
    {
        var bellows = Reactor("bellows", """{ "type": "OnItemFired", "filter": ["Adjacent", "Tag:Fire"] }""");
        var flask = Item("flask", 2, """{ "type": "Damage", "amount": 1 }""", extra: "\"tags\": [\"Fire\"],");
        var db = TestContent.Db(("items/sword.json", Sword), ("items/flask.json", flask), ("items/bellows.json", bellows), ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("sword", 0), ("bellows", 1), ("flask", 2));
        b.Steps(20);
        var reaction = Assert.Single(b.Of<SourceFired>(), f => f.Reaction);
        Assert.Equal(20, reaction.Tick);
    }

    [Fact]
    public void OnItemFired_DoesNotReactToItself_UnlessSelfFilter()
    {
        var any = Item("any", 2, """{ "type": "Damage", "amount": 1 }""", trigger: """{ "type": "OnItemFired", "filter": "AnyOwn" }""");
        var db = TestContent.Db(("items/any.json", any), ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("any", 0));
        b.Steps(20);
        Assert.DoesNotContain(b.Of<SourceFired>(), f => f.Reaction);
    }

    [Fact]
    public void OnItemFired_SelfFilter_ReactsToOwnFire()
    {
        var self = """
        { "id": "self", "cooldown": 2, "abilities": [
            { "trigger": "OnCooldown", "effects": [ { "type": "Damage", "amount": 1 } ] },
            { "trigger": { "type": "OnItemFired", "filter": "Self" }, "effects": [ { "type": "Shield", "amount": 3 } ] } ] }
        """;
        var db = TestContent.Db(("items/self.json", self), ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("self", 0));
        b.Steps(20);
        Assert.Equal(3, b.Player.Shield);
    }

    [Fact]
    public void OnDamageTaken_ReactsWhenOwnerHit()
    {
        var spikes = Reactor("spikes", "\"OnDamageTaken\"", """{ "type": "Damage", "amount": 3 }""");
        var db = TestContent.Db(("items/spikes.json", spikes), ("enemies/dummy.json", Enemy(maxHp: 100, abilityIds: "\"stab\"")),
            ("enemies/stab.json", Item("stab", 2, """{ "type": "Damage", "amount": 5 }""", extra: "\"kind\": \"EnemyAbility\",")));
        var b = Fight(db, "dummy", ("spikes", 0));
        b.Steps(40);
        Assert.Equal(94, b.Enemy.Hp);
    }

    [Fact]
    public void OnHpBelow_FiresOnce()
    {
        var panic = Reactor("panic", """{ "type": "OnHpBelow", "percent": 50 }""", """{ "type": "Heal", "amount": 1 }""");
        var db = TestContent.Db(("items/panic.json", panic), ("enemies/dummy.json", Enemy(maxHp: 100, abilityIds: "\"stab\"")),
            ("enemies/stab.json", Item("stab", 1, """{ "type": "Damage", "amount": 20 }""", extra: "\"kind\": \"EnemyAbility\",")));
        var b = Fight(db, "dummy", ("panic", 0));
        b.Steps(40);
        Assert.Equal(new[] { 30 }, ReactionsBy(b, "panic"));
    }

    [Fact]
    public void OnStatusApplied_EnemySide()
    {
        var watcher = Reactor("watch", """{ "type": "OnStatusApplied", "status": "Burn", "side": "Enemy" }""", """{ "type": "Shield", "amount": 2 }""");
        var db = TestContent.Db(("items/watch.json", watcher), ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("watch", 0));
        b.ApplyStatus(Side.Player, StatusType.Burn, 1, null, 0);
        Assert.Equal(0, b.Player.Shield);
        b.ApplyStatus(Side.Enemy, StatusType.Poison, 1, null, 0);
        Assert.Equal(0, b.Player.Shield);
        b.ApplyStatus(Side.Enemy, StatusType.Burn, 1, null, 0);
        Assert.Equal(2, b.Player.Shield);
    }

    [Fact]
    public void OnShieldGained_ReactsToOtherSources()
    {
        var spiky = Reactor("spiky", "\"OnShieldGained\"", """{ "type": "Damage", "amount": 4 }""");
        var buckler = Item("buckler", 1, """{ "type": "Shield", "amount": 5 }""");
        var db = TestContent.Db(("items/spiky.json", spiky), ("items/buckler.json", buckler), ("enemies/dummy.json", Enemy(maxHp: 100)));
        var b = Fight(db, "dummy", ("spiky", 0), ("buckler", 1));
        b.Steps(10);
        Assert.Equal(96, b.Enemy.Hp);
    }

    [Fact]
    public void EveryNthUse_FiresOnThird()
    {
        var item = """
        { "id": "third", "cooldown": 1, "abilities": [
            { "trigger": "OnCooldown", "effects": [ { "type": "Damage", "amount": 1 } ] },
            { "trigger": { "type": "EveryNthUse", "n": 3 }, "effects": [ { "type": "Damage", "amount": 100 } ] } ] }
        """;
        var db = TestContent.Db(("items/third.json", item), ("enemies/dummy.json", Enemy(maxHp: 1000)));
        var b = Fight(db, "dummy", ("third", 0));
        b.Steps(60);
        Assert.Equal(new[] { 30, 60 }, ReactionsBy(b, "third"));
        Assert.Equal(1000 - 6 - 200, b.Enemy.Hp);
    }

    [Fact]
    public void MutualReactionLoop_StopsAtDepth8_BattleContinues()
    {
        var a = """
        { "id": "a", "cooldown": 1, "abilities": [
            { "trigger": "OnCooldown", "effects": [ { "type": "Damage", "amount": 1 } ] },
            { "trigger": { "type": "OnItemFired", "filter": "Adjacent" }, "effects": [ { "type": "Damage", "amount": 1 } ] } ] }
        """;
        var bReactor = Reactor("b", """{ "type": "OnItemFired", "filter": "Adjacent" }""");
        var db = TestContent.Db(("items/a.json", a), ("items/b.json", bReactor), ("enemies/dummy.json", Enemy(maxHp: 1000)));
        var battle = Fight(db, "dummy", ("a", 0), ("b", 1));
        battle.Steps(10);
        Assert.Single(battle.Of<ChainCapped>());
        Assert.Equal(Battle.MaxChainDepth, battle.Of<SourceFired>().Count(f => f.Reaction));
        Assert.Equal(1000 - 1 - Battle.MaxChainDepth, battle.Enemy.Hp);
        Assert.NotEqual(BattleResult.Ongoing, battle.RunToEnd());
    }

    [Fact]
    public void Reaction_CanTargetTheTriggeringItem()
    {
        var bellows = Reactor("bellows", """{ "type": "OnItemFired", "filter": "Adjacent" }""", """{ "type": "Charge", "amount": 10, "target": "TriggeringItem" }""");
        var db = TestContent.Db(("items/sword.json", Sword), ("items/bellows.json", bellows), ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("sword", 0), ("bellows", 1), ("sword", 2));
        b.Steps(20);
        var charged = b.Of<Charged>().Select(c => b.FindSource(c.TargetSourceId)!.Slot).ToList();
        Assert.Equal(new int?[] { 0, 2 }, charged);
    }
}
