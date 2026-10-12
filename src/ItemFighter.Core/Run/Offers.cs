using ItemFighter.Core.Content;
using ItemFighter.Core.Determinism;

namespace ItemFighter.Core.Run;

public sealed record OwnedItem(string ItemId, int Level);

/// <summary>One encounter choice: an enemy to fight, or a shop (ShopId names its ShopConfig; null means every item).</summary>
public sealed record Encounter(EncounterKind Kind, string? EnemyId, string? ShopId = null);

/// <summary>The items offered after a win, in display order.</summary>
public sealed record DropOffer(IReadOnlyList<string> Choices);

/// <summary>What offers need to know about the run, without the mutable run itself.</summary>
public sealed record RunView(RunConfig Config, ContentDatabase Db, string? CharacterId, int Mission,
    IReadOnlyList<OwnedItem> Owned, IReadOnlyList<string> PassiveIds);

/// <summary>
/// Weighted random picks for encounter choices, shops and drops. Every item is in one pool; weight = rarity weight × tag bonus.
/// Every pick uses the Rng it is given, so the run decides determinism by how it forks.
/// </summary>
public static class Offers
{
    /// <summary>
    /// rarityWeights[rarity] × (100 + TagBonusPercent × owned items sharing a tag with it), as an integer.
    /// </summary>
    public static int Weight(SourceDefinition def, IReadOnlyList<int> rarityWeights, RunView view)
    {
        int rarity = (int)def.Rarity < rarityWeights.Count ? rarityWeights[(int)def.Rarity] : 0;
        int matches = def.Tags.Count == 0 ? 0 : view.Owned.Count(o => view.Db.TryGet(o.ItemId, out var owned) && owned.Tags.Any(def.HasTag));
        long weight = (long)rarity * (100 + view.Config.TagBonusPercent * matches);
        return (int)Math.Min(weight, int.MaxValue / 64);
    }

    /// <summary>True when getting this item would do something: not owned, or owned below max level.</summary>
    public static bool CanOffer(string itemId, RunView view) =>
        view.Owned.All(o => o.ItemId != itemId || o.Level < ContentLoader.LevelCount);

    /// <summary>Up to count distinct items, never ones in exclude or owned at max level.</summary>
    public static IReadOnlyList<string> PickItems(Rng rng, int count, IReadOnlyList<int> rarityWeights, RunView view, IReadOnlySet<string> exclude)
    {
        var candidates = view.Db.OfKind(SourceKind.Item)
            .Where(d => !exclude.Contains(d.Id) && CanOffer(d.Id, view))
            .Select(d => (d.Id, Weight(d, rarityWeights, view)));
        return PickDistinct(rng, candidates, count);
    }

    /// <summary>
    /// A day's choices. A fight day offers different enemies within DifficultySpread of the run's difficulty, all equally
    /// likely; when that is too few to fill the choices, the range widens a step at a time. A shop day offers the shops.
    /// </summary>
    public static IReadOnlyList<Encounter> EncounterChoices(Rng rng, RunView view, int runDifficulty)
    {
        var config = view.Config;
        var fightPool = FightPool(view, runDifficulty);
        var days = config.DayWeights.Where(d => d.Value > 0 && (d.Key == DayKind.Shop ? config.Shops.Count > 0 : fightPool.Count > 0))
            .OrderBy(d => d.Key).Select(d => (d.Key, d.Value)).ToList();
        if (days.Count == 0) days.Add(fightPool.Count > 0 ? (DayKind.Fight, 1) : (DayKind.Shop, 1));

        if (PickOne(rng, days) == DayKind.Shop)
        {
            var shops = config.Shops.Select(s => (s.Id, 1));
            return PickDistinct(rng, shops, config.ChoicesPerPick).Select(id => new Encounter(EncounterKind.Shop, null, id)).ToList();
        }
        return PickDistinct(rng, fightPool, config.ChoicesPerPick).Select(id => new Encounter(EncounterKind.Fight, id)).ToList();
    }

    private static List<(string Id, int Weight)> FightPool(IReadOnlyList<SourceDefinition> enemies, int runDifficulty, int spread, int wanted)
    {
        var pool = new List<SourceDefinition>();
        for (int s = spread; pool.Count < wanted && s <= spread + 10; s++)
            pool = enemies.Where(e => Math.Abs(e.Difficulty - runDifficulty) <= s).ToList();
        return pool.Select(e => (e.Id, 1)).ToList();
    }

    private static List<(string Id, int Weight)> FightPool(RunView view, int runDifficulty) =>
        FightPool(view.Db.OfKind(SourceKind.Enemy).ToList(), runDifficulty, view.Config.DifficultySpread, view.Config.ChoicesPerPick);

    /// <summary>Stock for a shop: ShopSize items, limited to the shop's tags when it has any.</summary>
    public static IReadOnlyList<string> Shop(Rng rng, RunView view, string? shopId = null)
    {
        var tags = view.Config.Shops.FirstOrDefault(s => s.Id == shopId)?.Tags ?? [];
        var exclude = tags.Count == 0 ? new HashSet<string>()
            : view.Db.OfKind(SourceKind.Item).Where(d => !d.Tags.Any(tags.Contains)).Select(d => d.Id).ToHashSet();
        return PickItems(rng, view.Config.ShopSize, view.Config.ShopWeights(view.Mission), view, exclude);
    }

    /// <summary>
    /// The items offered after beating an enemy: ChoicesPerPick random items, rarer for harder enemies
    /// (dropRarityWeights by difficulty), more likely when they share tags with what you own.
    /// </summary>
    public static DropOffer Drop(Rng rng, string enemyId, RunView view)
    {
        var weights = view.Config.DropWeights(view.Db.Get(enemyId).Difficulty);
        return new DropOffer(PickItems(rng, view.Config.ChoicesPerPick, weights, view, new HashSet<string>()));
    }

    private static T PickOne<T>(Rng rng, IReadOnlyList<(T Value, int Weight)> options)
    {
        int total = options.Sum(o => o.Weight);
        int roll = rng.NextInt(total);
        foreach (var (value, weight) in options)
        {
            if (roll < weight) return value;
            roll -= weight;
        }
        return options[^1].Value;
    }

    /// <summary>Weighted picks without replacement, in a stable order (by id) so results don't depend on load order.</summary>
    private static List<string> PickDistinct(Rng rng, IEnumerable<(string Id, int Weight)> candidates, int count)
    {
        var pool = candidates.Where(c => c.Weight > 0).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        var picks = new List<string>();
        while (picks.Count < count && pool.Count > 0)
        {
            string id = PickOne(rng, pool);
            picks.Add(id);
            pool.RemoveAll(c => c.Id == id);
        }
        return picks;
    }
}
