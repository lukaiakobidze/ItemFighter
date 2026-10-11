using ItemFighter.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Step = ItemFighter.Platform.Automation.Step;

namespace ItemFighter.Platform;

/// <summary>Named scripted sessions for headless screenshot checks (run under xvfb-run).</summary>
public static class AutomationScripts
{
    public static IReadOnlyList<Step> Get(string name) => name switch
    {
        "menu" => [new(Wait: 0.3, Shot: "menu")],
        "lab" => [.. ToLab(), new(Wait: 0.2, Do: Automation.Hover(FightLabScreen.GridCell(3)), Shot: "lab")],
        "fight" => Fight(),
        "boss" => Boss(),
        _ => throw new ArgumentException($"unknown automation script '{name}'"),
    };

    private static Step[] ToLab() =>
    [
        new(Wait: 0.2, Do: Automation.Click(MainMenuScreen.FightLabButton)),
        new(Wait: 0.2, Until: g => g.Current is FightLabScreen, Do: g => ((FightLabScreen)g.Current!).Setup.Seed = 3),
    ];

    /// <summary>A busier board against the boss, to check statuses, phases and many floating numbers.</summary>
    private static Step[] Boss() =>
    [
        .. ToLab(),
        new(Wait: 0.1, Do: g =>
        {
            var lab = (FightLabScreen)g.Current!;
            string?[] items = ["frost_shard", "venom_vial", "plague_bellows", "fire_flask", "bellows", "tower_shield"];
            for (int i = 0; i < items.Length; i++) lab.Setup.Items[i] = items[i];
            lab.Setup.Levels[3] = 3;
            lab.Setup.EnemyId = "ember_king";
            lab.Setup.Passives = ["kindling"];
            lab.StartFight();
        }),
        new(Do: g => ((FightScreen)g.Current!).Speed = 4),
        new(Wait: 1.5, Do: g => ((FightScreen)g.Current!).Speed = 1),
        new(Wait: 0.6, Do: g => g.Input.Inject(new Vector2(1000, 300)), Shot: "boss-mid"),
        new(Do: g => ((FightScreen)g.Current!).Speed = 8),
        new(Until: g => g.Current is FightScreen f && f.Battle.Result != Core.Engine.BattleResult.Ongoing),
        new(Wait: 1.2, Shot: "boss-end"),
    ];

    /// <summary>
    /// Whetstone (slot 3) sharpens the sword (slot 0) as soon as it is ready, screenshots along the way, then
    /// fast-forwards to the result.
    /// </summary>
    private static Step[] Fight() =>
    [
        .. ToLab(),
        new(Wait: 0.1, Do: g => ((FightLabScreen)g.Current!).StartFight()),
        new(Wait: 0.5, Shot: "fight-start"),
        new(Until: g => g.Current is FightScreen f && f.Targeting.CanActivate(f.Battle.Player.Items.Single(i => i.Slot == 3), f.Battle),
            Do: Automation.Click(FightScreen.CardRect(3))),
        new(Wait: 0.1, Do: Automation.Hover(FightScreen.CardRect(0)), Shot: "fight-targeting"),
        new(Wait: 0.1, Do: Automation.Click(FightScreen.CardRect(0))),
        new(Wait: 0.3, Do: Automation.Hover(FightScreen.CardRect(0)), Shot: "fight-mid"),
        new(Do: g => ((FightScreen)g.Current!).Speed = 8),
        new(Until: g => g.Current is FightScreen f && f.Battle.Result != Core.Engine.BattleResult.Ongoing),
        new(Wait: 1.2, Do: g => g.Input.Inject(new Vector2(640, 600)), Shot: "fight-end"),
        new(Do: Automation.Key(Keys.Escape)),
    ];
}
