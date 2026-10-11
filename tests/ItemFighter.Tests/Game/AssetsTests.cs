using ItemFighter.Platform;

namespace ItemFighter.Tests.Game;

public class AssetsTests
{
    [Fact]
    public void LoadPng_Garbage_ReturnsNull()
    {
        using var stream = new MemoryStream("this is not a png"u8.ToArray());
        Assert.Null(Assets.LoadPng(null, stream));
    }

    [Fact]
    public void LoadPng_TruncatedPng_ReturnsNull()
    {
        using var stream = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0]);
        Assert.Null(Assets.LoadPng(null, stream));
    }

    [Fact]
    public void ArtPath_UsesKindFolderAndId() =>
        Assert.Equal(Path.Combine("root", "Items", "fire_flask.png"), Assets.ArtPath("root", "Items", "fire_flask"));
}
