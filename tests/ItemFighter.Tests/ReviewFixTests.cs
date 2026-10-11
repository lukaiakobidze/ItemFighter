using System.Diagnostics;
using ItemFighter.Core.Engine;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests;

/// <summary>Regression tests for the whole-branch review findings.</summary>
public class ReviewFixTests
{
    private static readonly string Sword = Item("sword", 2, """{ "type": "Damage", "amount": 10 }""");

    [Fact]
    public void CopyEffect_AdjacentLoop_IsCappedInsteadOfOverflowing()
    {
        var mirror = Item("mirror", 1, """{ "type": "CopyEffect", "target": "AdjacentItems" }""");
        var db = TestContent.Db(("items/mirror.json", mirror), ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("mirror", 0), ("mirror", 1));
        b.Steps(20);
        Assert.NotEmpty(b.Of<ChainCapped>());
    }

    [Fact]
    public void CopyEffect_SelfTarget_DoesNotCopyItself()
    {
        var self = Item("selfcopy", 1, """{ "type": "Damage", "amount": 1 }, { "type": "CopyEffect", "target": "SelfItem" }""");
        var db = TestContent.Db(("items/selfcopy.json", self), ("enemies/dummy.json", Enemy(maxHp: 1000)));
        var b = Fight(db, "dummy", ("selfcopy", 0));
        b.Steps(10);
        Assert.Equal(999, b.Enemy.Hp);
    }

    [Fact]
    public void ReactionFanOut_IsBoundedPerTick()
    {
        var any = Item("any", null, """{ "type": "Damage", "amount": 1 }""", trigger: """{ "type": "OnItemFired", "filter": "AnyOwn" }""");
        var db = TestContent.Db(("items/sword.json", Sword), ("items/any.json", any), ("enemies/dummy.json", Enemy(maxHp: 1_000_000)));
        var b = Fight(db, "dummy", ("sword", 0), ("any", 1), ("any", 2), ("any", 3), ("any", 4), ("any", 5));
        var watch = Stopwatch.StartNew();
        b.Steps(20);
        Assert.True(b.Of<SourceFired>().Count(f => f.Reaction) <= Battle.MaxReactionsPerTick);
        Assert.True(b.Events.Count < 5000, $"events: {b.Events.Count}");
        Assert.True(watch.ElapsedMilliseconds < 500);
    }

    [Fact]
    public void FrozenItem_ChargedToFull_WaitsForFreezeToEnd()
    {
        var gear = Item("gear", 1, """{ "type": "Charge", "amount": 30, "target": "LeftItem" }""");
        var db = TestContent.Db(("items/sword.json", Sword), ("items/gear.json", gear), ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("sword", 0), ("gear", 1));
        var sword = b.AllSources.First(s => s.Def.Id == "sword");
        b.Step();
        b.ApplyItemStatus(sword, StatusType.Freeze, 50, null, 0);
        b.Steps(40);
        Assert.DoesNotContain(b.Of<SourceFired>(), f => f.SourceId == sword.Id && f.Tick > 1);
        b.Steps(20);
        Assert.Contains(b.Of<SourceFired>(), f => f.SourceId == sword.Id && f.Tick > 50);
    }

    [Fact]
    public void Heal_DoesNotReviveADeadCombatant()
    {
        var bigHit = Item("big", 1, """{ "type": "Damage", "amount": 50 }""");
        var db = TestContent.Db(("items/big.json", bigHit),
            ("enemies/dummy.json", Enemy(maxHp: 40, abilityIds: "\"second\"")),
            ("enemies/second.json", Item("second", null, """{ "type": "Heal", "amount": 30 }""",
                trigger: """{ "type": "OnHpBelow", "percent": 50 }""", extra: "\"kind\": \"EnemyAbility\",")));
        var b = Fight(db, "dummy", ("big", 0));
        b.Steps(10); // the first hit lands on tick 10
        Assert.Equal(BattleResult.PlayerWon, b.Result);
        Assert.Empty(b.Of<Healed>());
    }

    [Theory]
    [InlineData("""{ "id": "bad", "cooldown": 1, "levels": { "d": ["1", 2, 3, 4] }, "abilities": [] }""")]
    [InlineData("""{ "id": "bad", "cooldown": 1e10, "abilities": [] }""")]
    public void Loader_ReportsOddValues_AndKeepsLoadingOtherFiles(string bad)
    {
        var result = TestContent.Load(("items/bad.json", bad), ("items/sword.json", Sword));
        Assert.Contains(result.Errors, e => e.Id == "bad");
        Assert.True(result.Database.TryGet("sword", out _));
    }
}

public class ScreenReviewFixTests
{
    [Theory]
    [InlineData("OwnItemTag:")]
    [InlineData("OwnItemtag:Weapon")]
    [InlineData("Anything")]
    public void Validator_RejectsBadTargetFilters(string filter)
    {
        var json = $$"""{ "id": "hone", "cooldown": 1, "activation": "Active", "requiresTarget": true, "targetFilter": "{{filter}}", "abilities": [] }""";
        var result = TestContent.Load(("items/hone.json", json));
        Assert.Contains(Core.Content.ContentValidator.Validate(result.Database, new ScriptRegistry()), e => e.Id == "hone" && e.Message.Contains("targetFilter"));
    }

    [Fact]
    public void Article_OfEmptyWord_DoesNotThrow() => Assert.Equal("a ", Core.Content.Words.Article(""));
}
