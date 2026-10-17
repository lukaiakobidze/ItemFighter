using System.Text.Json;
using System.Text.Json.Serialization;
using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;

namespace ItemFighter.Core.Run;

public enum EncounterKind { Fight, Shop, Boss }

/// <summary>What a day offers: a fight day shows different enemies, a shop day shows the shops.</summary>
public enum DayKind { Fight, Shop }

/// <summary>
/// A mission's boss: one enemy picked at random from <see cref="Pool"/> each run (Luka 2026-10-17), or the single
/// <see cref="Enemy"/>. Enemies always keep their base stats.
/// </summary>
public sealed record BossConfig(string? Enemy = null)
{
    public List<string> Pool { get; init; } = [];

    public IReadOnlyList<string> Candidates => Pool.Count > 0 ? Pool : Enemy is null ? [] : [Enemy];
}

/// <summary>What a shop sells: items, or passives from Data/passives/.</summary>
public enum ShopSells { Items, Passives }

/// <summary>
/// A kind of shop. Tags limits an item shop's stock to items with any of those tags; empty means every item.
/// A passive shop sells passives you don't have yet.
/// </summary>
public sealed record ShopConfig(string Id, string Name, IReadOnlyList<string>? Tags = null, ShopSells Sells = ShopSells.Items);

/// <summary>
/// Every number of the run loop, from Data/run.json. A key missing from the file keeps the default below.
/// Rarity-weight arrays are always 5 numbers: Common, Uncommon, Rare, Epic, Legendary.
/// </summary>
public sealed class RunConfig
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public int Missions { get; init; } = 5;
    public int EncountersPerMission { get; init; } = 10;
    public int ChoicesPerPick { get; init; } = 3;
    public int HotbarSize { get; init; } = Board.SlotCount;
    public int BackpackSize { get; init; } = 12;

    public int StartingGold { get; init; } = 10;
    /// <summary>Gold for winning a normal fight, by the enemy's difficulty 1 to 10 (the last entry repeats), so harder fights pay more.</summary>
    public int[] FightGold { get; init; } = [5, 6, 7, 8, 9, 10, 11, 12, 13, 14];
    public int BossGold { get; init; } = 15;
    public Dictionary<Rarity, int> Prices { get; init; } = new()
    {
        [Rarity.Common] = 4, [Rarity.Uncommon] = 7, [Rarity.Rare] = 12, [Rarity.Epic] = 18, [Rarity.Legendary] = 25,
    };
    public int SellPercent { get; init; } = 50;
    public int ShopSize { get; init; } = 5;
    public int RerollBaseCost { get; init; } = 1;
    public int RerollCostStep { get; init; } = 1;

    /// <summary>The first this many days of every mission are always fight days.</summary>
    public int FightDaysAtStart { get; init; } = 1;
    /// <summary>How many shop days a mission has: [fewest, most], picked at random per mission. Every other day is a fight.</summary>
    public int[] ShopDaysPerMission { get; init; } = [2, 3];
    public List<ShopConfig> Shops { get; init; } =
        [new ShopConfig("shop", "Shop"), new ShopConfig("passive_shop", "Passive Shop", Sells: ShopSells.Passives)];
    /// <summary>
    /// The run's difficulty goes up by 1 every this many days (5 → each half of a 10-day mission), from 1 up to 10.
    /// </summary>
    public int DaysPerDifficulty { get; init; } = 5;
    /// <summary>
    /// Chance (%) that each fight option is one difficulty harder than the run's, by day within the current difficulty
    /// (1st, 2nd, ... day of each half-mission; the last entry repeats). Every other option is the run's difficulty.
    /// </summary>
    public int[] HarderEnemyPercent { get; init; } = [5, 15, 25, 35, 40];
    /// <summary>One boss per mission. Missions past the end of the list reuse the last boss.</summary>
    public List<BossConfig> Bosses { get; init; } = [];
    /// <summary>Rarity weights of the items offered after a win, by the beaten enemy's difficulty 1 to 10 (the last entry repeats).</summary>
    public List<int[]> DropRarityWeights { get; init; } =
    [
        [100, 15, 3, 0, 0],
        [90, 20, 5, 1, 0],
        [80, 30, 8, 2, 0],
        [70, 35, 12, 3, 1],
        [60, 40, 15, 5, 1],
        [50, 40, 20, 8, 2],
        [40, 40, 25, 10, 3],
        [30, 40, 30, 15, 5],
        [25, 35, 30, 20, 8],
        [20, 30, 30, 25, 12],
    ];
    /// <summary>Rarity weights of shop items, per mission (the last entry repeats).</summary>
    public List<int[]> ShopRarityWeights { get; init; } =
    [
        [100, 15, 3, 0, 0],
        [80, 30, 8, 2, 0],
        [60, 40, 15, 5, 1],
        [45, 40, 25, 10, 3],
        [30, 40, 30, 15, 5],
    ];
    /// <summary>Each owned item sharing a tag with an offered item makes that item this much more likely.</summary>
    public int TagBonusPercent { get; init; } = 15;

    public int Price(Rarity rarity) => Prices.TryGetValue(rarity, out int p) ? p : 0;

    /// <summary>The run's difficulty on a day (encounter is 0-based within the mission), 1 to 10.</summary>
    public int RunDifficulty(int mission, int encounter)
    {
        int day = (mission - 1) * EncountersPerMission + encounter;
        return Math.Clamp(1 + day / Math.Max(1, DaysPerDifficulty), 1, 10);
    }

    /// <summary>Chance (%) that a fight option on this day is one difficulty harder (encounter is 0-based within the mission).</summary>
    public int HarderEnemyChance(int mission, int encounter)
    {
        if (HarderEnemyPercent.Length == 0) return 0;
        int day = (mission - 1) * EncountersPerMission + encounter;
        return HarderEnemyPercent[Math.Min(day % Math.Max(1, DaysPerDifficulty), HarderEnemyPercent.Length - 1)];
    }

    public int GoldForFight(int enemyDifficulty) => FightGold.Length == 0 ? 0 : FightGold[Math.Clamp(enemyDifficulty - 1, 0, FightGold.Length - 1)];

    public int[] ShopWeights(int mission) => ShopRarityWeights.Count == 0 ? [1, 0, 0, 0, 0]
        : ShopRarityWeights[Math.Clamp(mission - 1, 0, ShopRarityWeights.Count - 1)];

    public int[] DropWeights(int enemyDifficulty) => DropRarityWeights.Count == 0 ? [1, 0, 0, 0, 0]
        : DropRarityWeights[Math.Clamp(enemyDifficulty - 1, 0, DropRarityWeights.Count - 1)];

    /// <summary>The boss of a mission (1-based), or null when run.json lists none.</summary>
    public BossConfig? Boss(int mission) => Bosses.Count == 0 ? null : Bosses[Math.Clamp(mission - 1, 0, Bosses.Count - 1)];

    public static RunConfig Parse(string json, ContentDatabase db, out List<string> errors)
    {
        errors = [];
        RunConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<RunConfig>(json, Json);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException)
        {
            errors.Add($"run.json can't be read: {e.Message}");
            return new RunConfig();
        }
        if (config is null)
        {
            errors.Add("run.json is empty");
            return new RunConfig();
        }
        config.Validate(db, errors);
        return errors.Count == 0 ? config : new RunConfig();
    }

    private void Validate(ContentDatabase db, List<string> errors)
    {
        void Check(bool ok, string message)
        {
            if (!ok) errors.Add(message);
        }
        Check(Missions >= 1, "'missions' must be at least 1");
        Check(EncountersPerMission >= 0, "'encountersPerMission' can't be negative");
        Check(ChoicesPerPick >= 1, "'choicesPerPick' must be at least 1");
        Check(HotbarSize >= 1 && HotbarSize <= Board.SlotCount, $"'hotbarSize' must be 1 to {Board.SlotCount}");
        Check(BackpackSize >= 0, "'backpackSize' can't be negative");
        Check(StartingGold >= 0 && SellPercent >= 0 && ShopSize >= 0 && RerollBaseCost >= 0 && RerollCostStep >= 0 && BossGold >= 0,
            "gold, prices, sizes and costs can't be negative");
        Check(FightGold.All(g => g >= 0) && Prices.Values.All(p => p >= 0), "gold and prices can't be negative");
        Check(FightDaysAtStart >= 0, "'fightDaysAtStart' can't be negative");
        Check(ShopDaysPerMission is { Length: 2 } && ShopDaysPerMission[0] >= 0 && ShopDaysPerMission[0] <= ShopDaysPerMission[1],
            "'shopDaysPerMission' must be [fewest, most], like [2, 3]");
        Check(DaysPerDifficulty >= 1, "'daysPerDifficulty' must be at least 1");
        Check(HarderEnemyPercent.All(p => p is >= 0 and <= 100), "'harderEnemyPercent' must be 0 to 100");
        Check(Shops.Count > 0 && Shops.All(s => s is not null && !string.IsNullOrWhiteSpace(s.Id) && !string.IsNullOrWhiteSpace(s.Name)),
            "'shops' needs at least one shop, each with an id and a name");
        Check(TagBonusPercent >= 0, "'tagBonusPercent' can't be negative");
        foreach (var w in DropRarityWeights.Concat(ShopRarityWeights))
            Check(w is { Length: 5 } && w.All(x => x >= 0), "rarity weights must be 5 numbers (Common..Legendary), none negative");
        foreach (var boss in Bosses)
        {
            if (boss is null || boss.Candidates.Count == 0) { errors.Add("every boss entry needs a 'pool' of enemy ids"); continue; }
            foreach (string id in boss.Candidates)
            {
                if (!db.TryGet(id, out var def)) errors.Add($"unknown boss enemy '{id}'");
                else if (def.Kind != SourceKind.Enemy) errors.Add($"boss '{id}' is not an enemy");
            }
        }
    }
}
