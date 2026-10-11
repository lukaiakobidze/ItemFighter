using ItemFighter.Content;
using ItemFighter.Screens;

namespace ItemFighter.Tests.Game;

public class LabSetupTests
{
    private static readonly Core.Content.ContentDatabase Db = GameContent.Load().Database;

    private static string TempFile() => Path.Combine(Path.GetTempPath(), $"lab-{Guid.NewGuid():N}.json");

    [Fact]
    public void RoundTrips()
    {
        string path = TempFile();
        var setup = LabSetup.Default(Db);
        setup.Items[5] = "axe";
        setup.Levels[5] = 3;
        setup.EnemyId = "grizzly_bear";
        setup.Seed = 42;
        setup.Save(path);
        var loaded = LabSetup.Load(path, Db);
        Assert.Equal(setup.Items, loaded.Items);
        Assert.Equal(setup.Levels, loaded.Levels);
        Assert.Equal("grizzly_bear", loaded.EnemyId);
        Assert.Equal(42ul, loaded.Seed);
        File.Delete(path);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "Items": ["no_such_item", null, null, null, null, null], "Levels": [9, 1, 1, 1, 1, 1], "EnemyId": "nobody", "Passives": ["nope"] }""")]
    [InlineData("""{ "Items": ["rusty_sword"], "Levels": [] }""")]
    public void BadFile_FallsBackToSomethingPlayable(string json)
    {
        string path = TempFile();
        File.WriteAllText(path, json);
        var loaded = LabSetup.Load(path, Db);
        Assert.Equal(6, loaded.Items.Length);
        Assert.Equal(6, loaded.Levels.Length);
        Assert.All(loaded.Items, id => Assert.True(id is null || Db.TryGet(id, out _)));
        Assert.All(loaded.Levels, l => Assert.InRange(l, 1, 4));
        Assert.True(Db.TryGet(loaded.EnemyId, out _));
        Assert.All(loaded.Passives, id => Assert.True(Db.TryGet(id, out _)));
        Assert.NotNull(loaded.ToBattleSetup());
        File.Delete(path);
    }

    [Fact]
    public void MissingFile_GivesDefault() =>
        Assert.Equal(LabSetup.Default(Db).Items, LabSetup.Load(TempFile(), Db).Items);
}
