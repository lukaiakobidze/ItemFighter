using ItemFighter.Core.Run;
using ItemFighter.Screens;
using ItemFighter.Screens.Run;
using ItemFighter.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Step = ItemFighter.Platform.Automation.Step;

namespace ItemFighter.Platform;

/// <summary>Named scripted sessions for headless screenshot checks (run under xvfb-run). They use their own save folder.</summary>
public static class AutomationScripts
{
    public static IReadOnlyList<Step> Get(string name) => name switch
    {
        "menu" => [new(Wait: 0.3, Shot: "menu")],
        "lab" => [.. ToLab(), new(Wait: 0.2, Do: Automation.Hover(FightLabScreen.GridCell(3)), Shot: "lab")],
        "fight" => Fight(),
        "bear" => Bear(),
        "blur" => Blur(),
        "nails" => Nails(),
        "boxer" => Boxer(),
        "run" => RunLoop(),
        _ => throw new ArgumentException($"unknown automation script '{name}'"),
    };

    private static FightScreen FightOf(Game1 g) => (FightScreen)g.Current!;

    private static Step[] ToLab(params string?[] board) =>
    [
        new(Wait: 0.2, Do: Automation.Click(MainMenuScreen.FightLabButton)),
        new(Wait: 0.2, Until: g => g.Current is FightLabScreen, Do: g =>
        {
            var setup = ((FightLabScreen)g.Current!).Setup;
            setup.Seed = 3;
            for (int i = 0; i < board.Length; i++) setup.Items[i] = board[i];
        }),
    ];

    /// <summary>The default board: Weights is clicked as soon as it is ready, then the fight runs to the end.</summary>
    private static Step[] Fight() =>
    [
        .. ToLab(),
        new(Wait: 0.1, Do: g => ((FightLabScreen)g.Current!).StartFight()),
        new(Wait: 0.5, Shot: "fight-start"),
        new(Until: g => g.Current is FightScreen f && f.Targeting.CanActivate(f.Battle.Player.Items.Single(i => i.Slot == 3), f.Battle),
            Do: Automation.Hover(FightScreen.CardRect(3)), Shot: "fight-ready"),
        new(Wait: 0.1, Do: Automation.Click(FightScreen.CardRect(3))),
        new(Wait: 0.4, Do: Automation.Hover(FightScreen.CardRect(0)), Shot: "fight-mid"),
        new(Do: Automation.Key(Keys.Space)),
        new(Wait: 0.5, Shot: "fight-paused", Do: g =>
        {
            if (!FightOf(g).Paused) throw new InvalidOperationException("Space did not pause the fight");
        }),
        new(Do: Automation.Click(FightScreen.PauseButton)),
        // Several updates can run before the next draw, so wait for the click to be drawn and handled.
        new(Until: g => !FightOf(g).Paused, Do: g =>
        {
            if (FightOf(g).Paused) throw new InvalidOperationException("the Resume button did not resume the fight");
        }),
        new(Do: g => FightOf(g).Speed = 8),
        new(Until: g => g.Current is FightScreen f && f.Battle.Result != Core.Engine.BattleResult.Ongoing),
        new(Wait: 1.2, Do: g => g.Input.Inject(new Vector2(640, 600)), Shot: "fight-end"),
        // The bottom edge of "New seed" overlaps the cards underneath; it must still start a new fight.
        new(Do: g => g.Input.Inject(new Vector2(FightScreen.AgainButton.X + 20, FightScreen.AgainButton.Bottom - 5), left: true)),
        new(Wait: 0.3, Until: g => g.Current is FightScreen f && f.Battle.Tick < 20, Shot: "new-seed"),
        new(Do: Automation.Key(Keys.Escape)),
    ];

    /// <summary>A full board against the grizzly bear, to check statuses, slow and many floating numbers.</summary>
    private static Step[] Bear() =>
    [
        .. ToLab("axe", "weights", "apple", "ice_bow", "tower_shield", "fang_knife"),
        new(Wait: 0.1, Do: g =>
        {
            var lab = (FightLabScreen)g.Current!;
            lab.Setup.EnemyId = "grizzly_bear";
            lab.StartFight();
        }),
        new(Do: g => FightOf(g).Speed = 4),
        new(Until: g => FightOf(g).Targeting.CanActivate(FightOf(g).Battle.Player.Items.Single(i => i.Slot == 1), FightOf(g).Battle),
            Do: Automation.Click(FightScreen.CardRect(1))),
        new(Wait: 0.3, Do: g => FightOf(g).Speed = 1),
        new(Wait: 0.5, Do: g => g.Input.Inject(new Vector2(500, 160)), Shot: "bear-mid"),
        new(Do: g => FightOf(g).Speed = 8),
        new(Until: g => FightOf(g).Battle.Result != Core.Engine.BattleResult.Ongoing),
        new(Wait: 1.2, Shot: "bear-end"),
    ];

    /// <summary>Luka's 2026-10-16 Furious Boxer, who gains Power each time he is hit.</summary>
    private static Step[] Boxer() =>
    [
        .. ToLab("sword", "twin_blades", "metal_plating", "spiky_club"),
        new(Wait: 0.1, Do: g =>
        {
            var lab = (FightLabScreen)g.Current!;
            lab.Setup.EnemyId = "furious_boxer";
            lab.StartFight();
        }),
        new(Do: g => FightOf(g).Speed = 4),
        new(Wait: 3, Do: g => FightOf(g).Speed = 1),
        new(Wait: 0.4, Shot: "boxer-mid"),
    ];

    /// <summary>Luka's 2026-10-12 rares against the Blur, whose rifle speeds up with every shot.</summary>
    private static Step[] Blur() =>
    [
        .. ToLab("whetstone_blade", "shield_bash", "spiked_buckler", "war_banner", "twin_blades", "gold_pouch"),
        new(Wait: 0.1, Do: g =>
        {
            var lab = (FightLabScreen)g.Current!;
            lab.Setup.EnemyId = "the_blur";
            lab.StartFight();
        }),
        new(Do: g => FightOf(g).Speed = 4),
        new(Wait: 3, Do: g => FightOf(g).Speed = 1),
        new(Wait: 0.4, Do: Automation.Hover(FightScreen.CardRect(2)), Shot: "blur-mid"),
        new(Do: g => FightOf(g).Speed = 8),
        new(Until: g => FightOf(g).Battle.Result != Core.Engine.BattleResult.Ongoing),
        new(Wait: 1.2, Shot: "blur-end"),
    ];

    /// <summary>Luka's 2026-10-15 rares: Pain and Nails, with the Magnetized Glove clicked once it has nails to pull.</summary>
    private static Step[] Nails() =>
    [
        .. ToLab("hurling_hammer", "magnetized_glove", "spiky_club", "sharpened_chestplate", "aggravating_potion", "flammable_toxins"),
        new(Wait: 0.1, Do: g =>
        {
            var lab = (FightLabScreen)g.Current!;
            lab.Setup.EnemyId = "grizzly_bear";
            lab.StartFight();
        }),
        new(Do: g => FightOf(g).Speed = 4),
        new(Until: g => FightOf(g).Battle.Enemy.Statuses.Nail >= 3
            && FightOf(g).Targeting.CanActivate(FightOf(g).Battle.Player.Items.Single(i => i.Slot == 1), FightOf(g).Battle),
            Do: g => FightOf(g).Speed = 1),
        new(Wait: 0.2, Shot: "nails-before"),
        new(Do: Automation.Click(FightScreen.CardRect(1))),
        new(Wait: 0.3, Shot: "nails-uprooted"),
        new(Do: g => FightOf(g).Speed = 8),
        new(Until: g => FightOf(g).Battle.Result != Core.Engine.BattleResult.Ongoing),
        new(Wait: 1.2, Shot: "nails-end"),
    ];

    private static RunSession SessionOf(Game1 g) => ((RunScreen)g.Current!).Session;

    private static void Check(bool ok, string what)
    {
        if (!ok) throw new InvalidOperationException("automation check failed: " + what);
    }

    /// <summary>A short run: character pick, map, prep, a fight, the drop, a shop, then save and continue from the menu.</summary>
    private static Step[] RunLoop() =>
    [
        new(Wait: 0.2, Do: g =>
        {
            CharacterSelectScreen.SeedOverride = 11;
            Automation.Click(MainMenuScreen.NewRunButton)(g);
        }),
        new(Wait: 0.2, Until: g => g.Current is CharacterSelectScreen, Shot: "run-character"),
        new(Do: Automation.Click(CharacterSelectScreen.CardRect(0))),
        new(Wait: 0.3, Until: g => g.Current is MapScreen, Do: Automation.Hover(MapScreen.ChoiceRect(0, 3)), Shot: "run-map"),
        // Fight the zombie whatever the map offered, so the script can rely on winning with the starting items.
        new(Do: g =>
        {
            var session = SessionOf(g);
            session.Run.Choose(new Encounter(EncounterKind.Fight, "squire_zombie"));
            session.Commit();
            session.ShowCurrent(g);
        }),
        new(Wait: 0.3, Until: g => g.Current is PrepScreen, Do: Automation.Hover(InventoryPanel.HotbarSlot(0)), Shot: "run-prep"),
        new(Do: Automation.Click(PrepScreen.FightButton)),
        new(Wait: 0.2, Until: g => g.Current is FightScreen, Do: g => FightOf(g).Speed = 8),
        new(Until: g => FightOf(g).Battle.Result != Core.Engine.BattleResult.Ongoing),
        new(Wait: 1.2, Do: g => g.Input.Inject(new Vector2(640, 600)), Shot: "run-fight-end"),
        new(Do: g =>
        {
            Check(FightOf(g).Battle.Result == Core.Engine.BattleResult.PlayerWon, "the starting items beat the zombie");
            Automation.Click(FightScreen.ReplayButton)(g);
        }),
        new(Wait: 0.3, Until: g => g.Current is DropScreen, Do: Automation.Hover(DropScreen.ChoiceRect(1, 3)), Shot: "run-drop"),
        new(Do: Automation.Click(DropScreen.TakeRect(0, 3))),
        new(Wait: 0.3, Until: g => g.Current is MapScreen, Shot: "run-map2"),
        new(Do: g =>
        {
            var session = SessionOf(g);
            Check(session.Run.Encounter == 1 && session.Run.Gold == 15, "won fight gave 5 gold and moved on");
            session.Run.Choose(new Encounter(EncounterKind.Shop, null));
            session.Commit();
            session.ShowCurrent(g);
        }),
        new(Wait: 0.3, Until: g => g.Current is ShopScreen, Do: Automation.Hover(ShopScreen.StockRect(1)), Shot: "run-shop"),
        new(Do: Automation.Click(ShopScreen.BuyRect(0))),
        new(Wait: 0.2, Do: Automation.Click(InventoryPanel.HotbarSlot(0))),
        new(Wait: 0.2, Do: Automation.Hover(InventoryPanel.BackpackSlot(0)), Shot: "run-shop-holding"),
        new(Do: Automation.Click(InventoryPanel.BackpackSlot(0))),
        new(Wait: 0.2, Shot: "run-shop-moved"),
        new(Do: Automation.Click(ShopScreen.LeaveButton)),
        new(Wait: 0.3, Until: g => g.Current is MapScreen, Do: Automation.Click(RunScreen.MenuButton)),
        new(Wait: 0.3, Until: g => g.Current is MainMenuScreen, Shot: "run-menu-continue"),
        new(Do: g =>
        {
            Check(((MainMenuScreen)g.Current!).HasSavedRun, "save and quit kept the run");
            Automation.Click(MainMenuScreen.ContinueButton)(g);
        }),
        new(Wait: 0.3, Until: g => g.Current is MapScreen, Shot: "run-continued"),
        new(Do: g =>
        {
            var session = SessionOf(g);
            Check(session.Run.Encounter == 2, "continue resumed at encounter 3");
            session.Run.Choose(new Encounter(EncounterKind.Fight, "grizzly_bear"));
            session.Commit();
            session.ShowCurrent(g);
        }),
        new(Wait: 0.3, Until: g => g.Current is PrepScreen, Do: Automation.Click(PrepScreen.FightButton)),
        new(Wait: 0.5, Until: g => g.Current is FightScreen, Do: Automation.Click(FightScreen.GiveUpButton)),
        new(Wait: 0.2, Shot: "run-giveup-confirm"),
        new(Do: Automation.Click(FightScreen.GiveUpButton)),
        new(Wait: 0.3, Until: g => g.Current is RunEndScreen, Shot: "run-end"),
        new(Do: g => Check(!File.Exists(RunSession.DefaultSavePath), "a lost run deletes its save")),
    ];
}
