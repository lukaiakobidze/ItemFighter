using ItemFighter.Content;
using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using ItemFighter.Core.Run;

namespace ItemFighter.Tests.Run;

public class RunStateTests
{
    internal static readonly LoadedContent Content = GameContent.Load();

    internal static RunState NewRun(ulong seed = 7, RunConfig? config = null) =>
        RunState.New(seed, "silver_knight", config ?? Content.Run, Content.Database);

    private static Encounter FirstFight(RunState run) => new(EncounterKind.Fight, "squire_zombie");

    /// <summary>Wins the current fight without simulating it.</summary>
    internal static void WinFight(RunState run, Encounter e)
    {
        run.Choose(e);
        run.StartFight();
        run.FinishFight(won: true);
    }

    [Fact]
    public void New_StartsWithStartingItemsAndGold()
    {
        var run = NewRun();
        Assert.Equal(10, run.Gold);
        Assert.Equal(new OwnedItem("sword", 1), run.Hotbar[0]);
        Assert.Equal(new OwnedItem("metal_plating", 1), run.Hotbar[1]);
        Assert.All(run.Hotbar.Skip(2), Assert.Null);
        Assert.Empty(run.Backpack);
        Assert.Equal(6, run.Hotbar.Length);
        Assert.Equal(RunPhase.ChoosingEncounter, run.Phase);
        Assert.Equal(1, run.Mission);
        Assert.Equal(0, run.Encounter);
    }

    [Fact]
    public void EncounterChoices_SameRunSameChoices()
    {
        Assert.Equal(NewRun(3).EncounterChoices(), NewRun(3).EncounterChoices());
        Assert.Equal(3, NewRun(3).EncounterChoices().Count);
    }

    [Fact]
    public void Choose_Shop_ThenBuy_SpendsGold_AddsItem()
    {
        var run = NewRun();
        run.Choose(new Encounter(EncounterKind.Shop, null));
        Assert.Equal(RunPhase.Shopping, run.Phase);
        Assert.Equal(5, run.ShopStock!.Count);
        string id = run.ShopStock[0];
        int price = run.Price(id);
        Assert.True(run.Buy(0));
        Assert.Equal(10 - price, run.Gold);
        Assert.Contains(run.AllOwned, o => o.ItemId == id);
        Assert.Equal(4, run.ShopStock.Count);
        run.LeaveShop();
        Assert.Equal(RunPhase.ChoosingEncounter, run.Phase);
        Assert.Equal(1, run.Encounter);
    }

    [Fact]
    public void Buy_NotEnoughGold_Refused()
    {
        var run = NewRun();
        run.Choose(new Encounter(EncounterKind.Shop, null));
        run.Gold = 0;
        Assert.False(run.Buy(0));
        Assert.Equal(5, run.ShopStock!.Count);
        Assert.Equal(2, run.AllOwned.Count);
    }

    [Fact]
    public void Reroll_CostRises_StockChanges()
    {
        var run = NewRun();
        run.Choose(new Encounter(EncounterKind.Shop, null));
        var first = run.ShopStock!.ToList();
        Assert.Equal(1, run.RerollCost);
        Assert.True(run.Reroll());
        Assert.Equal(9, run.Gold);
        Assert.Equal(2, run.RerollCost);
        Assert.True(run.Reroll());
        Assert.Equal(7, run.Gold);
        Assert.NotEqual(first, run.ShopStock);
        run.Gold = 1;
        Assert.False(run.Reroll());
    }

    [Fact]
    public void Sell_GivesHalfPrice_TimesLevel()
    {
        var run = NewRun();
        run.Hotbar[0] = new OwnedItem("sword", 3); // Common: price 4 × level 3 = 12, half = 6
        Assert.Equal(6, run.SellValue(run.Hotbar[0]!));
        run.Sell(new ItemRef(true, 0));
        Assert.Equal(16, run.Gold);
        Assert.Null(run.Hotbar[0]);
    }

    [Fact]
    public void AddItem_Duplicate_MergesToNextLevel()
    {
        var run = NewRun();
        Assert.True(run.AddItem("sword"));
        Assert.Equal(new OwnedItem("sword", 2), run.Hotbar[0]);
        Assert.Equal(2, run.AllOwned.Count);
        Assert.True(run.AddItem("axe"));
        Assert.Equal(new OwnedItem("axe", 1), run.Hotbar[2]); // new items fill empty hotbar slots first
    }

    [Fact]
    public void Inventory_Full_RefusesNewItem_ButMergeStillWorks()
    {
        var run = NewRun();
        for (int i = 2; i < 6; i++) run.Hotbar[i] = new OwnedItem("dagger", 4);
        while (run.Backpack.Count < 12) run.Backpack.Add(new OwnedItem("dagger", 4));
        Assert.False(run.HasRoom);
        int before = run.AllOwned.Count;
        Assert.False(run.AddItem("apple"));
        Assert.False(run.AddItem("no_such_item"));
        Assert.Equal(before, run.AllOwned.Count);
        Assert.True(run.AddItem("sword")); // merging needs no room
        Assert.Equal(before, run.AllOwned.Count);
        Assert.Equal(2, run.Hotbar[0]!.Level);
    }

    [Fact]
    public void Drop_WhenFull_IsRefused_UntilYouSell()
    {
        var run = NewRun();
        WinFight(run, FirstFight(run));
        Assert.Equal(RunPhase.Dropping, run.Phase);
        var drop = run.PendingDrop!;
        int index = drop.Choices.ToList().FindIndex(id => Content.Database.Get(id).Kind == SourceKind.Item && run.AllOwned.All(o => o.ItemId != id));
        Assert.True(index >= 0, "the zombie's drop should offer a new item");
        string filler = Content.Database.OfKind(SourceKind.Item).First(d => !drop.Choices.Contains(d.Id) && run.AllOwned.All(o => o.ItemId != d.Id)).Id;
        for (int i = 0; i < 4; i++) run.Hotbar[2 + i] = new OwnedItem(filler, 4);
        while (run.Backpack.Count < 12) run.Backpack.Add(new OwnedItem(filler, 4));
        Assert.False(run.TakeDrop(index));
        Assert.Equal(RunPhase.Dropping, run.Phase);
        run.Sell(new ItemRef(false, 0));
        Assert.True(run.TakeDrop(index));
        Assert.Equal(RunPhase.ChoosingEncounter, run.Phase);
    }

    [Fact]
    public void Move_SwapsHotbarAndBackpack()
    {
        var run = NewRun();
        run.Backpack.Add(new OwnedItem("axe", 1));
        Assert.True(run.Move(new ItemRef(false, 0), new ItemRef(true, 0)));
        Assert.Equal("axe", run.Hotbar[0]!.ItemId);
        Assert.Equal("sword", run.Backpack[0].ItemId);
        Assert.True(run.Move(new ItemRef(true, 1), new ItemRef(true, 4)));
        Assert.Null(run.Hotbar[1]);
        Assert.Equal("metal_plating", run.Hotbar[4]!.ItemId);
        Assert.True(run.Move(new ItemRef(true, 4), new ItemRef(false, run.Backpack.Count)));
        Assert.Equal(2, run.Backpack.Count);
        Assert.Null(run.Hotbar[4]);
        Assert.False(run.Move(new ItemRef(true, 2), new ItemRef(false, 0))); // moving an empty slot does nothing
    }

    [Fact]
    public void Move_ToFullBackpack_Refused()
    {
        var run = NewRun(config: new RunConfig { BackpackSize = 0 });
        Assert.False(run.Move(new ItemRef(true, 0), new ItemRef(false, 0)));
        Assert.Equal("sword", run.Hotbar[0]!.ItemId);
    }

    [Fact]
    public void Prep_CanFight_RequiresAnItem()
    {
        var run = NewRun();
        run.Choose(FirstFight(run));
        Assert.Equal(RunPhase.Preparing, run.Phase);
        Assert.True(run.CanFight);
        run.Move(new ItemRef(true, 0), new ItemRef(false, 0));
        run.Move(new ItemRef(true, 1), new ItemRef(false, 1));
        Assert.False(run.CanFight); // everything is in the backpack
        Assert.Throws<InvalidOperationException>(() => run.StartFight());
    }

    [Fact]
    public void FinishFight_Won_GivesGold_AndDrop()
    {
        var run = NewRun();
        WinFight(run, FirstFight(run));
        Assert.Equal(15, run.Gold); // 10 + 5 for a mission-1 fight
        Assert.Equal(1, run.FightsWon);
        Assert.Equal(RunPhase.Dropping, run.Phase);
        Assert.Equal(3, run.PendingDrop!.Choices.Count);
        run.SkipDrop();
        Assert.Equal(RunPhase.ChoosingEncounter, run.Phase);
        Assert.Equal(1, run.Encounter);
    }

    [Fact]
    public void FinishFight_Elite_DoubleGold()
    {
        var run = NewRun();
        WinFight(run, new Encounter(EncounterKind.Elite, "squire_zombie"));
        Assert.Equal(20, run.Gold);
    }

    [Fact]
    public void FinishFight_Lost_EndsRun()
    {
        var run = NewRun();
        run.Choose(FirstFight(run));
        run.StartFight();
        run.FinishFight(won: false);
        Assert.Equal(RunPhase.Lost, run.Phase);
    }

    [Fact]
    public void Boss_AfterTenEncounters_ThenNextMission()
    {
        var run = NewRun();
        for (int i = 0; i < 10; i++)
        {
            run.Choose(new Encounter(EncounterKind.Shop, null));
            run.LeaveShop();
        }
        var boss = Assert.Single(run.EncounterChoices());
        Assert.Equal(new Encounter(EncounterKind.Boss, "flaming_rock"), boss);
        WinFight(run, boss);
        Assert.Equal(10 + 15, run.Gold);
        run.SkipDrop();
        Assert.Equal(2, run.Mission);
        Assert.Equal(0, run.Encounter);
        Assert.Equal(RunPhase.ChoosingEncounter, run.Phase);
    }

    [Fact]
    public void LastBoss_Won_RunWon()
    {
        var run = NewRun(config: new RunConfig { Missions = 1, EncountersPerMission = 0, Bosses = [new BossConfig("grizzly_bear", 200)] });
        var boss = Assert.Single(run.EncounterChoices());
        WinFight(run, boss);
        run.SkipDrop();
        Assert.Equal(RunPhase.Won, run.Phase);
    }

    [Fact]
    public void StartFight_HpScalesByMission_Elite_Boss()
    {
        var run = NewRun();
        run.Mission = 3;
        run.Choose(new Encounter(EncounterKind.Fight, "squire_zombie"));
        Assert.Equal(150, run.StartFight().EnemyHpPercent); // +25% per mission after the first
        run.FinishFight(false);

        run = NewRun();
        run.Mission = 3;
        run.Choose(new Encounter(EncounterKind.Elite, "squire_zombie"));
        Assert.Equal(225, run.StartFight().EnemyHpPercent); // × 150%

        run = NewRun();
        run.Mission = 2;
        run.Encounter = 10;
        run.Choose(run.EncounterChoices()[0]);
        var setup = run.StartFight();
        Assert.Equal("green_snake", setup.EnemyId);
        Assert.Equal(200, setup.EnemyHpPercent); // bosses use their own hpPercent
    }

    [Fact]
    public void StartFight_BuildsBoardFromHotbar_AndRuns()
    {
        var run = NewRun();
        run.Passives.Add("venomous");
        run.Move(new ItemRef(true, 1), new ItemRef(true, 5));
        run.Choose(FirstFight(run));
        var setup = run.StartFight();
        Assert.Equal(new[] { new BoardItem("sword", 0, 1), new BoardItem("metal_plating", 5, 1) }, setup.Player.Board);
        Assert.Equal(new[] { "venomous" }, setup.Player.PassiveIds);
        var battle = new Battle(setup, Content.Database, Content.Scripts);
        while (battle.Result == BattleResult.Ongoing) battle.Step();
    }

    [Fact]
    public void Hotbar_IsRememberedBetweenFights()
    {
        var run = NewRun();
        run.Move(new ItemRef(true, 0), new ItemRef(true, 3));
        var before = run.Hotbar.ToArray();
        WinFight(run, FirstFight(run));
        run.SkipDrop();
        run.Choose(FirstFight(run));
        Assert.Equal(before, run.Hotbar);
    }
}

public class FullRunTests
{
    /// <summary>Walks whole runs with random choices and forced wins: every phase is reachable and the run ends Won.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void WholeRun_WithForcedWins_EndsWon(ulong seed)
    {
        var run = RunStateTests.NewRun(seed);
        var pick = new Core.Determinism.Rng(seed);
        int steps = 0;
        while (!run.IsOver && steps++ < 1000)
        {
            switch (run.Phase)
            {
                case RunPhase.ChoosingEncounter:
                    var choices = run.EncounterChoices();
                    run.Choose(choices[pick.NextInt(choices.Count)]);
                    break;
                case RunPhase.Preparing:
                    if (!run.CanFight) run.Move(new ItemRef(false, 0), new ItemRef(true, 0));
                    run.StartFight();
                    run.FinishFight(won: true);
                    break;
                case RunPhase.Dropping:
                    if (!run.TakeDrop(pick.NextInt(3))) run.SkipDrop();
                    break;
                case RunPhase.Shopping:
                    while (run.Buy(0)) { }
                    if (run.Gold > 20) run.Reroll();
                    run.LeaveShop();
                    break;
            }
            Assert.True(run.AllOwned.Count <= run.Config.HotbarSize + run.Config.BackpackSize);
            Assert.True(run.Gold >= 0);
        }
        Assert.Equal(RunPhase.Won, run.Phase);
        Assert.Equal(5, run.Mission);
        Assert.True(run.FightsWon >= 5);
    }
}
