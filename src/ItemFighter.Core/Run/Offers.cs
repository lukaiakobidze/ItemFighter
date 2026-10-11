using ItemFighter.Core.Content;
using ItemFighter.Core.Determinism;

namespace ItemFighter.Core.Run;

public sealed record OwnedItem(string ItemId, int Level);

/// <summary>One encounter choice: an enemy to fight, or a shop (ShopId names its ShopConfig; null means every item).</summary>
public sealed record Encounter(EncounterKind Kind, string? EnemyId, string? ShopId = null);

public enum DropKind { Items, Passives }

/// <summary>A post-fight drop: up to 3 ids (items, or passives with items filling empty slots), in display order.</summary>
public sealed record DropOffer(DropKind Kind, IReadOnlyList<string> Choices);

/// <summary>What offers need to know about the run, without the mutable run itself.</summary>
public sealed record RunView(RunConfig Config, ContentDatabase Db, string? CharacterId, int Mission,
    IReadOnlyList<OwnedItem> Owned, IReadOnlyList<string> PassiveIds);

/// <summary>
/// Weighted random picks for encounter choices, shops and drops. Weight = rarity weight × character pool bonus × tag bonus.
/// Every pick uses the Rng it is given, so the run decides determinism by how it forks.
/// </summary>
public static class Offers
{
    /// <summary>
    /// rarityWeights[rarity] × (CharacterPoolMultiplier if the def is in the character's pool) ×
    /// (100 + TagBonusPercent × owned items sharing a tag with it), as an integer.
    /// </summary>
    public static int Weight(SourceDefinition def, IReadOnlyList<int> rarityWeights, RunView view)
    {
        int rarity = (int)def.Rarity < rarityWeights.Count ? rarityWeights[(int)def.Rarity] : 0;
        int pool = view.CharacterId is not null && def.Pools.Contains(view.CharacterId, StringComparer.Ordinal) ? view.Config.CharacterPoolMultiplier : 1;
        int matches = def.Tags.Count == 0 ? 0 : view.Owned.Count(o => view.Db.TryGet(o.ItemId, out var owned) && owned.Tags.Any(def.HasTag));
        long weight = (long)rarity * pool * (100 + view.Config.TagBonusPercent * matches);
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
    /// A day's choices. A fight day offers different enemies, a shop day offers the shops. Fight days use enemies of
    /// difficulty maxDifficulty down to (maxDifficulty - DifficultyWindow + 1); when that is too few to fill the
    /// choices, easier enemies are added, so early-game content keeps a day full.
    /// </summary>
    public static IReadOnlyList<Encounter> EncounterChoices(Rng rng, RunView view, int maxDifficulty)
    {
        var config = view.Config;
        var fightPool = FightPool(view, maxDifficulty);
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

    private static List<(string Id, int Weight)> FightPool(RunView view, int maxDifficulty)
    {
        var config = view.Config;
        var allowed = view.Db.OfKind(SourceKind.Enemy)
            .Select(e => (Def: e, Weight: config.EnemyRarityWeights.TryGetValue(e.Rarity, out int w) ? w : 0))
            .Where(e => e.Weight > 0 && e.Def.Difficulty <= maxDifficulty).ToList();
        int floor = maxDifficulty - config.DifficultyWindow + 1;
        var pool = allowed.Where(e => e.Def.Difficulty >= floor).ToList();
        // Too few enemies in the window: add the next easier difficulty until the day can be filled.
        foreach (int difficulty in allowed.Select(e => e.Def.Difficulty).Where(d => d < floor).Distinct().OrderByDescending(d => d))
        {
            if (pool.Count >= config.ChoicesPerPick) break;
            pool.AddRange(allowed.Where(e => e.Def.Difficulty == difficulty));
        }
        return pool.Select(e => (e.Def.Id, e.Weight)).ToList();
    }

    /// <summary>Stock for a shop: ShopSize items, limited to the shop's tags when it has any.</summary>
    public static IReadOnlyList<string> Shop(Rng rng, RunView view, string? shopId = null)
    {
        var tags = view.Config.Shops.FirstOrDefault(s => s.Id == shopId)?.Tags ?? [];
        var exclude = tags.Count == 0 ? new HashSet<string>()
            : view.Db.OfKind(SourceKind.Item).Where(d => !d.Tags.Any(tags.Contains)).Select(d => d.Id).ToHashSet();
        return PickItems(rng, view.Config.ShopSize, view.Config.ShopWeights(view.Mission), view, exclude);
    }

    /// <summary>
    /// Items (ItemDropChancePercent): the enemy's drop item first, then random items.
    /// Otherwise passives: one of the enemy's passives first, then random passives, with items filling any empty slots.
    /// Rarer enemies drop rarer things (dropRarityWeights).
    /// </summary>
    public static DropOffer Drop(Rng rng, string enemyId, RunView view)
    {
        var config = view.Config;
        var enemy = view.Db.Get(enemyId);
        var weights = config.DropWeights(enemy.Rarity);
        int count = config.ChoicesPerPick;
        bool items = rng.Chance(config.ItemDropChancePercent);

        if (!items)
        {
            var owned = view.PassiveIds.ToHashSet(StringComparer.Ordinal);
            var choices = new List<string>();
            var enemyPassives = enemy.AbilityIds.Distinct()
                .Select(id => view.Db.TryGet(id, out var d) ? d : null)
                .Where(d => d is { IsPassiveAbility: true } && !owned.Contains(d.Id))
                .Select(d => (d!.Id, Math.Max(1, Weight(d, weights, view))));
            choices.AddRange(PickDistinct(rng, enemyPassives, 1));
            var others = view.Db.OfKind(SourceKind.Passive)
                .Where(d => !owned.Contains(d.Id) && !choices.Contains(d.Id))
                .Select(d => (d.Id, Weight(d, weights, view)));
            choices.AddRange(PickDistinct(rng, others, count - choices.Count));
            if (choices.Count > 0)
            {
                choices.AddRange(PickItems(rng, count - choices.Count, weights, view, new HashSet<string>()));
                return new DropOffer(DropKind.Passives, choices);
            }
        }

        var drops = new List<string>();
        if (enemy.DropItemId is string dropItem && CanOffer(dropItem, view)) drops.Add(dropItem);
        drops.AddRange(PickItems(rng, count - drops.Count, weights, view, drops.ToHashSet()));
        return new DropOffer(DropKind.Items, drops);
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
