using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using ItemFighter.Core.Engine.Effects;
using ItemFighter.Core.Engine.Triggers;
using ItemFighter.Core.Run;

namespace ItemFighter.Content;

public sealed record LoadedContent(ContentDatabase Database, ScriptRegistry Scripts, IReadOnlyList<ContentError> Errors, RunConfig Run);

/// <summary>Loads the game's content: JSON under Data/ plus the C# scripts in this assembly.</summary>
public static class GameContent
{
    public static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "Data");

    public static ScriptRegistry Scripts() => ScriptRegistry.FromAssemblies(typeof(GameContent).Assembly);

    /// <summary>The run loop's numbers. Not a content definition, so it is read separately.</summary>
    public const string RunConfigFile = "run.json";

    /// <summary>The fight's status and time-limit numbers. Also read separately.</summary>
    public const string RulesFile = "rules.json";

    /// <summary>Loads and validates everything. Errors from every step are returned together; nothing throws.</summary>
    public static LoadedContent Load(string? dataDirectory = null, IEnumerable<(string Path, string Json)>? extra = null)
    {
        var all = ContentLoader.ReadDirectory(dataDirectory ?? DataDirectory).Concat(extra ?? []).ToList();
        var files = all.Where(f => f.Path != RunConfigFile && f.Path != RulesFile);
        var loaded = ContentLoader.LoadFromStrings(files, TriggerRegistry.CreateDefault(), EffectRegistry.CreateDefault());
        var rulesErrors = new List<string>();
        var rulesFile = all.LastOrDefault(f => f.Path == RulesFile);
        if (rulesFile.Json is not null) loaded.Database.Rules = CombatRules.Parse(rulesFile.Json, out rulesErrors);
        var scripts = Scripts();
        var errors = loaded.Errors.Concat(ContentValidator.Validate(loaded.Database, scripts)).ToList();
        errors.AddRange(rulesErrors.Select(e => new ContentError(RulesFile, null, e)));
        var run = new RunConfig();
        var runFile = all.LastOrDefault(f => f.Path == RunConfigFile);
        if (runFile.Json is not null)
        {
            run = RunConfig.Parse(runFile.Json, loaded.Database, out var runErrors);
            errors.AddRange(runErrors.Select(e => new ContentError(RunConfigFile, null, e)));
        }
        return new LoadedContent(loaded.Database, scripts, errors, run);
    }
}
