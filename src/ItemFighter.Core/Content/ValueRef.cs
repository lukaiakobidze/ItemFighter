using ItemFighter.Core.Engine;


namespace ItemFighter.Core.Content;

/// <summary>A number in content: either a literal or "{key}", looked up in the source's level table.</summary>
public readonly record struct ValueRef(int? Literal, string? LevelKey, int Scale = 1)
{
    public static ValueRef Of(int value) => new(value, null);

    public int Resolve(SourceInstance source) =>
        Literal ?? source.Def.Levels[LevelKey!][Math.Clamp(source.Level, 1, 4) - 1] * Scale;

    public int ResolveAt(SourceDefinition def, int level) =>
        Literal ?? def.Levels[LevelKey!][Math.Clamp(level, 1, 4) - 1] * Scale;

    public override string ToString() => Literal?.ToString() ?? "{" + LevelKey + "}";
}
