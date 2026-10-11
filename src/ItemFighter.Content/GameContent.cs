using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using ItemFighter.Core.Engine.Effects;
using ItemFighter.Core.Engine.Triggers;

namespace ItemFighter.Content;

public sealed record LoadedContent(ContentDatabase Database, ScriptRegistry Scripts, IReadOnlyList<ContentError> Errors);

/// <summary>Loads the game's content: JSON under Data/ plus the C# scripts in this assembly.</summary>
public static class GameContent
{
    public static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "Data");

    public static ScriptRegistry Scripts() => ScriptRegistry.FromAssemblies(typeof(GameContent).Assembly);

    /// <summary>Loads and validates everything. Errors from both steps are returned together; nothing throws.</summary>
    public static LoadedContent Load(string? dataDirectory = null, IEnumerable<(string Path, string Json)>? extra = null)
    {
        var files = ContentLoader.ReadDirectory(dataDirectory ?? DataDirectory).Concat(extra ?? []);
        var loaded = ContentLoader.LoadFromStrings(files, TriggerRegistry.CreateDefault(), EffectRegistry.CreateDefault());
        var scripts = Scripts();
        var errors = loaded.Errors.Concat(ContentValidator.Validate(loaded.Database, scripts)).ToList();
        return new LoadedContent(loaded.Database, scripts, errors);
    }
}
