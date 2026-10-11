using System.Text;

namespace ItemFighter.Core.Determinism;

/// <summary>FNV-1a 64-bit hash. Unlike string.GetHashCode it is identical across runs and machines.</summary>
public static class StableHash
{
    public static ulong Of(string s)
    {
        ulong hash = 0xcbf29ce484222325UL;
        foreach (byte b in Encoding.UTF8.GetBytes(s))
        {
            hash ^= b;
            hash *= 0x100000001b3UL;
        }
        return hash;
    }
}
