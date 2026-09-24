namespace Pickleball.Sim
{
    /// <summary>
    /// A PCG32 generator, chosen specifically over System.Random: System.Random's algorithm is not
    /// guaranteed identical between the Mono/IL2CPP runtime a phone uses and the .NET runtime a
    /// server would use, so two "identically seeded" System.Random instances on client and server
    /// could silently diverge. PCG32 is pure integer arithmetic with no runtime-specific behavior, so
    /// the same seed produces the same sequence everywhere -- required for a server to re-simulate a
    /// match and get bit-identical shot outcomes from a recorded seed.
    ///
    /// This is a struct and a mutable field, not a class: the caller (ShotSystem) owns one instance
    /// per match and threads it through Sim calls by ref, rather than this reaching for global
    /// mutable state. A server re-simulating several matches concurrently needs one independent
    /// stream per match, not one shared static generator.
    /// </summary>
    public struct DeterministicRandom
    {
        private ulong state;
        private readonly ulong inc;

        public DeterministicRandom(ulong seed, ulong sequence = 1)
        {
            state = 0UL;
            inc = (sequence << 1) | 1UL;
            state = Step(0UL, inc, out _);
            state = Step(state + seed, inc, out _);
        }

        private static ulong Step(ulong currentState, ulong increment, out uint output)
        {
            ulong oldState = currentState;
            ulong nextState = unchecked(oldState * 6364136223846793005UL + increment);
            uint xorshifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
            int rot = (int)(oldState >> 59);
            output = (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
            return nextState;
        }

        public uint NextUInt32()
        {
            state = Step(state, inc, out uint output);
            return output;
        }

        /// <summary>[0, 1) with 24 bits of precision -- matches a float mantissa, so every value is
        /// exactly representable.</summary>
        public float NextFloat01() => (NextUInt32() >> 8) * (1.0f / (1 << 24));

        public float NextRange(float min, float max) => min + (max - min) * NextFloat01();
    }
}
