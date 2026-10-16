using ItemFighter.Content;
using ItemFighter.Core.Content;
using ItemFighter.Core.Determinism;
using ItemFighter.Core.Engine;
using ItemFighter.Core.Run;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests.Run;

/// <summary>Luka 2026-10-11: fight days and shop days, no elites, enemies keep base stats, hidden difficulty 1-10.</summary>
public class DaysTests
{
    private static readonly LoadedContent Content = GameContent.Load();

    private static ContentDatabase Db(params (string Id, int Difficulty, string Rarity)[] enemies) =>
        TestContent.Db(enemies.Select(e => ($"enemies/{e.Id}.json",
            $$"""{ "id": "{{e.Id}}", "maxHp": 10, "difficulty": {{e.Difficulty}}, "rarity": "{{e.Rarity}}" }""")).Concat(
            [("items/a.json", Item("a", 2, """{ "type": "Damage", "amount": 1 }""", extra: "\"tags\": [\"Weapon\"],")),
             ("items/b.json", Item("b", 2, """{ "type": "Damage", "amount": 1 }""", extra: "\"tags\": [\"Fire\"],"))]).ToArray());

    private static RunView View(ContentDatabase db, RunConfig config) => new(config, db, null, 1, [], []);

    [Fact]
    public void Difficulty_LoadsDefaultsToOne_AndIsValidated()
    {
        Assert.Equal(2, Content.Database.Get("grizzly_bear").Difficulty);
        Assert.Equal(1, Content.Database.Get("squire_zombie").Difficulty);
        var bad = TestContent.Load(("enemies/x.json", """{ "id": "x", "maxHp": 10, "difficulty": 11 }"""));
        Assert.Contains(ContentValidator.Validate(bad.Database, new ScriptRegistry()), e => e.Message.Contains("difficulty"));
    }

    [Theory]
    [InlineData(1, 0, 1)]
    [InlineData(1, 4, 1)]
    [InlineData(1, 5, 2)]
    [InlineData(2, 0, 3)]
    [InlineData(5, 9, 10)]
    [InlineData(5, 10, 10)]
    public void RunDifficulty_RisesEveryFiveDays(int mission, int encounter, int expected) =>
        Assert.Equal(expected, new RunConfig().RunDifficulty(mission, encounter));

    private static IReadOnlyList<Encounter> Fights(ContentDatabase db, int runDifficulty, int harderPercent, ulong seed = 1) =>
        Offers.FightChoices(new Rng(seed), View(db, new RunConfig()), runDifficulty, harderPercent);

    private static readonly ContentDatabase Ladder =
        Db(Enumerable.Range(1, 10).SelectMany(d => new[] { ($"x{d}", d, "Common"), ($"y{d}", d, "Common"), ($"z{d}", d, "Common") }).ToArray());

    /// <summary>Luka 2026-10-16: a fight day offers the run's difficulty, sometimes one harder; never easier.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void FightDay_OffersTheRunDifficulty_OrOneHarder(int runDifficulty)
    {
        var seen = new HashSet<int>();
        for (ulong seed = 1; seed <= 300; seed++)
            foreach (var c in Fights(Ladder, runDifficulty, 30, seed)) seen.Add(Ladder.Get(c.EnemyId!).Difficulty);
        Assert.Equal(new[] { runDifficulty, runDifficulty + 1 }, seen.Order());
    }

    [Fact]
    public void HarderPercent_ZeroNeverHarder_HundredAlwaysHarder()
    {
        for (ulong seed = 1; seed <= 50; seed++)
        {
            Assert.All(Fights(Ladder, 3, 0, seed), c => Assert.Equal(3, Ladder.Get(c.EnemyId!).Difficulty));
            Assert.All(Fights(Ladder, 3, 100, seed), c => Assert.Equal(4, Ladder.Get(c.EnemyId!).Difficulty));
        }
    }

    /// <summary>Luka 2026-10-16: difficulty 2 is very unlikely on day 1, likely by day 4-5, but still less likely than difficulty 1.</summary>
    [Fact]
    public void HarderEnemies_GetMoreLikely_AcrossEachHalfMission_ButStayUnderHalf()
    {
        var config = new RunConfig();
        var odds = Enumerable.Range(0, config.EncountersPerMission).Select(e => config.HarderEnemyChance(1, e)).ToList();
        Assert.True(odds[0] <= 10, $"day 1 is {odds[0]}%");
        for (int day = 1; day < config.DaysPerDifficulty; day++) Assert.True(odds[day] > odds[day - 1]);
        Assert.InRange(odds[3], 25, 49);
        Assert.InRange(odds[4], 25, 49);
        Assert.Equal(odds[0], odds[config.DaysPerDifficulty]); // the second half starts over one difficulty up
    }

    [Fact]
    public void MissionPlan_FirstDayIsAFight_AndTwoOrThreeShopDays()
    {
        var config = new RunConfig();
        var shopDays = new HashSet<int>();
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var plan = Offers.MissionPlan(new Rng(seed), config);
            Assert.Equal(config.EncountersPerMission, plan.Count);
            Assert.Equal(DayKind.Fight, plan[0]);
            int shops = plan.Count(d => d == DayKind.Shop);
            Assert.InRange(shops, 2, 3);
            shopDays.Add(shops);
        }
        Assert.Equal(new[] { 2, 3 }, shopDays.Order());
    }

    [Fact]
    public void Run_FollowsItsMissionPlan()
    {
        for (ulong seed = 1; seed <= 50; seed++)
        {
            var run = RunState.New(seed, "silver_knight", Content.Run, Content.Database);
            var kinds = new List<EncounterKind>();
            for (int e = 0; e < Content.Run.EncountersPerMission; e++)
            {
                run.Encounter = e;
                kinds.Add(run.EncounterChoices()[0].Kind);
            }
            Assert.Equal(EncounterKind.Fight, kinds[0]);
            Assert.InRange(kinds.Count(k => k == EncounterKind.Shop), 2, 3);
        }
    }

    [Fact]
    public void Grizzly_CanShowUpInMissionOne_ButNeverDifficultyFourOrMore()
    {
        bool seenBear = false;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var run = RunState.New(seed, "silver_knight", Content.Run, Content.Database);
            for (int e = 0; e < 10; e++)
            {
                run.Encounter = e;
                var choices = run.EncounterChoices();
                seenBear |= choices.Any(c => c.EnemyId == "grizzly_bear");
                Assert.All(choices.Where(c => c.EnemyId is not null), c => Assert.InRange(Content.Database.Get(c.EnemyId!).Difficulty, 1, 3));
            }
        }
        Assert.True(seenBear);
    }

    [Fact]
    public void FightDay_IsOnlyFights_OfDifferentEnemies_ShopDay_IsOnlyShops()
    {
        bool sawFight = false, sawShop = false;
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var run = RunState.New(seed, "silver_knight", Content.Run, Content.Database);
            run.Encounter = (int)(seed % 10);
            var choices = run.EncounterChoices();
            Assert.Single(choices.Select(c => c.Kind).Distinct());
            if (choices[0].Kind == EncounterKind.Fight)
            {
                sawFight = true;
                Assert.Equal(3, choices.Count);
                Assert.Equal(3, choices.Select(c => c.EnemyId).Distinct().Count());
            }
            else
            {
                sawShop = true;
                Assert.Equal(EncounterKind.Shop, choices[0].Kind);
                Assert.Equal(Content.Run.Shops.Count, choices.Count);
            }
        }
        Assert.True(sawFight && sawShop);
    }

    [Fact]
    public void HardDays_FallBackToTheNearestDifficulty_SoThereAreStillChoices()
    {
        var db = Db(("a1", 1, "Common"), ("b1", 1, "Common"), ("c1", 1, "Common"), ("bear", 4, "Common"));
        var choices = Fights(db, 6, 0);
        Assert.Equal(3, choices.Count);
        Assert.Contains(choices, c => c.EnemyId == "bear");
        Assert.DoesNotContain(Fights(db, 1, 0), c => c.EnemyId == "bear");
    }

    [Fact]
    public void HarderEnemy_GivesMoreGold()
    {
        var db = Db(("easy", 1, "Common"), ("hard", 3, "Common"));
        var run = RunState.New(1, null, new RunConfig(), db);
        Assert.True(run.GoldFor(new Encounter(EncounterKind.Fight, "hard")) > run.GoldFor(new Encounter(EncounterKind.Fight, "easy")));
        run.AddItem("a");
        run.Choose(new Encounter(EncounterKind.Fight, "hard"));
        run.StartFight();
        run.FinishFight(true);
        Assert.Equal(10 + 7, run.Gold);
    }

    [Fact]
    public void Enemies_KeepTheirBaseHp_EvenLateAndForBosses()
    {
        var run = RunState.New(1, "silver_knight", Content.Run, Content.Database);
        run.Mission = 4;
        run.Choose(new Encounter(EncounterKind.Fight, "squire_zombie"));
        Assert.Equal(100, run.StartFight().EnemyHpPercent);
        run = RunState.New(1, "silver_knight", Content.Run, Content.Database);
        run.Mission = 5;
        run.Encounter = 10;
        run.Choose(run.EncounterChoices()[0]);
        Assert.Equal(100, run.StartFight().EnemyHpPercent);
    }

    [Fact]
    public void ShopWithTags_StocksOnlyThoseTags()
    {
        var db = Db(("x", 1, "Common"));
        var config = new RunConfig { Shops = [new ShopConfig("smith", "Blacksmith", ["Weapon"])] };
        var run = RunState.New(1, null, config, db);
        run.Choose(new Encounter(EncounterKind.Shop, null, "smith"));
        Assert.Equal(["a"], run.ShopStock!);
    }

    /// <summary>Luka 2026-10-16: you can't fight an enemy you already fought this run (repeats only once every enemy is used).</summary>
    [Fact]
    public void FoughtEnemies_AreNotOfferedAgain()
    {
        var db = Db(("e1", 1, "Common"), ("e2", 1, "Common"), ("e3", 1, "Common"), ("e4", 1, "Common"), ("e5", 1, "Common"));
        var view = View(db, new RunConfig());
        for (ulong seed = 1; seed <= 50; seed++)
        {
            var choices = Offers.FightChoices(new Rng(seed), view, 1, 0, new HashSet<string> { "e1", "e2" });
            Assert.Equal(new[] { "e3", "e4", "e5" }, choices.Select(c => c.EnemyId).Order());
            var few = Offers.FightChoices(new Rng(seed), view, 1, 0, new HashSet<string> { "e1", "e2", "e3", "e4" });
            Assert.Equal(3, few.Count);
            Assert.Contains(few, c => c.EnemyId == "e5"); // unfought first, then repeats to fill the day
        }
    }

    [Fact]
    public void Run_RemembersWhoYouFought()
    {
        var run = RunState.New(1, "silver_knight", Content.Run, Content.Database);
        run.Choose(new Encounter(EncounterKind.Fight, "squire_zombie"));
        run.StartFight();
        run.FinishFight(true);
        Assert.Contains("squire_zombie", run.FoughtEnemies);
        run.TakeDrop(0);
        for (int e = 1; e < Content.Run.EncountersPerMission; e++)
        {
            run.Encounter = e;
            Assert.DoesNotContain(run.EncounterChoices(), c => c.EnemyId == "squire_zombie");
        }
    }
}
