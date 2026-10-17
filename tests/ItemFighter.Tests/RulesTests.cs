using ItemFighter.Content;
using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests;

/// <summary>Status and time-limit numbers come from rules.json, not code.</summary>
public class RulesTests
{
    private static Battle Dummy(CombatRules rules)
    {
        var db = TestContent.Db(("enemies/dummy.json", Enemy(maxHp: 1000)));
        db.Rules = rules;
        return Fight(db, "dummy");
    }

    [Fact]
    public void GameRulesFile_LoadsCleanly_WithTheAgreedNumbers()
    {
        var content = GameContent.Load();
        Assert.DoesNotContain(content.Errors, e => e.File == GameContent.RulesFile);
        var r = content.Database.Rules;
        Assert.Equal(1200, r.TimeLimitTicks);
        Assert.Equal(10, r.BurnTickInterval); // Luka 2026-10-17: burn ticks every 1s
        Assert.Equal(10, r.PoisonThreshold);
        Assert.Equal(5, r.ChillFreezeAtStacks);
    }

    [Fact]
    public void UnknownKey_IsReported()
    {
        CombatRules.Parse("""{ "poisonTreshold": 5 }""", out var errors);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void BadValue_IsReported_AndDefaultsKept()
    {
        var rules = CombatRules.Parse("""{ "poisonThreshold": 0 }""", out var errors);
        Assert.NotEmpty(errors);
        Assert.Equal(10, rules.PoisonThreshold);
    }

    [Fact]
    public void HugeTime_IsReported_InsteadOfCrashingLater()
    {
        CombatRules.Parse("""{ "fightTimeLimitSeconds": 1000000000 }""", out var errors);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void TimeLimit_FromRules()
    {
        var b = Dummy(CombatRules.Parse("""{ "fightTimeLimitSeconds": 3 }""", out _));
        b.RunToEnd();
        Assert.True(b.TimedOut);
        Assert.Equal(30, b.Tick);
    }

    [Fact]
    public void Poison_ThresholdAndBurst_FromRules()
    {
        var b = Dummy(CombatRules.Parse("""{ "poisonThreshold": 4, "poisonBurstPerStack": 5 }""", out _));
        b.ApplyStatus(Side.Enemy, StatusType.Poison, 4, null, 0);
        Assert.Equal(980, b.Enemy.Hp);
    }

    [Fact]
    public void Burn_IntervalLossAndShieldMultiplier_FromRules()
    {
        var b = Dummy(CombatRules.Parse("""{ "burnTickSeconds": 1, "burnLossPerTick": 2, "burnShieldMultiplier": 3 }""", out _));
        b.Enemy.Shield = 6;
        b.ApplyStatus(Side.Enemy, StatusType.Burn, 5, null, 0);
        b.Steps(9);
        Assert.Empty(b.Of<DamageDealt>());
        b.Steps(1);
        // 5 burn: 2 of it eats 6 shield (x3), the other 3 hit HP; then burn drops by 2.
        Assert.Equal(0, b.Enemy.Shield);
        Assert.Equal(997, b.Enemy.Hp);
        Assert.Equal(3, b.Enemy.Statuses.Burn);
    }

    [Fact]
    public void Chill_FreezeAtAndDuration_FromRules()
    {
        var b = Dummy(CombatRules.Parse("""{ "chillFreezeAtStacks": 2, "freezeSeconds": 3 }""", out _));
        b.ApplyStatus(Side.Enemy, StatusType.Chill, 2, null, 0);
        Assert.Equal(0, b.Enemy.Statuses.Chill);
        Assert.Equal(b.Tick + 30, b.Enemy.Statuses.FrozenUntilTick);
    }
}
