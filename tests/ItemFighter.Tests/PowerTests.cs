using ItemFighter.Content;
using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests;

/// <summary>Luka 2026-10-16: Power, for the Furious Boxer.</summary>
public class PowerTests
{
    [Fact]
    public void Power_AddsPowerDamagePerStack_ToEveryHit()
    {
        var db = TestContent.Db(("enemies/dummy.json", Enemy()),
            ("items/fists.json", Item("fists", 2, """{ "type": "Damage", "amount": 3, "hits": 2 }""")));
        var b = Fight(db, "dummy", ("fists", 0));
        b.ApplyStatus(Side.Player, StatusType.Power, 2, null, 0);
        b.Steps(20);
        var hits = b.Of<DamageDealt>().ToList();
        Assert.Equal(2, hits.Count);
        Assert.All(hits, d => Assert.Equal(3 + 2 * b.Rules.PowerDamagePerStack, d.Total));
    }

    [Fact]
    public void Power_DoesNotBoostBurn()
    {
        var db = TestContent.Db(("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy");
        b.ApplyStatus(Side.Player, StatusType.Power, 5, null, 0);
        b.ApplyStatus(Side.Enemy, StatusType.Burn, 1, null, 0);
        b.Steps(10);
        Assert.All(b.Of<DamageDealt>(), d => Assert.Equal(1, d.Total));
    }

    [Fact]
    public void FuriousBoxer_GainsPower_WhenHit()
    {
        var b = Fight(GameContent.Load().Database, "furious_boxer", ("sword", 0));
        b.Steps(60);
        Assert.True(b.Enemy.Statuses.Power > 0);
    }
}
