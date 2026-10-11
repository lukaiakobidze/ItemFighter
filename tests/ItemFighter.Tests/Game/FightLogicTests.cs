using ItemFighter.Core.Engine;
using ItemFighter.FightView;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests.Game;

public class FightLogicTests
{
    private static readonly string Sword = Item("sword", 2, """{ "type": "Damage", "amount": 5 }""", extra: "\"tags\": [\"Weapon\"],");
    private static readonly string Shield = Item("shieldy", 3, """{ "type": "Shield", "amount": 5 }""");
    private static readonly string Bomb = Item("bomb", 1, """{ "type": "Damage", "amount": 5 }""", trigger: "\"OnActivate\"", extra: "\"activation\": \"Active\",");
    private static readonly string Hone = Item("hone", 1, """{ "type": "ModifyStat", "stat": "Damage", "amount": 3 }""", trigger: "\"OnActivate\"",
        extra: "\"activation\": \"Active\", \"requiresTarget\": true, \"targetFilter\": \"OwnItemTag:Weapon\",");

    private static Battle NewFight(int enemyHp = 10_000)
    {
        var db = TestContent.Db(("items/sword.json", Sword), ("items/shieldy.json", Shield), ("items/bomb.json", Bomb), ("items/hone.json", Hone),
            ("enemies/dummy.json", Enemy(maxHp: enemyHp)));
        return Fight(db, "dummy", ("sword", 0), ("hone", 1), ("bomb", 2), ("shieldy", 3));
    }

    private static SourceInstance Slot(Battle b, int slot) => b.Player.Items.Single(i => i.Slot == slot);

    [Fact]
    public void Clock_StepsTenPerSecond_TimesSpeed()
    {
        var b = NewFight();
        var clock = new FightClock(b);
        clock.Advance(1.0);
        Assert.Equal(10, b.Tick);
        clock.Speed = 4;
        clock.Advance(0.5);
        Assert.Equal(30, b.Tick);
    }

    [Fact]
    public void Clock_KeepsFractions_AndCapsCatchUp()
    {
        var b = NewFight();
        var clock = new FightClock(b);
        for (int i = 0; i < 6; i++) clock.Advance(1.0 / 60);
        Assert.Equal(1, b.Tick);
        clock.Advance(30); // a long hitch does not fast-forward the whole fight
        Assert.Equal(1 + FightClock.MaxStepsPerAdvance, b.Tick);
    }

    [Fact]
    public void Clock_StopsWhenBattleEnds()
    {
        var b = NewFight(enemyHp: 5);
        var clock = new FightClock(b);
        clock.Speed = 4;
        for (int i = 0; i < 20; i++) clock.Advance(1);
        Assert.Equal(BattleResult.PlayerWon, b.Result);
        int end = b.Tick;
        clock.Advance(1);
        Assert.Equal(end, b.Tick);
    }

    private static Battle ReadyFight()
    {
        var b = NewFight();
        new FightClock(b).Advance(1.2); // actives (1s) are ready
        return b;
    }

    [Fact]
    public void Click_ReadyUntargetedActive_ReturnsCommandForNextTick()
    {
        var b = ReadyFight();
        var t = new TargetingState();
        var cmd = Assert.IsType<ActivateCommand>(t.Click(Slot(b, 2), b));
        Assert.Equal(new ActivateCommand(b.Tick + 1, Slot(b, 2).Id, null), cmd);
        Assert.Null(t.Selecting);
    }

    [Fact]
    public void Click_TargetedActive_ThenValidTarget_ReturnsCommandWithTarget()
    {
        var b = ReadyFight();
        var t = new TargetingState();
        Assert.Null(t.Click(Slot(b, 1), b));
        Assert.Same(Slot(b, 1), t.Selecting);
        Assert.True(t.IsValidTarget(Slot(b, 0)));
        Assert.False(t.IsValidTarget(Slot(b, 3)));
        var cmd = Assert.IsType<ActivateCommand>(t.Click(Slot(b, 0), b));
        Assert.Equal(Slot(b, 0).Id, cmd.TargetSourceId);
        Assert.Null(t.Selecting);
    }

    [Fact]
    public void Click_InvalidTarget_IsIgnored_AndKeepsSelecting()
    {
        var b = ReadyFight();
        var t = new TargetingState();
        t.Click(Slot(b, 1), b);
        Assert.Null(t.Click(Slot(b, 3), b));
        Assert.Same(Slot(b, 1), t.Selecting);
    }

    [Fact]
    public void Click_SelectedItemAgain_Cancels()
    {
        var b = ReadyFight();
        var t = new TargetingState();
        t.Click(Slot(b, 1), b);
        Assert.Null(t.Click(Slot(b, 1), b));
        Assert.Null(t.Selecting);
    }

    [Fact]
    public void Cancel_ReturnsToIdle()
    {
        var b = ReadyFight();
        var t = new TargetingState();
        t.Click(Slot(b, 1), b);
        t.Cancel();
        Assert.Null(t.Selecting);
    }

    [Fact]
    public void BattleEnd_CancelsTargeting()
    {
        var b = ReadyFight();
        var t = new TargetingState();
        t.Click(Slot(b, 1), b);
        b.RunToEnd();
        t.Update(b);
        Assert.Null(t.Selecting);
        Assert.Null(t.Click(Slot(b, 0), b));
    }

    [Fact]
    public void Selection_IsDropped_WhenTheActiveStopsBeingReady()
    {
        var b = ReadyFight();
        var t = new TargetingState();
        t.Click(Slot(b, 1), b);
        Slot(b, 1).FreezeTicks = 20;
        t.Update(b);
        Assert.Null(t.Selecting);
    }

    [Fact]
    public void Click_NotReady_NoCommand()
    {
        var b = NewFight();
        var t = new TargetingState();
        Assert.Null(t.Click(Slot(b, 2), b));
        Assert.Null(t.Click(Slot(b, 0), b)); // passive items can't be clicked
        Assert.Null(t.Selecting);
    }

    [Fact]
    public void FloatingTexts_OnePerDamageEvent_WithAnchors()
    {
        var b = NewFight();
        var texts = new FloatingTexts();
        var cursor = new EventCursor(b);
        new FightClock(b) { Speed = 4 }.Advance(1); // 40 ticks: sword x2, shield x1
        texts.Consume(cursor.TakeNew(), b);
        var damage = texts.Active.Where(t => t.Text.StartsWith('-')).ToList();
        Assert.Equal(b.Of<DamageDealt>().Count(), damage.Count);
        Assert.All(damage, d => Assert.Equal(FloatAnchor.ForSide(Side.Enemy), d.Anchor));
        Assert.Contains(texts.Active, t => t.Text == "+5" && t.Anchor == FloatAnchor.ForSide(Side.Player));
        Assert.Empty(cursor.TakeNew());
    }

    [Fact]
    public void FloatingTexts_Expire()
    {
        var b = NewFight();
        var texts = new FloatingTexts();
        new FightClock(b) { Speed = 4 }.Advance(1);
        texts.Consume(b.Events, b);
        texts.Update(FloatingTexts.Lifetime + 0.01);
        Assert.Empty(texts.Active);
    }
}
