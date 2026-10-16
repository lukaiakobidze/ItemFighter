using ItemFighter.Core.Run;
using ItemFighter.UI;
using Microsoft.Xna.Framework;

namespace ItemFighter.Screens.Run;

/// <summary>Before a fight: see who you're fighting and arrange the hotbar. It starts as last fight's hotbar.</summary>
public sealed class PrepScreen(Game1 game, RunSession session) : RunScreen(game, session)
{
    public static readonly Rectangle FightButton = new(908, 560, 340, 64);

    protected override string Title => "Get ready";

    protected override void DrawContent(Ui ui)
    {
        var e = Run.Current!;
        if (!Db.TryGet(e.EnemyId!, out var enemy)) return;
        var card = new Rectangle(24, 64, 260, 306);
        DrawCard(ui, enemy, card);
        int hp = enemy.MaxHp;
        float x = 312, y = 70;
        string kind = e.Kind == EncounterKind.Boss ? "Mission boss" : "Fight";
        ui.Text($"{kind}: {enemy.Name}", new Vector2(x, y), 26, e.Kind == EncounterKind.Boss ? Palette.Damage : Palette.Text, bold: true);
        ui.Text($"{hp} HP", new Vector2(x, y + 40), 18, Palette.TextDim);
        float ay = y + 72;
        foreach (string line in Tooltips.For(enemy, 1, Db).Lines.Skip(1)) // skip the HP fact, shown above
            ay += ui.Wrapped(line, new Vector2(x, ay), 540, 17, Palette.TextDim) + 6;
        ui.Wrapped("Put the items you want to fight with on the hotbar. Everything in the backpack sits this fight out. " +
            $"You have {Session.Run.Db.Rules.FightTimeLimitSeconds:0.#} seconds to win; running out of time loses the run.",
            new Vector2(x, ay + 18), 540, 17, Palette.Text);
    }

    protected override int DrawSideExtras(Ui ui, int y)
    {
        bool can = Run.CanFight;
        if (!can) ui.Text("Put an item on the hotbar first.", new Vector2(FightButton.X, FightButton.Y - 26), 15, Palette.Damage);
        if (ui.Button(FightButton, "Fight!", 28, can)) Session.StartFight(Game);
        return y;
    }
}
