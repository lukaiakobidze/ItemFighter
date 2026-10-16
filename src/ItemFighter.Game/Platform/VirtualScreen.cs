using Microsoft.Xna.Framework;

namespace ItemFighter.Platform;

/// <summary>The game draws at a fixed virtual size, scaled to fit the window with bars where the aspect differs.</summary>
public sealed class VirtualScreen(int width = UI.Layout.Width, int height = UI.Layout.Height)
{
    public int Width => width;
    public int Height => height;
    public Rectangle Bounds => new(0, 0, width, height);

    /// <summary>Where the virtual screen lands inside a window of the given size.</summary>
    public Rectangle Destination(Point window)
    {
        if (window.X <= 0 || window.Y <= 0) return Bounds;
        float scale = Math.Min(window.X / (float)width, window.Y / (float)height);
        int w = (int)MathF.Round(width * scale);
        int h = (int)MathF.Round(height * scale);
        return new Rectangle((window.X - w) / 2, (window.Y - h) / 2, w, h);
    }

    /// <summary>Converts a window pixel (e.g. the mouse) to virtual coordinates.</summary>
    public Vector2 ToVirtual(Point windowPos, Point windowSize)
    {
        var d = Destination(windowSize);
        return new Vector2((windowPos.X - d.X) * width / (float)d.Width, (windowPos.Y - d.Y) * height / (float)d.Height);
    }
}
