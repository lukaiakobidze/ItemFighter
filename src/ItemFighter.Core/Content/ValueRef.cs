using ItemFighter.Core.Engine;

namespace ItemFighter.Core.Content;

/// <summary>A number in content: either a literal or "{key}", looked up in the source's level table.</summary>
public readonly record struct ValueRef(int? Literal, string? LevelKey)
{
    public static ValueRef Of(int value) => new(value, null);

    public int Resolve(SourceInstance source) =>
        Literal ?? source.Def.Levels[LevelKey!][Math.Clamp(source.Level, 1, 4) - 1];

    public override string ToString() => Literal?.ToString() ?? "{" + LevelKey + "}";
}
