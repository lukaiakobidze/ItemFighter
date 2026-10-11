using System.Text.Json;
using System.Text.Json.Serialization;
using ItemFighter.Core.Content;

namespace ItemFighter.Core.Run;

/// <summary>Saves a run as JSON. Loading never throws: bad files give null, and content that no longer exists is dropped.</summary>
public static class RunSave
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string ToJson(RunState run) => JsonSerializer.Serialize(run, Json);

    public static RunState? FromJson(string json, RunConfig config, ContentDatabase db)
    {
        RunState? run;
        try
        {
            run = JsonSerializer.Deserialize<RunState>(json, Json);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
        if (run is null || run.Version != RunState.SaveVersion || run.IsOver || !Enum.IsDefined(run.Phase)) return null;
        if (run.Current is not null && !Enum.IsDefined(run.Current.Kind)) return null;
        run.Attach(config, db);
        try
        {
            Sanitize(run, config, db);
        }
        catch (Exception e) when (e is ArgumentException or NullReferenceException or InvalidOperationException or IndexOutOfRangeException)
        {
            return null; // a damaged file must never stop the game from starting
        }
        return run;
    }

    public static void Save(string path, RunState run)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, ToJson(run));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public static RunState? Load(string path, RunConfig config, ContentDatabase db)
    {
        try
        {
            return File.Exists(path) ? FromJson(File.ReadAllText(path), config, db) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Delete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    private static void Sanitize(RunState run, RunConfig config, ContentDatabase db)
    {
        bool IsItem(string? id) => id is not null && db.TryGet(id, out var d) && d.Kind == SourceKind.Item;
        OwnedItem? Clean(OwnedItem? o) => o is not null && IsItem(o.ItemId) ? o with { Level = Math.Clamp(o.Level, 1, ContentLoader.LevelCount) } : null;

        var saved = run.Hotbar ?? [];
        var hotbar = new OwnedItem?[config.HotbarSize];
        for (int i = 0; i < hotbar.Length && i < saved.Length; i++) hotbar[i] = Clean(saved[i]);
        run.Hotbar = hotbar;
        // Items beyond a smaller hotbar are kept in the backpack rather than lost.
        var overflow = saved.Skip(hotbar.Length).Select(Clean).OfType<OwnedItem>();
        run.Backpack = (run.Backpack ?? []).Select(Clean).OfType<OwnedItem>().Concat(overflow).ToList();
        run.Passives = (run.Passives ?? []).Where(id => id is not null && run.IsPassiveId(id)).Distinct().ToList();
        if (run.CharacterId is not null && !(db.TryGet(run.CharacterId, out var c) && c.Kind == SourceKind.Character)) run.CharacterId = null;
        if (run.Seed == 0) run.Seed = 1;
        run.Mission = Math.Clamp(run.Mission, 1, config.Missions);
        run.Encounter = Math.Clamp(run.Encounter, 0, config.EncountersPerMission);
        run.Gold = Math.Max(0, run.Gold);
        run.ShopRerolls = Math.Max(0, run.ShopRerolls);

        if (run.Phase == RunPhase.Fighting) run.Phase = RunPhase.Preparing; // a quit mid-fight replays that fight
        bool isShop = run.Current is { Kind: EncounterKind.Shop };
        bool enemyOk = run.Current?.EnemyId is string e && db.TryGet(e, out var en) && en.Kind == SourceKind.Enemy;
        bool matches = run.Phase switch
        {
            RunPhase.Shopping => isShop,
            RunPhase.Preparing or RunPhase.Dropping => !isShop && enemyOk,
            _ => true,
        };
        if (!matches) run.Phase = RunPhase.ChoosingEncounter;
        if (run.Phase == RunPhase.Shopping) run.ShopStock = (run.ShopStock ?? []).Where(IsItem).ToList();
        if (run.Phase == RunPhase.Dropping)
        {
            var choices = (run.PendingDrop?.Choices ?? []).Where(id => id is not null && (IsItem(id) || run.IsPassiveId(id))).ToList();
            run.PendingDrop = new DropOffer(run.PendingDrop?.Kind ?? DropKind.Items, choices);
        }
        if (run.Phase == RunPhase.ChoosingEncounter)
        {
            run.Current = null;
            run.PendingDrop = null;
            run.ShopStock = null;
        }
    }
}
