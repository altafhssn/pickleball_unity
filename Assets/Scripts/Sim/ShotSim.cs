using System;
using System.Numerics;

namespace Pickleball.Sim
{
    /// <summary>
    /// The court's X/Z play area consumed by deterministic shot math. CourtDimensions creates the
    /// canonical instance shared with the painted lines and live fault checks; the explicit struct
    /// keeps alternate dimensions possible for isolated simulation tests.
    /// </summary>
    public readonly struct CourtBounds
    {
        public readonly Vector2 min;
        public readonly Vector2 max;

        public CourtBounds(Vector2 min, Vector2 max)
        {
            this.min = min;
            this.max = max;
        }
    }

    /// <summary>
    /// The shot-scoring and target-placement math, moved out of the MonoBehaviour ShotSystem used to
    /// own it. Pure functions over explicit inputs (including an explicit RNG stream) so a server can
    /// call the exact same code a client did to verify a submitted match log -- see ShotSystem for
    /// the thin Unity-facing adapter that still owns the Inspector-tunable values and the match's
    /// single DeterministicRandom instance.
    /// </summary>
    public static class ShotSim
    {
        // ============================================================
        // The swipe's three controls. A stroke has three measurable properties and each drives one
        // thing, so the player controls all three independently:
        //   direction  -- its angle (SwipeGesture.AimDirection): where across the court
        //   placement  -- its length (SwingDepth): how deep it lands
        //   speed      -- how fast it was drawn (SwingPace): how hard the ball travels
        // Distances are in DPI- and sensitivity-adjusted reference pixels.
        // ============================================================

        /// <summary>Stroke length that places a shot at full depth.</summary>
        public const float FullDepthSwipe = 240f;

        /// <summary>Shortest stroke that can reach full pace. A one- or two-frame flick's measured
        /// speed is mostly sampling noise, so a tiny twitch stays soft whatever speed it reads.</summary>
        public const float FullPaceMinSwipe = 100f;

        /// <summary>Placement: how deep the shot lands, 0..1, from the stroke's length alone. A
        /// short swipe lands short, a long one deep -- however fast it was drawn.</summary>
        public static float SwingDepth(float referenceDistance) =>
            SimMath.Clamp01(SimMath.Max(0f, referenceDistance) / FullDepthSwipe);

        /// <summary>Speed: how hard the ball is struck, 0..1, from how fast the stroke was drawn --
        /// independent of its length once it is past <see cref="FullPaceMinSwipe"/>. Holding never
        /// charges a shot.</summary>
        public static float SwingPace(float referenceDistance, float seconds, float fullPaceSwipeSpeed = 1200f)
        {
            float distance = SimMath.Max(0f, referenceDistance);
            float speed = distance / SimMath.Max(0.01f, seconds);
            return Math.Min(SimMath.Clamp01(speed / SimMath.Max(1f, fullPaceSwipeSpeed)),
                SimMath.Clamp01(distance / FullPaceMinSwipe));
        }
        // ============================================================
        // Gear stat -> gameplay effect bindings. One stat per gear slot (see GearRules): the paddle's
        // Power, the grip's Accuracy and the shoes' Speed.
        //
        // Every binding has the same "effect = cap * stat / (stat + k)" diminishing-returns shape, so
        // a fully upgraded slot is a clear edge but never a guaranteed win. These are pure and
        // stateless and only ever read a LoadoutStats the caller already resolved -- ShotSim never
        // touches MetaGameState itself. The AI plays with the player's own LoadoutStats, so both sides
        // run through exactly these curves.
        // ============================================================
        private static float DrEffect(float stat, float capFraction, float k) => capFraction * stat / (stat + k);

        /// <summary>Power (paddle): the ball arrives faster. Multiplies a shot's -- including a
        /// serve's -- flight time; capped at 22%.</summary>
        public static float PowerDurationMultiplier(float power) => 1f - DrEffect(power, 0.22f, 120f);

        /// <summary>Accuracy (grip): shots land where aimed. Multiplies CalculateTargetPosition's
        /// scatter term; capped at 45%.</summary>
        public static float AccuracyScatterMultiplier(float accuracy) => 1f - DrEffect(accuracy, 0.45f, 150f);

        /// <summary>Speed (shoes): the player covers the court faster. Not consumed by anything in this
        /// file -- PlayerController and OpponentAI apply it to their own movement, since it doesn't
        /// touch ball physics. Kept here so every gear-effect constant lives in one place. Capped at +30%.</summary>
        public static float SpeedMoveMultiplier(float speed) => 1f + DrEffect(speed, 0.30f, 140f);

        /// <summary>Speed's reach bonus: extra world units added to the contact reach; capped at 0.35.</summary>
        public static float SpeedReachBonus(float speed) => DrEffect(speed, 0.35f, 140f);

        // Long rallies wear both players down. "Per-rally" is the rally's total shot count (both
        // sides combined) -- the same currentRallyCount RallyManager already tracks. This is a match
        // rule, not a gear stat: it applies to both sides identically.
        public const int FatigueOnsetShot = 6;
        private const float FatigueDecayPerShotPastOnset = 0.05f;
        private const float FatigueMaxDecay = 0.35f; // quality never drops below 65% of its pre-fatigue value

        /// <summary>1 up to <see cref="FatigueOnsetShot"/>; decays 5%/shot past it, floored at a 35%
        /// total cut. A shotIndexInRally of 0 (the default for a caller that doesn't pass one) is
        /// always before onset, so this is a no-op unless a real rally position is supplied.</summary>
        public static float RallyFatigueMultiplier(int shotIndexInRally)
        {
            int shotsPastOnset = shotIndexInRally - FatigueOnsetShot;
            if (shotsPastOnset <= 0) return 1f;
            return 1f - SimMath.Clamp(shotsPastOnset * FatigueDecayPerShotPastOnset, 0f, FatigueMaxDecay);
        }

        public static float CalculateTimingScore(float timingError, out ShotQuality quality)
        {
            if (timingError < 0.08f)
            {
                quality = ShotQuality.Perfect;
                return 1.0f;
            }
            else if (timingError < 0.18f)
            {
                quality = ShotQuality.Great;
                return 0.85f;
            }
            else if (timingError < 0.32f)
            {
                quality = ShotQuality.Good;
                return 0.70f;
            }
            else if (timingError < 0.50f)
            {
                quality = ShotQuality.Weak;
                return 0.50f;
            }
            else
            {
                quality = ShotQuality.Miss;
                return 0.0f;
            }
        }

        /// <summary>The aimed landing spot from the swipe and shot type, before any accuracy scatter.
        /// Pure and RNG-free -- this is what an aim-preview line should draw (the player's *intent*),
        /// and it is the deterministic core CalculateTargetPosition adds scatter on top of. Splitting
        /// it out also stops the trajectory preview from having to touch the match RNG stream.</summary>
        public static Vector3 PredictTargetPosition(
            Vector3 startPos, Vector2 swipeVector, bool isPlayerHitting, ShotType shotType, CourtBounds bounds)
        {
            // The swipe vector is direction x placement: its length is how deep the shot lands.
            float depth = SimMath.Clamp01(swipeVector.Length());
            Vector2 direction = depth > 0.001f ? Vector2.Normalize(swipeVector) : Vector2.Zero;
            float targetX = SimMath.Clamp(direction.X * 4.2f, bounds.min.X, bounds.max.X);

            float depthRatio = isPlayerHitting ? depth : 1f - depth;

            float targetZ;
            if (shotType == ShotType.Dink)
            {
                float dinkMinZ = isPlayerHitting ? 0.8f : -2.4f;
                float dinkMaxZ = isPlayerHitting ? 2.4f : -0.8f;
                targetZ = SimMath.Lerp(dinkMinZ, dinkMaxZ, depthRatio);
                targetX = SimMath.Clamp(targetX, -3.2f, 3.2f);
            }
            else if (shotType == ShotType.Lob)
            {
                float deepMinZ = isPlayerHitting ? 1.8f : -8.8f;
                float deepMaxZ = isPlayerHitting ? 8.8f : -1.8f;
                targetZ = SimMath.Lerp(deepMinZ, deepMaxZ, depthRatio);
            }
            else if (shotType == ShotType.Topspin || shotType == ShotType.Smash)
            {
                float deepMinZ = isPlayerHitting ? 1f : -8.5f;
                float deepMaxZ = isPlayerHitting ? 8.5f : -1f;
                targetZ = SimMath.Lerp(deepMinZ, deepMaxZ, depthRatio);
            }
            else
            {
                targetZ = (isPlayerHitting ? 1f : -1f) * SimMath.Lerp(0.8f, bounds.max.Y - 0.3f, depth);
            }

            return new Vector3(targetX, 0f, targetZ);
        }

        public static Vector3 CalculateTargetPosition(
            Vector3 startPos, Vector2 swipeVector, float shotQuality, bool isPlayerHitting, ShotType shotType,
            CourtBounds bounds, ref DeterministicRandom rng, LoadoutStats loadout = default(LoadoutStats))
        {
            Vector3 aimed = PredictTargetPosition(startPos, swipeVector, isPlayerHitting, shotType, bounds);

            float inaccuracy = (1.0f - shotQuality) * 1.2f * AccuracyScatterMultiplier(loadout.accuracy);
            float targetX = aimed.X + rng.NextRange(-inaccuracy, inaccuracy);
            float targetZ = aimed.Z + rng.NextRange(-inaccuracy, inaccuracy);
            if (shotType == ShotType.Dink)
                targetZ = (isPlayerHitting ? 1f : -1f) * SimMath.Clamp(SimMath.Abs(targetZ), 0.7f, 2.4f);

            return new Vector3(targetX, 0f, targetZ);
        }

        public static ShotData EvaluateAndBuildShot(
            int hitterId,
            Vector3 startPos,
            Vector3 idealStrikePos,
            Vector3 actualPlayerPos,
            float timingError,
            Vector2 swipeVector,
            ShotType shotType,
            CourtBounds bounds,
            float defaultArcHeight,
            ref DeterministicRandom rng,
            LoadoutStats loadout = default(LoadoutStats),
            int shotIndexInRally = 0,
            float pace01 = -1f)
        {
            float timingScore = CalculateTimingScore(timingError, out ShotQuality quality);

            float distFromIdeal = Vector3.Distance(actualPlayerPos, idealStrikePos);
            float positionScore = SimMath.Clamp01(1.0f - (distFromIdeal / 2.0f));

            // Strength is intent, not accuracy: a perfectly timed gentle push is a good shot.
            float swipeAccuracy = 1f;
            // The swipe vector's length is placement (depth); pace is how hard it is struck. A caller
            // with only one strength value (the AI) passes none and gets pace = depth.
            float depth = SimMath.Clamp01(swipeVector.Length());
            float pace = pace01 >= 0f ? SimMath.Clamp01(pace01) : depth;

            float compositeScore = (timingScore * 0.50f) + (positionScore * 0.30f) + (swipeAccuracy * 0.20f);

            // Rally fatigue: quality itself (the timing-only Perfect/Great/.../Miss readout above)
            // deliberately does not decay; only the mechanical compositeScore that drives accuracy
            // and duration does, so "quality" keeps reading as pure swing-timing feedback.
            compositeScore *= RallyFatigueMultiplier(shotIndexInRally);
            compositeScore = SimMath.Clamp01(compositeScore);

            Vector3 targetPos = CalculateTargetPosition(startPos, swipeVector, compositeScore, hitterId == 0, shotType, bounds, ref rng, loadout);

            float bounceMult = 1.0f;
            float spin = 0f;

            switch (shotType)
            {
                case ShotType.Dink:
                    bounceMult = 0.45f;
                    spin = -0.5f;
                    break;

                case ShotType.Topspin:
                    bounceMult = 1.35f;
                    spin = 1.0f;
                    break;

                case ShotType.Lob:
                    bounceMult = 0.9f;
                    spin = 0f;
                    break;

                case ShotType.Slice:
                    bounceMult = 0.75f;
                    spin = -1.0f;
                    break;

                case ShotType.Smash:
                    // The put-away: flat, fast, and it kicks hard off the bounce. Duration scales with
                    // the swing so a well-timed smash is close to unreturnable and a mistimed one is
                    // merely a fast drive the opponent can still dig out.
                    bounceMult = 1.6f;
                    spin = 0.6f;
                    break;

                case ShotType.Serve:
                    bounceMult = 1.0f;
                    break;

                case ShotType.Flat:
                default:
                    bounceMult = 1.0f;
                    break;
            }

            // Flight time follows distance: kitchen exchanges are quick, deep resets buy time.
            Vector3 groundTravel = targetPos - startPos;
            groundTravel.Y = 0f;
            float speed;
            switch (shotType)
            {
                case ShotType.Dink: speed = 6.4f; break;
                case ShotType.Lob: speed = 8.2f; break;
                case ShotType.Slice: speed = 10.2f; break;
                case ShotType.Smash: speed = SimMath.Lerp(15f, 20f, compositeScore); break;
                case ShotType.Topspin: speed = SimMath.Lerp(12f, 16f, compositeScore); break;
                default: speed = SimMath.Lerp(10f, 13f, compositeScore); break;
            }
            speed *= SimMath.Lerp(0.55f, 1f, pace);
            float duration = SimMath.Clamp(groundTravel.Length() / speed,
                shotType == ShotType.Lob ? 1.45f : 0.42f, 2.9f);
            // Even a baseline drop needs enough lift to clear the net near its landing point.
            float height = CalculateArcHeight(startPos, targetPos, shotType, defaultArcHeight, pace);

            // Power speeds up the attacking strokes. Dinks and lobs are touch shots whose value is
            // their softness and hang time, so a stronger paddle leaves them alone.
            if (shotType != ShotType.Dink && shotType != ShotType.Lob)
                duration *= PowerDurationMultiplier(loadout.power);

            return new ShotData(hitterId, shotType, startPos, targetPos, height, duration)
            {
                timingScore = timingScore,
                positionScore = positionScore,
                swipeAccuracy = swipeAccuracy,
                compositeScore = compositeScore,
                quality = quality,
                bounceMultiplier = bounceMult,
                spinRate = spin
            };
        }

        public static bool IsInBounds(Vector3 pos, CourtBounds bounds)
        {
            float absZ = SimMath.Abs(pos.Z);
            return SimMath.Abs(pos.X) <= bounds.max.X && absZ >= CourtDimensions.NetLineMargin && absZ <= bounds.max.Y;
        }

        /// <param name="pace01">How hard the drive was struck. A soft drive loops higher on its way to
        /// the same spot; a hard one stays flat. Touch shots (dink, lob, smash, serve) keep their shape.</param>
        public static float CalculateArcHeight(Vector3 start, Vector3 target, ShotType type, float defaultArc = 2.5f,
            float pace01 = 1f)
        {
            float height;
            switch (type)
            {
                case ShotType.Dink: height = 0.65f; break;
                case ShotType.Topspin: height = 0.85f; break;
                case ShotType.Slice: height = 0.9f; break;
                case ShotType.Lob: height = 4.5f; break;
                case ShotType.Smash: height = 0.3f; break;
                case ShotType.Serve: height = 2f; break;
                default: height = SimMath.Clamp(defaultArc * 0.42f, 0.8f, 1.2f); break;
            }
            if (type == ShotType.Flat || type == ShotType.Topspin || type == ShotType.Slice)
                height *= SimMath.Lerp(1.5f, 1f, SimMath.Clamp01(pace01));
            return SimMath.Max(height, BallFlight.ClearanceArc(start, target,
                type == ShotType.Dink ? 1.12f : 1.06f));
        }
    }
}
