using System.Text.Json;
using ItemFighter.Core.Engine;

namespace ItemFighter.Core.Content;

public sealed class ContentException(string message) : Exception(message);

/// <summary>Reads the fields of one trigger or effect object. Errors throw <see cref="ContentException"/>, which the loader reports.</summary>
public sealed class ParseContext
{
    private readonly IReadOnlySet<string> _levelKeys;

    public ParseContext(JsonElement json, IReadOnlySet<string> levelKeys)
    {
        Json = json;
        _levelKeys = levelKeys;
    }

    public JsonElement Json { get; }

    public bool Has(string prop) => Json.ValueKind == JsonValueKind.Object && Json.TryGetProperty(prop, out _);

    private JsonElement? Prop(string prop) =>
        Json.ValueKind == JsonValueKind.Object && Json.TryGetProperty(prop, out var v) ? v : null;

    public ValueRef ReadValue(string prop, int? defaultValue = null)
    {
        var v = Prop(prop);
        if (v is null)
            return defaultValue is int d ? ValueRef.Of(d) : throw new ContentException($"missing '{prop}'");
        return ParseValue(v.Value, prop);
    }

    public ValueRef ParseValue(JsonElement v, string prop)
    {
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n)) return ValueRef.Of(n);
        if (v.ValueKind == JsonValueKind.String)
        {
            string s = v.GetString()!;
            if (s.Length > 2 && s[0] == '{' && s[^1] == '}')
            {
                string key = s[1..^1];
                if (!_levelKeys.Contains(key)) throw new ContentException($"unknown level value '{s}'");
                return new ValueRef(null, key);
            }
        }
        throw new ContentException($"'{prop}' must be a whole number or a level value like \"{{damage}}\"");
    }

    public string ReadString(string prop, string? defaultValue = null)
    {
        var v = Prop(prop);
        if (v is null) return defaultValue ?? throw new ContentException($"missing '{prop}'");
        if (v.Value.ValueKind != JsonValueKind.String) throw new ContentException($"'{prop}' must be a string");
        return v.Value.GetString()!;
    }

    public int ReadInt(string prop, int? defaultValue = null)
    {
        var v = Prop(prop);
        if (v is null) return defaultValue ?? throw new ContentException($"missing '{prop}'");
        if (v.Value.ValueKind != JsonValueKind.Number || !v.Value.TryGetInt32(out int n))
            throw new ContentException($"'{prop}' must be a whole number");
        return n;
    }

    public T ReadEnum<T>(string prop, T defaultValue) where T : struct, Enum => Has(prop) ? ReadEnum<T>(prop) : defaultValue;

    public T ReadEnum<T>(string prop) where T : struct, Enum
    {
        var v = Prop(prop) ?? throw new ContentException($"missing '{prop}'");
        string s = v.ValueKind == JsonValueKind.String ? v.GetString()! : v.ToString();
        if (!Enum.TryParse<T>(s, ignoreCase: false, out var result) || !Enum.IsDefined(result))
            throw new ContentException($"unknown {typeof(T).Name.ToLowerInvariant()} '{s}' in '{prop}'");
        return result;
    }

    /// <summary>A string, or a list of strings.</summary>
    public IReadOnlyList<string> ReadStrings(string prop)
    {
        var v = Prop(prop);
        if (v is null) return [];
        return v.Value.ValueKind switch
        {
            JsonValueKind.String => [v.Value.GetString()!],
            JsonValueKind.Array => v.Value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String
                ? e.GetString()! : throw new ContentException($"'{prop}' must contain strings")).ToList(),
            _ => throw new ContentException($"'{prop}' must be a string or a list of strings"),
        };
    }

    public TargetSpec ReadTarget(string prop, TargetSpec defaultValue)
    {
        var v = Prop(prop);
        if (v is null) return defaultValue;
        return TargetSpec.Parse(v.Value.ValueKind == JsonValueKind.String ? v.Value.GetString()! : v.Value.ToString());
    }
}
