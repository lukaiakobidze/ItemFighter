using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ItemFighter.Platform;

/// <summary>Fonts and PNG art loaded straight from files at runtime. Missing or broken art gives null (draw a placeholder).</summary>
public sealed class Assets : IDisposable
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly GraphicsDevice _device;
    private readonly Dictionary<string, Texture2D?> _textures = new(StringComparer.Ordinal);
    private readonly FontSystem _regular = new();
    private readonly FontSystem _bold = new();

    public Assets(GraphicsDevice device, string fontDirectory, string artDirectory)
    {
        _device = device;
        ArtDirectory = artDirectory;
        _regular.AddFont(File.ReadAllBytes(Path.Combine(fontDirectory, "DejaVuSans.ttf")));
        _bold.AddFont(File.ReadAllBytes(Path.Combine(fontDirectory, "DejaVuSans-Bold.ttf")));
        Pixel = new Texture2D(device, 1, 1);
        Pixel.SetData([Color.White]);
    }

    public string ArtDirectory { get; }
    public Texture2D Pixel { get; }

    public SpriteFontBase Font(int size, bool bold = false) => (bold ? _bold : _regular).GetFont(size);

    /// <summary>Art for a content id, e.g. Texture("Items", "fire_flask"); null when there is no usable PNG.</summary>
    public Texture2D? Texture(string kind, string id)
    {
        string path = ArtPath(ArtDirectory, kind, id);
        if (_textures.TryGetValue(path, out var cached)) return cached;
        Texture2D? texture = null;
        if (File.Exists(path))
        {
            try
            {
                using var stream = File.OpenRead(path);
                texture = LoadPng(_device, stream);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        _textures[path] = texture;
        return texture;
    }

    /// <summary>Forgets loaded art so changed files are picked up.</summary>
    public void ClearArt()
    {
        foreach (var t in _textures.Values) t?.Dispose();
        _textures.Clear();
    }

    public static string ArtPath(string root, string kind, string id) => Path.Combine(root, kind, id + ".png");

    public static Texture2D? LoadPng(GraphicsDevice? device, Stream stream)
    {
        try
        {
            var head = new byte[PngSignature.Length];
            if (stream.Read(head, 0, head.Length) != head.Length || !head.AsSpan().SequenceEqual(PngSignature)) return null;
            if (device is null) return null;
            stream.Seek(0, SeekOrigin.Begin);
            using var flat = Texture2D.FromStream(device, stream);
            return WithMipmaps(device, flat);
        }
        catch (Exception) // any decoder failure means "no art", never a crash
        {
            return null;
        }
    }

    /// <summary>
    /// A copy with a full mip chain (each level a 2×2 average of the one above), so big art drawn small, like a 256px
    /// passive in a 36px box, is smoothly filtered instead of shimmering and jagged.
    /// </summary>
    private static Texture2D WithMipmaps(GraphicsDevice device, Texture2D source)
    {
        int w = source.Width, h = source.Height;
        var level = new Color[w * h];
        source.GetData(level);
        var result = new Texture2D(device, w, h, mipmap: true, SurfaceFormat.Color);
        for (int i = 0; i < result.LevelCount; i++)
        {
            result.SetData(i, null, level, 0, level.Length);
            if (w == 1 && h == 1) break;
            int nw = Math.Max(1, w / 2), nh = Math.Max(1, h / 2);
            var next = new Color[nw * nh];
            for (int y = 0; y < nh; y++)
            for (int x = 0; x < nw; x++)
            {
                int x0 = Math.Min(w - 1, x * 2), x1 = Math.Min(w - 1, x * 2 + 1);
                int y0 = Math.Min(h - 1, y * 2), y1 = Math.Min(h - 1, y * 2 + 1);
                Color a = level[y0 * w + x0], b = level[y0 * w + x1], c = level[y1 * w + x0], d = level[y1 * w + x1];
                next[y * nw + x] = new Color((a.R + b.R + c.R + d.R + 2) / 4, (a.G + b.G + c.G + d.G + 2) / 4,
                    (a.B + b.B + c.B + d.B + 2) / 4, (a.A + b.A + c.A + d.A + 2) / 4);
            }
            level = next;
            w = nw;
            h = nh;
        }
        return result;
    }

    public void Dispose()
    {
        ClearArt();
        Pixel.Dispose();
        _regular.Dispose();
        _bold.Dispose();
    }
}
