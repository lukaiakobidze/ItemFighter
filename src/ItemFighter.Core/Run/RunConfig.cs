using System.Text.Json;
using System.Text.Json.Serialization;
using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;

namespace ItemFighter.Core.Run;

public enum EncounterKind { Fight, Elite, Shop, Boss }

/// <summary>A mission's boss: an enemy and how much bigger its HP is than normal.</summary>
public sealed record BossConfig(string Enemy, int HpPercent = 200);

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
    };

    public int Missions { get; init; } = 5;
    public int EncountersPerMission { get; init; } = 10;
    public int ChoicesPerPick { get; init; } = 3;
    public int HotbarSize { get; init; } = Board.SlotCount;
    public int BackpackSize { get; init; } = 12;

    public int StartingGold { get; init; } = 10;
    /// <summary>Gold for winning a normal fight, per mission (the last entry repeats).</summary>
    public int[] FightGold { get; init; } = [5, 6, 7, 8, 9];
    public int EliteGoldPercent { get; init; } = 200;
    public int BossGold { get; init; } = 15;
    public Dictionary<Rarity, int> Prices { get; init; } = new()
    {
        [Rarity.Common] = 4, [Rarity.Uncommon] = 7, [Rarity.Rare] = 12, [Rarity.Epic] = 18, [Rarity.Legendary] = 25,
    };
    public int SellPercent { get; init; } = 50;
    public int ShopSize { get; init; } = 5;
    public int RerollBaseCost { get; init; } = 1;
    public int RerollCostStep { get; init; } = 1;

    /// <summary>How often each encounter type shows up among the choices (bosses are never random).</summary>
    public Dictionary<EncounterKind, int> EncounterWeights { get; init; } = new()
    {
        [EncounterKind.Fight] = 60, [EncounterKind.Elite] = 15, [EncounterKind.Shop] = 25,
    };
    public int EnemyHpPercentPerMission { get; init; } = 25;
    public int EliteHpPercent { get; init; } = 150;
    /// <summary>One boss per mission. Missions past the end of the list reuse the last boss.</summary>
    public List<BossConfig> Bosses { get; init; } = [];
    /// <summary>How often each enemy rarity is met.</summary>
    public Dictionary<Rarity, int> EnemyRarityWeights { get; init; } = new()
    {
        [Rarity.Common] = 100, [Rarity.Uncommon] = 50, [Rarity.Rare] = 20, [Rarity.Epic] = 8, [Rarity.Legendary] = 3,
    };
    /// <summary>Rarity weights of random drop choices, by the rarity of the enemy that dropped them.</summary>
    public Dictionary<Rarity, int[]> DropRarityWeights { get; init; } = new()
    {
        [Rarity.Common] = [100, 20, 5, 1, 0],
        [Rarity.Uncommon] = [60, 40, 15, 4, 1],
        [Rarity.Rare] = [30, 40, 25, 10, 3],
        [Rarity.Epic] = [15, 30, 30, 20, 8],
        [Rarity.Legendary] = [5, 20, 30, 30, 15],
    };
    /// <summary>Rarity weights of shop items, per mission (the last entry repeats).</summary>
    public List<int[]> ShopRarityWeights { get; init; } =
    [
        [100, 15, 3, 0, 0],
        [80, 30, 8, 2, 0],
        [60, 40, 15, 5, 1],
        [45, 40, 25, 10, 3],
        [30, 40, 30, 15, 5],
    ];
    public int CharacterPoolMultiplier { get; init; } = 3;
    public int TagBonusPercent { get; init; } = 15;
    public int ItemDropChancePercent { get; init; } = 50;

    public int Price(Rarity rarity) => Prices.TryGetValue(rarity, out int p) ? p : 0;

    public int GoldForFight(int mission) => FightGold.Length == 0 ? 0 : FightGold[Math.Clamp(mission - 1, 0, FightGold.Length - 1)];

    public int[] ShopWeights(int mission) => ShopRarityWeights.Count == 0 ? [1, 0, 0, 0, 0]
        : ShopRarityWeights[Math.Clamp(mission - 1, 0, ShopRarityWeights.Count - 1)];

    public int[] DropWeights(Rarity enemyRarity) => DropRarityWeights.TryGetValue(enemyRarity, out var w) ? w : [1, 0, 0, 0, 0];

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
        Check(EncounterWeights.Values.All(w => w >= 0) && EnemyRarityWeights.Values.All(w => w >= 0), "weights can't be negative");
        Check(EnemyHpPercentPerMission >= 0 && EliteHpPercent > 0, "HP percents must be positive");
        Check(CharacterPoolMultiplier >= 0 && TagBonusPercent >= 0, "'characterPoolMultiplier' and 'tagBonusPercent' can't be negative");
        Check(ItemDropChancePercent is >= 0 and <= 100, "'itemDropChancePercent' must be 0 to 100");
        foreach (var w in DropRarityWeights.Values.Concat(ShopRarityWeights))
            Check(w is { Length: 5 } && w.All(x => x >= 0), "rarity weights must be 5 numbers (Common..Legendary), none negative");
        foreach (var boss in Bosses)
        {
            if (boss is null || !db.TryGet(boss.Enemy ?? "", out var def)) errors.Add($"unknown boss enemy '{boss?.Enemy}'");
            else if (def.Kind != SourceKind.Enemy) errors.Add($"boss '{boss.Enemy}' is not an enemy");
            else Check(boss.HpPercent > 0, $"boss '{boss.Enemy}' needs a positive hpPercent");
        }
    }
}
