using System;
using System.Numerics;

namespace Pickleball.Sim
{
    /// <summary>Shared flight, rebound and contact planning for both players.</summary>
    public static class BallFlight
    {
        public const float NetHeight = 0.9f;
        public const float ContactBufferSeconds = 0.18f;
        /// <summary>How long after the ball leaves a player's reach a swing still connects, as a late
        /// shot. Humans release a swipe a little after they decide to hit; without this, a swing that
        /// the timing ring scored as merely "late" whiffed outright.</summary>
        public const float LateContactGraceSeconds = 0.1f;
        /// <summary>Bottom of the rebound-speed clamp. At the old 2.6 nearly every rebound peaked at
        /// ankle height (0.19) and stayed above the 0.12 contact floor for only ~170 ms.</summary>
        public const float MinReboundSpeed = 3.2f;
        private const float ContactScanStep = 1f / 240f;

        public static Vector3 Sample(Vector3 start, Vector3 target, float arc, float t)
        {
            t = SimMath.Clamp01(t);
            Vector3 p = Vector3.Lerp(start, target, t);
            p.Y += 4f * arc * t * (1f - t);
            return p;
        }

        public static float ClearanceArc(Vector3 start, Vector3 target, float clearance)
        {
            float dz = target.Z - start.Z;
            if (SimMath.Abs(dz) < 0.001f) return 0f;
            float t = -start.Z / dz;
            if (t <= 0f || t >= 1f) return 0f;
            float baseY = SimMath.Lerp(start.Y, target.Y, t);
            return SimMath.Max(0f, (clearance - baseY) / (4f * t * (1f - t)));
        }

        public static void Rebound(Vector3 start, Vector3 target, float arc, float duration,
            float bounceMultiplier, float spin, out Vector3 end, out float height, out float seconds)
        {
            duration = SimMath.Max(0.1f, duration);
            float multiplier = bounceMultiplier > 0.01f ? bounceMultiplier : 1f;
            const float gravity = 18f;
            float impactSpeed = (4f * arc + start.Y - target.Y) / duration;
            float reboundSpeed = SimMath.Clamp(impactSpeed * 0.44f, MinReboundSpeed, 5.3f);
            height = reboundSpeed * reboundSpeed / (2f * gravity);
            seconds = 2f * reboundSpeed / gravity;
            Vector3 groundVelocity = (target - start) / duration;
            groundVelocity.Y = 0f;
            // Topspin kicks forward; backspin checks the bounce. Neither is sidespin.
            float retention = SimMath.Clamp(0.48f * multiplier + spin * 0.025f, 0.18f, 0.78f);
            end = target + groundVelocity * (retention * seconds);
        }

        public static bool CanContact(Vector3 player, Vector3 ball, int side, float reach,
            bool bounced, bool mustBounce)
        {
            float sign = side == 0 ? -1f : 1f;
            if (ball.Z * sign <= 0f || ball.Y < 0.12f || ball.Y > 2.7f) return false;
            if (!bounced && (mustBounce || player.Z * sign <= CourtDimensions.KitchenDepth + 0.12f))
                return false;
            Vector3 delta = ball - player;
            delta.Y = 0f;
            return delta.LengthSquared() <= reach * reach;
        }

        /// <summary>Where to stand and when to strike. Returns false when no contact is reachable in
        /// time -- the stance and arrival are then only a best effort towards the ball.</summary>
        public static bool PlanContact(Vector3 start, Vector3 target, float arc, float duration,
            float bounceMultiplier, float spin, Vector3 player, int side, bool mustBounce,
            float moveSpeed, float reach, float reactionDelay, out Vector3 stance, out float arrival,
            float elapsed = 0f)
        {
            float sign = side == 0 ? -1f : 1f;
            var ball = new Trajectory(start, target, arc, duration, bounceMultiplier, spin);
            if (!mustBounce)
            {
                // Of the reachable volleys, take the one that costs the least movement -- not the
                // earliest one. Returning the first match meant the lowest t, which is the point
                // where the ball is still closest to the net, so every single intercept dragged the
                // receiver onto the kitchen line and left them there for the next ball. In singles
                // that is exactly how you get passed: hold your ground, let the ball come to you,
                // and only close in when it genuinely lands short (which this still does, because
                // then the nearest reachable contact *is* the forward one).
                float bestTravel = float.MaxValue;
                Vector3 bestStance = default;
                float bestArrival = 0f;
                for (int i = 8; i <= 19; i++)
                {
                    float t = i / 20f;
                    Vector3 p = Sample(start, target, arc, t);
                    Vector3 candidate = Stance(p, sign);
                    if (duration * t < elapsed) continue;
                    float available = SimMath.Max(0f, duration * t - elapsed - reactionDelay);
                    if (!CanContact(candidate, p, side, reach, false, false)) continue;

                    float travel = Vector3.Distance(player, candidate);
                    if (travel > moveSpeed * available + reach * 0.35f) continue;
                    if (travel >= bestTravel) continue;

                    bestTravel = travel;
                    bestStance = candidate;
                    bestArrival = duration * t - elapsed;
                }
                if (bestTravel < float.MaxValue)
                {
                    stance = bestStance;
                    arrival = CentredArrival(ball, stance, side, false, reach, elapsed, bestArrival,
                        EarliestArrival(bestTravel, moveSpeed, reach, reactionDelay));
                    return true;
                }
            }
            Vector3 end = ball.End;
            float height = ball.Height;
            float seconds = ball.Seconds;
            float bestBounceTravel = float.MaxValue;
            stance = default;
            arrival = 0f;
            for (int i = 4; i <= 17; i++)
            {
                float t = i / 20f;
                float remaining = duration + seconds * t - elapsed;
                if (remaining < 0.02f) continue;
                Vector3 contact = Sample(target, end, height, t);
                Vector3 candidate = Stance(contact, sign);
                if (!CanContact(candidate, contact, side, reach, true, mustBounce)) continue;
                float travel = Vector3.Distance(player, candidate);
                if (travel > moveSpeed * SimMath.Max(0f, remaining - reactionDelay) + reach * 0.35f) continue;
                if (travel >= bestBounceTravel) continue;
                bestBounceTravel = travel;
                stance = candidate;
                arrival = remaining;
            }
            if (bestBounceTravel < float.MaxValue)
            {
                arrival = CentredArrival(ball, stance, side, mustBounce, reach, elapsed, arrival,
                    EarliestArrival(bestBounceTravel, moveSpeed, reach, reactionDelay));
                return true;
            }
            float bounceT = SimMath.Clamp((elapsed - duration) / seconds + 0.08f, 0.42f, 0.9f);
            stance = Stance(Sample(target, end, height, bounceT), sign);
            arrival = SimMath.Max(0f, duration + seconds * bounceT - elapsed);
            return false;
        }

        /// <summary>Where the ball is <paramref name="elapsed"/> seconds after it was struck, following
        /// the flight and its rebound. False once the rebound has ended (the ball is dead).</summary>
        public static bool PositionAt(Vector3 start, Vector3 target, float arc, float duration,
            float bounceMultiplier, float spin, float elapsed, out Vector3 position)
        {
            var ball = new Trajectory(start, target, arc, duration, bounceMultiplier, spin);
            return ball.TryPosition(elapsed, out position, out _);
        }

        /// <summary>The span of shot time around <paramref name="atElapsed"/> during which a player
        /// standing at <paramref name="player"/> can legally strike the ball, following the flight and
        /// its rebound. False if the ball cannot be struck at <paramref name="atElapsed"/> itself.</summary>
        public static bool ContactWindow(Vector3 start, Vector3 target, float arc, float duration,
            float bounceMultiplier, float spin, Vector3 player, int side, bool mustBounce, float reach,
            float atElapsed, out float open, out float close)
        {
            var ball = new Trajectory(start, target, arc, duration, bounceMultiplier, spin);
            return ball.Window(player, side, reach, mustBounce, atElapsed, out open, out close);
        }

        /// <summary>Scans the ball's path from <paramref name="fromElapsed"/> towards
        /// <paramref name="toElapsed"/> (either direction) and returns the first moment a player at
        /// <paramref name="player"/> could legally strike it, and where the ball was then.</summary>
        public static bool FindContact(Vector3 start, Vector3 target, float arc, float duration,
            float bounceMultiplier, float spin, Vector3 player, int side, bool mustBounce, float reach,
            float fromElapsed, float toElapsed, out float contactElapsed, out Vector3 contactPosition)
        {
            var ball = new Trajectory(start, target, arc, duration, bounceMultiplier, spin);
            float direction = toElapsed >= fromElapsed ? 1f : -1f;
            int steps = (int)Math.Ceiling(SimMath.Abs(toElapsed - fromElapsed) / ContactScanStep);
            for (int i = 0; i <= steps; i++)
            {
                float elapsed = i == steps ? toElapsed : fromElapsed + direction * i * ContactScanStep;
                if (ball.Hittable(elapsed, player, side, reach, mustBounce, out contactPosition))
                {
                    contactElapsed = elapsed;
                    return true;
                }
            }
            contactElapsed = 0f;
            contactPosition = default;
            return false;
        }

        /// <summary>Soonest the player could be standing at a stance <paramref name="travel"/> away,
        /// with the same reach slack PlanContact's reachability check allows.</summary>
        private static float EarliestArrival(float travel, float moveSpeed, float reach, float reactionDelay)
        {
            return reactionDelay + SimMath.Max(0f, travel - reach * 0.35f) / SimMath.Max(0.01f, moveSpeed);
        }

        /// <summary>The candidate search above picks the stance needing the least movement, which for
        /// a ball coming towards the player is the *last* sample still in reach -- after a bounce, the
        /// final frame before the ball drops under the contact floor. Timing the strike there put the
        /// ring's "perfect" moment on the edge of the window, so any release after it whiffed. Keep the
        /// stance, but time the strike to the middle of the span the ball is hittable from it.</summary>
        private static float CentredArrival(in Trajectory ball, Vector3 stance, int side, bool mustBounce,
            float reach, float elapsed, float plannedArrival, float earliestArrival)
        {
            if (!ball.Window(stance, side, reach, mustBounce, elapsed + plannedArrival, out float open, out float close))
                return plannedArrival;
            open = SimMath.Max(open, elapsed + earliestArrival);
            if (close < open) return plannedArrival;
            return SimMath.Max(0f, (open + close) * 0.5f - elapsed);
        }

        /// <summary>One shot's full path -- the flight, then its single rebound -- sampled by shot
        /// time, matching BallController's own playback.</summary>
        private readonly struct Trajectory
        {
            private readonly Vector3 start;
            private readonly Vector3 target;
            private readonly float arc;
            private readonly float duration;
            public readonly Vector3 End;
            public readonly float Height;
            public readonly float Seconds;

            public Trajectory(Vector3 start, Vector3 target, float arc, float duration,
                float bounceMultiplier, float spin)
            {
                this.start = start;
                this.target = target;
                this.arc = arc;
                this.duration = duration;
                Rebound(start, target, arc, duration, bounceMultiplier, spin, out End, out Height, out Seconds);
            }

            public bool TryPosition(float elapsed, out Vector3 position, out bool bounced)
            {
                bounced = elapsed >= duration;
                if (!bounced)
                {
                    position = Sample(start, target, arc, SimMath.Max(0f, elapsed) / duration);
                    return true;
                }
                float reboundElapsed = elapsed - duration;
                position = Sample(target, End, Height, reboundElapsed / Seconds);
                return reboundElapsed < Seconds; // past the rebound: second bounce, the ball is dead
            }

            public bool Hittable(float elapsed, Vector3 player, int side, float reach, bool mustBounce,
                out Vector3 position)
            {
                if (elapsed < 0f || !TryPosition(elapsed, out position, out bool bounced))
                {
                    position = default;
                    return false;
                }
                return CanContact(player, position, side, reach, bounced, mustBounce);
            }

            public bool Window(Vector3 player, int side, float reach, bool mustBounce, float atElapsed,
                out float open, out float close)
            {
                open = close = atElapsed;
                if (!Hittable(atElapsed, player, side, reach, mustBounce, out _)) return false;
                while (Hittable(open - ContactScanStep, player, side, reach, mustBounce, out _)) open -= ContactScanStep;
                while (Hittable(close + ContactScanStep, player, side, reach, mustBounce, out _)) close += ContactScanStep;
                return true;
            }
        }

        private static Vector3 Stance(Vector3 contact, float sign)
        {
            return new Vector3(SimMath.Clamp(contact.X, -4.7f, 4.7f), 1f,
                sign * SimMath.Clamp(contact.Z * sign + 0.55f, 0.6f, 10.2f));
        }
    }
}
