using UnityEngine;
using Pickleball.Gameplay;
using Sim = Pickleball.Sim;

namespace Pickleball.Systems
{
    /// <summary>
    /// Thin Unity-facing adapter over Pickleball.Sim.ShotSim. The actual shot-scoring math now lives
    /// in a netstandard2.0 assembly with zero UnityEngine dependency (Assets/Scripts/Sim), so the
    /// identical code can run unmodified on a future server for match re-simulation and anti-cheat.
    /// This class exists only to hold the Inspector-tunable values, own the match's single
    /// deterministic RNG stream, and convert between UnityEngine.Vector3 and the engine-agnostic
    /// System.Numerics.Vector3 the sim uses. Its public API is unchanged from before this split.
    /// </summary>
    public class ShotSystem : MonoBehaviour
    {
        private void OnDestroy() { if (Instance == this) Instance = null; }
        [Header("Shot Tuning")]
        [SerializeField] private float defaultArcHeight = 2.5f;

        [Header("Determinism")]
        [Tooltip("Randomized at Awake until a real match-start message assigns a server-issued seed. Uncheck to pin a fixed seed for local testing.")]
        [SerializeField] private bool useRandomSeed = true;
        [SerializeField] private int fixedSeedForTesting = 12345;

        public static ShotSystem Instance { get; private set; }

        private Sim.DeterministicRandom rng;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            ulong seed = useRandomSeed ? (ulong)System.Guid.NewGuid().GetHashCode() : (ulong)fixedSeedForTesting;
            rng = new Sim.DeterministicRandom(seed);
        }

        /// <summary>Reseeds the match's shared RNG stream. Will be called once a real match-start
        /// message carries a server-assigned seed; until then Awake's seed stands.</summary>
        public void SeedMatch(ulong seed) => rng = new Sim.DeterministicRandom(seed);

        /// <summary>
        /// The single source of gameplay-outcome-affecting randomness for this match: shot
        /// inaccuracy today, and the intended home for AI shot-selection/aim/timing jitter and serve
        /// targeting once those are threaded through too. Cosmetic randomness -- audio pitch
        /// variance, screen shake -- deliberately does not route through here: only rolls that affect
        /// score or ball trajectory need to be reproducible by a re-simulating server from a recorded
        /// seed.
        /// </summary>
        public float NextRandomRange(float min, float max) => rng.NextRange(min, max);

        public ShotData EvaluateAndBuildShot(
            int hitterId,
            Vector3 startPos,
            Vector3 idealStrikePos,
            Vector3 actualPlayerPos,
            float timingError,          // Absolute delta from ideal hit moment (seconds)
            Vector2 swipeVector,         // Direction, with placement (depth, 0..1) as its length
            ShotType shotType = ShotType.Flat,
            Sim.LoadoutStats loadout = default(Sim.LoadoutStats),
            int shotIndexInRally = 0,
            float pace01 = -1f)          // How hard it is struck, 0..1; < 0 means "same as depth"
        {
            Sim.ShotData simShot = Sim.ShotSim.EvaluateAndBuildShot(
                hitterId,
                ToSim(startPos),
                ToSim(idealStrikePos),
                ToSim(actualPlayerPos),
                timingError,
                ToSim(swipeVector),
                (Sim.ShotType)shotType,
                Bounds(),
                defaultArcHeight,
                ref rng,
                loadout,
                shotIndexInRally,
                pace01);

            return FromSim(simShot);
        }

        public float CalculateTimingScore(float timingError, out ShotQuality quality)
        {
            float score = Sim.ShotSim.CalculateTimingScore(timingError, out Sim.ShotQuality simQuality);
            quality = (ShotQuality)simQuality;
            return score;
        }

        public Vector3 CalculateTargetPosition(Vector3 startPos, Vector2 swipeVector, float shotQuality, bool isPlayerHitting, ShotType shotType = ShotType.Flat, Sim.LoadoutStats loadout = default(Sim.LoadoutStats))
        {
            System.Numerics.Vector3 result = Sim.ShotSim.CalculateTargetPosition(
                ToSim(startPos), ToSim(swipeVector), shotQuality, isPlayerHitting, (Sim.ShotType)shotType,
                Bounds(), ref rng, loadout);
            return FromSim(result);
        }

        /// <summary>The aimed landing spot with no accuracy scatter and, crucially, no RNG draw --
        /// safe to call every frame from the aim preview without advancing the match's shared
        /// deterministic stream (which would desync a re-simulating server).</summary>
        public Vector3 PredictTargetPosition(Vector3 startPos, Vector2 swipeVector, bool isPlayerHitting, ShotType shotType = ShotType.Flat)
        {
            System.Numerics.Vector3 result = Sim.ShotSim.PredictTargetPosition(
                ToSim(startPos), ToSim(swipeVector), isPlayerHitting, (Sim.ShotType)shotType, Bounds());
            return FromSim(result);
        }

        /// <summary>
        /// Whether a landing spot is inside the court lines. Nothing in the match loop checked this
        /// before this system existed -- CalculateTargetPosition clamps the *aimed* target into
        /// bounds but then adds inaccuracy on top with no second clamp, and the result was never
        /// validated against the lines afterwards.
        /// </summary>
        public bool IsInBounds(Vector3 pos) => Sim.ShotSim.IsInBounds(ToSim(pos), Bounds());

        public float PredictArcHeight(Vector3 start, Vector3 target, ShotType type, float pace01 = 1f) =>
            Sim.ShotSim.CalculateArcHeight(ToSim(start), ToSim(target), (Sim.ShotType)type, defaultArcHeight, pace01);

        private static Sim.CourtBounds Bounds() => Sim.CourtDimensions.PlayBounds;

        private static System.Numerics.Vector3 ToSim(Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
        private static System.Numerics.Vector2 ToSim(Vector2 v) => new System.Numerics.Vector2(v.x, v.y);
        private static Vector3 FromSim(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        private static ShotData FromSim(Sim.ShotData s)
        {
            return new ShotData(s.hitterId, (ShotType)s.shotType, FromSim(s.startPosition), FromSim(s.targetPosition), s.arcHeight, s.duration)
            {
                timingScore = s.timingScore,
                positionScore = s.positionScore,
                swipeAccuracy = s.swipeAccuracy,
                compositeScore = s.compositeScore,
                quality = (ShotQuality)s.quality,
                bounceMultiplier = s.bounceMultiplier,
                spinRate = s.spinRate
            };
        }
    }
}
