using System.Text.Json;
using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;

namespace ItemFighter.Screens;

/// <summary>What the Fight Lab is set to. Saved between sessions; anything unreadable or unknown falls back to defaults.</summary>
public sealed class LabSetup
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string?[] Items { get; set; } = new string?[Board.SlotCount];
    public int[] Levels { get; set; } = Enumerable.Repeat(1, Board.SlotCount).ToArray();
    public string EnemyId { get; set; } = "";
    public string? CharacterId { get; set; }
    public List<string> Passives { get; set; } = [];
    public ulong Seed { get; set; } = 1;

    public static LabSetup Default(ContentDatabase db)
    {
        var setup = new LabSetup { CharacterId = "wanderer", EnemyId = "goblin_scrapper" };
        string[] starter = ["rusty_sword", "buckler", "fire_flask", "whetstone"];
        for (int i = 0; i < starter.Length; i++) setup.Items[i] = starter[i];
        setup.Sanitize(db);
        return setup;
    }

    public static LabSetup Load(string path, ContentDatabase db)
    {
        LabSetup? setup = null;
        try
        {
            if (File.Exists(path)) setup = JsonSerializer.Deserialize<LabSetup>(File.ReadAllText(path), Json);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            setup = null;
        }
        if (setup is null) return Default(db);
        setup.Sanitize(db);
        return setup;
    }

    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Drops ids that no longer exist (content changes) and clamps everything into range.</summary>
    public void Sanitize(ContentDatabase db)
    {
        var items = new string?[Board.SlotCount];
        var levels = new int[Board.SlotCount];
        for (int i = 0; i < Board.SlotCount; i++)
        {
            string? id = Items is not null && i < Items.Length ? Items[i] : null;
            items[i] = id is not null && Is(db, id, SourceKind.Item) ? id : null;
            levels[i] = Math.Clamp(Levels is not null && i < Levels.Length ? Levels[i] : 1, 1, ContentLoader.LevelCount);
        }
        Items = items;
        Levels = levels;
        if (EnemyId is null || !Is(db, EnemyId, SourceKind.Enemy))
            EnemyId = Is(db, "goblin_scrapper", SourceKind.Enemy) ? "goblin_scrapper" : db.OfKind(SourceKind.Enemy).FirstOrDefault()?.Id ?? "";
        if (CharacterId is not null && !Is(db, CharacterId, SourceKind.Character)) CharacterId = null;
        Passives = (Passives ?? []).Where(p => p is not null && Is(db, p, SourceKind.Passive)).Distinct().ToList();
        if (Seed == 0) Seed = 1;
    }

    public BattleSetup ToBattleSetup()
    {
        var board = new List<BoardItem>();
        for (int i = 0; i < Board.SlotCount; i++)
            if (Items[i] is string id) board.Add(new BoardItem(id, i, Levels[i]));
        return new BattleSetup(new PlayerSetup(CharacterId, board, Passives), EnemyId, Seed);
    }

    private static bool Is(ContentDatabase db, string id, SourceKind kind) => db.TryGet(id, out var def) && def.Kind == kind;
}
