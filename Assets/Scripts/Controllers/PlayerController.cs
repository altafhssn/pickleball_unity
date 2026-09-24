using System;
using UnityEngine;
using Pickleball.Systems;
using Pickleball.VFX;
using Pickleball.Utils;
using Pickleball.Data;
using Pickleball.UI;
using Sim = Pickleball.Sim;

namespace Pickleball.Gameplay
{
    public class PlayerController : MonoBehaviour, IMatchParticipant
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 6.0f;
        [SerializeField] private Vector3 homePosition = new Vector3(0f, 1.0f, -6f);
        [SerializeField] private float strikeZoneOffsetZ = -0.5f;
        [SerializeField] private float baseMissReachDistance = 1.6f;
        [Tooltip("Neutral mid-court depth the player recovers to between shots. Singles play is " +
                 "baseline/mid-court, not net-camping: with no partner to cover a wide pass, a player " +
                 "who holds the kitchen line after a drive or lob just gets beaten down the line. Only " +
                 "a live dink exchange shades further forward than this.")]
        [SerializeField] private float readyPositionDepth = -5.2f;

        [Header("References")]
        [SerializeField] private BallController ballController;

        [Header("Visuals & Juice")]
        private PaddleVisual paddleVisual;
        private CharacterVisual characterVisual;

        private Vector3 targetMovePosition;
        private bool isAutoPositioning = false;
        private float idealStrikeTimestamp = 0f;
        private bool isServing = false;
        private bool serveFromRight = true;
        private bool hasBufferedShot;
        private Vector2 bufferedSwipe;
        private ShotType bufferedShotType;
        private float bufferedRelease;
        private float bufferedUntil;
        private Vector3 moveVelocity;
        private float nextTrackingTime;
        private bool trackingIncoming;
        private bool holdingContact;
        /// <summary>Whether the latest plan can actually reach the incoming ball in time.</summary>
        private bool planReachable = true;

        /// <summary>A ball must stay in reach at least this long ahead for the player to stop and wait
        /// for it. Tracking re-checks every 0.08 s, so any genuine contact window (~0.2 s or more)
        /// still has this much left when it is first seen.</summary>
        private const float MinHoldContactSeconds = 0.1f;

        private float Reach => baseMissReachDistance + Sim.ShotSim.SpeedReachBonus(loadout.speed);

        /// <summary>Resolved once in Start() rather than on every shot: gear doesn't change mid-match,
        /// and this feeds Update() every frame for the Speed stat's move-speed multiplier. See
        /// ShotSim's Power/Spin/Control/Speed/Serve/Stamina bindings (Docs/GearProgression.md#1-stats)
        /// for what each field does.</summary>
        private Sim.LoadoutStats loadout;

        public event Action<ShotData> OnShotHit;

        public int SideId => 0;
        public Vector3 Position => transform.position;

        /// <summary>IMatchParticipant unifies the two previously differently-shaped "receive a shot"
        /// methods (this one, and OpponentAI.ReactToPlayerShot) so RallyManager can call either side
        /// identically. PrepareForIncomingBall stays as the real implementation since it's the more
        /// descriptive name for what a human-controlled side actually does.</summary>
        public void PrepareForIncomingShot(ShotData incomingShot) => PrepareForIncomingBall(incomingShot.targetPosition, incomingShot.duration);

        private void Awake()
        {
            if (ballController == null) ballController = FindObjectOfType<BallController>();

            // The rigged character supersedes the primitive paddle when it is present; only fall back
            // to building the placeholder paddle if no character was set up on this object.
            characterVisual = GetComponent<CharacterVisual>();
            paddleVisual = GetComponent<PaddleVisual>();
            if (characterVisual == null && paddleVisual == null)
            {
                paddleVisual = gameObject.AddComponent<PaddleVisual>();
                paddleVisual.Initialize(false);
            }
        }

        private void PlaySwingVisual(bool isForehand, ShotType shotType, float power)
        {
            if (characterVisual != null) characterVisual.PlaySwing(isForehand, shotType, power);
            if (paddleVisual != null) paddleVisual.PlaySwing(isForehand, shotType, power);
        }

        private void Start()
        {
            if (ballController == null) ballController = FindObjectOfType<BallController>();
            transform.position = homePosition;
            targetMovePosition = homePosition;
            loadout = MetaGameState.GetLoadoutStats();

            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnShotGestureCompleted += HandleShotGestureCompleted;
                InputManager.Instance.OnTouchTooShort += HandleTouchTooShort;
                InputManager.Instance.ExpectedContactHeight = ExpectedContactHeight;
            }

        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnShotGestureCompleted -= HandleShotGestureCompleted;
                InputManager.Instance.OnTouchTooShort -= HandleTouchTooShort;
                if (InputManager.Instance.ExpectedContactHeight == (Func<float>)ExpectedContactHeight)
                    InputManager.Instance.ExpectedContactHeight = null;
            }

        }

        /// <summary>How high the incoming ball will be when the player strikes it: now, if it is
        /// already in reach, otherwise at the planned strike moment. Infinity when no ball is on its way.</summary>
        private float ExpectedContactHeight()
        {
            if (!trackingIncoming || ballController == null || ballController.CurrentShot.hitterId == SideId)
                return float.PositiveInfinity;
            if (holdingContact) return ballController.transform.position.y;
            float untilStrike = Mathf.Max(0f, idealStrikeTimestamp - MatchClock.NowSeconds());
            return ballController.PositionIn(untilStrike, out Vector3 atStrike)
                ? atStrike.y : ballController.transform.position.y;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) { hasBufferedShot = false; return; }
            if (RallyManager.Instance != null &&
                (RallyManager.Instance.State == MatchState.PointEnded || RallyManager.Instance.State == MatchState.MatchOver))
            {
                isAutoPositioning = false;
                trackingIncoming = false;
                hasBufferedShot = false;
                moveVelocity = Vector3.zero;
                if (StrikeTimingIndicator.Instance != null) StrikeTimingIndicator.Instance.Hide();
                return;
            }
            if (trackingIncoming && ballController != null &&
                ballController.CurrentShot.hitterId != SideId && Time.time >= nextTrackingTime &&
                (ballController.State == BallState.InFlight || ballController.State == BallState.Bouncing))
            {
                nextTrackingTime = Time.time + 0.08f;
                // Hold a reachable contact instead of continually running past the ball -- but only
                // one that stays in reach long enough to swing at. Stopping the instant a passing ball
                // grazed the edge of reach planted the player while it went by (seen live: in reach
                // for 17 ms, then gone). Once holding, stay put until the ball leaves reach.
                bool inReach = ballController.CanContact(transform.position, SideId, Reach, MustLetBallBounce());
                if (!inReach) holdingContact = false;
                else if (!holdingContact)
                {
                    holdingContact = ballController.ContactWindowAroundNow(transform.position, SideId, Reach,
                        RuleRequiresBounce(), out float open, out float close) && close >= MinHoldContactSeconds;
                    // The player can stop short of the planned stance, which shifts when the ball is
                    // really in reach -- time the ring from where they actually stand. Only when this
                    // is the contact the ring is already counting down to, though: the stop can also
                    // come from a brief earlier chance (a volley before a planned after-bounce
                    // contact), and snapping the ring to that and back made it jump ~250 ms mid-swing.
                    float now = MatchClock.NowSeconds();
                    float shotElapsed = ballController.ElapsedShotSeconds;
                    float bounceAt = ballController.CurrentShot.duration;
                    bool sameContact = (shotElapsed < bounceAt) == (shotElapsed + idealStrikeTimestamp - now < bounceAt);
                    if (holdingContact && sameContact && !hasBufferedShot)
                    {
                        idealStrikeTimestamp = now + (open + close) * 0.5f;
                        if (StrikeTimingIndicator.Instance != null)
                            StrikeTimingIndicator.Instance.Retime(idealStrikeTimestamp);
                    }
                }
                if (!holdingContact)
                {
                    planReachable = ballController.PlanContact(transform.position, SideId, MustLetBallBounce(),
                        moveSpeed * Sim.ShotSim.SpeedMoveMultiplier(loadout.speed), Reach, 0f,
                        out targetMovePosition, out float arrival);
                    if (!hasBufferedShot)
                        idealStrikeTimestamp = MatchClock.NowSeconds() + arrival;
                    UpdateTimingRing(arrival);
                }
                else
                {
                    targetMovePosition = transform.position;
                    moveVelocity = Vector3.zero;
                }
            }
            if (isAutoPositioning)
            {
                float effectiveMoveSpeed = moveSpeed * Sim.ShotSim.SpeedMoveMultiplier(loadout.speed);
                transform.position = Vector3.SmoothDamp(transform.position, targetMovePosition,
                    ref moveVelocity, 0.07f, effectiveMoveSpeed, Time.deltaTime);
            }

            if (!hasBufferedShot) return;
            if (ballController == null || (RallyManager.Instance != null && !RallyManager.Instance.CanSideHit(SideId)))
            {
                hasBufferedShot = false;
                return;
            }
            if (MatchClock.NowSeconds() > bufferedUntil)
            {
                // The early swing never met the ball. Say so, rather than dropping it silently.
                hasBufferedShot = false;
                ShowMissedSwing("TOO EARLY");
                return;
            }
            if (ballController.CanContact(transform.position, SideId, Reach, MustLetBallBounce()))
            {
                hasBufferedShot = false;
                ExecuteRallyShot(bufferedSwipe, bufferedRelease, bufferedShotType, ballController.transform.position);
            }
        }

        public void EnterServeMode(bool fromRight)
        {
            serveFromRight = fromRight;
            hasBufferedShot = false;
            moveVelocity = Vector3.zero;
            isServing = true;
            isAutoPositioning = false;

            System.Numerics.Vector3 simPosition = Sim.ServeRules.ServerPosition(SideId, serveFromRight);
            transform.position = new Vector3(simPosition.X, simPosition.Y, simPosition.Z);
            targetMovePosition = transform.position;

            if (StrikeTimingIndicator.Instance != null)
            {
                StrikeTimingIndicator.Instance.Hide();
            }

            // Hold at the same waist-height contact point used by the underhand serve.
            if (ballController != null)
            {
                ballController.StopBall();
                ballController.transform.position = transform.position + Vector3.up * 0.2f;
            }
        }

        public void PrepareForIncomingBall(Vector3 predictedLandingPos, float flightDuration)
        {
            hasBufferedShot = false;
            trackingIncoming = true;
            holdingContact = false;
            nextTrackingTime = Time.time + 0.08f;
            float arrival = flightDuration;
            targetMovePosition = new Vector3(predictedLandingPos.x, 1f, predictedLandingPos.z + strikeZoneOffsetZ);
            planReachable = true;
            if (ballController != null)
                planReachable = ballController.PlanContact(transform.position, SideId, MustLetBallBounce(),
                    moveSpeed * Sim.ShotSim.SpeedMoveMultiplier(loadout.speed), Reach, 0f,
                    out targetMovePosition, out arrival);
            isAutoPositioning = true;

            // Tick-quantized rather than raw Time.time: this value is subtracted from the swipe's
            // release timestamp to score timing accuracy, and a re-simulating server needs that delta
            // to come out identically from the same recorded inputs -- see MatchClock.
            idealStrikeTimestamp = MatchClock.NowSeconds() + arrival;

            // Show on-court timing ring -- only for a ball the player can actually get to.
            if (StrikeTimingIndicator.Instance != null)
            {
                if (planReachable)
                    StrikeTimingIndicator.Instance.Show(targetMovePosition, arrival, idealStrikeTimestamp);
                else
                    StrikeTimingIndicator.Instance.Hide();
            }
        }

        /// <summary>Keeps the ring on the current plan. When no contact is reachable any more -- a
        /// missed window, or a clean winner -- the planner falls back to chasing the ball, and the
        /// ring used to keep counting down (and flash gold) at a spot the player would never reach.</summary>
        private void UpdateTimingRing(float arrival)
        {
            StrikeTimingIndicator ring = StrikeTimingIndicator.Instance;
            if (ring == null) return;
            if (!planReachable) ring.Hide();
            else if (ring.IsShowing) ring.Retarget(targetMovePosition, idealStrikeTimestamp);
            else ring.Show(targetMovePosition, arrival, idealStrikeTimestamp);
        }

        /// <summary>Feedback for a swing that did not connect.</summary>
        private static void ShowMissedSwing(string reason)
        {
            if (GameplayHUD.Instance != null)
                GameplayHUD.Instance.ShowShotFeedback(reason, UITheme.QualityColor(ShotQuality.Good));
        }

        /// <summary>Why a swing released now cannot connect, judged from the ball's actual path rather
        /// than the ring: a late swing used to read "OUT OF REACH" even when the ball had been right
        /// there a moment before.</summary>
        private string MissedSwingReason()
        {
            if (MustLetBallBounce()) return "LET IT BOUNCE";
            bool requiresBounce = RuleRequiresBounce();
            const float restOfFlight = 3f;
            if (ballController.ContactWithin(transform.position, SideId, Reach, requiresBounce, restOfFlight) ||
                (planReachable && ballController.ContactWithin(targetMovePosition, SideId, Reach, requiresBounce, restOfFlight)))
                return "TOO EARLY";
            if (ballController.RecentContact(transform.position, SideId, Reach, requiresBounce,
                    ballController.ElapsedShotSeconds, out _, out _))
                return "TOO LATE";
            return "OUT OF REACH";
        }

        /// <summary>A touch that lifted without becoming a swipe. It used to do nothing at all, so a
        /// tap or a too-small flick on the player's turn read as the game ignoring them.</summary>
        private void HandleTouchTooShort()
        {
            if (ballController == null || Time.timeScale <= 0f) return;
            if (RallyManager.Instance != null && !RallyManager.Instance.CanSideHit(SideId)) return;
            if (isServing)
            {
                ShowMissedSwing("TAP OR SWIPE TO SERVE");
                return;
            }
            bool ballIncoming = trackingIncoming && ballController.CurrentShot.hitterId != SideId &&
                (ballController.State == BallState.InFlight || ballController.State == BallState.Bouncing);
            if (ballIncoming) ShowMissedSwing("SWIPE TO HIT");
        }

        /// <summary>Shots 2 and 3 of a rally (return of serve, and the third ball) must be let bounce
        /// before they can be struck -- the two-bounce rule. Returns true if the player is currently
        /// forbidden from taking the ball out of the air.</summary>
        private bool MustLetBallBounce()
        {
            return RuleRequiresBounce() && ballController != null && ballController.BounceCount < 1;
        }

        /// <summary>The two-bounce rule alone, regardless of whether the ball has bounced yet -- what a
        /// scan along the ball's whole path needs, since it can look back to before the bounce.</summary>
        private static bool RuleRequiresBounce()
        {
            int nextShotIndex = (RallyManager.Instance != null ? RallyManager.Instance.CurrentRallyCount : 0) + 1;
            return nextShotIndex <= 3;
        }

        private void HandleShotGestureCompleted(Vector2 swipeVector, float releaseTimestamp, ShotType shotType)
        {
            if (ballController == null || Time.timeScale <= 0f) return;
            if (RallyManager.Instance != null && !RallyManager.Instance.CanSideHit(SideId)) return;

            if (isServing)
            {
                ExecuteServe(swipeVector, releaseTimestamp);
            }
            else
            {
                if (ballController.State != BallState.InFlight && ballController.State != BallState.Bouncing) return;
                if (ballController.CurrentShot.hitterId == SideId) return;
                if (ballController.CanContact(transform.position, SideId, Reach, MustLetBallBounce()))
                {
                    ExecuteRallyShot(swipeVector, releaseTimestamp, shotType, ballController.transform.position);
                    return;
                }

                // Released just after the ball left reach: still a hit, struck where the ball last was
                // and scored as late, rather than an outright whiff.
                bool requiresBounce = RuleRequiresBounce();
                if (ballController.RecentContact(transform.position, SideId, Reach, requiresBounce,
                        Sim.BallFlight.LateContactGraceSeconds, out Vector3 lateContact, out float windowCentre))
                {
                    // Score it against the contact actually made: once that window passed, tracking
                    // may already have moved the ring on to a later chance.
                    idealStrikeTimestamp = MatchClock.NowSeconds() + windowCentre;
                    ExecuteRallyShot(swipeVector, releaseTimestamp, shotType, lateContact);
                    return;
                }

                // Released early: hold the swing if the ball comes into reach (from here or from the
                // stance the player is moving to) within the buffer window.
                float bufferSeconds = Sim.BallFlight.ContactBufferSeconds;
                if (ballController.ContactWithin(transform.position, SideId, Reach, requiresBounce, bufferSeconds) ||
                    (planReachable && ballController.ContactWithin(targetMovePosition, SideId, Reach, requiresBounce, bufferSeconds)))
                {
                    bufferedSwipe = swipeVector;
                    bufferedShotType = shotType;
                    bufferedRelease = releaseTimestamp;
                    bufferedUntil = releaseTimestamp + bufferSeconds;
                    hasBufferedShot = true;
                    return;
                }

                ShowMissedSwing(MissedSwingReason());
            }
        }

        private void ExecuteServe(Vector2 swipeVector, float releaseTimestamp)
        {
            isServing = false;

            if (StrikeTimingIndicator.Instance != null)
            {
                StrikeTimingIndicator.Instance.Hide();
            }

            // Input encodes motion strength in vector length, independently of shot timing.
            float power = Mathf.Clamp01(swipeVector.magnitude);

            float depth01 = power;
            System.Numerics.Vector3 simTarget = Sim.ServeRules.BuildTarget(
                SideId, serveFromRight, swipeVector.x, depth01);
            Vector3 targetPos = new Vector3(simTarget.X, simTarget.Y, simTarget.Z);

            // Weak serves get a low arc -- low enough, and BallController's own net-clearance check
            // faults it, no special-casing here.
            float arc = Mathf.Lerp(0.7f, 1.25f, power);
            // Flight time is roughly in line with the AI's serve (1.05-1.5s over a similar distance);
            // the old 2.1-2.9s range made the player's serve crawl across the court by comparison.
            float duration = Mathf.Lerp(1.9f, 1.3f, power);

            ShotData serveShot = new ShotData(0, ShotType.Serve, transform.position + Vector3.up * 0.2f, targetPos, arc, duration);

            // Serve stat only shortens flight time here -- the swipe already fully determines the
            // target with no RNG scatter to reduce (see ShotSim.ServeDurationMultiplier).
            serveShot.duration *= Sim.ShotSim.ServeDurationMultiplier(loadout.serve);

            serveShot.swipeAccuracy = power;
            serveShot.timingScore = power;
            serveShot.positionScore = 1.0f;
            serveShot.compositeScore = (serveShot.timingScore * 0.55f) + (serveShot.positionScore * 0.25f) + (power * 0.2f);
            serveShot.quality = serveShot.compositeScore > 0.85f ? ShotQuality.Perfect :
                                serveShot.compositeScore > 0.7f ? ShotQuality.Great :
                                serveShot.compositeScore > 0.5f ? ShotQuality.Good : ShotQuality.Weak;

            PlaySwingVisual(true, ShotType.Serve, power);

            ballController.LaunchShot(serveShot);
            OnShotHit?.Invoke(serveShot);
            targetMovePosition = new Vector3(transform.position.x * 0.35f, 1f, -7.5f);
            isAutoPositioning = true;
        }

        /// <summary>Strikes the ball from <paramref name="contactPosition"/>. Callers establish that the
        /// contact is legal (reach, height and kitchen rules -- the Speed stat widens reach by up to
        /// 0.35 units): the ball now, or where it was for a late swing.</summary>
        private void ExecuteRallyShot(Vector2 swipeVector, float releaseTimestamp, ShotType shotType,
            Vector3 contactPosition)
        {
            if (ShotSystem.Instance == null) return;

            // Smash needs a high ball; a downward flick at a low one stays a soft downward stroke. The
            // preview already applied this for the expected contact -- this covers the actual one.
            shotType = (ShotType)Sim.SwipeGesture.ForContactHeight((Sim.ShotType)shotType, contactPosition.y);

            // Signed, so a whiffed swing can say *why* it was bad. timingError itself stays absolute
            // for the sim's symmetric scoring window.
            float signedTiming = releaseTimestamp - idealStrikeTimestamp;
            float timingError = Mathf.Abs(signedTiming);

            // +1: RallyManager.CurrentRallyCount is shots already resolved this rally (the serve
            // counts as 1), so the shot about to be built is the next one -- see
            // ShotSim.RallyFatigueMultiplier for what this drives.
            int shotIndexInRally = (RallyManager.Instance != null ? RallyManager.Instance.CurrentRallyCount : 0) + 1;

            ShotData shot = ShotSystem.Instance.EvaluateAndBuildShot(
                hitterId: 0,
                startPos: contactPosition,
                idealStrikePos: targetMovePosition,
                actualPlayerPos: transform.position,
                timingError: timingError,
                swipeVector: swipeVector,
                shotType: shotType,
                loadout: loadout,
                shotIndexInRally: shotIndexInRally
            );

            isAutoPositioning = false;

            if (StrikeTimingIndicator.Instance != null)
            {
                StrikeTimingIndicator.Instance.Hide();
            }

            // Tell the player *why* a bad swing was bad, so mistiming is learnable rather than just
            // a lost point with a generic "opponent scores".
            if (shot.quality == ShotQuality.Miss && GameplayHUD.Instance != null)
            {
                GameplayHUD.Instance.ShowShotFeedback(signedTiming < 0f ? "TOO EARLY" : "TOO LATE",
                    UITheme.QualityColor(ShotQuality.Miss));
            }

            // Determine Forehand vs Backhand swing
            bool isForehand = (contactPosition.x >= transform.position.x);
            trackingIncoming = false;
            PlaySwingVisual(isForehand, shot.shotType, Mathf.Clamp01(swipeVector.magnitude));

            ballController.LaunchShot(shot);
            OnShotHit?.Invoke(shot);
            if (RallyManager.Instance == null || RallyManager.Instance.State == MatchState.InRally)
            {
                // Recover to a neutral mid-court ready position, shaded only lightly toward the
                // outgoing angle so the down-the-line pass stays covered. Holding the kitchen line
                // is reserved for a genuine dink exchange -- a dink played from an already-forward
                // stance. The old rule also crept forward whenever the player was simply *near* the
                // net (z > -4.5), which latched: every recovery after the first ball glued the
                // player to the kitchen line, where a singles opponent pins deep and passes wide.
                float kitchenLine = -Sim.CourtDimensions.KitchenDepth; // -2.5 on the player's side
                bool dinkExchange = shot.shotType == ShotType.Dink && transform.position.z > kitchenLine - 1.1f;
                float recoverZ = dinkExchange ? kitchenLine - 0.65f : readyPositionDepth;
                targetMovePosition = new Vector3(shot.targetPosition.x * 0.18f, 1f, recoverZ);
                isAutoPositioning = true;
            }
        }

        public void ResetPosition()
        {
            trackingIncoming = false;
            hasBufferedShot = false;
            moveVelocity = Vector3.zero;
            transform.position = homePosition;
            targetMovePosition = homePosition;
            isAutoPositioning = false;
            isServing = false;

            // Only reached on a new serve, so a rematch doesn't open on the previous celebration pose.
            if (characterVisual != null) characterVisual.ResetResultPose();

            if (StrikeTimingIndicator.Instance != null)
            {
                StrikeTimingIndicator.Instance.Hide();
            }
        }

        private void OnDisable() { hasBufferedShot = false; moveVelocity = Vector3.zero; }
    }
}
