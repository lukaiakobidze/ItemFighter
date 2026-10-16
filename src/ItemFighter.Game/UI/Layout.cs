using Microsoft.Xna.Framework;

namespace ItemFighter.UI;

/// <summary>
/// The virtual canvas every screen is laid out on. It is scaled to the window, so a bigger canvas makes everything
/// smaller on screen (Luka 2026-10-16: 1600×900, so the game has room to breathe).
/// </summary>
public static class Layout
{
    public const int Width = 1600;
    public const int Height = 900;
    public const int Margin = 48;

    public static Rectangle Full => new(0, 0, Width, Height);
}
