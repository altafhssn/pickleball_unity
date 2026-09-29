using System;
using UnityEngine;
using Pickleball.VFX;
using Sim = Pickleball.Sim;

namespace Pickleball.Gameplay
{
    public enum BallState
    {
        Idle,
        InFlight,
        Bouncing,
        Bounced,
        Out
    }

    public class BallController : MonoBehaviour
    {
        [Header("Ball Visuals & Physics")]
        [SerializeField] private Transform ballVisual;
        [SerializeField] private float strikeZoneDistanceThreshold = 1.5f;

        private ShotData currentShot;
        private float flightTimer = 0f;
        private bool isFlightActive = false;
        private bool strikeZoneTriggered = false;
        private bool hasFaulted = false;
        
        private Vector3 bounceStart;
        private Vector3 bounceTarget;
        private float bounceArcHeight;
        private float bounceDuration;

        public BallState State { get; private set; } = BallState.Idle;
        public ShotData CurrentShot => currentShot;
        public Vector3 TargetLandingPosition => currentShot.targetPosition;
        public float FlightProgress => State == BallState.Bouncing ? (bounceDuration > 0 ? Mathf.Clamp01(flightTimer / bounceDuration) : 1f) : (currentShot.duration > 0 ? Mathf.Clamp01(flightTimer / currentShot.duration) : 1f);
        public int BounceCount { get; private set; }
        public float ElapsedShotSeconds => State == BallState.Bouncing || State == BallState.Bounced
            ? currentShot.duration + flightTimer : flightTimer;

        // Events
        public event Action<BallController> OnEnteredStrikeZone;
        public event Action<Vector3> OnBallBounced;
        public event Action OnFlightCompleted;
        public event Action OnNetFault;

        private void Awake()
        {
            // A ball about a tenth of the player's height, as in the swipe-tennis reference -- still
            // several times a real pickleball, so it reads at phone size -- and emissive so it stays
            // bright against the court. It was 0.28 for the old high camera, where a smaller ball got
            // lost; from behind the player that read as a beach ball. Purely visual: nothing in the
            // flight or contact math reads this scale.
            Transform vis = ballVisual != null ? ballVisual : transform;
            if (vis == transform) vis.localScale = Vector3.one * 0.2f;
            var rend = vis.GetComponent<Renderer>();
            if (rend != null && rend.sharedMaterial != null)
            {
                Material m = Pickleball.Systems.OwnedRuntimeAssets.Track(gameObject, new Material(rend.sharedMaterial));
                rend.sharedMaterial = m;
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor"))
                    m.SetColor("_EmissionColor", new Color(1f, 0.78f, 0.15f) * 0.6f);
            }

            // Landing marker -- created here so it needs no scene wiring.
            if (GetComponentInChildren<Pickleball.VFX.BallLandingMarker>() == null)
            {
                var go = new GameObject("BallLandingMarker");
                go.transform.SetParent(transform, false);
                var marker = go.AddComponent<Pickleball.VFX.BallLandingMarker>();
                marker.Bind(this);
            }
        }

        private void Update()
        {
            if (!isFlightActive) return;

            flightTimer += Time.deltaTime;
            float t = FlightProgress;

            // Calculate deterministic position along parabolic trajectory arc
            Vector3 currentPos;
            if (State == BallState.Bouncing)
            {
                currentPos = CalculateParabolicPosition(bounceStart, bounceTarget, bounceArcHeight, t);
            }
            else
            {
                currentPos = CalculateParabolicPosition(currentShot.startPosition, currentShot.targetPosition, currentShot.arcHeight, t);
            }

            // Ball visual spin rotation
            if (Time.deltaTime > 0)
            {
                float speed = Vector3.Distance(transform.position, currentPos) / Time.deltaTime;
                transform.Rotate(Vector3.forward, speed * 100f * Time.deltaTime);
            }

            // Net clearance check
            if (!hasFaulted)
            {
                float prevZ = transform.position.z;
                float currentZ = currentPos.z;
                if ((prevZ < 0f && currentZ >= 0f) || (prevZ > 0f && currentZ <= 0f))
                {
                    float tZero = Mathf.InverseLerp(prevZ, currentZ, 0f);
                    float dz = currentShot.targetPosition.z - currentShot.startPosition.z;
                    float curveT = Mathf.Abs(dz) > 0.0001f ? -currentShot.startPosition.z / dz : 0f;
                    float yAtZero = State == BallState.InFlight
                        ? CalculateParabolicPosition(currentShot.startPosition, currentShot.targetPosition, currentShot.arcHeight, curveT).y
                        : Mathf.Lerp(transform.position.y, currentPos.y, tZero);
                    if (yAtZero < 0.9f)
                    {
                        hasFaulted = true;
                        transform.position = Vector3.Lerp(transform.position, currentPos, tZero);
                        isFlightActive = false;
                        OnNetFault?.Invoke();
                        return;
                    }
                }
            }

            transform.position = currentPos;

            // Check strike zone trigger (when ball is moving towards the player/opponent and close enough)
            if (!strikeZoneTriggered && t >= 0.5f && State == BallState.InFlight)
            {
                float distToTarget = Vector3.Distance(transform.position, currentShot.targetPosition);
                if (distToTarget <= strikeZoneDistanceThreshold)
                {
                    strikeZoneTriggered = true;
                    OnEnteredStrikeZone?.Invoke(this);
                }
            }

            // Check flight/bounce completion
            if (t >= 1.0f)
            {
                if (State == BallState.InFlight)
                {
                    State = BallState.Bouncing;
                    BounceCount++;
                    float overflow = Mathf.Max(0f, flightTimer - currentShot.duration);
                    SetupBounce();
                    flightTimer = overflow;
                    OnBallBounced?.Invoke(currentShot.targetPosition);
                    // A bounce listener may end the point; never restart a stopped ball.
                    if (!isFlightActive || State != BallState.Bouncing) return;
                    transform.position = CalculateParabolicPosition(bounceStart, bounceTarget,
                        bounceArcHeight, FlightProgress);
                    if (flightTimer >= bounceDuration)
                    {
                        isFlightActive = false;
                        State = BallState.Bounced;
                        BounceCount++;
                        OnFlightCompleted?.Invoke();
                    }
                }
                else if (State == BallState.Bouncing)
                {
                    isFlightActive = false;
                    State = BallState.Bounced;
                    BounceCount++;
                    OnFlightCompleted?.Invoke();
                }
            }
        }

        private void SetupBounce()
        {
            Sim.BallFlight.Rebound(ToSim(currentShot.startPosition), ToSim(currentShot.targetPosition),
                currentShot.arcHeight, currentShot.duration, currentShot.bounceMultiplier,
                currentShot.spinRate, out System.Numerics.Vector3 end, out bounceArcHeight, out bounceDuration);
            bounceStart = currentShot.targetPosition;
            bounceTarget = FromSim(end);
        }

        /// <summary>Where to stand and when to strike; false if no contact is reachable in time.</summary>
        public bool PlanContact(Vector3 playerPosition, int side, bool mustBounce,
            float moveSpeed, float reach, float reactionDelay, out Vector3 stance, out float arrival)
        {
            bool reachable = Sim.BallFlight.PlanContact(ToSim(currentShot.startPosition), ToSim(currentShot.targetPosition),
                currentShot.arcHeight, currentShot.duration, currentShot.bounceMultiplier, currentShot.spinRate,
                ToSim(playerPosition), side, mustBounce, moveSpeed, reach, reactionDelay,
                out System.Numerics.Vector3 planned, out arrival, ElapsedShotSeconds);
            stance = FromSim(planned);
            return reachable;
        }

        /// <summary>Where the ball will be <paramref name="secondsFromNow"/> from now on its current
        /// path. False if it will be dead (or no shot is in play) by then.</summary>
        public bool PositionIn(float secondsFromNow, out Vector3 position)
        {
            position = transform.position;
            if (!isFlightActive) return false;
            if (!Sim.BallFlight.PositionAt(ToSim(currentShot.startPosition), ToSim(currentShot.targetPosition),
                currentShot.arcHeight, currentShot.duration, currentShot.bounceMultiplier, currentShot.spinRate,
                ElapsedShotSeconds + secondsFromNow, out System.Numerics.Vector3 found))
                return false;
            position = FromSim(found);
            return true;
        }

        public bool CanContact(Vector3 playerPosition, int side, float reach, bool mustBounce)
        {
            return isFlightActive && currentShot.hitterId != side &&
                Sim.BallFlight.CanContact(ToSim(playerPosition), ToSim(transform.position), side,
                    reach, BounceCount > 0, mustBounce);
        }

        /// <summary>The span, in seconds relative to now (open &lt;= 0 &lt;= close), during which a player
        /// standing at playerPosition can strike this ball. False if they cannot strike it right now.
        /// Pass the rally's two-bounce rule as mustBounce, not whether the ball has bounced yet: the
        /// span can reach back before the bounce.</summary>
        public bool ContactWindowAroundNow(Vector3 playerPosition, int side, float reach, bool mustBounce,
            out float open, out float close)
        {
            open = close = 0f;
            if (!isFlightActive || currentShot.hitterId == side) return false;
            float now = ElapsedShotSeconds;
            if (!Sim.BallFlight.ContactWindow(ToSim(currentShot.startPosition), ToSim(currentShot.targetPosition),
                currentShot.arcHeight, currentShot.duration, currentShot.bounceMultiplier, currentShot.spinRate,
                ToSim(playerPosition), side, mustBounce, reach, now, out float openAt, out float closeAt))
                return false;
            open = openAt - now;
            close = closeAt - now;
            return true;
        }

        /// <summary>Where the ball was the last time, within the past <paramref name="seconds"/>, that a
        /// player standing at playerPosition could strike it -- a swing released just too late -- and
        /// the middle of that contact window, in seconds relative to now (negative: it has passed).</summary>
        public bool RecentContact(Vector3 playerPosition, int side, float reach, bool mustBounce, float seconds,
            out Vector3 contact, out float windowCentre)
        {
            contact = default;
            windowCentre = 0f;
            if (!isFlightActive || currentShot.hitterId == side) return false;
            float now = ElapsedShotSeconds;
            var start = ToSim(currentShot.startPosition);
            var target = ToSim(currentShot.targetPosition);
            var player = ToSim(playerPosition);
            if (!Sim.BallFlight.FindContact(start, target, currentShot.arcHeight, currentShot.duration,
                currentShot.bounceMultiplier, currentShot.spinRate, player, side, mustBounce, reach,
                now, Mathf.Max(0f, now - seconds), out float contactAt, out System.Numerics.Vector3 found))
                return false;
            contact = FromSim(found);
            windowCentre = contactAt - now;
            if (Sim.BallFlight.ContactWindow(start, target, currentShot.arcHeight, currentShot.duration,
                currentShot.bounceMultiplier, currentShot.spinRate, player, side, mustBounce, reach, contactAt,
                out float open, out float close))
                windowCentre = (open + close) * 0.5f - now;
            return true;
        }

        /// <summary>Whether a player standing at playerPosition can strike the ball at some point in
        /// the next <paramref name="seconds"/>.</summary>
        public bool ContactWithin(Vector3 playerPosition, int side, float reach, bool mustBounce, float seconds)
        {
            if (!isFlightActive || currentShot.hitterId == side) return false;
            float now = ElapsedShotSeconds;
            return Sim.BallFlight.FindContact(ToSim(currentShot.startPosition), ToSim(currentShot.targetPosition),
                currentShot.arcHeight, currentShot.duration, currentShot.bounceMultiplier, currentShot.spinRate,
                ToSim(playerPosition), side, mustBounce, reach, now, now + seconds, out _, out _);
        }

        private static System.Numerics.Vector3 ToSim(Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
        private static Vector3 FromSim(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        public void LaunchShot(ShotData shot)
        {
            currentShot = shot;
            flightTimer = 0f;
            isFlightActive = true;
            strikeZoneTriggered = false;
            hasFaulted = false;
            BounceCount = 0;
            State = BallState.InFlight;
            transform.position = shot.startPosition;

            // Sync trail visual -- clear first so the teleport to startPosition doesn't leave a
            // court-long streak from wherever the ball last was.
            BallTrailEffect trail = GetComponent<BallTrailEffect>();
            if (trail != null)
            {
                trail.ClearTrail();
                trail.SetTrailQuality(shot.quality, shot.compositeScore);
            }
        }

        public Vector3 CalculateParabolicPosition(Vector3 start, Vector3 target, float arcHeight, float t)
        {
            return FromSim(Sim.BallFlight.Sample(ToSim(start), ToSim(target), arcHeight, t));
        }

        /// <summary>Catch up a received shot on its original trajectory, including its original
        /// rebound. Call after the rally has registered the shot so bounce/fault events see the
        /// correct hitter and sequence. Never rebuild a shorter parabola from the receipt point.</summary>
        public void AdvanceToElapsed(float elapsed)
        {
            if (!isFlightActive || float.IsNaN(elapsed) || float.IsInfinity(elapsed)) return;
            elapsed = Mathf.Max(ElapsedShotSeconds, elapsed);
            float netT = Mathf.Abs(currentShot.targetPosition.z - currentShot.startPosition.z) < .0001f
                ? -1f : -currentShot.startPosition.z / (currentShot.targetPosition.z - currentShot.startPosition.z);
            if (!hasFaulted && netT >= 0f && netT <= 1f && elapsed >= currentShot.duration * netT && WouldHitNet(currentShot))
            {
                transform.position = CalculateParabolicPosition(currentShot.startPosition, currentShot.targetPosition, currentShot.arcHeight, netT);
                isFlightActive = false;
                ForceNetFault();
                return;
            }
            if (elapsed < currentShot.duration)
            {
                flightTimer = elapsed;
                transform.position = CalculateParabolicPosition(currentShot.startPosition, currentShot.targetPosition,
                    currentShot.arcHeight, elapsed / currentShot.duration);
                return;
            }
            bool firstBounce = BounceCount == 0;
            SetupBounce();
            State = BallState.Bouncing;
            BounceCount = 1;
            flightTimer = elapsed - currentShot.duration;
            transform.position = CalculateParabolicPosition(bounceStart, bounceTarget, bounceArcHeight, FlightProgress);
            if (firstBounce) OnBallBounced?.Invoke(currentShot.targetPosition);
            if (!isFlightActive || State != BallState.Bouncing) return;
            if (flightTimer >= bounceDuration)
            {
                isFlightActive = false;
                State = BallState.Bounced;
                BounceCount = 2;
                OnFlightCompleted?.Invoke();
            }
        }

        public void StopBall()
        {
            isFlightActive = false;
            State = BallState.Idle;
            BallTrailEffect trail = GetComponent<BallTrailEffect>();
            if (trail != null) trail.ClearTrail();
        }

        public bool WouldHitNet(ShotData shot)
        {
            float dz = shot.targetPosition.z - shot.startPosition.z;
            if (Mathf.Abs(dz) < 0.0001f) return false;
            float t = -shot.startPosition.z / dz;
            if (t < 0f || t > 1f) return false;
            return CalculateParabolicPosition(shot.startPosition, shot.targetPosition, shot.arcHeight, t).y < 0.9f;
        }

        public void ForceNetFault()
        {
            if (hasFaulted) return;
            hasFaulted = true;
            OnNetFault?.Invoke();
        }
    }
}
