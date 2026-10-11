using ItemFighter.Core.Engine;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests;

public class BattleLoopTests
{
    private static readonly string Sword = Item("sword", 2, """{ "type": "Damage", "amount": 10 }""");

    [Fact]
    public void Item_FiresOnItsCooldown()
    {
        var db = TestContent.Db(("items/sword.json", Sword), ("enemies/dummy.json", Enemy(maxHp: 100)));
        var b = Fight(db, "dummy", ("sword", 0));
        b.Steps(20);
        var fired = Assert.Single(b.Of<SourceFired>());
        Assert.Equal(20, fired.Tick);
        Assert.Equal(90, b.Enemy.Hp);
    }

    [Fact]
    public void Shield_AbsorbsBeforeHp()
    {
        var db = TestContent.Db(("items/sword.json", Sword), ("enemies/dummy.json", Enemy(maxHp: 100)));
        var b = Fight(db, "dummy", ("sword", 0));
        b.Enemy.Shield = 15;
        b.Steps(20);
        Assert.Equal(5, b.Enemy.Shield);
        Assert.Equal(100, b.Enemy.Hp);
        b.Steps(20);
        Assert.Equal(0, b.Enemy.Shield);
        Assert.Equal(95, b.Enemy.Hp);
        var hits = b.Of<DamageDealt>().ToList();
        Assert.Equal((0, 10), (hits[0].HpDamage, hits[0].ShieldDamage));
        Assert.Equal((5, 5), (hits[1].HpDamage, hits[1].ShieldDamage));
    }

    [Fact]
    public void ShieldEffect_AddsShieldToOwner()
    {
        var db = TestContent.Db(("items/buckler.json", Item("buckler", 1, """{ "type": "Shield", "amount": 7 }""")), ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("buckler", 0));
        b.Steps(10);
        Assert.Equal(7, b.Player.Shield);
        Assert.Equal(7, Assert.Single(b.Of<ShieldGained>()).Amount);
    }

    [Fact]
    public void Heal_CappedAtMax()
    {
        var db = TestContent.Db(("items/apple.json", Item("apple", 1, """{ "type": "Heal", "amount": 30 }""")), ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("apple", 0));
        b.Player.Hp = 90;
        b.Steps(10);
        Assert.Equal(100, b.Player.Hp);
        Assert.Equal(10, Assert.Single(b.Of<Healed>()).Amount);
    }

    [Fact]
    public void TwoItems_SameTick_FireInSlotOrder()
    {
        var db = TestContent.Db(("items/sword.json", Sword), ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("sword", 3), ("sword", 0));
        b.Steps(20);
        var slots = b.Of<SourceFired>().Select(f => b.FindSource(f.SourceId)!.Slot).ToList();
        Assert.Equal(new int?[] { 0, 3 }, slots);
    }

    [Fact]
    public void EnemyAbility_FiresOnItsCooldown()
    {
        var db = TestContent.Db(("enemies/dummy.json", Enemy(maxHp: 50, abilityIds: "\"stab\"")),
            ("enemies/stab.json", Item("stab", 3, """{ "type": "Damage", "amount": 6 }""", extra: "\"kind\": \"EnemyAbility\",")));
        var b = Fight(db, "dummy");
        b.Steps(30);
        Assert.Equal(94, b.Player.Hp);
    }

    [Fact]
    public void NoFatigue_TimeAloneNeverDealsDamage()
    {
        var db = TestContent.Db(("enemies/dummy.json", Enemy(maxHp: 1000)));
        var b = Fight(db, "dummy");
        b.RunToEnd();
        Assert.Empty(b.Of<DamageDealt>());
        Assert.Equal(100, b.Player.Hp);
        Assert.Equal(1000, b.Enemy.Hp);
    }

    [Fact]
    public void Win_WhenEnemyDies()
    {
        var db = TestContent.Db(("items/sword.json", Sword), ("enemies/dummy.json", Enemy(maxHp: 25)));
        var b = Fight(db, "dummy", ("sword", 0));
        Assert.Equal(BattleResult.PlayerWon, b.RunToEnd());
        Assert.Equal(60, b.Tick);
        Assert.Equal(BattleResult.PlayerWon, b.Of<BattleEnded>().Single().Result);
    }

    [Fact]
    public void BothDieFromItems_SameTick_IsDraw()
    {
        var db = TestContent.Db(("items/sword.json", Sword), ("enemies/dummy.json", Enemy(maxHp: 10, abilityIds: "\"stab\"")),
            ("enemies/stab.json", Item("stab", 2, """{ "type": "Damage", "amount": 100 }""", extra: "\"kind\": \"EnemyAbility\",")));
        var b = Fight(db, "dummy", ("sword", 0));
        Assert.Equal(BattleResult.Draw, b.RunToEnd());
    }

    [Fact]
    public void TimeLimit_2Minutes_CountsAsLoss()
    {
        var db = TestContent.Db(("items/apple.json", Item("apple", 0.1m, """{ "type": "Heal", "amount": 1000 }""")),
            ("enemies/dummy.json", Enemy(maxHp: 1_000_000)));
        var b = Fight(db, "dummy", ("apple", 0));
        Assert.Equal(BattleResult.EnemyWon, b.RunToEnd());
        Assert.True(b.TimedOut);
        Assert.Equal(1200, Battle.TimeLimitTicks);
        Assert.Equal(Battle.TimeLimitTicks, b.Tick);
        Assert.Equal(BattleResult.EnemyWon, b.Of<BattleEnded>().Single().Result);
    }

    [Fact]
    public void KillingBlowOnLastTick_StillWins()
    {
        var db = TestContent.Db(("items/sword.json", Sword), ("enemies/dummy.json", Enemy(maxHp: 1000)));
        var b = Fight(db, "dummy", ("sword", 0));
        b.Steps(Battle.TimeLimitTicks - 1);
        b.Enemy.Hp = 1;
        b.Enemy.Shield = 0;
        // Sword fires every 20 ticks, so its last hit lands on tick 1200.
        Assert.Equal(BattleResult.PlayerWon, b.RunToEnd());
        Assert.False(b.TimedOut);
    }

    [Fact]
    public void Step_AfterEnd_DoesNothing()
    {
        var db = TestContent.Db(("items/sword.json", Sword), ("enemies/dummy.json", Enemy(maxHp: 5)));
        var b = Fight(db, "dummy", ("sword", 0));
        b.RunToEnd();
        int events = b.Events.Count;
        b.Step();
        Assert.Equal(events, b.Events.Count);
    }

    [Fact]
    public void InvalidBoard_Throws()
    {
        var db = TestContent.Db(("items/sword.json", Sword), ("enemies/dummy.json", Enemy()));
        Assert.Throws<ArgumentException>(() => Fight(db, "dummy", ("sword", 0), ("sword", 0)));
        Assert.Throws<ArgumentException>(() => Fight(db, "dummy", ("sword", Board.SlotCount)));
        Assert.Throws<ArgumentException>(() => Fight(db, "dummy", ("dummy", 0)));
    }

    [Fact]
    public void Battle_IsDeterministic()
    {
        var db = TestContent.Db(("items/sword.json", Sword), ("enemies/dummy.json", Enemy(maxHp: 300, abilityIds: "\"stab\"")),
            ("enemies/stab.json", Item("stab", 3, """{ "type": "Damage", "amount": 6 }""", extra: "\"kind\": \"EnemyAbility\",")));
        var a = Fight(db, "dummy", 9, ("sword", 0), ("sword", 2));
        var c = Fight(db, "dummy", 9, ("sword", 0), ("sword", 2));
        a.RunToEnd();
        c.RunToEnd();
        Assert.Equal(a.Log(), c.Log());
    }
}
