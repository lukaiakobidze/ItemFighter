using ItemFighter.Screens;
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
}
