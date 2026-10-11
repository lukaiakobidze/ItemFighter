using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using ItemFighter.Core.Engine.Effects;
using ItemFighter.Core.Engine.Triggers;

namespace ItemFighter.Tests;

/// <summary>Builds content databases from inline JSON for tests.</summary>
public static class TestContent
{
    public static ContentLoadResult Load(params (string Path, string Json)[] files) =>
        Load(TriggerRegistry.CreateDefault(), EffectRegistry.CreateDefault(), files);

    public static ContentLoadResult Load(TriggerRegistry triggers, EffectRegistry effects, params (string Path, string Json)[] files) =>
        ContentLoader.LoadFromStrings(files, triggers, effects);

    public static ContentDatabase Db(params (string Path, string Json)[] files)
    {
        var result = Load(files);
        Assert.True(result.Errors.Count == 0, string.Join("\n", result.Errors));
        return result.Database;
    }
}
