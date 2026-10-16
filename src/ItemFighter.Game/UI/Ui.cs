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
        if (fill is null && border is null && NineSlice("panel", r)) return;
        Rect(r, fill ?? Palette.Panel);
        Border(r, border ?? Palette.Border, 1);
    }

    /// <summary>
    /// Draws UI/&lt;id&gt;.png stretched over a rectangle of any size without stretching its corners: the outer quarter of
    /// the image on each side stays as drawn (shown <paramref name="corner"/> virtual pixels wide), the middle stretches.
    /// Returns false (and draws nothing) when the PNG isn't there, so callers fall back to plain shapes.
    /// </summary>
    public bool NineSlice(string id, Rectangle r, int corner = 12, Color? tint = null)
    {
        var tex = assets.Texture("UI", id);
        if (tex is null) return false;
        int sx = tex.Width / 4, sy = tex.Height / 4;
        int c = Math.Min(corner, Math.Min(r.Width, r.Height) / 2);
        int[] srcX = [0, sx, tex.Width - sx, tex.Width], srcY = [0, sy, tex.Height - sy, tex.Height];
        int[] dstX = [r.X, r.X + c, r.Right - c, r.Right], dstY = [r.Y, r.Y + c, r.Bottom - c, r.Bottom];
        for (int row = 0; row < 3; row++)
        for (int col = 0; col < 3; col++)
        {
            var dst = new Rectangle(dstX[col], dstY[row], dstX[col + 1] - dstX[col], dstY[row + 1] - dstY[row]);
            if (dst.Width <= 0 || dst.Height <= 0) continue;
            var src = new Rectangle(srcX[col], srcY[row], srcX[col + 1] - srcX[col], srcY[row + 1] - srcY[row]);
            batch.Draw(tex, dst, src, tint ?? Color.White);
        }
        return true;
    }

    /// <summary>A small UI icon (UI/&lt;id&gt;.png, e.g. "gold") in a square; false when there is no PNG.</summary>
    public bool Icon(string id, Rectangle r)
    {
        var tex = assets.Texture("UI", id);
        if (tex is null) return false;
        batch.Draw(tex, r, Color.White);
        return true;
    }

    /// <summary>A UI picture (logo, background) fitted inside a rectangle keeping its shape; false when there is no PNG.</summary>
    public bool Picture(string id, Rectangle r, bool cover = false)
    {
        var tex = assets.Texture("UI", id);
        if (tex is null) return false;
        float scale = cover ? Math.Max(r.Width / (float)tex.Width, r.Height / (float)tex.Height) : Math.Min(r.Width / (float)tex.Width, r.Height / (float)tex.Height);
        int w = (int)(tex.Width * scale), h = (int)(tex.Height * scale);
        batch.Draw(tex, new Rectangle(r.Center.X - w / 2, r.Center.Y - h / 2, w, h), Color.White);
        return true;
    }

    /// <summary>"[icon] text": the UI icon when its PNG exists, otherwise <paramref name="fallback"/> before the text.</summary>
    public void IconText(string icon, string fallback, string text, Vector2 pos, int size, Color color, bool bold = false)
    {
        int s = (int)(size * 1.15f);
        if (Icon(icon, new Rectangle((int)pos.X, (int)(pos.Y + size * 0.6f - s / 2f), s, s))) Text(text, new Vector2(pos.X + s + 6, pos.Y), size, color, bold);
        else Text(fallback + text, pos, size, color, bold);
    }

    /// <summary>
    /// Real screen pixels per virtual pixel. Text is rasterised at size × Scale and drawn at 1/Scale, so it stays sharp
    /// when the 1280×720 layout is scaled up to a bigger window or fullscreen.
    /// </summary>
    public float Scale { get; set; } = 1f;

    private SpriteFontBase Font(int size, bool bold) => assets.Font(Math.Max(1, (int)MathF.Round(size * Scale)), bold);

    private float TextWidth(SpriteFontBase font, string text) => font.MeasureString(text).X / Scale;

    private float Snap(float v) => MathF.Round(v * Scale) / Scale;

    private void DrawText(SpriteFontBase font, string text, Vector2 pos, Color color) =>
        batch.DrawString(font, text, new Vector2(Snap(pos.X), Snap(pos.Y)), color, scale: new Vector2(1 / Scale));

    public Vector2 Measure(string text, int size, bool bold = false) => Font(size, bold).MeasureString(text) / Scale;

    public void Text(string text, Vector2 pos, int size, Color color, bool bold = false, Align align = Align.Left)
    {
        var font = Font(size, bold);
        if (align != Align.Left)
        {
            float w = TextWidth(font, text);
            pos.X -= align == Align.Center ? w / 2 : w;
        }
        DrawText(font, text, pos, color);
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
        var font = Font(size, bold);
        var lines = Wrap(s => TextWidth(font, s), text, width);
        int lineHeight = (int)(size * 1.25f);
        for (int i = 0; i < lines.Count; i++)
            DrawText(font, lines[i], new Vector2(pos.X, pos.Y + i * lineHeight), color);
        return lines.Count * lineHeight;
    }

    public int WrappedHeight(string text, float width, int size, bool bold = false)
    {
        var font = Font(size, bold);
        return Wrap(s => TextWidth(font, s), text, width).Count * (int)(size * 1.25f);
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

    /// <summary>
    /// A small icon on the shared frame: UI/icon_frame_bg.png behind it and UI/icon_frame_border.png on top, the border
    /// tinted with <paramref name="border"/> (so one white border image serves every colour). Without those PNGs the
    /// frame is drawn as a dark square with a thin coloured line.
    /// </summary>
    public void FramedIcon(string kind, string id, string name, Rectangle r, Color border, int thickness = 1)
    {
        var bg = assets.Texture("UI", "icon_frame_bg");
        if (bg is null) Rect(r, new Color(14, 15, 22));
        else batch.Draw(bg, r, Color.White);
        int pad = Math.Max(2, r.Width / 10);
        var inner = new Rectangle(r.X + pad, r.Y + pad, r.Width - pad * 2, r.Height - pad * 2);
        if (assets.Texture(kind, id) is not null) Art(kind, id, name, inner);
        else TextIn(name, r, Math.Max(10, r.Height / 3), Palette.TextDim, bold: true);
        var frame = assets.Texture("UI", "icon_frame_border");
        if (frame is null) Border(r, border, thickness);
        else
        {
            batch.Draw(frame, r, border);
            if (thickness > 1) Border(r, border, thickness);
        }
    }

    /// <summary>A content picture, or a placeholder (colour + name) when there is no PNG.</summary>
    public void Art(string kind, string id, string name, Rectangle r, Color? tint = null)
    {
        var tex = assets.Texture(kind, id);
        if (tex is null)
        {
            Rect(r, Palette.PlaceholderFor(id));
            var font = Font(r.Height >= 120 ? 18 : 13, bold: true);
            var lines = Wrap(s => TextWidth(font, s), name, r.Width - 8);
            float lh = font.LineHeight / Scale;
            float y = r.Center.Y - lines.Count * lh / 2;
            foreach (var l in lines)
            {
                float w = TextWidth(font, l);
                DrawText(font, l, new Vector2(r.Center.X - w / 2, y), Color.White * 0.85f);
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

    /// <summary>While true (e.g. under a modal overlay) nothing reacts to the mouse.</summary>
    public bool InputBlocked { get; set; }

    public bool Hover(Rectangle r) => !InputBlocked && r.Contains(input.Mouse);

    public bool Clicked(Rectangle r)
    {
        if (InputBlocked || !input.LeftClicked || !Hover(r)) return false;
        input.ConsumeClicks();
        return true;
    }

    public bool RightClicked(Rectangle r)
    {
        if (InputBlocked || !input.RightClicked || !Hover(r)) return false;
        input.ConsumeClicks();
        return true;
    }

    public bool Button(Rectangle r, string label, int size = 20, bool enabled = true, bool selected = false)
    {
        bool hover = enabled && Hover(r);
        string skin = !enabled ? "button_disabled" : selected ? "button_selected" : hover ? "button_hover" : "button";
        if (!NineSlice(skin, r, 10))
        {
            var fill = !enabled ? new Color(40, 42, 54) : selected ? new Color(92, 78, 30) : hover ? Palette.PanelLight : Palette.Panel;
            Rect(r, fill);
            Border(r, selected ? Palette.Accent : hover ? Palette.Text : Palette.Border, selected ? 2 : 1);
        }
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
        if (NineSlice("tooltip", r, 10)) Border(r, _tipAccent, 2);
        else
        {
            Rect(r, new Color(14, 15, 22, 245));
            Border(r, _tipAccent, 2);
        }
        float cy = y + pad;
        cy += Wrapped(_tipTitle, new Vector2(x + pad, cy), width - pad * 2, 18, _tipAccent, true) + 4;
        foreach (var l in _tipLines)
            cy += Wrapped(l, new Vector2(x + pad, cy), width - pad * 2, 15, Palette.Text) + 4;
        _tipTitle = null;
    }
}
