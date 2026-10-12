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

    /// <summary>Luka 2026-10-12: the run's difficulty d offers enemies of d-1, d and d+1 (second half of mission 1 → 1 to 3).</summary>
    [Theory]
    [InlineData(1, 1, 2)]
    [InlineData(2, 1, 3)]
    [InlineData(5, 4, 6)]
    [InlineData(10, 9, 10)]
    public void FightDay_OffersEnemiesWithinOneOfTheRunDifficulty(int runDifficulty, int low, int high)
    {
        var enemies = Enumerable.Range(1, 10).SelectMany(d => new[] { ($"x{d}", d, "Common"), ($"y{d}", d, "Common") }).ToArray();
        var db = Db(enemies);
        var config = new RunConfig { DayWeights = new() { [DayKind.Fight] = 1 } };
        var seen = new HashSet<int>();
        for (ulong seed = 1; seed <= 300; seed++)
            foreach (var c in Offers.EncounterChoices(new Rng(seed), View(db, config), runDifficulty))
                seen.Add(db.Get(c.EnemyId!).Difficulty);
        Assert.Equal(Enumerable.Range(low, high - low + 1), seen.Order());
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
            var choices = RunState.New(seed, "silver_knight", Content.Run, Content.Database).EncounterChoices();
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
    public void HardDays_FallBackToEasierEnemies_SoThereAreStillChoices()
    {
        var db = Db(("a1", 1, "Common"), ("b1", 1, "Common"), ("c1", 1, "Common"), ("bear", 4, "Common"));
        var config = new RunConfig { DayWeights = new() { [DayKind.Fight] = 1 } };
        var choices = Offers.EncounterChoices(new Rng(1), View(db, config), runDifficulty: 6);
        Assert.Equal(3, choices.Count);
        Assert.Contains(choices, c => c.EnemyId == "bear");
        var early = Offers.EncounterChoices(new Rng(1), View(db, config), runDifficulty: 1);
        Assert.DoesNotContain(early, c => c.EnemyId == "bear");
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
}
