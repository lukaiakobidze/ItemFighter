using ItemFighter.Core.Engine;

namespace ItemFighter.Tests;

/// <summary>Interactions between real starter items. Numbers are placeholder balance; update when content changes.</summary>
public class ComboTests
{
    private static Battle Fight(string enemy, string[] passives, params string[] items)
    {
        var content = ContentIntegrityTests.WithDummy();
        var board = items.Select((id, slot) => new BoardItem(id, slot)).ToList();
        return new Battle(new BattleSetup(new PlayerSetup(null, board, passives), enemy, 1), content.Database, content.Scripts);
    }

    private static int FireCount(Battle b, string defId) =>
        b.Events.OfType<SourceFired>().Count(f => !f.Reaction && b.FindSource(f.SourceId)!.Def.Id == defId);

    [Fact]
    public void FireFlask_NextToBellows_FiresFasterThanAlone()
    {
        var alone = Fight("test_dummy", [], "fire_flask");
        AutoPilot.Run(alone, untilTick: 100);
        var together = Fight("test_dummy", [], "fire_flask", "bellows");
        AutoPilot.Run(together, untilTick: 100);
        Assert.Equal(2, FireCount(alone, "fire_flask"));
        Assert.True(FireCount(together, "fire_flask") >= 3);
    }

    [Fact]
    public void Kindling_AddsOneBurnPerFireApplication()
    {
        var b = Fight("test_dummy", ["kindling"], "fire_flask");
        b.Steps(40);
        Assert.Equal(3 + 1, b.Events.OfType<StatusApplied>().Single().Amount);
    }

    [Fact]
    public void Whetstone_OnGreatAxe_Adds5Damage()
    {
        var b = Fight("test_dummy", [], "great_axe", "whetstone");
        var axe = b.Player.Items.Single(i => i.Def.Id == "great_axe");
        var stone = b.Player.Items.Single(i => i.Def.Id == "whetstone");
        b.Steps(stone.CooldownTicks);
        b.Enqueue(new ActivateCommand(b.Tick + 1, stone.Id, axe.Id));
        b.Steps(2 * axe.CooldownTicks - b.Tick);
        var hits = b.Events.OfType<DamageDealt>().Where(d => d.SourceId == axe.Id).Select(d => d.Total).ToList();
        Assert.Equal(new[] { 20, 25 }, hits);
    }

    [Fact]
    public void EmberKing_EntersPhase2()
    {
        var b = Fight("ember_king", [], "great_axe", "rusty_sword", "twin_daggers", "fire_flask", "venom_vial", "war_drum");
        AutoPilot.Run(b);
        Assert.Single(b.Events.OfType<PhaseStarted>());
        Assert.Contains(b.Enemy.Sources, s => s.Def.Id == "molten_rage");
    }

    [Fact]
    public void SpikedMail_VsThornyHide_ChainStopsAndFightEnds()
    {
        var b = Fight("bog_witch", [], "spiked_mail", "rusty_sword");
        Assert.NotEqual(BattleResult.Ongoing, AutoPilot.Run(b));
    }
}
