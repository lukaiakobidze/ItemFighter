using ItemFighter.Content;
using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests;

/// <summary>Luka 2026-10-15: Pain, Nail and Uproot, plus the cleanse and copy-poison effects.</summary>
public class PainNailTests
{
    private static ContentDatabase Db(params (string, string)[] extra) =>
        TestContent.Db([("enemies/dummy.json", Enemy()), .. extra]);

    [Fact]
    public void Pain_EachOwnFire_HurtsForThePain_AndStays()
    {
        var db = Db(("items/sword.json", Item("sword", 2, """{ "type": "Damage", "amount": 1 }""")));
        var b = Fight(db, "dummy", ("sword", 0));
        b.ApplyStatus(Side.Player, StatusType.Pain, 3, null, 0);
        b.Steps(40);
        var pain = b.Of<DamageDealt>().Where(d => d.Kind == DamageKind.Pain).ToList();
        Assert.Equal(new[] { 20, 40 }, pain.Select(d => d.Tick));
        Assert.All(pain, d => Assert.Equal(3, d.Total));
        Assert.Equal(3, b.Player.Statuses.Pain);
    }

    [Fact]
    public void Pain_OnTheEnemy_HurtsItWhenItsAbilitiesFire()
    {
        var db = TestContent.Db(("enemies/dummy.json", Enemy(abilityIds: "\"bite\"")),
            ("enemies/abilities/bite.json", Item("bite", 2, """{ "type": "Damage", "amount": 1 }""", extra: "\"kind\": \"EnemyAbility\",")));
        var b = Fight(db, "dummy");
        b.ApplyStatus(Side.Enemy, StatusType.Pain, 2, null, 0);
        b.Steps(20);
        Assert.Equal(2, b.Enemy.MaxHp - b.Enemy.Hp);
    }

    [Fact]
    public void Pain_CountsAsTakingDamage_NotAsDealingIt()
    {
        var db = Db(("items/sword.json", Item("sword", 2, """{ "type": "Damage", "amount": 1 }""")),
            ("items/thorn.json", Item("thorn", null, """{ "type": "Shield", "amount": 1 }""", "\"OnDamageTaken\"")));
        var b = Fight(db, "dummy", ("sword", 0), ("thorn", 1));
        b.ApplyStatus(Side.Player, StatusType.Pain, 1, null, 0);
        b.Steps(20);
        Assert.Single(b.Of<ShieldGained>());
    }

    [Fact]
    public void Uproot_RemovesAllNails_ForNailDamageEach_AsOneHit()
    {
        var db = Db(("items/glove.json", Item("glove", 2, """{ "type": "Uproot" }""")));
        var b = Fight(db, "dummy", ("glove", 0));
        b.ApplyStatus(Side.Enemy, StatusType.Nail, 3, null, 0);
        b.Steps(20);
        var hit = Assert.Single(b.Of<DamageDealt>());
        Assert.Equal(3 * b.Rules.NailDamage, hit.Total);
        Assert.Equal(0, b.Enemy.Statuses.Nail);
        Assert.Single(b.Of<NailsUprooted>());
    }

    [Fact]
    public void NailDamage_FromRulesAndModifiers()
    {
        var db = Db(("items/glove.json", Item("glove", 2, """{ "type": "Uproot" }""")),
            ("passives/sharp.json", """{ "id": "sharp", "modifiers": [ { "stat": "NailDamage", "amount": 2 } ] }"""));
        db.Rules = CombatRules.Parse("""{ "nailDamage": 5 }""", out _);
        var b = new Battle(new BattleSetup(new PlayerSetup(null, [new BoardItem("glove", 0)], ["sharp"]), "dummy", 1), db);
        b.ApplyStatus(Side.Enemy, StatusType.Nail, 2, null, 0);
        b.Steps(20);
        Assert.Equal(2 * 7, b.Of<DamageDealt>().Single().Total);
    }

    [Fact]
    public void Uproot_WithNoNails_DoesNothing()
    {
        var db = Db(("items/glove.json", Item("glove", 2, """{ "type": "Uproot" }""")));
        var b = Fight(db, "dummy", ("glove", 0));
        b.Steps(20);
        Assert.Empty(b.Of<DamageDealt>());
    }

    [Fact]
    public void RemoveStatus_ClearsPoisonAndBurnOnYou_OnlyThose()
    {
        var db = Db(("items/clean.json", Item("clean", 2, """{ "type": "RemoveStatus", "statuses": ["Poison", "Burn"] }""")));
        var b = Fight(db, "dummy", ("clean", 0));
        b.ApplyStatus(Side.Player, StatusType.Poison, 4, null, 0);
        b.ApplyStatus(Side.Player, StatusType.Pain, 2, null, 0);
        b.Steps(19);
        b.ApplyStatus(Side.Player, StatusType.Burn, 1, null, 0);
        b.Steps(1);
        Assert.Equal(0, b.Player.Statuses.Poison);
        Assert.Equal(0, b.Player.Statuses.Burn);
        Assert.Equal(2, b.Player.Statuses.Pain);
    }

    [Fact]
    public void RemoveStatus_UnknownOrItemStatus_IsAnError()
    {
        Assert.NotEmpty(TestContent.Load(("items/x.json", Item("x", 2, """{ "type": "RemoveStatus", "statuses": ["Freeze"] }"""))).Errors);
        Assert.NotEmpty(TestContent.Load(("items/y.json", Item("y", 2, """{ "type": "RemoveStatus" }"""))).Errors);
    }

    [Fact]
    public void ApplyStatus_PercentOfCurrent_DoublesPoison()
    {
        var db = Db(("items/potion.json", Item("potion", 2, """{ "type": "ApplyStatus", "status": "Poison", "percentOfCurrent": 100 }""")));
        var b = Fight(db, "dummy", ("potion", 0));
        b.ApplyStatus(Side.Enemy, StatusType.Poison, 4, null, 0);
        b.Steps(20);
        Assert.Equal(8, b.Enemy.Statuses.Poison);
    }

    [Fact]
    public void ShippedItems_LoadAndDescribe()
    {
        var c = GameContent.Load();
        Assert.Empty(c.Errors);
        string Line(string id) => string.Join(" ", SourceDescriber.Lines(c.Database.Get(id), 1));
        Assert.Contains("Pain", Line("spiky_club"));
        Assert.Contains("remove all Poison and Burn from yourself", Line("tasty_cleanser"));
        Assert.Contains("Poison equal to the Poison already on the enemy", Line("aggravating_potion"));
        Assert.Contains("uproot", Line("magnetized_glove"));
        Assert.Equal(Activation.Active, c.Database.Get("magnetized_glove").Activation);
    }
}
