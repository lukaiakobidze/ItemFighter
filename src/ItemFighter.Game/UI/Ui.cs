using FontStashSharp;
using ItemFighter.Platform;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ItemFighter.UI;

public enum Align { Left, Center, Right }

/// <summary>
/// Immediate-mode drawing and widgets on the virtual screen. Screens describe their whole UI every frame;
/// a widget returns true on the frame it is clicked.
/// </summary>
public sealed class Ui(SpriteBatch batch, Assets assets, Input input, VirtualScreen screen)
{
    private string? _tipTitle;
    private IReadOnlyList<string> _tipLines = [];
    private Color _tipAccent;

    public SpriteBatch Batch => batch;
    public Assets Assets => assets;
    public Input Input => input;
    public VirtualScreen Screen => screen;

    /// <summary>Seconds since the game started, for pulses and fades.</summary>
    public double Time { get; set; }

    public void Rect(Rectangle r, Color c) => batch.Draw(assets.Pixel, r, c);

    public void Border(Rectangle r, Color c, int t = 2)
    {
        Rect(new Rectangle(r.X, r.Y, r.Width, t), c);
        Rect(new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        Rect(new Rectangle(r.X, r.Y, t, r.Height), c);
        Rect(new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }

    public void Panel(Rectangle r, Color? fill = null, Color? border = null)
    {
        Rect(r, fill ?? Palette.Panel);
        Border(r, border ?? Palette.Border, 1);
    }

    public Vector2 Measure(string text, int size, bool bold = false) => assets.Font(size, bold).MeasureString(text);

    public void Text(string text, Vector2 pos, int size, Color color, bool bold = false, Align align = Align.Left)
    {
        var font = assets.Font(size, bold);
        if (align != Align.Left)
        {
            float w = font.MeasureString(text).X;
            pos.X -= align == Align.Center ? w / 2 : w;
        }
        batch.DrawString(font, text, new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y)), color);
    }

    /// <summary>Text centred in a rectangle.</summary>
    public void TextIn(string text, Rectangle r, int size, Color color, bool bold = false)
    {
        var m = Measure(text, size, bold);
        Text(text, new Vector2(r.Center.X - m.X / 2, r.Center.Y - m.Y / 2), size, color, bold);
    }

    /// <summary>Draws word-wrapped text and returns the height used.</summary>
    public int Wrapped(string text, Vector2 pos, float width, int size, Color color, bool bold = false)
    {
        var font = assets.Font(size, bold);
        var lines = Wrap(s => font.MeasureString(s).X, text, width);
        int lineHeight = (int)(size * 1.25f);
        for (int i = 0; i < lines.Count; i++)
            batch.DrawString(font, lines[i], new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y + i * lineHeight)), color);
        return lines.Count * lineHeight;
    }

    public int WrappedHeight(string text, float width, int size, bool bold = false)
    {
        var font = assets.Font(size, bold);
        return Wrap(s => font.MeasureString(s).X, text, width).Count * (int)(size * 1.25f);
    }

    /// <summary>Greedy word wrap. A single word longer than the width is cut by characters.</summary>
    public static List<string> Wrap(Func<string, float> measure, string text, float width)
    {
        var lines = new List<string>();
        foreach (string paragraph in text.Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (measure(candidate) <= width) { line = candidate; continue; }
                if (line.Length > 0) lines.Add(line);
                line = word;
                while (line.Length > 1 && measure(line) > width)
                {
                    int cut = line.Length - 1;
                    while (cut > 1 && measure(line[..cut]) > width) cut--;
                    lines.Add(line[..cut]);
                    line = line[cut..];
                }
            }
            lines.Add(line);
        }
        return lines;
    }

    public void Bar(Rectangle r, float fraction, Color fill, Color back)
    {
        Rect(r, back);
        int w = (int)(r.Width * Math.Clamp(fraction, 0f, 1f));
        if (w > 0) Rect(new Rectangle(r.X, r.Y, w, r.Height), fill);
    }

    /// <summary>A content picture, or a placeholder (colour + name) when there is no PNG.</summary>
    public void Art(string kind, string id, string name, Rectangle r, Color? tint = null)
    {
        var tex = assets.Texture(kind, id);
        if (tex is null)
        {
            Rect(r, Palette.PlaceholderFor(id));
            var font = assets.Font(r.Height >= 120 ? 18 : 13, bold: true);
            var lines = Wrap(s => font.MeasureString(s).X, name, r.Width - 8);
            float lh = font.LineHeight;
            float y = r.Center.Y - lines.Count * lh / 2;
            foreach (var l in lines)
            {
                float w = font.MeasureString(l).X;
                batch.DrawString(font, l, new Vector2(MathF.Round(r.Center.X - w / 2), MathF.Round(y)), Color.White * 0.85f);
                y += lh;
            }
        }
        else
        {
            float scale = Math.Min(r.Width / (float)tex.Width, r.Height / (float)tex.Height);
            int w = (int)(tex.Width * scale), h = (int)(tex.Height * scale);
            batch.Draw(tex, new Rectangle(r.Center.X - w / 2, r.Center.Y - h / 2, w, h), Color.White);
        }
        if (tint is Color t) Rect(r, t);
    }

    public bool Hover(Rectangle r) => r.Contains(input.Mouse);

    public bool Clicked(Rectangle r)
    {
        if (!input.LeftClicked || !Hover(r)) return false;
        input.ConsumeClicks();
        return true;
    }

    public bool RightClicked(Rectangle r)
    {
        if (!input.RightClicked || !Hover(r)) return false;
        input.ConsumeClicks();
        return true;
    }

    public bool Button(Rectangle r, string label, int size = 20, bool enabled = true, bool selected = false)
    {
        bool hover = enabled && Hover(r);
        var fill = !enabled ? new Color(40, 42, 54) : selected ? new Color(92, 78, 30) : hover ? Palette.PanelLight : Palette.Panel;
        Rect(r, fill);
        Border(r, selected ? Palette.Accent : hover ? Palette.Text : Palette.Border, selected ? 2 : 1);
        TextIn(label, r, size, enabled ? Palette.Text : Palette.TextDim, bold: true);
        return enabled && Clicked(r);
    }

    /// <summary>Shows a tooltip this frame (drawn on top of everything by <see cref="DrawTooltip"/>).</summary>
    public void Tooltip(string title, IReadOnlyList<string> lines, Color? accent = null)
    {
        _tipTitle = title;
        _tipLines = lines;
        _tipAccent = accent ?? Palette.Accent;
    }

    public void DrawTooltip()
    {
        if (_tipTitle is null) return;
        const int width = 340, pad = 10;
        int height = pad * 2 + WrappedHeight(_tipTitle, width - pad * 2, 18, true) + 4;
        foreach (var l in _tipLines) height += WrappedHeight(l, width - pad * 2, 15) + 4;
        var m = input.Mouse;
        int x = (int)m.X + 18, y = (int)m.Y + 18;
        if (x + width > screen.Width - 4) x = (int)m.X - width - 12;
        if (y + height > screen.Height - 4) y = screen.Height - height - 4;
        x = Math.Max(4, x);
        y = Math.Max(4, y);
        var r = new Rectangle(x, y, width, height);
        Rect(r, new Color(14, 15, 22, 245));
        Border(r, _tipAccent, 2);
        float cy = y + pad;
        cy += Wrapped(_tipTitle, new Vector2(x + pad, cy), width - pad * 2, 18, _tipAccent, true) + 4;
        foreach (var l in _tipLines)
            cy += Wrapped(l, new Vector2(x + pad, cy), width - pad * 2, 15, Palette.Text) + 4;
        _tipTitle = null;
    }
}
