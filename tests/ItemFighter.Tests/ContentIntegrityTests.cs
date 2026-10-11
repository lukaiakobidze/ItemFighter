using ItemFighter.Content;
using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;

namespace ItemFighter.Tests;

/// <summary>Checks the real game content in src/ItemFighter.Content/Data.</summary>
public class ContentIntegrityTests
{
    public const string Dummy = """{ "id": "test_dummy", "kind": "Enemy", "maxHp": 10000 }""";

    public static LoadedContent WithDummy() => GameContent.Load(extra: [("enemies/test_dummy.json", Dummy)]);

    [Fact]
    public void AllContent_LoadsAndValidates_WithZeroErrors()
    {
        var content = GameContent.Load();
        Assert.True(content.Errors.Count == 0, string.Join("\n", content.Errors));
    }

    [Fact]
    public void Counts()
    {
        var db = GameContent.Load().Database;
        Assert.True(db.OfKind(SourceKind.Item).Count() >= 12);
        Assert.True(db.OfKind(SourceKind.Enemy).Count() >= 5);
        Assert.True(db.OfKind(SourceKind.Character).Any());
    }

    public static IEnumerable<object[]> ItemIds() =>
        GameContent.Load().Database.OfKind(SourceKind.Item).Where(d => d.HasCooldown()).Select(d => new object[] { d.Id });

    [Theory]
    [MemberData(nameof(ItemIds))]
    public void EveryItemSoloVsDummy_FiresWithin30s(string itemId)
    {
        var content = WithDummy();
        // Targeted actives need something to aim at.
        var board = new List<BoardItem> { new(itemId, 0) };
        if (content.Database.Get(itemId).RequiresTarget) board.Add(new BoardItem("sword", 1));
        var b = new Battle(new BattleSetup(new PlayerSetup(null, board, []), "test_dummy", 1), content.Database, content.Scripts);
        AutoPilot.Run(b, untilTick: Battle.FatigueStartTick - 1);
        Assert.Contains(b.Events, e => e is SourceFired f && f.SourceId == b.Player.Items.Single(i => i.Slot == 0).Id);
    }

    [Fact]
    public void EveryEnemy_CanFightAndFinish()
    {
        var content = GameContent.Load();
        foreach (var enemy in content.Database.OfKind(SourceKind.Enemy))
        {
            var b = new Battle(new BattleSetup(new PlayerSetup("silver_knight", [new BoardItem("sword", 0)], []), enemy.Id, 1), content.Database, content.Scripts);
            Assert.NotEqual(BattleResult.Ongoing, AutoPilot.Run(b));
        }
    }
}

internal static class DefinitionExtensions
{
    public static bool HasCooldown(this SourceDefinition d) => d.CooldownTicks is not null;
}
