using ItemFighter.Content;
using ItemFighter.Core.Content;
using ItemFighter.Core.Determinism;
using ItemFighter.Core.Run;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests.Run;

public class OffersTests
{
    private static string ItemJson(string id, string tags = "\"Weapon\"", string rarity = "Common", string pools = "\"neutral\"") =>
        Item(id, 2, """{ "type": "Damage", "amount": 1 }""", extra: $"\"tags\": [{tags}], \"rarity\": \"{rarity}\", \"pools\": [{pools}],");

    private static ContentDatabase SmallDb() => TestContent.Db(
        ("items/a.json", ItemJson("a")),
        ("items/b.json", ItemJson("b", "\"Fire\"")),
        ("items/c.json", ItemJson("c", "\"Shield\"", pools: "\"knight\"")),
        ("items/d.json", ItemJson("d", "\"Fire\"", rarity: "Rare")),
        ("characters/knight.json", """{ "id": "knight", "maxHp": 100 }"""),
        ("enemies/zombie.json", """{ "id": "zombie", "maxHp": 10, "dropItem": "a" }"""),
        ("enemies/snake.json", """{ "id": "snake", "maxHp": 10, "dropItem": "b", "abilityIds": ["venomous"] }"""),
        ("enemies/venomous.json", Item("venomous", null, """{ "type": "ApplyStatus", "status": "Poison", "amount": 2 }""", "\"OnDamageDealt\"",
            extra: "\"kind\": \"EnemyAbility\",")));

    private static RunView View(ContentDatabase db, RunConfig? config = null, string? character = null, params OwnedItem[] owned) =>
        new(config ?? new RunConfig(), db, character, 1, owned, []);

    private static readonly int[] Flat = [100, 100, 100, 100, 100];

    [Fact]
    public void ItemWeight_CharacterPool_Triples()
    {
        var db = SmallDb();
        Assert.Equal(3 * Offers.Weight(db.Get("a"), Flat, View(db, character: "knight")),
            Offers.Weight(db.Get("c"), Flat, View(db, character: "knight")));
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
        // The zombie normally drops "a", but a maxed "a" can't be offered, so the item slots are all random.
        var drop = Offers.Drop(new Rng(1), "zombie", false, view with { Config = new RunConfig { ItemDropChancePercent = 100 } });
        Assert.DoesNotContain("a", drop.Choices);
    }

    [Fact]
    public void Drop_Items_FirstIsEnemyDropItem_ThenTwoDistinctOthers()
    {
        var db = SmallDb();
        var view = View(db, new RunConfig { ItemDropChancePercent = 100 });
        for (ulong seed = 1; seed < 50; seed++)
        {
            var drop = Offers.Drop(new Rng(seed), "snake", false, view);
            Assert.Equal(DropKind.Items, drop.Kind);
            Assert.Equal(3, drop.Choices.Count);
            Assert.Equal("b", drop.Choices[0]);
            Assert.Equal(3, drop.Choices.Distinct().Count());
        }
    }

    [Fact]
    public void Drop_Passives_FirstIsEnemyPassive()
    {
        var db = SmallDb();
        var drop = Offers.Drop(new Rng(1), "snake", false, View(db, new RunConfig { ItemDropChancePercent = 0 }));
        Assert.Equal(DropKind.Passives, drop.Kind);
        Assert.Equal("venomous", drop.Choices[0]);
        Assert.Equal(3, drop.Choices.Count); // no other passives exist, so items fill the rest
        Assert.All(drop.Choices.Skip(1), id => Assert.Equal(SourceKind.Item, db.Get(id).Kind));
    }

    [Fact]
    public void Drop_NoPassivesAnywhere_IsItems()
    {
        var db = SmallDb();
        var drop = Offers.Drop(new Rng(1), "zombie", false, View(db, new RunConfig { ItemDropChancePercent = 0 }));
        Assert.Equal(DropKind.Items, drop.Kind);
        Assert.Equal("a", drop.Choices[0]);
    }

    [Fact]
    public void Drop_OwnedEnemyPassive_IsNotOfferedAgain()
    {
        var db = SmallDb();
        var view = View(db, new RunConfig { ItemDropChancePercent = 0 }) with { PassiveIds = ["venomous"] };
        var drop = Offers.Drop(new Rng(1), "snake", false, view);
        Assert.DoesNotContain("venomous", drop.Choices);
    }

    [Fact]
    public void Drop_SameSeedSameKey_SameOffer()
    {
        var content = GameContent.Load();
        var view = new RunView(content.Run, content.Database, "silver_knight", 2, [new OwnedItem("sword", 1)], []);
        for (ulong seed = 1; seed < 30; seed++)
            Assert.Equal(Offers.Drop(new Rng(seed).Fork("drop:1:3"), "green_snake", false, view).Choices,
                Offers.Drop(new Rng(seed).Fork("drop:1:3"), "green_snake", false, view).Choices);
    }

    [Fact]
    public void Encounters_RarerEnemiesAppearLessOften()
    {
        var db = TestContent.Db(
            ("enemies/common.json", """{ "id": "common", "maxHp": 10, "rarity": "Common" }"""),
            ("enemies/rare.json", """{ "id": "rare", "maxHp": 10, "rarity": "Rare" }"""));
        var config = new RunConfig { EncounterWeights = new() { [EncounterKind.Fight] = 1 } };
        var counts = new Dictionary<string, int> { ["common"] = 0, ["rare"] = 0 };
        for (ulong seed = 1; seed <= 2000; seed++)
        {
            // Only the first choice: later ones avoid repeats, which skews a 2-enemy pool.
            var e = Offers.EncounterChoices(new Rng(seed), View(db, config))[0];
            Assert.Equal(EncounterKind.Fight, e.Kind);
            counts[e.EnemyId!]++;
        }
        // Common 100 vs Rare 20 by default: about 5 to 1.
        double ratio = (double)counts["common"] / counts["rare"];
        Assert.InRange(ratio, 3.5, 7.0);
    }

    [Fact]
    public void Encounters_ShowChoicesPerPick_AndEnemiesExist()
    {
        var content = GameContent.Load();
        var view = new RunView(content.Run, content.Database, "silver_knight", 1, [], []);
        for (ulong seed = 1; seed < 100; seed++)
        {
            var choices = Offers.EncounterChoices(new Rng(seed), view);
            Assert.Equal(3, choices.Count);
            Assert.All(choices.Where(c => c.Kind != EncounterKind.Shop), c => Assert.Equal(SourceKind.Enemy, content.Database.Get(c.EnemyId!).Kind));
            Assert.Equal(choices.Count, choices.Distinct().Count());
        }
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
