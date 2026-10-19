using ItemFighter.Content;
using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests;

/// <summary>Luka 2026-10-19: Heat, Overheat and the Locomotive Engineer.</summary>
public class HeatTests
{
    private static ContentDatabase Db(params (string Path, string Json)[] extra) =>
        TestContent.Db([("enemies/dummy.json", Enemy()), .. extra]);

    [Fact]
    public void Heat_Builds_AndOverheats_AtTheThreshold()
    {
        var b = Fight(Db(), "dummy");
        b.ApplyStatus(Side.Player, StatusType.Heat, 60, null, 0);
        Assert.Equal(60, b.Player.Statuses.Heat);
        Assert.Empty(b.Of<Overheated>());

        b.ApplyStatus(Side.Player, StatusType.Heat, 40, null, 0);
        Assert.Equal(0, b.Player.Statuses.Heat);
        var o = Assert.Single(b.Of<Overheated>());
        Assert.Equal(Side.Player, o.DamageTarget);
        Assert.Equal(b.Player.MaxHp - b.Rules.OverheatDamage, b.Player.Hp);
    }

    [Fact]
    public void Overheat_StallsEveryItem()
    {
        var db = Db(("items/stick.json", Item("stick", 1, """{ "type": "Damage", "amount": 1 }""")));
        var b = Fight(db, "dummy", ("stick", 0));
        b.ApplyStatus(Side.Player, StatusType.Heat, 100, null, 0);
        b.Steps(b.Rules.OverheatStallTicks);
        Assert.DoesNotContain(b.Of<DamageDealt>(), d => d.Target == Side.Enemy);
        b.Steps(12);
        Assert.Contains(b.Of<DamageDealt>(), d => d.Target == Side.Enemy);
    }

    [Fact]
    public void Heat_DoesNotCool_ByDefault()
    {
        var b = Fight(Db(), "dummy");
        b.ApplyStatus(Side.Player, StatusType.Heat, 30, null, 0);
        b.Steps(100);
        Assert.Equal(30, b.Player.Statuses.Heat);
    }

    [Fact]
    public void HotHeat_StopsFreeze_ButChillStillSlows_AndFreezesWhenHeatDrops()
    {
        var b = Fight(Db(), "dummy");
        b.ApplyStatus(Side.Player, StatusType.Heat, b.Rules.HeatFreezeImmunityAt, null, 0);
        b.ApplyStatus(Side.Player, StatusType.Chill, 7, null, 0);
        Assert.Empty(b.Of<ChillFroze>());
        Assert.Equal(b.Rules.ChillFreezeAtStacks, b.Player.Statuses.Chill);

        b.Player.Statuses.Heat = 0;
        b.Steps(1);
        Assert.Single(b.Of<ChillFroze>());
        Assert.True(b.Player.IsFrozenAt(b.Tick));
    }

    [Fact]
    public void HotIron_AddsOnePerFiveHeat()
    {
        var b = Fight(GameContent.Load().Database, "squire_zombie", ("hot_iron", 0));
        b.ApplyStatus(Side.Player, StatusType.Heat, 23, null, 0);
        b.Steps(80);
        var hit = b.Of<DamageDealt>().First(d => d.Target == Side.Enemy);
        Assert.Equal(5 + 4, hit.Total);
    }

    [Fact]
    public void Engineer_OverheatHitsTheEnemy_AndStallsHalfAsLong()
    {
        var db = GameContent.Load().Database;
        var b = new Battle(new BattleSetup(new PlayerSetup("locomotive_engineer", [], []), "squire_zombie", 1), db);
        Assert.Equal(120, b.Player.MaxHp);
        b.ApplyStatus(Side.Player, StatusType.Heat, 100, null, 0);
        var o = Assert.Single(b.Of<Overheated>());
        Assert.Equal(Side.Enemy, o.DamageTarget);
        Assert.Equal(b.Rules.OverheatStallTicks / 2, o.StallTicks);
        Assert.Equal(120, b.Player.Hp);
        Assert.Contains(b.Of<DamageDealt>(), d => d.Target == Side.Enemy && d.Kind == DamageKind.Overheat);
    }

    [Fact]
    public void ShovelOfCoal_AddsFiveHeatEveryFiveSeconds()
    {
        var b = Fight(GameContent.Load().Database, "squire_zombie", ("shovel_of_coal", 0));
        b.Steps(100);
        Assert.Equal(10, b.Player.Statuses.Heat);
    }
}
