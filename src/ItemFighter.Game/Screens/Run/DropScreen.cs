using ItemFighter.Core.Content;
using ItemFighter.Core.Run;
using ItemFighter.UI;
using Microsoft.Xna.Framework;

namespace ItemFighter.Screens.Run;

/// <summary>After a win: take 1 of 3 items or passives, or skip.</summary>
public sealed class DropScreen(Game1 game, RunSession session) : RunScreen(game, session)
{
    public static readonly Rectangle SkipButton = new(908, 566, 340, 56);

    protected override string Title => "Victory! Pick your reward";

    public static Rectangle ChoiceRect(int index, int count)
    {
        const int width = 230, gap = 24;
        int total = count * width + (count - 1) * gap;
        return new Rectangle(24 + (842 - total) / 2 + index * (width + gap), 64, width, 262);
    }

    public static Rectangle TakeRect(int index, int count)
    {
        var r = ChoiceRect(index, count);
        return new Rectangle(r.X, r.Bottom + 6, r.Width, 38);
    }

    protected override void DrawContent(Ui ui)
    {
        var drop = Run.PendingDrop;
        var choices = drop?.Choices ?? [];
        if (choices.Count == 0) ui.Text("Nothing dropped this time.", new Vector2(24, 80), 20, Palette.TextDim);
        for (int i = 0; i < choices.Count; i++)
        {
            if (!Db.TryGet(choices[i], out var def)) continue;
            bool passive = Run.IsPassiveId(def.Id);
            var owned = Run.AllOwned.FirstOrDefault(o => o.ItemId == def.Id);
            int level = owned is null ? 1 : owned.Level + 1;
            var r = ChoiceRect(i, choices.Count);
            DrawCard(ui, def, r, level);
            string note = passive ? (def.Kind == SourceKind.EnemyAbility ? "the enemy's passive" : "passive")
                : i == 0 && drop!.Kind == DropKind.Items && Db.TryGet(Run.Current?.EnemyId ?? "", out var e) && e.DropItemId == def.Id ? "the enemy's drop"
                : owned is not null ? $"merges: level {level}" : "";
            if (note.Length > 0)
            {
                ui.Rect(new Rectangle(r.X + 6, r.Y + 6, r.Width - 12, 20), new Color(0, 0, 0, 150));
                ui.Text(note, new Vector2(r.Center.X, r.Y + 8), 13, Palette.Accent, bold: true, align: Align.Center);
            }
            bool room = passive || owned is not null || Run.HasRoom;
            if (ui.Button(TakeRect(i, choices.Count), room ? "Take" : "No room: sell something", 16, room) && Run.TakeDrop(i))
            {
                Session.Commit();
                Session.ShowCurrent(Game);
                return;
            }
        }
    }

    protected override int DrawSideExtras(Ui ui, int y)
    {
        if (ui.Button(SkipButton, "Skip", 22))
        {
            Run.SkipDrop();
            Session.Commit();
            Session.ShowCurrent(Game);
        }
        return y;
    }
}
