using System.Text.Json.Serialization;
using ItemFighter.Core.Content;
using ItemFighter.Core.Determinism;
using ItemFighter.Core.Engine;

namespace ItemFighter.Core.Run;

public enum RunPhase { ChoosingEncounter, Preparing, Fighting, Dropping, Shopping, Won, Lost }

/// <summary>Where an owned item sits: a hotbar slot, or a backpack index (Backpack.Count means "the end").</summary>
public sealed record ItemRef(bool InHotbar, int Index);

/// <summary>
/// One run in progress. Plain data plus the rules that change it; the Game shows it and calls these methods.
/// Every random choice forks a fresh Rng from Seed with a key naming the moment, so nothing random needs saving.
/// </summary>
public sealed class RunState
{
    public const int SaveVersion = 2;

    public int Version { get; set; } = SaveVersion;
    public ulong Seed { get; set; }
    public string? CharacterId { get; set; }
    /// <summary>1-based mission number.</summary>
    public int Mission { get; set; } = 1;
    /// <summary>Encounters done in this mission. Equal to EncountersPerMission means the boss is next.</summary>
    public int Encounter { get; set; }
    public int Gold { get; set; }
    public OwnedItem?[] Hotbar { get; set; } = [];
    public List<OwnedItem> Backpack { get; set; } = [];
    public List<string> Passives { get; set; } = [];
    public RunPhase Phase { get; set; }
    public Encounter? Current { get; set; }
    public DropOffer? PendingDrop { get; set; }
    public List<string>? ShopStock { get; set; }
    public int ShopRerolls { get; set; }
    public int FightsWon { get; set; }
    /// <summary>Max HP gained this run (from passives like Battle Hardened).</summary>
    public int BonusMaxHp { get; set; }
    /// <summary>Enemies fought this run (bosses excluded). They aren't offered again while there are others.</summary>
    public List<string> FoughtEnemies { get; set; } = [];

    [JsonIgnore] public RunConfig Config { get; private set; } = new();
    [JsonIgnore] public ContentDatabase Db { get; private set; } = new();

    public void Attach(RunConfig config, ContentDatabase db)
    {
        Config = config;
        Db = db;
    }

    public static RunState New(ulong seed, string? characterId, RunConfig config, ContentDatabase db)
    {
        var run = new RunState { Seed = seed == 0 ? 1 : seed, CharacterId = characterId, Gold = config.StartingGold };
        run.Attach(config, db);
        run.Hotbar = new OwnedItem?[config.HotbarSize];
        if (characterId is not null && db.TryGet(characterId, out var character))
            foreach (string id in character.StartingItems) run.AddItem(id);
        return run;
    }

    [JsonIgnore] public IReadOnlyList<OwnedItem> AllOwned => Hotbar.OfType<OwnedItem>().Concat(Backpack).ToList();

    [JsonIgnore] public RunView View => new(Config, Db, CharacterId, Mission, AllOwned, Passives);

    [JsonIgnore] public bool IsBossNext => Encounter >= Config.EncountersPerMission;

    [JsonIgnore] public bool IsOver => Phase is RunPhase.Won or RunPhase.Lost;

    private Rng RngFor(string key) => new Rng(Seed).Fork(key);

    // ---- encounters -------------------------------------------------------------------------------------------

    /// <summary>This mission's days (fight or shop), the same every time for this run.</summary>
    [JsonIgnore] public IReadOnlyList<DayKind> MissionPlan => Offers.MissionPlan(RngFor($"plan:{Mission}"), Config);

    public IReadOnlyList<Encounter> EncounterChoices()
    {
        if (IsBossNext) return [Boss(Mission)];
        var rng = RngFor($"enc:{Mission}:{Encounter}");
        return MissionPlan[Encounter] == DayKind.Shop && Config.Shops.Count > 0
            ? Offers.ShopChoices(rng, View)
            : Offers.FightChoices(rng, View, Config.RunDifficulty(Mission, Encounter), Config.HarderEnemyChance(Mission, Encounter), FoughtEnemies.ToHashSet());
    }

    /// <summary>The mission's boss from run.json, or the enemy with the most HP when none is listed.</summary>
    public Encounter Boss(int mission)
    {
        string? id = Config.Boss(mission)?.Enemy;
        if (id is null || !Db.TryGet(id, out var def) || def.Kind != SourceKind.Enemy)
            id = Db.OfKind(SourceKind.Enemy).OrderByDescending(e => e.MaxHp).ThenBy(e => e.Id, StringComparer.Ordinal).First().Id;
        return new Encounter(EncounterKind.Boss, id);
    }

    public void Choose(Encounter encounter)
    {
        Require(RunPhase.ChoosingEncounter);
        Current = encounter;
        if (encounter.Kind == EncounterKind.Shop)
        {
            ShopRerolls = 0;
            ShopStock = Offers.Shop(RngFor(ShopKey), View, encounter.ShopId).ToList();
            Phase = RunPhase.Shopping;
        }
        else Phase = RunPhase.Preparing;
    }

    // ---- fights -----------------------------------------------------------------------------------------------

    [JsonIgnore] public bool CanFight => Hotbar.Any(h => h is not null);

    /// <summary>Gold for winning an encounter: the boss pays BossGold, other fights pay more for harder enemies.</summary>
    public int GoldFor(Encounter encounter) =>
        encounter.Kind == EncounterKind.Boss ? Config.BossGold
        : Config.GoldForFight(Db.TryGet(encounter.EnemyId ?? "", out var e) ? e.Difficulty : 1);

    /// <summary>Extra gold per win from items on the hotbar ("goldOnWin", e.g. Gold Pouch).</summary>
    [JsonIgnore] public int GoldFromItems => Hotbar.OfType<OwnedItem>()
        .Sum(i => Db.TryGet(i.ItemId, out var def) && def.GoldOnWin is ValueRef g ? Math.Max(0, g.ResolveAt(def, i.Level)) : 0);

    /// <summary>The fight for the current encounter. Same run + same encounter = same fight, so a restart replays it.</summary>
    public BattleSetup StartFight()
    {
        Require(RunPhase.Preparing);
        if (!CanFight) throw new InvalidOperationException("put at least one item on the hotbar first");
        var encounter = Current!;
        var board = new List<BoardItem>();
        for (int i = 0; i < Hotbar.Length && i < Board.SlotCount; i++)
            if (Hotbar[i] is OwnedItem item) board.Add(new BoardItem(item.ItemId, i, item.Level));
        ulong seed = StableHash.Of($"fight:{Seed}:{Mission}:{Encounter}");
        Phase = RunPhase.Fighting;
        if (encounter.Kind == EncounterKind.Fight && !FoughtEnemies.Contains(encounter.EnemyId!)) FoughtEnemies.Add(encounter.EnemyId!);
        return new BattleSetup(new PlayerSetup(CharacterId, board, Passives.ToList(), BonusMaxHp), encounter.EnemyId!, seed == 0 ? 1 : seed);
    }

    public void FinishFight(bool won)
    {
        Require(RunPhase.Fighting);
        var encounter = Current!;
        if (!won)
        {
            Phase = RunPhase.Lost;
            return;
        }
        FightsWon++;
        BonusMaxHp += Passives.Sum(p => Db.TryGet(p, out var def) ? def.MaxHpPerWin : 0);
        Gold += GoldFor(encounter) + GoldFromItems;
        PendingDrop = Offers.Drop(RngFor($"drop:{Mission}:{Encounter}"), encounter.EnemyId!, View);
        Phase = RunPhase.Dropping;
    }

    public bool IsPassiveId(string? id) => id is not null && Db.TryGet(id, out var def) && def.Kind == SourceKind.Passive;

    /// <summary>Takes drop choice index. False (nothing changes) when the item needs room you don't have.</summary>
    public bool TakeDrop(int index)
    {
        Require(RunPhase.Dropping);
        var choices = PendingDrop?.Choices ?? [];
        if (index < 0 || index >= choices.Count) return false;
        if (!AddItem(choices[index])) return false;
        Advance();
        return true;
    }

    public void SkipDrop()
    {
        Require(RunPhase.Dropping);
        Advance();
    }

    /// <summary>Moves on after an encounter: next encounter, or after a boss the next mission (or the win).</summary>
    private void Advance()
    {
        bool bossDone = Current?.Kind == EncounterKind.Boss;
        Current = null;
        PendingDrop = null;
        ShopStock = null;
        ShopRerolls = 0;
        if (bossDone)
        {
            if (Mission >= Config.Missions)
            {
                Phase = RunPhase.Won;
                return;
            }
            Mission++;
            Encounter = 0;
        }
        else Encounter++;
        Phase = RunPhase.ChoosingEncounter;
    }

    // ---- shop and gold ----------------------------------------------------------------------------------------

    private string ShopKey => $"shop:{Mission}:{Encounter}:{ShopRerolls}";

    public int Price(string itemId, int level = 1) => Db.TryGet(itemId, out var def) ? Config.Price(def.Rarity) * level : 0;

    public int SellValue(OwnedItem item) => Price(item.ItemId, item.Level) * Config.SellPercent / 100;

    [JsonIgnore] public int RerollCost => Config.RerollBaseCost + ShopRerolls * Config.RerollCostStep;

    public bool Buy(int stockIndex)
    {
        Require(RunPhase.Shopping);
        if (ShopStock is null || stockIndex < 0 || stockIndex >= ShopStock.Count) return false;
        string id = ShopStock[stockIndex];
        int price = Price(id);
        if (Gold < price) return false;
        if (Db.TryGet(id, out var def) && def.Kind == SourceKind.Passive)
        {
            if (Passives.Contains(id)) return false;
            Passives.Add(id);
        }
        else if (!Offers.CanOffer(id, View) || !AddItem(id)) return false;
        Gold -= price;
        ShopStock.RemoveAt(stockIndex);
        return true;
    }

    public bool Reroll()
    {
        Require(RunPhase.Shopping);
        int cost = RerollCost;
        if (Gold < cost) return false;
        Gold -= cost;
        ShopRerolls++;
        ShopStock = Offers.Shop(RngFor(ShopKey), View, Current?.ShopId).ToList();
        return true;
    }

    public void LeaveShop()
    {
        Require(RunPhase.Shopping);
        Advance();
    }

    // ---- inventory --------------------------------------------------------------------------------------------

    [JsonIgnore] public bool HasRoom => Backpack.Count < Config.BackpackSize || Hotbar.Any(h => h is null);

    [JsonIgnore] public bool CanManageItems => Phase is RunPhase.ChoosingEncounter or RunPhase.Preparing or RunPhase.Dropping or RunPhase.Shopping;

    public OwnedItem? At(ItemRef where) =>
        where.InHotbar ? (where.Index >= 0 && where.Index < Hotbar.Length ? Hotbar[where.Index] : null)
        : where.Index >= 0 && where.Index < Backpack.Count ? Backpack[where.Index] : null;

    /// <summary>
    /// Gains an item: a copy you own below max level goes up one level; otherwise it goes in the first empty hotbar slot,
    /// then the backpack. False when there is no room (or the id is not an item).
    /// </summary>
    public bool AddItem(string itemId)
    {
        if (!Db.TryGet(itemId, out var def) || def.Kind != SourceKind.Item) return false;
        for (int i = 0; i < Hotbar.Length; i++)
            if (Hotbar[i] is { } h && h.ItemId == itemId && h.Level < ContentLoader.LevelCount)
            {
                Hotbar[i] = h with { Level = h.Level + 1 };
                return true;
            }
        for (int i = 0; i < Backpack.Count; i++)
            if (Backpack[i].ItemId == itemId && Backpack[i].Level < ContentLoader.LevelCount)
            {
                Backpack[i] = Backpack[i] with { Level = Backpack[i].Level + 1 };
                return true;
            }
        if (AllOwned.Any(o => o.ItemId == itemId)) return false; // already at max level
        int empty = Array.IndexOf(Hotbar, null);
        if (empty >= 0)
        {
            Hotbar[empty] = new OwnedItem(itemId, 1);
            return true;
        }
        if (Backpack.Count >= Config.BackpackSize) return false;
        Backpack.Add(new OwnedItem(itemId, 1));
        return true;
    }

    /// <summary>Sells an item for SellValue. Your last item can't be sold, so there is always something to fight with.</summary>
    public bool Sell(ItemRef where)
    {
        if (!CanManageItems || At(where) is not OwnedItem item || AllOwned.Count <= 1) return false;
        if (where.InHotbar) Hotbar[where.Index] = null;
        else Backpack.RemoveAt(where.Index);
        Gold += SellValue(item);
        return true;
    }

    /// <summary>Moves an item, swapping with whatever is at the target. A backpack target past the end appends.</summary>
    public bool Move(ItemRef from, ItemRef to)
    {
        if (!CanManageItems || At(from) is not OwnedItem item || from == to) return false;
        if (to.InHotbar && (to.Index < 0 || to.Index >= Hotbar.Length)) return false;
        if (!to.InHotbar && to.Index < 0) return false;
        var target = At(to);

        if (!to.InHotbar && target is null)
        {
            // Appending to the backpack.
            if (from.InHotbar && Backpack.Count >= Config.BackpackSize) return false;
            Remove(from);
            Backpack.Add(item);
            return true;
        }
        Put(from, target);
        Put(to, item);
        if (!from.InHotbar && target is null) Backpack.RemoveAll(b => b is null);
        return true;
    }

    private void Remove(ItemRef where)
    {
        if (where.InHotbar) Hotbar[where.Index] = null;
        else Backpack.RemoveAt(where.Index);
    }

    private void Put(ItemRef where, OwnedItem? item)
    {
        if (where.InHotbar) Hotbar[where.Index] = item;
        else Backpack[where.Index] = item!;
    }

    private void Require(RunPhase phase)
    {
        if (Phase != phase) throw new InvalidOperationException($"the run is {Phase}, not {phase}");
    }
}
