using ItemFighter.UI;

namespace ItemFighter.Screens;

/// <summary>One full-window screen. Draw describes the whole UI each frame and handles its clicks.</summary>
public abstract class Screen(Game1 game)
{
    protected Game1 Game => game;

    public virtual void Update(double seconds) { }

    /// <summary>Called when another screen replaces this one (and before content reloads).</summary>
    public virtual void OnLeave() { }

    public abstract void Draw(Ui ui);
}
