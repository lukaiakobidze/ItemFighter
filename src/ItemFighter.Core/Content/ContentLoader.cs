using System.Text.Json;
using ItemFighter.Core.Engine.Effects;
using ItemFighter.Core.Engine.Triggers;

namespace ItemFighter.Core.Content;

public sealed record ContentError(string File, string? Id, string Message)
{
    public override string ToString() => Id is null ? $"{File}: {Message}" : $"{File} [{Id}]: {Message}";
}

public sealed record ContentLoadResult(ContentDatabase Database, IReadOnlyList<ContentError> Errors);

/// <summary>
/// Reads content JSON. Each file holds one definition object or an array of them.
/// A bad file or definition is reported and skipped; loading never stops halfway.
/// </summary>
public static class ContentLoader
{
    public const int LevelCount = 4;
    public const int TicksPerSecond = 10;

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static ContentLoadResult LoadDirectory(string dataDir, TriggerRegistry triggers, EffectRegistry effects) =>
        LoadFromStrings(ReadDirectory(dataDir), triggers, effects);

    /// <summary>All *.json files under a directory as (relative path with '/', text), sorted by path.</summary>
    public static IReadOnlyList<(string Path, string Json)> ReadDirectory(string dataDir) =>
        Directory.EnumerateFiles(dataDir, "*.json", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(dataDir, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (f, File.ReadAllText(Path.Combine(dataDir, f))))
            .ToList();

    public static ContentLoadResult LoadFromStrings(IEnumerable<(string Path, string Json)> files, TriggerRegistry triggers, EffectRegistry effects)
    {
        var db = new ContentDatabase();
        var errors = new List<ContentError>();
        foreach (var (path, json) in files)
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(json, JsonOptions); }
            catch (JsonException ex)
            {
                errors.Add(new ContentError(path, null, $"invalid JSON: {ex.Message}"));
                continue;
            }
            using (doc)
            {
                var root = doc.RootElement;
                var items = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToList() : [root];
                foreach (var element in items)
                {
                    string? id = element.ValueKind == JsonValueKind.Object && element.TryGetProperty("id", out var idProp)
                        && idProp.ValueKind == JsonValueKind.String ? idProp.GetString() : null;
                    try
                    {
                        var def = ParseDefinition(element, path, triggers, effects);
                        if (!db.TryAdd(def)) errors.Add(new ContentError(path, def.Id, $"duplicate id '{def.Id}'"));
                    }
                    catch (ContentException ex)
                    {
                        errors.Add(new ContentError(path, id, ex.Message));
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or FormatException or OverflowException or ArgumentException)
                    {
                        errors.Add(new ContentError(path, id, $"unreadable value ({ex.Message})"));
                    }
                }
            }
        }
        return new ContentLoadResult(db, errors);
    }

    public const int MaxCooldownSeconds = 600;

    public static int SecondsToTicks(decimal seconds) =>
        Math.Max(1, (int)Math.Round(seconds * TicksPerSecond, MidpointRounding.AwayFromZero));

    private static SourceDefinition ParseDefinition(JsonElement json, string path, TriggerRegistry triggers, EffectRegistry effects)
    {
        if (json.ValueKind != JsonValueKind.Object) throw new ContentException("a definition must be a JSON object");
        var levels = ParseLevels(json);
        var ctx = new ParseContext(json, levels.Keys.ToHashSet(StringComparer.Ordinal));

        string id = ctx.ReadString("id");
        var kind = ctx.Has("kind") ? ctx.ReadEnum<SourceKind>("kind") : DefaultKind(path);
        int? cooldown = null;
        if (json.TryGetProperty("cooldown", out var cd))
        {
            if (cd.ValueKind != JsonValueKind.Number || !cd.TryGetDecimal(out decimal seconds) || seconds <= 0 || seconds > MaxCooldownSeconds)
                throw new ContentException($"'cooldown' must be a number of seconds above 0 and at most {MaxCooldownSeconds}");
            cooldown = SecondsToTicks(seconds);
        }
        var activation = ctx.ReadEnum("activation", cooldown is null ? Activation.None : Activation.Passive);
        if (activation != Activation.None && cooldown is null)
            throw new ContentException($"activation '{activation}' needs a 'cooldown'");

        return new SourceDefinition
        {
            Id = id,
            Name = ctx.ReadString("name", id),
            Kind = kind,
            File = path,
            Rarity = ctx.ReadEnum("rarity", Rarity.Common),
            Tags = ctx.ReadStrings("tags"),
            CooldownTicks = cooldown,
            Activation = activation,
            RequiresTarget = json.TryGetProperty("requiresTarget", out var rt) && rt.ValueKind == JsonValueKind.True,
            TargetFilter = ctx.ReadString("targetFilter", "OwnItem"),
            Levels = levels,
            Abilities = ParseAbilities(json, levels.Keys.ToHashSet(StringComparer.Ordinal), activation, triggers, effects),
            Modifiers = ParseModifiers(json),
            Script = json.TryGetProperty("script", out _) ? ctx.ReadString("script") : null,
            Description = json.TryGetProperty("description", out _) ? ctx.ReadString("description") : null,
            MaxHp = ctx.ReadInt("maxHp", 0),
            AbilityIds = ctx.ReadStrings("abilityIds"),
            PassiveIds = ctx.ReadStrings("passiveIds"),
            Phases = ParsePhases(json),
            StartingItems = ctx.ReadStrings("startingItems"),
            GoldOnWin = ctx.Has("goldOnWin") ? ctx.ReadValue("goldOnWin") : null,
            Difficulty = ctx.ReadInt("difficulty", 1),
        };
    }

    private static SourceKind DefaultKind(string path) => path.Split('/')[0] switch
    {
        "items" => SourceKind.Item,
        "passives" => SourceKind.Passive,
        "characters" => SourceKind.Character,
        "enemies" => SourceKind.Enemy,
        _ => throw new ContentException("missing 'kind' (or put the file under items/, passives/, characters/ or enemies/)"),
    };

    private static Dictionary<string, int[]> ParseLevels(JsonElement json)
    {
        var levels = new Dictionary<string, int[]>(StringComparer.Ordinal);
        if (!json.TryGetProperty("levels", out var obj)) return levels;
        if (obj.ValueKind != JsonValueKind.Object) throw new ContentException("'levels' must be an object of name: [4 numbers]");
        foreach (var prop in obj.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array || prop.Value.GetArrayLength() != LevelCount)
                throw new ContentException($"level value '{prop.Name}' must have {LevelCount} entries");
            levels[prop.Name] = prop.Value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out int n) ? n
                : throw new ContentException($"level value '{prop.Name}' must contain whole numbers")).ToArray();
        }
        return levels;
    }

    private static List<AbilityDefinition> ParseAbilities(JsonElement json, IReadOnlySet<string> levelKeys, Activation activation, TriggerRegistry triggers, EffectRegistry effects)
    {
        var result = new List<AbilityDefinition>();
        if (!json.TryGetProperty("abilities", out var arr)) return result;
        if (arr.ValueKind != JsonValueKind.Array) throw new ContentException("'abilities' must be a list");
        string defaultTrigger = activation == Activation.Active ? "OnActivate" : "OnCooldown";
        foreach (var ability in arr.EnumerateArray())
        {
            if (ability.ValueKind != JsonValueKind.Object) throw new ContentException("each ability must be an object");
            var trigger = ParseTyped(ability, "trigger", defaultTrigger, levelKeys, triggers.Create);
            var list = new List<IEffect>();
            if (ability.TryGetProperty("effects", out var effs))
            {
                if (effs.ValueKind != JsonValueKind.Array) throw new ContentException("'effects' must be a list");
                foreach (var eff in effs.EnumerateArray())
                {
                    var ctx = new ParseContext(eff, levelKeys);
                    list.Add(effects.Create(ctx.ReadString("type"), ctx));
                }
            }
            result.Add(new AbilityDefinition(trigger, list));
        }
        return result;
    }

    /// <summary>A trigger written as "OnCooldown" or as { "type": "OnHpBelow", "percent": 50 }.</summary>
    private static T ParseTyped<T>(JsonElement parent, string prop, string defaultType, IReadOnlySet<string> levelKeys, Func<string, ParseContext, T> create)
    {
        if (!parent.TryGetProperty(prop, out var v))
            return CreateFromName(defaultType, levelKeys, create);
        if (v.ValueKind == JsonValueKind.String)
            return CreateFromName(v.GetString()!, levelKeys, create);
        var ctx = new ParseContext(v, levelKeys);
        return create(ctx.ReadString("type"), ctx);
    }

    private static T CreateFromName<T>(string type, IReadOnlySet<string> levelKeys, Func<string, ParseContext, T> create)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new { type }));
        return create(type, new ParseContext(doc.RootElement.Clone(), levelKeys));
    }

    private static List<ModifierDefinition> ParseModifiers(JsonElement json)
    {
        var result = new List<ModifierDefinition>();
        if (!json.TryGetProperty("modifiers", out var arr)) return result;
        if (arr.ValueKind != JsonValueKind.Array) throw new ContentException("'modifiers' must be a list");
        var none = new HashSet<string>();
        foreach (var m in arr.EnumerateArray())
        {
            var ctx = new ParseContext(m, none);
            string stat = ctx.ReadString("stat");
            if (!ModifierDefinition.KnownStats.Contains(stat)) throw new ContentException($"unknown modifier stat '{stat}'");
            SourceKind? kind = null;
            string? tag = null;
            if (m.TryGetProperty("filter", out var f))
            {
                var fctx = new ParseContext(f, none);
                if (fctx.Has("kind")) kind = fctx.ReadEnum<SourceKind>("kind");
                if (fctx.Has("tag")) tag = fctx.ReadString("tag");
            }
            result.Add(new ModifierDefinition(stat, ctx.ReadInt("amount"), new ModifierFilter(kind, tag)));
        }
        return result;
    }

    private static List<PhaseDefinition> ParsePhases(JsonElement json)
    {
        var result = new List<PhaseDefinition>();
        if (!json.TryGetProperty("phases", out var arr)) return result;
        if (arr.ValueKind != JsonValueKind.Array) throw new ContentException("'phases' must be a list");
        var none = new HashSet<string>();
        foreach (var p in arr.EnumerateArray())
        {
            var ctx = new ParseContext(p, none);
            int percent = ctx.ReadInt("hpBelowPercent");
            if (percent is <= 0 or >= 100) throw new ContentException("'hpBelowPercent' must be between 1 and 99");
            result.Add(new PhaseDefinition(percent, ctx.ReadStrings("addAbilities")));
        }
        return result;
    }
}
