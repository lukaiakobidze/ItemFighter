using ItemFighter.Content;
using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using ItemFighter.Core.Run;
using ItemFighter.Tests.Run;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests;

/// <summary>Luka 2026-10-17: the Vulnerable status, the Black Knight and per-mission boss pools.</summary>
public class BlackKnightTests
{
    private static Battle FistsFight(int amount = 10)
    {
        var db = TestContent.Db(("enemies/dummy.json", Enemy()),
            ("items/fists.json", Item("fists", 2, $$"""{ "type": "Damage", "amount": {{amount}} }""")));
        return Fight(db, "dummy", ("fists", 0));
    }

    [Fact]
    public void Vulnerable_MakesHitsDoHalfAgainAsMuch()
    {
        var b = FistsFight();
        b.ApplyStatus(Side.Enemy, StatusType.Vulnerable, 50, null, 0);
        b.Steps(20);
        Assert.Equal(15, Assert.Single(b.Of<DamageDealt>()).Total);
    }

    [Fact]
    public void Vulnerable_RoundsHalvesUp() // 9 * 1.5 = 13.5
    {
        var b = FistsFight(9);
        b.ApplyStatus(Side.Enemy, StatusType.Vulnerable, 50, null, 0);
        b.Steps(20);
        Assert.Equal(14, Assert.Single(b.Of<DamageDealt>()).Total);
    }

    [Fact]
    public void Vulnerable_WearsOff()
    {
        var b = FistsFight();
        b.ApplyStatus(Side.Enemy, StatusType.Vulnerable, 5, null, 0);
        b.Steps(20);
        Assert.Equal(10, Assert.Single(b.Of<DamageDealt>()).Total);
        Assert.Equal(0, b.Enemy.Statuses.Vulnerable);
    }

    [Fact]
    public void Vulnerable_DoesNotBoostBurnOrPoison()
    {
        var db = TestContent.Db(("enemies/dummy.json", Enemy()));
        var b = Fight(db, "dummy");
        b.ApplyStatus(Side.Enemy, StatusType.Vulnerable, 100, null, 0);
        b.ApplyStatus(Side.Enemy, StatusType.Burn, 2, null, 0);
        b.ApplyStatus(Side.Enemy, StatusType.Poison, b.Rules.PoisonThreshold, null, 0);
        b.Steps(6);
        var poison = b.Of<DamageDealt>().Single(d => d.Kind == DamageKind.Poison);
        Assert.Equal(b.Rules.PoisonThreshold * b.Rules.PoisonBurstPerStack, poison.Total);
        Assert.Equal(2, b.Of<DamageDealt>().First(d => d.Kind == DamageKind.Burn).Total);
    }

    [Fact]
    public void Vulnerable_NewApplication_KeepsTheLongerTimer()
    {
        var b = FistsFight();
        b.ApplyStatus(Side.Enemy, StatusType.Vulnerable, 40, null, 0);
        b.ApplyStatus(Side.Enemy, StatusType.Vulnerable, 10, null, 0);
        Assert.Equal(40, b.Enemy.Statuses.Vulnerable);
    }

    [Fact]
    public void Vulnerable_IsReadAsSeconds_AndDescribed()
    {
        var db = GameContent.Load().Database;
        Assert.Equal("Every 7s: deal 15 damage to you, then make you Vulnerable for 4s.", string.Join("\n", SourceDescriber.Lines(db.Get("crushing_blow"), 1)));
    }

    [Fact]
    public void BlackKnight_CrushingBlow_LeavesYouVulnerable()
    {
        var b = Fight(GameContent.Load().Database, "black_knight", ("sword", 0));
        b.Steps(75);
        Assert.Contains(b.Of<StatusApplied>(), s => s.Status == StatusType.Vulnerable && s.TargetSide == Side.Player && s.Amount == 40);
        Assert.Equal(15, b.Of<DamageDealt>().First(d => d.Target == Side.Player && b.FindSource(d.SourceId!.Value)!.Def.Id == "crushing_blow").Total);
    }

    [Fact]
    public void BlackKnight_IsTheFirstMissionsBoss_AndNeverAFightDayEnemy()
    {
        var content = GameContent.Load();
        Assert.True(content.Database.Get("black_knight").IsBoss);
        var run = RunStateTests.NewRun();
        run.Encounter = run.Config.EncountersPerMission;
        Assert.Equal("black_knight", Assert.Single(run.EncounterChoices()).EnemyId);
        for (ulong seed = 1; seed < 40; seed++)
        for (int day = 0; day < 10; day++)
        {
            var r = RunStateTests.NewRun(seed);
            r.Encounter = day;
            Assert.DoesNotContain(r.EncounterChoices(), e => e.EnemyId == "black_knight");
        }
    }

    [Fact]
    public void BossPool_PicksOneForTheRun_AndEveryCandidateCanCome()
    {
        var content = GameContent.Load();
        var config = new RunConfig { Bosses = [new BossConfig { Pool = ["grizzly_bear", "yeti", "the_blur"] }] };
        var seen = new HashSet<string>();
        for (ulong seed = 1; seed < 60; seed++)
        {
            var run = RunState.New(seed, "silver_knight", config, content.Database);
            string id = run.Boss(1).EnemyId!;
            Assert.Equal(id, run.Boss(1).EnemyId); // the same every time for this run
            seen.Add(id);
        }
        Assert.Equal(3, seen.Count);
    }

    [Fact]
    public void BossPool_ParsesFromJson_AndRejectsUnknownEnemies()
    {
        var db = TestContent.Db(("enemies/dummy.json", Enemy()));
        var config = RunConfig.Parse("""{ "bosses": [ { "pool": ["dummy"] } ] }""", db, out var errors);
        Assert.Empty(errors);
        Assert.Equal(["dummy"], config.Bosses[0].Candidates);
        RunConfig.Parse("""{ "bosses": [ { "pool": ["nobody"] } ] }""", db, out errors);
        Assert.Contains(errors, e => e.Contains("nobody"));
    }
}
