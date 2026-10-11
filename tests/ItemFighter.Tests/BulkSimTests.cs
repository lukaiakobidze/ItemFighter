using ItemFighter.Core.Content;
using ItemFighter.Core.Determinism;
using ItemFighter.Core.Engine;

namespace ItemFighter.Tests;

public class BulkSimTests
{
    [Fact]
    public void RandomBoard_FillsAllSlots_WithDistinctItems()
    {
        var db = ContentIntegrityTests.WithDummy().Database;
        var setup = RandomBoard.Create(db, new Rng(4));
        Assert.Equal(Board.SlotCount, setup.Board.Count);
        Assert.Equal(Enumerable.Range(0, Board.SlotCount), setup.Board.Select(b => b.Slot).Order());
        Assert.Equal(Board.SlotCount, setup.Board.Select(b => b.ItemId).Distinct().Count());
        Assert.All(setup.Board, b => Assert.Equal(SourceKind.Item, db.Get(b.ItemId).Kind));
    }

    [Fact]
    public void RandomBoards_1000Fights_NoExceptions_AllEndByCap()
    {
        var content = ContentIntegrityTests.WithDummy();
        var enemies = content.Database.OfKind(SourceKind.Enemy).Where(e => e.Id != "test_dummy").ToList();
        for (ulong seed = 0; seed < 1000; seed++)
        {
            var enemy = enemies[(int)(seed % (ulong)enemies.Count)];
            var player = RandomBoard.Create(content.Database, new Rng(seed));
            var b = new Battle(new BattleSetup(player, enemy.Id, seed), content.Database, content.Scripts);
            Assert.NotEqual(BattleResult.Ongoing, AutoPilot.Run(b));
            Assert.InRange(b.Tick, 1, Battle.MaxTicks);
        }
    }

    [Fact]
    public void RandomBoards_SameSeed_SameResult()
    {
        var content = ContentIntegrityTests.WithDummy();
        for (ulong seed = 0; seed < 100; seed++)
        {
            (BattleResult, int, int) Run()
            {
                var b = new Battle(new BattleSetup(RandomBoard.Create(content.Database, new Rng(seed)), "grizzly_bear", seed), content.Database, content.Scripts);
                AutoPilot.Run(b);
                return (b.Result, b.Tick, b.Events.Count);
            }
            Assert.Equal(Run(), Run());
        }
    }
}
