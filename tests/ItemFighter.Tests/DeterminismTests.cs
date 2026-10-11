using ItemFighter.Core.Determinism;

namespace ItemFighter.Tests;

public class DeterminismTests
{
    [Fact]
    public void StableHash_KnownVector()
    {
        Assert.Equal(0xcbf29ce484222325UL, StableHash.Of(""));
        Assert.Equal(0xaf63dc4c8601ec8cUL, StableHash.Of("a"));
    }

    [Fact]
    public void Rng_SameSeed_SameSequence()
    {
        var a = new Rng(42);
        var b = new Rng(42);
        for (int i = 0; i < 1000; i++)
            Assert.Equal(a.NextInt(100), b.NextInt(100));
    }

    [Fact]
    public void Rng_DifferentSeeds_DifferentSequences()
    {
        var a = new Rng(1);
        var b = new Rng(2);
        var same = Enumerable.Range(0, 100).Count(_ => a.NextInt(1000) == b.NextInt(1000));
        Assert.True(same < 10);
    }

    [Fact]
    public void Rng_Fork_IsIndependentOfParentUsage()
    {
        var fresh = new Rng(7).Fork("combat");
        var used = new Rng(7);
        for (int i = 0; i < 50; i++) used.NextInt(10);
        var forkedLater = used.Fork("combat");
        for (int i = 0; i < 100; i++)
            Assert.Equal(fresh.NextInt(1000), forkedLater.NextInt(1000));
    }

    [Fact]
    public void Rng_Fork_DifferentNames_Differ()
    {
        var a = new Rng(7).Fork("combat");
        var b = new Rng(7).Fork("shop");
        var same = Enumerable.Range(0, 100).Count(_ => a.NextInt(1000) == b.NextInt(1000));
        Assert.True(same < 10);
    }

    [Fact]
    public void Rng_NextInt_InRange_AndRoughlyUniform()
    {
        var rng = new Rng(123);
        var buckets = new int[10];
        for (int i = 0; i < 100_000; i++)
        {
            int v = rng.NextInt(10);
            Assert.InRange(v, 0, 9);
            buckets[v]++;
        }
        Assert.All(buckets, b => Assert.InRange(b, 9_000, 11_000));
    }

    [Fact]
    public void Rng_Chance_Extremes()
    {
        var rng = new Rng(5);
        for (int i = 0; i < 100; i++)
        {
            Assert.False(rng.Chance(0));
            Assert.True(rng.Chance(100));
        }
    }
}
