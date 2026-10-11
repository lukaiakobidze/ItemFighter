using ItemFighter.Core.Run;

namespace ItemFighter.Tests.Run;

public class RunSaveTests
{
    private static RunState Load(string json) =>
        RunSave.FromJson(json, RunStateTests.Content.Run, RunStateTests.Content.Database)!;

    [Fact]
    public void Save_RoundTrips()
    {
        var run = RunStateTests.NewRun();
        run.Passives.Add("venomous");
        run.Backpack.Add(new OwnedItem("axe", 2));
        RunStateTests.WinFight(run, new Encounter(EncounterKind.Fight, "green_snake"));
        var loaded = Load(RunSave.ToJson(run));
        Assert.Equal(RunSave.ToJson(run), RunSave.ToJson(loaded));
        Assert.Equal(run.PendingDrop!.Choices, loaded.PendingDrop!.Choices);
        Assert.Equal(RunPhase.Dropping, loaded.Phase);
        Assert.True(loaded.TakeDrop(0) || !loaded.HasRoom);
    }

    [Fact]
    public void Save_Corrupt_ReturnsNull()
    {
        Assert.Null(RunSave.FromJson("{ not json", RunStateTests.Content.Run, RunStateTests.Content.Database));
        Assert.Null(RunSave.FromJson("", RunStateTests.Content.Run, RunStateTests.Content.Database));
        Assert.Null(RunSave.FromJson("""{ "version": 999 }""", RunStateTests.Content.Run, RunStateTests.Content.Database));
    }

    [Fact]
    public void Save_UnknownItemIds_AreDropped()
    {
        var run = RunStateTests.NewRun();
        run.Backpack.Add(new OwnedItem("axe", 9));
        run.Passives.Add("gone");
        string json = RunSave.ToJson(run).Replace("\"metal_plating\"", "\"deleted_item\"");
        var loaded = Load(json);
        Assert.Null(loaded.Hotbar[1]);
        Assert.Equal(4, loaded.Backpack.Single().Level);
        Assert.Empty(loaded.Passives);
    }

    [Fact]
    public void Save_UnknownEnemy_GoesBackToChoosing()
    {
        var run = RunStateTests.NewRun();
        run.Choose(new Encounter(EncounterKind.Fight, "squire_zombie"));
        var loaded = Load(RunSave.ToJson(run).Replace("\"squire_zombie\"", "\"deleted_enemy\""));
        Assert.Equal(RunPhase.ChoosingEncounter, loaded.Phase);
        Assert.Null(loaded.Current);
    }

    [Fact]
    public void Save_MidFight_ResumesSameEncounter()
    {
        var run = RunStateTests.NewRun();
        run.Choose(new Encounter(EncounterKind.Fight, "squire_zombie"));
        var setup = run.StartFight();
        var loaded = Load(RunSave.ToJson(run));
        Assert.Equal(RunPhase.Preparing, loaded.Phase);
        Assert.Equal(run.Current, loaded.Current);
        Assert.Equal(setup, loaded.StartFight(), new SetupComparer());
    }

    [Fact]
    public void SaveFile_WriteLoadDelete()
    {
        string dir = Path.Combine(Path.GetTempPath(), "itemfighter-test-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "run.json");
        try
        {
            Assert.Null(RunSave.Load(path, RunStateTests.Content.Run, RunStateTests.Content.Database));
            RunSave.Save(path, RunStateTests.NewRun());
            Assert.NotNull(RunSave.Load(path, RunStateTests.Content.Run, RunStateTests.Content.Database));
            RunSave.Delete(path);
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    private sealed class SetupComparer : IEqualityComparer<Core.Engine.BattleSetup>
    {
        public bool Equals(Core.Engine.BattleSetup? a, Core.Engine.BattleSetup? b) =>
            a!.EnemyId == b!.EnemyId && a.Seed == b.Seed && a.EnemyHpPercent == b.EnemyHpPercent
            && a.Player.Board.SequenceEqual(b.Player.Board) && a.Player.PassiveIds.SequenceEqual(b.Player.PassiveIds);

        public int GetHashCode(Core.Engine.BattleSetup s) => 0;
    }
}
