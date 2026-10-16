using ItemFighter.Core.Engine;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests;

/// <summary>Luka 2026-10-16: shield blocks every kind of damage; burn still hits shield twice as hard.</summary>
public class ShieldRulesTests
{
    [Fact]
    public void PoisonBurst_IsBlockedByShield()
    {
        var b = Fight(TestContent.Db(("enemies/dummy.json", Enemy())), "dummy");
        b.Enemy.Shield = 20;
        b.ApplyStatus(Side.Enemy, StatusType.Poison, b.Rules.PoisonThreshold, null, 0);
        var burst = Assert.Single(b.Of<DamageDealt>());
        int total = b.Rules.PoisonThreshold * b.Rules.PoisonBurstPerStack;
        Assert.Equal(20, burst.ShieldDamage);
        Assert.Equal(total - 20, burst.HpDamage);
        Assert.Equal(0, b.Enemy.Shield);
    }
}
