using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using ItemFighter.Core.Run;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests;

/// <summary>Mechanics for Luka's 2026-10-12 rare items and the Blur.</summary>
public class NewItemMechanicsTests
{
    private static int EnemyLost(Battle b) => b.Enemy.MaxHp - b.Enemy.Hp;

    [Fact]
    public void Damage_Hits_AreSeparateHits()
    {
        var db = TestContent.Db(("items/twin.json", Item("twin", 2, """{ "type": "Damage", "amount": 6, "hits": 2 }""")),
            ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("twin", 0));
        b.Steps(20);
        Assert.Equal(new[] { 6, 6 }, b.Of<DamageDealt>().Select(d => d.HpDamage));
    }

    [Fact]
    public void Damage_PercentOfShield_UsesCurrentShield()
    {
        var db = TestContent.Db(("items/buckler.json", Item("buckler", 2, """{ "type": "Damage", "percentOfShield": 100 }""")),
            ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("buckler", 0));
        b.Player.Shield = 13;
        b.Steps(20);
        Assert.Equal(13, EnemyLost(b));
        Assert.Equal(13, b.Player.Shield); // the shield is not spent
    }

    [Fact]
    public void Damage_PerItemWithTag_CountsEveryBoardItemWithTheTag_ItselfIncluded()
    {
        string weapon(string id) => Item(id, 100, """{ "type": "Damage", "amount": 0 }""", extra: "\"tags\": [\"Weapon\"],");
        var db = TestContent.Db(
            ("items/banner.json", Item("banner", 2, """{ "type": "Damage", "amount": 5, "perItem": 3, "perItemTag": "Weapon" }""", extra: "\"tags\": [\"Weapon\"],")),
            ("items/w1.json", weapon("w1")), ("items/w2.json", weapon("w2")),
            ("items/rock.json", Item("rock", 100, """{ "type": "Damage", "amount": 0 }""")),
            ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("banner", 0), ("w1", 1), ("w2", 2), ("rock", 3));
        b.Steps(20);
        Assert.Equal(5 + 3 * 3, EnemyLost(b));
    }

    [Fact]
    public void Damage_NeedsSomeAmount()
    {
        var r = TestContent.Load(("items/x.json", Item("x", 2, """{ "type": "Damage" }""")));
        Assert.NotEmpty(r.Errors);
        var half = TestContent.Load(("items/y.json", Item("y", 2, """{ "type": "Damage", "amount": 1, "perItem": 3 }""")));
        Assert.NotEmpty(half.Errors);
    }

    [Fact]
    public void GrowingDamage_PlusOneEachHit_WithModifyStat()
    {
        var db = TestContent.Db(("items/whet.json", Item("whet", 2,
                """{ "type": "Damage", "amount": 5 }, { "type": "ModifyStat", "stat": "Damage", "amount": 1, "target": "SelfItem" }""")),
            ("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy", ("whet", 0));
        b.Steps(60);
        Assert.Equal(new[] { 5, 6, 7 }, b.Of<DamageDealt>().Select(d => d.HpDamage));
    }

    private static ContentDatabase BlurDb(string minCooldown = "0.5") {
        var db = TestContent.Db(
            ("enemies/blur.json", Enemy("blur", abilityIds: "\"fast_reload\", \"rifle\", \"punch\"")),
            ("enemies/abilities/rifle.json", Item("rifle", 6, """{ "type": "Damage", "amount": 8 }""", extra: "\"kind\": \"EnemyAbility\", \"tags\": [\"Weapon\"],")),
            ("enemies/abilities/punch.json", Item("punch", 7, """{ "type": "Damage", "amount": 1 }""", extra: "\"kind\": \"EnemyAbility\", \"tags\": [\"Weapon\"],")),
            ("enemies/abilities/fast_reload.json", Item("fast_reload", null,
                """{ "type": "ReduceCooldown", "percent": 5, "target": "TriggeringItem" }""",
                """{ "type": "OnItemFired", "filter": ["Slot:1", "Tag:Weapon"] }""", extra: "\"kind\": \"EnemyAbility\",")));
        db.Rules = CombatRules.Parse($$"""{ "minCooldownSeconds": {{minCooldown}} }""", out _);
        return db;
    }

    [Fact]
    public void FastReload_EachFirstSlotWeaponShot_Is5PercentFaster_OnlyForSlotOne()
    {
        var b = Fight(BlurDb(), "blur");
        var rifle = b.Enemy.Sources.Single(s => s.Def.Id == "rifle");
        var punch = b.Enemy.Sources.Single(s => s.Def.Id == "punch");
        b.Steps(60);
        Assert.Equal(57, rifle.CooldownTicks); // 60 → 57
        b.Steps(57);
        Assert.Equal(54, rifle.CooldownTicks); // 57 → 54 (5% of the current cooldown, rounded down)
        Assert.Equal(70, punch.CooldownTicks);
    }

    [Fact]
    public void ReduceCooldown_KeepsTheFractionDone_NoFreeFire()
    {
        var b = Fight(BlurDb(), "blur");
        var punch = b.Enemy.Sources.Single(s => s.Def.Id == "punch");
        b.Steps(42); // punch (7s) is 60% charged
        b.ReduceCooldown(punch, 50, null, 0);
        Assert.Equal(35, punch.CooldownTicks);
        Assert.Equal(punch.FullProgress * 60 / 100, punch.Progress);
    }

    [Fact]
    public void Damage_HitsBelowOne_IsAnError()
    {
        var r = TestContent.Load(("items/x.json", Item("x", 2, """{ "type": "Damage", "amount": 1, "hits": 0 }""")));
        Assert.NotEmpty(r.Errors);
    }

    [Fact]
    public void FastReload_StopsAtTheMinimumCooldown()
    {
        var b = Fight(BlurDb("2"), "blur");
        b.Player.Hp = 1_000_000;
        var rifle = b.Enemy.Sources.Single(s => s.Def.Id == "rifle");
        b.Steps(1200);
        Assert.Equal(20, rifle.CooldownTicks);
    }

    [Fact]
    public void SlotFilter_ForThePlayer_IsTheBoardSlot()
    {
        var db = TestContent.Db(
            ("items/a.json", Item("a", 2, """{ "type": "Damage", "amount": 1 }""", extra: "\"tags\": [\"Weapon\"],")),
            ("items/b.json", Item("b", 2, """{ "type": "Damage", "amount": 1 }""", extra: "\"tags\": [\"Weapon\"],")),
            ("passives/reload.json", Item("reload", null, """{ "type": "ReduceCooldown", "percent": 50, "target": "TriggeringItem" }""",
                """{ "type": "OnItemFired", "filter": ["Slot:1", "Tag:Weapon"] }""")),
            ("enemies/dummy.json", Enemy()));
        var b = new Battle(new BattleSetup(new PlayerSetup(null, [new BoardItem("a", 0), new BoardItem("b", 1)], ["reload"]), "dummy", 1), db);
        b.Steps(20);
        Assert.Equal(10, b.Player.Items.Single(i => i.Def.Id == "a").CooldownTicks);
        Assert.Equal(20, b.Player.Items.Single(i => i.Def.Id == "b").CooldownTicks);
    }

    [Fact]
    public void GoldOnWin_PaysPerWin_WhileOnTheHotbar_ByLevel()
    {
        var db = TestContent.Db(
            ("items/pouch.json", """{ "id": "pouch", "levels": { "gold": [5, 6, 8, 9] }, "goldOnWin": "{gold}" }"""),
            ("items/sword.json", Item("sword", 2, """{ "type": "Damage", "amount": 1 }""")),
            ("enemies/dummy.json", """{ "id": "dummy", "maxHp": 10, "difficulty": 1 }"""));
        var run = RunState.New(1, null, new RunConfig(), db);
        run.AddItem("sword");
        run.AddItem("pouch");
        Assert.Equal(5, run.GoldFromItems);
        run.Choose(new Encounter(EncounterKind.Fight, "dummy"));
        run.StartFight();
        run.FinishFight(true);
        Assert.Equal(10 + 5 + 5, run.Gold);
        Assert.Contains(SourceDescriber.Lines(db.Get("pouch"), 3), l => l.Contains("+8 gold"));
    }

    [Fact]
    public void GoldOnWin_InTheBackpack_PaysNothing()
    {
        var db = TestContent.Db(("items/pouch.json", """{ "id": "pouch", "goldOnWin": 5 }"""));
        var run = RunState.New(1, null, new RunConfig { HotbarSize = 1 }, db);
        run.Backpack.Add(new OwnedItem("pouch", 1));
        Assert.Equal(0, run.GoldFromItems);
    }
}
