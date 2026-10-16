using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests;

public class StatusTests
{
    private static readonly string Sword = Item("sword", 2, """{ "type": "Damage", "amount": 10 }""");

    private static Battle Dummy(params (string Id, int Slot)[] board)
    {
        var db = TestContent.Db(("items/sword.json", Sword), ("enemies/dummy.json", Enemy(maxHp: 1000)));
        return Fight(db, "dummy", board);
    }

    private static List<int> FireTicks(Battle b) => b.Of<SourceFired>().Select(f => f.Tick).ToList();

    [Fact]
    public void Burn_TicksEvery5_AndDecays()
    {
        var b = Dummy();
        b.ApplyStatus(Side.Enemy, StatusType.Burn, 3, null, 0);
        b.Steps(20);
        var burns = b.Of<DamageDealt>().Where(d => d.Kind == DamageKind.Burn).ToList();
        Assert.Equal(new[] { 5, 10, 15 }, burns.Select(d => d.Tick));
        Assert.Equal(new[] { 3, 2, 1 }, burns.Select(d => d.HpDamage));
        Assert.Equal(0, b.Enemy.Statuses.Burn);
    }

    [Fact]
    public void Burn_DoubleAgainstShield()
    {
        var b = Dummy();
        b.Enemy.Shield = 4;
        b.ApplyStatus(Side.Enemy, StatusType.Burn, 5, null, 0);
        b.Steps(5);
        Assert.Equal(0, b.Enemy.Shield);
        Assert.Equal(997, b.Enemy.Hp);

        var c = Dummy();
        c.Enemy.Shield = 10;
        c.ApplyStatus(Side.Enemy, StatusType.Burn, 2, null, 0);
        c.Steps(5);
        Assert.Equal(6, c.Enemy.Shield);
        Assert.Equal(1000, c.Enemy.Hp);
    }

    [Fact]
    public void Burn_OddShield_RoundsUpShieldCost()
    {
        // burn 3 vs shield 3: shield takes 3 (using 2 burn points), 1 burn point reaches HP.
        var b = Dummy();
        b.Enemy.Shield = 3;
        b.ApplyStatus(Side.Enemy, StatusType.Burn, 3, null, 0);
        b.Steps(5);
        Assert.Equal(0, b.Enemy.Shield);
        Assert.Equal(999, b.Enemy.Hp);
    }

    [Fact]
    public void Poison_NoTickDamage_BelowThreshold()
    {
        var b = Dummy();
        b.ApplyStatus(Side.Enemy, StatusType.Poison, 9, null, 0);
        b.Steps(100);
        Assert.Equal(9, b.Enemy.Statuses.Poison);
        Assert.Empty(b.Of<DamageDealt>());
    }

    [Fact]
    public void Poison_BurstsAtTen_ShieldBlocksIt_Resets()
    {
        var b = Dummy();
        b.Enemy.Shield = 100;
        b.ApplyStatus(Side.Enemy, StatusType.Poison, 4, null, 0);
        b.ApplyStatus(Side.Enemy, StatusType.Poison, 4, null, 0);
        Assert.Empty(b.Of<PoisonBurst>());
        b.ApplyStatus(Side.Enemy, StatusType.Poison, 4, null, 0);
        Assert.Equal(36, Assert.Single(b.Of<PoisonBurst>()).Damage);
        Assert.Equal(1000, b.Enemy.Hp);
        Assert.Equal(100 - 36, b.Enemy.Shield);
        Assert.Equal(0, b.Enemy.Statuses.Poison);
    }

    [Fact]
    public void Chill_NewStackRefreshesTimer()
    {
        var b = Dummy();
        b.ApplyStatus(Side.Enemy, StatusType.Chill, 1, null, 0);
        b.Steps(10);
        b.ApplyStatus(Side.Enemy, StatusType.Chill, 1, null, 0);
        b.Steps(b.Rules.ChillTicks - 1);
        Assert.Equal(2, b.Enemy.Statuses.Chill);
        b.Steps(2);
        Assert.Equal(0, b.Enemy.Statuses.Chill);
    }

    [Fact]
    public void Chill_AddsFivePercentOfCooldownPerStack()
    {
        var b = Dummy(("sword", 0));
        b.ApplyStatus(Side.Player, StatusType.Chill, 2, null, 0);
        b.Steps(30);
        Assert.Equal(new[] { 22 }, FireTicks(b)); // 2s + 10%
    }

    [Fact]
    public void Chill_FiveStacks_FreezesCombatantOneSecond_AndClears()
    {
        var b = Dummy(("sword", 0));
        b.ApplyStatus(Side.Player, StatusType.Chill, 3, null, 0);
        b.ApplyStatus(Side.Player, StatusType.Chill, 2, null, 0);
        Assert.Single(b.Of<ChillFroze>());
        Assert.Equal(0, b.Player.Statuses.Chill);
        b.Steps(45);
        Assert.Equal(new[] { 30 }, FireTicks(b));
    }

    [Fact]
    public void Freeze_StopsCooldown()
    {
        var b = Dummy(("sword", 0));
        b.ApplyItemStatus(b.Player.Items.Single(), StatusType.Freeze, 10, null, 0);
        b.Steps(35);
        Assert.Equal(new[] { 30 }, FireTicks(b));
    }

    [Fact]
    public void Haste_DoublesSpeed()
    {
        var b = Dummy(("sword", 0));
        b.ApplyItemStatus(b.Player.Items.Single(), StatusType.Haste, 20, null, 0);
        b.Steps(25);
        Assert.Equal(new[] { 10, 20 }, FireTicks(b));
    }

    [Fact]
    public void FreezeBeatsHaste()
    {
        var b = Dummy(("sword", 0));
        var sword = b.Player.Items.Single();
        b.ApplyItemStatus(sword, StatusType.Haste, 100, null, 0);
        b.ApplyItemStatus(sword, StatusType.Freeze, 10, null, 0);
        b.Steps(10);
        Assert.Equal(0, sword.Progress);
    }

    [Fact]
    public void ApplyStatusEffect_FromJson_UsesLevelValues()
    {
        var db = TestContent.Db(
            ("items/flask.json", """
             { "id": "flask", "cooldown": 1, "levels": { "burn": [3, 5, 8, 12] },
               "abilities": [ { "effects": [ { "type": "ApplyStatus", "status": "Burn", "amount": "{burn}" } ] } ] }
             """),
            ("enemies/dummy.json", Enemy()));
        var b = new Battle(new BattleSetup(new PlayerSetup(null, [new BoardItem("flask", 0, Level: 2)], []), "dummy", 1), db);
        b.Steps(10);
        Assert.Equal(5, b.Enemy.Statuses.Burn);
        var applied = Assert.Single(b.Of<StatusApplied>());
        Assert.Equal((Side.Enemy, StatusType.Burn, 5), (applied.TargetSide!.Value, applied.Status, applied.Amount));
    }

    [Fact]
    public void ApplyStatusEffect_ItemStatus_TargetsRandomEnemyAbility()
    {
        var db = TestContent.Db(
            ("items/frost.json", Item("frost", 1, """{ "type": "ApplyStatus", "status": "Freeze", "amount": 30, "target": "RandomEnemyAbility" }""")),
            ("enemies/dummy.json", Enemy(abilityIds: "\"stab\"")),
            ("enemies/stab.json", Item("stab", 3, """{ "type": "Damage", "amount": 6 }""", extra: "\"kind\": \"EnemyAbility\",")));
        var b = Fight(db, "dummy", ("frost", 0));
        b.Steps(10);
        var stab = b.Enemy.Sources.Single(s => s.Def.Id == "stab");
        Assert.Equal(30, stab.FreezeTicks);
    }

    [Fact]
    public void ApplyStatusEffect_CombatantStatusOnItemTarget_IsContentError()
    {
        var result = TestContent.Load(("items/bad.json", Item("bad", 1, """{ "type": "ApplyStatus", "status": "Burn", "amount": 3, "target": "SelfItem" }""")));
        Assert.Contains("must target Self or Enemy", Assert.Single(result.Errors).Message);
    }
}
