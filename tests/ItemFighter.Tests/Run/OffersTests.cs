using ItemFighter.Content;
using ItemFighter.Core.Content;
using ItemFighter.Core.Determinism;
using ItemFighter.Core.Run;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests.Run;

public class OffersTests
{
    private static string ItemJson(string id, string tags = "\"Weapon\"", string rarity = "Common") =>
        Item(id, 2, """{ "type": "Damage", "amount": 1 }""", extra: $"\"tags\": [{tags}], \"rarity\": \"{rarity}\",");

    private static ContentDatabase SmallDb() => TestContent.Db(
        ("items/a.json", ItemJson("a")),
        ("items/b.json", ItemJson("b", "\"Fire\"")),
        ("items/c.json", ItemJson("c", "\"Shield\"")),
        ("items/d.json", ItemJson("d", "\"Fire\"", rarity: "Rare")),
        ("characters/knight.json", """{ "id": "knight", "maxHp": 100 }"""),
        ("enemies/zombie.json", """{ "id": "zombie", "maxHp": 10 }"""),
        ("enemies/snake.json", """{ "id": "snake", "maxHp": 10, "abilityIds": ["venomous"] }"""),
        ("enemies/dragon.json", """{ "id": "dragon", "maxHp": 10, "difficulty": 10 }"""),
        ("enemies/venomous.json", Item("venomous", null, """{ "type": "ApplyStatus", "status": "Poison", "amount": 2 }""", "\"OnDamageDealt\"",
            extra: "\"kind\": \"EnemyAbility\",")));

    private static RunView View(ContentDatabase db, RunConfig? config = null, string? character = null, params OwnedItem[] owned) =>
        new(config ?? new RunConfig(), db, character, 1, owned, []);

    private static readonly int[] Flat = [100, 100, 100, 100, 100];

    [Fact]
    public void ItemWeight_OnePool_CharacterMakesNoDifference()
    {
        var db = SmallDb();
        Assert.Equal(Offers.Weight(db.Get("c"), Flat, View(db)), Offers.Weight(db.Get("c"), Flat, View(db, character: "knight")));
        Assert.Equal(Offers.Weight(db.Get("a"), Flat, View(db)), Offers.Weight(db.Get("c"), Flat, View(db)));
    }

    [Fact]
    public void ItemWeight_EachOwnedTagMatch_Adds15Percent()
    {
        var db = SmallDb();
        int none = Offers.Weight(db.Get("b"), Flat, View(db));
        int two = Offers.Weight(db.Get("b"), Flat, View(db, null, null, new OwnedItem("d", 1), new OwnedItem("b", 1), new OwnedItem("a", 1)));
        Assert.Equal(none * 130 / 100, two);
    }

    [Fact]
    public void ItemWeight_RarityWeightZero_NeverPicked()
    {
        var db = SmallDb();
        int[] noRare = [100, 100, 0, 100, 100];
        Assert.Equal(0, Offers.Weight(db.Get("d"), noRare, View(db)));
        for (ulong seed = 1; seed < 200; seed++)
            Assert.DoesNotContain("d", Offers.PickItems(new Rng(seed), 3, noRare, View(db), new HashSet<string>()));
    }

    [Fact]
    public void Offers_ExcludeOwnedMaxLevelItems()
    {
        var db = SmallDb();
        var view = View(db, null, null, new OwnedItem("a", ContentLoader.LevelCount), new OwnedItem("b", 2));
        for (ulong seed = 1; seed < 200; seed++)
        {
            var picks = Offers.PickItems(new Rng(seed), 4, Flat, view, new HashSet<string>());
            Assert.DoesNotContain("a", picks);
            Assert.Equal(picks.Count, picks.Distinct().Count());
        }
        for (ulong seed = 1; seed < 50; seed++)
            Assert.DoesNotContain("a", Offers.Drop(new Rng(seed), "zombie", view).Choices);
    }

    [Fact]
    public void Drop_IsAlwaysThreeDistinctItems_NeverPassives()
    {
        var db = SmallDb();
        for (ulong seed = 1; seed < 100; seed++)
        {
            var drop = Offers.Drop(new Rng(seed), "snake", View(db));
            Assert.Equal(3, drop.Choices.Count);
            Assert.Equal(3, drop.Choices.Distinct().Count());
            Assert.All(drop.Choices, id => Assert.Equal(SourceKind.Item, db.Get(id).Kind));
        }
    }

    [Fact]
    public void Drop_HarderEnemy_OffersRarerItems()
    {
        var db = SmallDb();
        int easy = 0, hard = 0;
        for (ulong seed = 1; seed <= 1000; seed++)
        {
            if (Offers.Drop(new Rng(seed), "zombie", View(db)).Choices.Contains("d")) easy++;
            if (Offers.Drop(new Rng(seed), "dragon", View(db)).Choices.Contains("d")) hard++;
        }
        Assert.True(hard > easy * 2, $"rare item offered {easy} times at difficulty 1, {hard} at difficulty 10");
    }

    [Fact]
    public void Drop_SameSeedSameKey_SameOffer()
    {
        var content = GameContent.Load();
        var view = new RunView(content.Run, content.Database, "silver_knight", 2, [new OwnedItem("sword", 1)], []);
        for (ulong seed = 1; seed < 30; seed++)
            Assert.Equal(Offers.Drop(new Rng(seed).Fork("drop:1:3"), "green_snake", view).Choices,
                Offers.Drop(new Rng(seed).Fork("drop:1:3"), "green_snake", view).Choices);
    }

    [Fact]
    public void Encounters_EnemiesOfOneDifficulty_AreEquallyLikely()
    {
        var db = TestContent.Db(
            ("enemies/one.json", """{ "id": "one", "maxHp": 10, "difficulty": 1 }"""),
            ("enemies/two.json", """{ "id": "two", "maxHp": 10, "difficulty": 1 }"""));
        var config = new RunConfig { ChoicesPerPick = 1 };
        var counts = new Dictionary<string, int> { ["one"] = 0, ["two"] = 0 };
        for (ulong seed = 1; seed <= 2000; seed++)
            counts[Offers.FightChoices(new Rng(seed), View(db, config), 1, 0)[0].EnemyId!]++;
        double ratio = (double)counts["one"] / counts["two"];
        Assert.InRange(ratio, 0.8, 1.25);
    }

    /// <summary>Luka 2026-10-16: passives are their own pool, and the passive shop sells from it.</summary>
    [Fact]
    public void PassiveShop_SellsOnlyPassivesYouDontHave_AndBuyingGivesThePassive()
    {
        var content = GameContent.Load();
        var passives = content.Database.OfKind(SourceKind.Passive).Select(p => p.Id).ToHashSet();
        Assert.NotEmpty(passives);
        var shop = Assert.Single(content.Run.Shops, s => s.Sells == ShopSells.Passives);
        var run = RunState.New(3, "silver_knight", content.Run, content.Database);
        run.Gold = 1000;
        run.Passives.Add(passives.Order().First());
        run.Choose(new Encounter(EncounterKind.Shop, null, shop.Id));
        Assert.NotEmpty(run.ShopStock!);
        Assert.All(run.ShopStock!, id => Assert.Contains(id, passives));
        Assert.DoesNotContain(passives.Order().First(), run.ShopStock!);
        string bought = run.ShopStock![0];
        int items = run.AllOwned.Count;
        Assert.True(run.Buy(0));
        Assert.Contains(bought, run.Passives);
        Assert.Equal(items, run.AllOwned.Count);
    }

    [Fact]
    public void Shop_ReturnsShopSizeDistinctItems()
    {
        var content = GameContent.Load();
        var view = new RunView(content.Run, content.Database, "silver_knight", 1, [], []);
        var stock = Offers.Shop(new Rng(5), view);
        Assert.Equal(5, stock.Count);
        Assert.Equal(5, stock.Distinct().Count());
        Assert.All(stock, id => Assert.Equal(SourceKind.Item, content.Database.Get(id).Kind));
    }
}
