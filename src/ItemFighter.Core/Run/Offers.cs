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
    /// The kind of each day of a mission (not counting the boss): the first FightDaysAtStart days are fights, then
    /// ShopDaysPerMission shop days land at random among the rest, and every other day is a fight.
    /// </summary>
    public static IReadOnlyList<DayKind> MissionPlan(Rng rng, RunConfig config)
    {
        int days = config.EncountersPerMission, start = Math.Min(config.FightDaysAtStart, days);
        int[] range = config.ShopDaysPerMission is { Length: 2 } r ? r : [0, 0];
        int shops = Math.Min(range[0] + rng.NextInt(range[1] - range[0] + 1), days - start);
        var plan = Enumerable.Repeat(DayKind.Fight, days).ToArray();
        var open = Enumerable.Range(start, days - start).ToList();
        for (int i = 0; i < shops; i++)
        {
            int pick = rng.NextInt(open.Count);
            plan[open[pick]] = DayKind.Shop;
            open.RemoveAt(pick);
        }
        return plan;
    }

    /// <summary>A shop day's choices: the shops from run.json.</summary>
    public static IReadOnlyList<Encounter> ShopChoices(Rng rng, RunView view) =>
        PickDistinct(rng, view.Config.Shops.Select(s => (s.Id, 1)), view.Config.ChoicesPerPick)
            .Select(id => new Encounter(EncounterKind.Shop, null, id)).ToList();

    /// <summary>
    /// A fight day's choices: different enemies, each one difficulty above the run's with harderPercent chance, else at
    /// the run's difficulty, all enemies of a difficulty equally likely. When no enemy is left at that difficulty, the
    /// nearest difficulty that has one is used (the easier one on a tie).
    /// </summary>
    public static IReadOnlyList<Encounter> FightChoices(Rng rng, RunView view, int runDifficulty, int harderPercent)
    {
        var left = view.Db.OfKind(SourceKind.Enemy).OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
        var picks = new List<Encounter>();
        while (picks.Count < view.Config.ChoicesPerPick && left.Count > 0)
        {
            int wanted = rng.Chance(harderPercent) ? runDifficulty + 1 : runDifficulty;
            int nearest = left.Select(e => e.Difficulty).OrderBy(d => Math.Abs(d - wanted)).ThenBy(d => d).First();
            var pool = left.Where(e => e.Difficulty == nearest).ToList();
            var enemy = pool[rng.NextInt(pool.Count)];
            picks.Add(new Encounter(EncounterKind.Fight, enemy.Id));
            left.Remove(enemy);
        }
        return picks;
    }

    /// <summary>
    /// Stock for a shop: ShopSize picks. An item shop sells items (only its tags when it has any); a passive shop sells
    /// passives you don't have yet.
    /// </summary>
    public static IReadOnlyList<string> Shop(Rng rng, RunView view, string? shopId = null)
    {
        var shop = view.Config.Shops.FirstOrDefault(s => s.Id == shopId);
        var weights = view.Config.ShopWeights(view.Mission);
        if (shop?.Sells == ShopSells.Passives)
            return PickDistinct(rng, view.Db.OfKind(SourceKind.Passive).Where(p => !view.PassiveIds.Contains(p.Id))
                .Select(p => (p.Id, Math.Max(1, (int)p.Rarity < weights.Length ? weights[(int)p.Rarity] : 0))), view.Config.ShopSize);
        var tags = shop?.Tags ?? [];
        var exclude = tags.Count == 0 ? new HashSet<string>()
            : view.Db.OfKind(SourceKind.Item).Where(d => !d.Tags.Any(tags.Contains)).Select(d => d.Id).ToHashSet();
        return PickItems(rng, view.Config.ShopSize, weights, view, exclude);
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
