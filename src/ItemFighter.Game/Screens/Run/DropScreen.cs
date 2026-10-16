using ItemFighter.Core.Content;
using ItemFighter.Core.Run;
using ItemFighter.UI;
using Microsoft.Xna.Framework;

namespace ItemFighter.Screens.Run;

/// <summary>After a win: take 1 of 3 items, or skip.</summary>
public sealed class DropScreen(Game1 game, RunSession session) : RunScreen(game, session)
{
    public static readonly Rectangle SkipButton = SideButton(0, 50);

    protected override string Title => "Victory! Pick your reward";

    public static Rectangle ChoiceRect(int index, int count)
    {
        const int size = 150, gap = 90;
        int total = count * size + (count - 1) * gap;
        return new Rectangle(Layout.Margin + (1152 - total) / 2 + index * (size + gap), 110, size, size);
    }

    public static Rectangle TakeRect(int index, int count)
    {
        var r = ChoiceRect(index, count);
        return new Rectangle(r.X - 10, r.Bottom + 44, r.Width + 20, 38);
    }

    protected override void DrawContent(Ui ui)
    {
        var drop = Run.PendingDrop;
        var choices = drop?.Choices ?? [];
        if (choices.Count == 0) ui.Text("Nothing dropped this time.", new Vector2(Layout.Margin, 120), 18, Palette.TextDim);
        for (int i = 0; i < choices.Count; i++)
        {
            if (!Db.TryGet(choices[i], out var def)) continue;
            var owned = Run.AllOwned.FirstOrDefault(o => o.ItemId == def.Id);
            int level = owned is null ? 1 : owned.Level + 1;
            var r = ChoiceRect(i, choices.Count);
            DrawSquare(ui, def, r, level);
            string name = owned is not null ? $"{def.Name} (level {level})" : def.Name;
            ui.TextIn(name, new Rectangle(r.X - 40, r.Bottom + 10, r.Width + 80, 24), 16, Palette.Text, bold: true);
            bool room = owned is { Level: < ContentLoader.LevelCount } || Run.HasRoom;
            if (ui.Button(TakeRect(i, choices.Count), room ? "Take" : "No room", 16, room) && Run.TakeDrop(i))
            {
                Session.Commit();
                Session.ShowCurrent(Game);
                return;
            }
        }
    }

    protected override int DrawSideExtras(Ui ui, int y)
    {
        if (ui.Button(SkipButton, "Skip", 20))
        {
            Run.SkipDrop();
            Session.Commit();
            Session.ShowCurrent(Game);
        }
        return y;
    }
}
