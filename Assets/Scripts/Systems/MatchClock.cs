using UnityEngine;
using Pickleball.Sim;

namespace Pickleball.Systems
{
    /// <summary>
    /// Fixed-rate tick counter that score-relevant gameplay timing reads instead of Time.time -- see
    /// MatchTick for why discrete ticks matter for a future replaying server. Only the strike-timing
    /// path (InputManager's release timestamp, PlayerController's ideal-strike scheduling) reads this;
    /// the two-second pause between points has no effect on match outcome and is deliberately left on
    /// Unity's Invoke rather than converted for its own sake.
    /// </summary>
    public class MatchClock : MonoBehaviour
    {
        public static MatchClock Instance { get; private set; }

        private float accumulator;
        public int CurrentTick { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }
        }

        private void Update()
        {
            accumulator += Time.deltaTime;
            while (accumulator >= MatchTick.SecondsPerTick)
            {
                accumulator -= MatchTick.SecondsPerTick;
                CurrentTick++;
            }
        }

        /// <summary>Tick-quantized seconds since this component started ticking, or Time.time as a
        /// fallback if no MatchClock exists yet -- keeps every caller safe to call unconditionally.</summary>
        public static float NowSeconds()
        {
            return Instance != null ? MatchTick.ToSeconds(Instance.CurrentTick) : Time.time;
        }
    }
}
