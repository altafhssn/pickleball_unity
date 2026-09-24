namespace Pickleball.Sim
{
    /// <summary>
    /// The fixed rate and conversion math for match timing. A server replaying a recorded input log
    /// needs timing deltas that don't depend on real elapsed wall-clock time or per-device frame-rate
    /// jitter -- accumulating Time.deltaTime in float seconds is not guaranteed to round identically
    /// across devices, while counting discrete integer ticks is. The actual tick counter that
    /// advances over real time (Pickleball.Systems.MatchClock) lives on the Unity side since it needs
    /// Time.deltaTime; this is just the shared, engine-agnostic conversion both sides use.
    /// </summary>
    public static class MatchTick
    {
        public const int TickRate = 60;
        public const float SecondsPerTick = 1f / TickRate;

        public static int FromSeconds(float seconds) => (int)System.Math.Round(seconds * TickRate);
        public static float ToSeconds(int ticks) => ticks * SecondsPerTick;
    }
}
