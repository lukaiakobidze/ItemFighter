namespace ItemFighter.Core.Determinism;

/// <summary>PCG32 (XSH-RR). The only source of randomness gameplay code may use.</summary>
public sealed class Rng
{
    private const ulong Multiplier = 6364136223846793005UL;

    private readonly ulong _seed;
    private readonly ulong _increment;
    private ulong _state;

    public Rng(ulong seed, ulong stream = 0)
    {
        _seed = seed;
        _increment = (stream << 1) | 1UL;
        _state = 0;
        NextUInt();
        _state += seed;
        NextUInt();
    }

    public ulong State => _state;

    /// <summary>A new generator for a named purpose. Depends only on this generator's seed and the name, not on how much it has been used.</summary>
    public Rng Fork(string streamName) => new(_seed ^ StableHash.Of(streamName), StableHash.Of("stream:" + streamName));

    public uint NextUInt()
    {
        ulong old = _state;
        _state = unchecked(old * Multiplier + _increment);
        uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        int rot = (int)(old >> 59);
        return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
    }

    /// <summary>Uniform integer in [0, maxExclusive), without modulo bias.</summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        uint bound = (uint)maxExclusive;
        uint threshold = unchecked((uint)-bound) % bound;
        while (true)
        {
            uint r = NextUInt();
            if (r >= threshold) return (int)(r % bound);
        }
    }

    public bool Chance(int percent) => NextInt(100) < percent;
}
