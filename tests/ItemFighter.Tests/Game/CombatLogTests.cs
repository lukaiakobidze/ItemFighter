using ItemFighter.Core.Engine;
using ItemFighter.FightView;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests.Game;

public class CombatLogTests
{
    [Fact]
    public void Lines_NameTheSourceAndTheAmount()
    {
        var sword = Item("sword", 1, """{ "type": "Damage", "amount": 7 }""", extra: "\"name\": \"Rusty Sword\",");
        var flask = Item("flask", 1, """{ "type": "ApplyStatus", "status": "Burn", "amount": 2 }""", extra: "\"name\": \"Fire Flask\",");
        var db = TestContent.Db(("items/sword.json", sword), ("items/flask.json", flask),
            ("enemies/dummy.json", """{ "id": "dummy", "name": "Training Dummy", "maxHp": 1000 }"""));
        var b = Fight(db, "dummy", ("sword", 0), ("flask", 1));
        b.Steps(15);
        var log = new CombatLog();
        log.Consume(b.Events, b);
        var texts = log.Entries.Select(e => e.Text).ToList();
        Assert.Contains("Rusty Sword hits Training Dummy for 7", texts);
        Assert.Contains("Fire Flask: 2 Burn on Training Dummy", texts);
        Assert.Contains(texts, t => t.StartsWith("Burn deals", StringComparison.Ordinal));
    }

    [Fact]
    public void KeepsOnlyTheNewestEntries()
    {
        var log = new CombatLog();
        var b = Fight(TestContent.Db(("enemies/dummy.json", Enemy())), "dummy");
        for (int i = 0; i < CombatLog.Capacity + 50; i++) log.Consume([new PhaseStarted(i, 1)], b);
        Assert.Equal(CombatLog.Capacity, log.Entries.Count);
    }
}
