using System;

namespace Pickleball.Sim
{
    /// <summary>
    /// This assembly has no UnityEngine reference (see Pickleball.Sim.asmdef), so the handful of
    /// Mathf helpers the shot math relies on have to be reimplemented rather than borrowed.
    /// </summary>
    internal static class SimMath
    {
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Abs(float v) => v < 0f ? -v : v;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
    }
}
