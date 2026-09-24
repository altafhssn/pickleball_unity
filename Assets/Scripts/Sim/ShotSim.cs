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
        /// <summary>Physical gesture speed controls strength; holding longer never charges a shot.</summary>
        public static float GesturePower(float referenceDistance, float seconds)
        {
            float speed = referenceDistance / SimMath.Max(0.01f, seconds);
            return SimMath.Clamp01(speed / 1200f);
        }
        /// <summary>How far past the net (in Z) a ball must land to count as in. bounds.min.Y is 0.5,
        /// which the fault check used directly -- so a legal, tightly-placed dink landing 0.1-0.5 past
        /// the net was called OUT on both sides, a 1-unit dead strip straddling the net. Net clearance
        /// (a ball that fails to cross at all) is enforced separately by BallController, so this only
        /// needs to exclude a ball that lands short of the net line itself.</summary>
        // ============================================================
        // Gear stat -> gameplay effect bindings (Docs/GearProgression.md#1-stats).
        //
        // Every binding has the same "effect = cap * stat / (stat + k)" diminishing-returns shape: a
        // maxed loadout carries ~17x the raw stat total of a starter, but this keeps the actual
        // gameplay advantage capped at a few tens of percent, which is what makes Photon PvP playable
        // across a wide gear spread. These are pure, stateless, and only ever read a LoadoutStats the
        // caller already resolved -- ShotSim still never touches MetaGameState/GearCatalog itself.
        // ============================================================
        private static float DrEffect(float stat, float capFraction, float k) => capFraction * stat / (stat + k);

        /// <summary>Power: ball arrives faster. Multiplies a shot's final duration; capped at 22%.</summary>
        public static float PowerDurationMultiplier(float power) => 1f - DrEffect(power, 0.22f, 120f);

        /// <summary>Control: shots land where aimed. Multiplies CalculateTargetPosition's scatter
        /// term; capped at 45%.</summary>
        public static float ControlScatterMultiplier(float control) => 1f - DrEffect(control, 0.45f, 150f);

        /// <summary>Spin: harder kick/skid off the bounce. Multiplies spinRate, and (for Topspin/Slice
        /// only) how far bounceMultiplier deviates from 1; capped at +40%.</summary>
        public static float SpinMultiplier(float spin) => 1f + DrEffect(spin, 0.40f, 130f);

        /// <summary>Serve: free points. Multiplies a serve's duration; capped at 18%. Not consumed by
        /// EvaluateAndBuildShot -- both PlayerController.ExecuteServe and OpponentAI.EnterServeMode
        /// build their ShotData directly rather than through the sim (a pre-existing asymmetry, not
        /// introduced here), so the caller applies this to its own serve duration.</summary>
        public static float ServeDurationMultiplier(float serve) => 1f - DrEffect(serve, 0.18f, 110f);

        /// <summary>Speed: reach balls you couldn't before. Not consumed by anything in this file --
        /// PlayerController applies these directly to its own move speed and miss-reach distance,
        /// since neither touches ball physics. Kept here anyway so every gear-effect constant lives in
        /// one place. Move-speed multiplier capped at +30%.</summary>
        public static float SpeedMoveMultiplier(float speed) => 1f + DrEffect(speed, 0.30f, 140f);

        /// <summary>Speed's reach bonus: extra world units added to the miss-reach distance; capped at 0.35.</summary>
        public static float SpeedReachBonus(float speed) => DrEffect(speed, 0.35f, 140f);

        // Stamina resists per-rally quality decay. "Per-rally" is read as the rally's total shot
        // count (both sides combined) -- the same currentRallyCount RallyManager already tracks --
        // not the hitter's own personal swing count, since that needs no new tracking and matches
        // the doc's "long rallies" framing (both players are tiring, not just one).
        private const float StaminaBaseOnsetShot = 6f;
        private const float StaminaOnsetRange = 6f; // cap: onset shot 6 (no stamina) .. 12 (maxed)
        private const float StaminaK = 100f;
        private const float FatigueDecayPerShotPastOnset = 0.05f;
        private const float FatigueMaxDecay = 0.35f; // quality never drops below 65% of its pre-fatigue value

        /// <summary>The rally shot index (1 = the serve) at which fatigue starts cutting into quality
        /// for a hitter with this much Stamina.</summary>
        public static int StaminaOnsetShot(float stamina)
        {
            return (int)Math.Round(StaminaBaseOnsetShot + StaminaOnsetRange * stamina / (stamina + StaminaK), MidpointRounding.AwayFromZero);
        }

        /// <summary>1 before onsetShotIndex; decays 5%/shot past it, floored at a 35% total cut.
        /// A shotIndexInRally of 0 (the default for any caller that doesn't pass one, e.g. the AI)
        /// is always before onset, so this is a no-op unless a real rally position is supplied.</summary>
        public static float RallyFatigueMultiplier(int shotIndexInRally, int onsetShotIndex)
        {
            int shotsPastOnset = shotIndexInRally - onsetShotIndex;
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
            float power = SimMath.Clamp01(swipeVector.Length());
            Vector2 direction = power > 0.001f ? Vector2.Normalize(swipeVector) : Vector2.Zero;
            float targetX = SimMath.Clamp(direction.X * 4.2f, bounds.min.X, bounds.max.X);

            float depthRatio = isPlayerHitting ? power : 1f - power;

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
                targetZ = (isPlayerHitting ? 1f : -1f) * SimMath.Lerp(0.8f, bounds.max.Y - 0.3f, power);
            }

            return new Vector3(targetX, 0f, targetZ);
        }

        public static Vector3 CalculateTargetPosition(
            Vector3 startPos, Vector2 swipeVector, float shotQuality, bool isPlayerHitting, ShotType shotType,
            CourtBounds bounds, ref DeterministicRandom rng, LoadoutStats loadout = default(LoadoutStats))
        {
            Vector3 aimed = PredictTargetPosition(startPos, swipeVector, isPlayerHitting, shotType, bounds);

            float inaccuracy = (1.0f - shotQuality) * 1.2f * ControlScatterMultiplier(loadout.control);
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
            int shotIndexInRally = 0)
        {
            float timingScore = CalculateTimingScore(timingError, out ShotQuality quality);

            float distFromIdeal = Vector3.Distance(actualPlayerPos, idealStrikePos);
            float positionScore = SimMath.Clamp01(1.0f - (distFromIdeal / 2.0f));

            // Strength is intent, not accuracy: a perfectly timed gentle push is a good shot.
            float swipeAccuracy = 1f;
            float power = SimMath.Clamp01(swipeVector.Length());

            float compositeScore = (timingScore * 0.50f) + (positionScore * 0.30f) + (swipeAccuracy * 0.20f);

            // Stamina: a shotIndexInRally of 0 (the default) is always below StaminaOnsetShot's
            // minimum of 6, so this is a no-op for any caller that doesn't pass a real rally
            // position -- quality itself (the timing-only Perfect/Great/.../Miss readout above)
            // deliberately does not decay; only the mechanical compositeScore that drives accuracy
            // and duration does, so "quality" keeps reading as pure swing-timing feedback.
            compositeScore *= RallyFatigueMultiplier(shotIndexInRally, StaminaOnsetShot(loadout.stamina));
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
            speed *= SimMath.Lerp(0.55f, 1f, power);
            float duration = SimMath.Clamp(groundTravel.Length() / speed,
                shotType == ShotType.Lob ? 1.45f : 0.42f, 2.9f);
            // Even a baseline drop needs enough lift to clear the net near its landing point.
            float height = CalculateArcHeight(startPos, targetPos, shotType, defaultArcHeight);

            // Spin: harder kick/skid -- scales
            // spinRate uniformly, and additionally scales how far bounceMultiplier deviates from 1
            // for the two shot types whose bounce is spin-driven (Topspin's kick, Slice's skid); the
            // other types' bounceMult is a fixed physical property of the shot shape, not spin, so it
            // is left alone.
            if (shotType != ShotType.Dink && shotType != ShotType.Lob)
                duration *= PowerDurationMultiplier(loadout.power);
            spin *= SpinMultiplier(loadout.spin);
            if (shotType == ShotType.Topspin || shotType == ShotType.Slice)
            {
                bounceMult = 1f + (bounceMult - 1f) * SpinMultiplier(loadout.spin);
            }

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

        public static float CalculateArcHeight(Vector3 start, Vector3 target, ShotType type, float defaultArc = 2.5f)
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
            return SimMath.Max(height, BallFlight.ClearanceArc(start, target,
                type == ShotType.Dink ? 1.12f : 1.06f));
        }
    }
}
