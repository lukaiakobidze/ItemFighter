using ItemFighter.Platform;
using Microsoft.Xna.Framework;

namespace ItemFighter.Tests.Game;

public class VirtualScreenTests
{
    private readonly VirtualScreen _screen = new();

    [Fact]
    public void SameSize_IsIdentity()
    {
        Assert.Equal(new Rectangle(0, 0, 1280, 720), _screen.Destination(new Point(1280, 720)));
        Assert.Equal(new Vector2(100, 200), _screen.ToVirtual(new Point(100, 200), new Point(1280, 720)));
    }

    [Fact]
    public void ToVirtual_Letterboxed_WideWindowHasSideBars()
    {
        // 2560x1080: scale 1.5, 1920 wide, bars of 320 on each side.
        var window = new Point(2560, 1080);
        Assert.Equal(new Rectangle(320, 0, 1920, 1080), _screen.Destination(window));
        Assert.Equal(new Vector2(0, 0), _screen.ToVirtual(new Point(320, 0), window));
        Assert.Equal(new Vector2(640, 360), _screen.ToVirtual(new Point(1280, 540), window));
    }

    [Fact]
    public void ToVirtual_Pillarboxed_TallWindowHasTopBars()
    {
        // 1280x1000: scale 1, bars of 140 top and bottom.
        var window = new Point(1280, 1000);
        Assert.Equal(new Rectangle(0, 140, 1280, 720), _screen.Destination(window));
        Assert.Equal(new Vector2(10, 0), _screen.ToVirtual(new Point(10, 140), window));
    }

    [Fact]
    public void ZeroSizedWindow_DoesNotDivideByZero()
    {
        var v = _screen.ToVirtual(new Point(5, 5), new Point(0, 0));
        Assert.False(float.IsNaN(v.X) || float.IsInfinity(v.X));
    }
}
