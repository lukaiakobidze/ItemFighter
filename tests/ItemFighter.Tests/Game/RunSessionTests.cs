using ItemFighter.Core.Run;
using ItemFighter.Screens.Run;
using ItemFighter.Tests.Run;

namespace ItemFighter.Tests.Game;

public class RunSessionTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(), "itemfighter-session-" + Guid.NewGuid().ToString("N"), "run.json");

    [Fact]
    public void Commit_SavesWhileRunning_DeletesWhenOver()
    {
        string path = TempPath();
        try
        {
            var session = new RunSession(RunStateTests.NewRun(), path);
            session.Commit();
            Assert.True(File.Exists(path));
            session.Run.Choose(new Encounter(EncounterKind.Fight, "squire_zombie"));
            session.Run.StartFight();
            session.Run.FinishFight(won: false);
            session.Commit();
            Assert.False(File.Exists(path));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Theory]
    [InlineData(RunPhase.ChoosingEncounter, RunScreenKind.Map)]
    [InlineData(RunPhase.Preparing, RunScreenKind.Prep)]
    [InlineData(RunPhase.Fighting, RunScreenKind.Prep)]
    [InlineData(RunPhase.Shopping, RunScreenKind.Shop)]
    [InlineData(RunPhase.Dropping, RunScreenKind.Drop)]
    [InlineData(RunPhase.Won, RunScreenKind.End)]
    [InlineData(RunPhase.Lost, RunScreenKind.End)]
    public void EachPhase_HasAScreen(RunPhase phase, RunScreenKind kind) => Assert.Equal(kind, RunSession.ScreenFor(phase));

    [Fact]
    public void FinishFight_ThroughSession_SavesTheDrop()
    {
        string path = TempPath();
        try
        {
            var session = new RunSession(RunStateTests.NewRun(), path);
            session.Run.Choose(new Encounter(EncounterKind.Fight, "squire_zombie"));
            var setup = session.BeginFight();
            Assert.Equal(RunPhase.Fighting, session.Run.Phase);
            session.EndFight(Core.Engine.BattleResult.PlayerWon);
            var loaded = RunSave.Load(path, RunStateTests.Content.Run, RunStateTests.Content.Database)!;
            Assert.Equal(RunPhase.Dropping, loaded.Phase);
            Assert.Equal("squire_zombie", setup.EnemyId);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }
}
