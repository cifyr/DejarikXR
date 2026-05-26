using System;

namespace Dejarik
{
    // A source of uniform doubles in [0,1), matching the web game's `Rng = () => number`.
    public delegate double Rng();

    public static class RngFactory
    {
        // Deterministic, seedable PRNG (mulberry32), bit-exact with the web game's rng.ts so games and
        // dice sequences reproduce identically across the TS and C# implementations. Uses unchecked uint
        // arithmetic to mirror JS `Math.imul` / `>>>` semantics.
        public static Rng Make(uint seed)
        {
            uint a = seed;
            return () =>
            {
                unchecked
                {
                    a += 0x6d2b79f5u;
                    uint t = (a ^ (a >> 15)) * (1u | a);
                    t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                    return (t ^ (t >> 14)) / 4294967296.0;
                }
            };
        }

        public static uint RandomSeed() => (uint)new Random().Next(int.MinValue, int.MaxValue);
    }
}
