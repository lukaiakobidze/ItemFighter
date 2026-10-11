using ItemFighter.Core.Engine;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests;

public class ActiveItemTests
{
    private static readonly string Sword = Item("sword", 2, """{ "type": "Damage", "amount": 10 }""", extra: "\"tags\": [\"Weapon\"],");
    private static readonly string Bomb = Item("bomb", 1, """{ "type": "Damage", "amount": 50 }""", extra: "\"activation\": \"Active\",");
    private static readonly string Whetstone = Item("whetstone", 1, """{ "type": "ModifyStat", "stat": "Damage", "amount": 5, "target": "Chosen" }""",
        extra: "\"activation\": \"Active\", \"requiresTarget\": true, \"targetFilter\": \"OwnItemTag:Weapon\",");

    private static Battle Setup(params (string Id, int Slot)[] board)
    {
        var db = TestContent.Db(("items/sword.json", Sword), ("items/bomb.json", Bomb), ("items/whetstone.json", Whetstone),
            ("items/charger.json", Item("charger", null, """{ "type": "Charge", "amount": 30, "target": "AdjacentItems" }""", trigger: """{ "type": "OnItemFired", "filter": "Adjacent" }""")),
            ("items/reset.json", Item("reset", 1, """{ "type": "ResetCooldown", "target": "Chosen" }""", extra: "\"activation\": \"Active\", \"requiresTarget\": true,")),
            ("items/copy.json", Item("copy", 1, """{ "type": "CopyEffect", "target": "Chosen" }""", extra: "\"activation\": \"Active\", \"requiresTarget\": true,")),
            ("enemies/dummy.json", Enemy(maxHp: 1000)));
        return Fight(db, "dummy", board);
    }

    private static SourceInstance At(Battle b, int slot) => b.Player.Items.Single(i => i.Slot == slot);

    [Fact]
    public void Active_BecomesReady_DoesNotAutoFire()
    {
        var b = Setup(("bomb", 0));
        b.Steps(30);
        Assert.True(At(b, 0).Ready);
        Assert.Equal(10, Assert.Single(b.Of<ItemReady>()).Tick);
        Assert.Empty(b.Of<SourceFired>());
        Assert.Equal(1000, b.Enemy.Hp);
    }

    [Fact]
    public void Activate_WhenReady_Fires_AndRestartsCooldown()
    {
        var b = Setup(("bomb", 0));
        b.Steps(12);
        b.Enqueue(new ActivateCommand(13, At(b, 0).Id, null));
        b.Steps(1);
        Assert.Equal(950, b.Enemy.Hp);
        Assert.False(At(b, 0).Ready);
        b.Steps(10);
        Assert.True(At(b, 0).Ready);
        Assert.Equal(23, b.Of<ItemReady>().Last().Tick);
    }

    [Fact]
    public void Activate_FutureCommand_WaitsForItsTick()
    {
        var b = Setup(("bomb", 0));
        b.Enqueue(new ActivateCommand(15, At(b, 0).Id, null));
        b.Steps(14);
        Assert.Equal(1000, b.Enemy.Hp);
        b.Steps(1);
        Assert.Equal(950, b.Enemy.Hp);
    }

    [Fact]
    public void Activate_Targeted_ModifyStat_BuffsTargetDamage()
    {
        var b = Setup(("sword", 0), ("whetstone", 1));
        b.Steps(10);
        b.Enqueue(new ActivateCommand(11, At(b, 1).Id, At(b, 0).Id));
        b.Steps(10);
        Assert.Equal(1000 - 15, b.Enemy.Hp);
        Assert.Equal(At(b, 0).Id, b.Of<SourceFired>().First().TargetSourceId);
    }

    [Fact]
    public void Charge_CompletesAtMostOncePerTick()
    {
        var b = Setup(("sword", 0), ("charger", 1));
        b.Steps(22);
        var swordFires = b.Of<SourceFired>().Where(f => !f.Reaction).Select(f => f.Tick).ToList();
        Assert.Equal(new[] { 20, 21, 22 }, swordFires);
    }

    [Fact]
    public void ResetCooldown_MakesActiveReadyAgain()
    {
        var b = Setup(("bomb", 0), ("reset", 1));
        b.Steps(10);
        b.Enqueue(new ActivateCommand(11, At(b, 0).Id, null));
        b.Enqueue(new ActivateCommand(11, At(b, 1).Id, At(b, 0).Id));
        b.Steps(1);
        Assert.True(At(b, 0).Ready);
        b.Enqueue(new ActivateCommand(12, At(b, 0).Id, null));
        b.Steps(1);
        Assert.Equal(900, b.Enemy.Hp);
    }

    [Fact]
    public void CopyEffect_RunsTargetsEffects()
    {
        var b = Setup(("sword", 0), ("copy", 1));
        b.Steps(10);
        b.Enqueue(new ActivateCommand(11, At(b, 1).Id, At(b, 0).Id));
        b.Steps(1);
        Assert.Equal(990, b.Enemy.Hp);
    }

    private static void AssertRejected(Battle b, ActivateCommand cmd, string reason)
    {
        var hp = b.Enemy.Hp;
        var state = b.Player.Items.Select(i => (i.Progress, i.Ready)).ToList();
        b.Enqueue(cmd);
        b.Steps(1);
        var rejected = Assert.Single(b.Of<CommandRejected>());
        Assert.Equal(reason, rejected.Reason);
        Assert.Equal(hp, b.Enemy.Hp);
        Assert.Equal(state.Select(s => s.Ready), b.Player.Items.Select(i => i.Ready));
    }

    [Fact]
    public void Reject_UnknownSource() => AssertRejected(Setup(("bomb", 0)), new ActivateCommand(1, 999, null), "unknown_source");

    [Fact]
    public void Reject_EnemySource()
    {
        var b = Setup(("bomb", 0));
        AssertRejected(b, new ActivateCommand(1, b.Enemy.Sources[0].Id, null), "unknown_source");
    }

    [Fact]
    public void Reject_NotActive()
    {
        var b = Setup(("sword", 0));
        AssertRejected(b, new ActivateCommand(1, At(b, 0).Id, null), "not_active");
    }

    [Fact]
    public void Reject_NotReady()
    {
        var b = Setup(("bomb", 0));
        AssertRejected(b, new ActivateCommand(1, At(b, 0).Id, null), "not_ready");
    }

    [Fact]
    public void Reject_Frozen()
    {
        var b = Setup(("bomb", 0));
        b.Steps(10);
        b.ApplyItemStatus(At(b, 0), StatusType.Freeze, 20, null, 0);
        AssertRejected(b, new ActivateCommand(11, At(b, 0).Id, null), "frozen");
    }

    [Fact]
    public void Reject_TargetRequired()
    {
        var b = Setup(("sword", 0), ("whetstone", 1));
        b.Steps(10);
        AssertRejected(b, new ActivateCommand(11, At(b, 1).Id, null), "target_required");
    }

    [Fact]
    public void Reject_InvalidTarget_WrongTag()
    {
        var b = Setup(("bomb", 0), ("whetstone", 1));
        b.Steps(10);
        AssertRejected(b, new ActivateCommand(11, At(b, 1).Id, At(b, 0).Id), "invalid_target");
    }

    [Fact]
    public void Reject_InvalidTarget_EnemyOrSelf()
    {
        var b = Setup(("sword", 0), ("reset", 1));
        b.Steps(10);
        AssertRejected(b, new ActivateCommand(11, At(b, 1).Id, b.Enemy.Sources[0].Id), "invalid_target");
        var c = Setup(("sword", 0), ("reset", 1));
        c.Steps(10);
        AssertRejected(c, new ActivateCommand(11, At(c, 1).Id, At(c, 1).Id), "invalid_target");
    }

    [Fact]
    public void Reject_Late()
    {
        var b = Setup(("bomb", 0));
        b.Steps(12);
        AssertRejected(b, new ActivateCommand(5, At(b, 0).Id, null), "late");
    }
}
