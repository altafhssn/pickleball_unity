using System;
using UnityEngine;
using Pickleball.Systems;
using Pickleball.VFX;
using Pickleball.Utils;
using Sim = Pickleball.Sim;

namespace Pickleball.Gameplay
{
    public enum AIDifficulty { Easy, Medium, Hard }

    public class OpponentAI : MonoBehaviour, IMatchParticipant
    {
        [Header("AI Settings")]
        [SerializeField] private AIDifficulty difficulty = AIDifficulty.Medium;
        [Tooltip("0 = a beginner who is slow, mistimes, and shanks shots; 1 = a near-perfect wall. " +
                 "Set per match by ScreenManager from the chosen practice level -- the Easy/Medium/Hard " +
                 "enum above only seeds a default.")]
        [SerializeField, Range(0f, 1f)] private float skill = 0.45f;
        [SerializeField] private Vector3 homePosition = new Vector3(0f, 1.0f, 6f);

        [Header("References")]
        [SerializeField] private BallController ballController;
        private PlayerController player;

        [Header("Visuals")]
        private PaddleVisual paddleVisual;
        private CharacterVisual characterVisual;

        // All of these are now interpolated from `skill` in ApplySkill() rather than snapped to one
        // of three buckets, so difficulty can ramp smoothly across a tour instead of in three steps.
        private float moveSpeed = 4.2f;
        private float reactionDelay = 0.22f;
        private float timingError = 0.18f;
        private float missReachDistance = 1.35f;
        private float aimJitter = 1.5f;      // world-units of random error added to the AI's aim
        private float unforcedErrorChance = 0.10f; // chance the AI dumps an easy ball into the net

        private Vector3 currentHomePosition;
        private Vector3 targetMovePosition;
        private bool isMoving = false;
        private bool awaitingHit = false;
        private float estimatedArrivalTime = 0f;
        private float movementStartTime;
        private Vector3 moveVelocity;
        private float nextTrackingTime;

        private ShotType incomingShotType = ShotType.Flat;
        private bool incomingWasServe = false;

        /// <summary>Renamed from OnOpponentHit so PlayerController and OpponentAI expose the same
        /// event name, satisfying IMatchParticipant without RallyManager needing per-side handlers.
        /// Only RallyManager ever subscribed to the old name.</summary>
        public event Action<ShotData> OnShotHit;

        public int SideId => 1;
        public Vector3 Position => transform.position;

        /// <summary>IMatchParticipant unifies this with PlayerController.PrepareForIncomingShot so
        /// RallyManager can call either side identically.</summary>
        public void PrepareForIncomingShot(ShotData incomingShot) => ReactToPlayerShot(incomingShot);

        /// <summary>
        /// The AI's serve. Previously hand-rolled inline in RallyManager.StartNewServe, which built
        /// the ShotData and called ball.LaunchShot/player.PrepareForIncomingBall directly and bypassed
        /// the normal shot pipeline entirely -- unlike a player serve, which already flows through
        /// OnShotHit like any other shot. Firing OnShotHit here instead removes that inconsistency:
        /// both serve paths now go through the same RallyManager handler, so an AI serve also bumps
        /// the rally counter and plays a hit sound/VFX, matching what a player serve already does.
        /// </summary>
        public void EnterServeMode(bool serveFromRight)
        {
            if (ballController == null) ballController = FindObjectOfType<BallController>();
            if (ballController == null) return;

            System.Numerics.Vector3 simPosition = Sim.ServeRules.ServerPosition(SideId, serveFromRight);
            transform.position = new Vector3(simPosition.X, simPosition.Y, simPosition.Z);
            targetMovePosition = transform.position;
            isMoving = false;
            awaitingHit = false;

            // A skilled AI serves hard, deep, and wide -- so the player's return of serve is already
            // under pressure -- rather than a soft lob to mid-court.
            float horizontalAim = UnityEngine.Random.Range(-1f, 1f) * Mathf.Lerp(0.35f, 0.95f, skill);
            float depth01 = Mathf.Clamp01(Mathf.Lerp(0.3f, 0.85f, skill) + UnityEngine.Random.Range(-0.12f, 0.12f));
            System.Numerics.Vector3 simTarget = Sim.ServeRules.BuildTarget(
                SideId, serveFromRight, horizontalAim, depth01);
            Vector3 serveTarget = new Vector3(simTarget.X, simTarget.Y, simTarget.Z);
            float arc = Mathf.Lerp(2.2f, 1.4f, skill);
            float dur = Mathf.Lerp(1.5f, 1.05f, skill);
            ShotData serveShot = new ShotData(1, ShotType.Serve, transform.position + Vector3.up * 0.2f, serveTarget, arc, dur);

            PlaySwingVisual(true, ShotType.Serve, Mathf.Lerp(0.55f, 1f, skill));
            ballController.LaunchShot(serveShot);
            OnShotHit?.Invoke(serveShot);
            targetMovePosition = new Vector3(transform.position.x * 0.35f, 1f, 7.5f);
            isMoving = true;
        }

        private void Awake()
        {
            ApplyDifficulty(difficulty);

            currentHomePosition = homePosition;

            if (ballController == null) ballController = FindObjectOfType<BallController>();

            // The rigged character supersedes the primitive paddle when it is present; only fall back
            // to building the placeholder paddle if no character was set up on this object.
            characterVisual = GetComponent<CharacterVisual>();
            paddleVisual = GetComponent<PaddleVisual>();
            if (characterVisual == null && paddleVisual == null)
            {
                paddleVisual = gameObject.AddComponent<PaddleVisual>();
                paddleVisual.Initialize(true);
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
            player = FindObjectOfType<PlayerController>();

            currentHomePosition = homePosition;
            transform.position = currentHomePosition;
            targetMovePosition = currentHomePosition;
        }

        public void SetDifficulty(AIDifficulty diff)
        {
            difficulty = diff;
            ApplyDifficulty(diff);
        }

        /// <summary>Sets a continuous skill level (0..1) and derives every behavioural parameter from
        /// it. This is the entry point ScreenManager uses to scale the opponent per match.</summary>
        public void ConfigureSkill(float skill01)
        {
            skill = Mathf.Clamp01(skill01);
            ApplySkill();
        }

        public float Skill => skill;

        private void ApplyDifficulty(AIDifficulty diff)
        {
            // The enum now just seeds a point on the continuous skill scale.
            switch (diff)
            {
                case AIDifficulty.Easy: skill = 0.15f; break;
                case AIDifficulty.Medium: skill = 0.45f; break;
                case AIDifficulty.Hard: skill = 0.80f; break;
            }
            ApplySkill();
        }

        private void ApplySkill()
        {
            reactionDelay      = Mathf.Lerp(0.42f, 0.03f, skill);
            timingError        = Mathf.Lerp(0.38f, 0.015f, skill);
            // Above ~0.7 the AI is faster and rangier than the player's own base 6.0 / 1.6 -- that is
            // deliberate: the top of the curve (last tour, high ranked) should feel like a wall.
            moveSpeed          = Mathf.Lerp(3.4f, 6.6f, skill);
            missReachDistance  = Mathf.Lerp(1.2f, 2.05f, skill);
            aimJitter          = Mathf.Lerp(2.3f, 0.12f, skill);
            // Only genuine beginners (skill < 0.5) ever shank an easy ball into the net. A competent
            // opponent does not hand you free points.
            unforcedErrorChance = Mathf.Lerp(0.12f, 0f, Mathf.Clamp01(skill / 0.5f));
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (RallyManager.Instance != null && (RallyManager.Instance.State == MatchState.PointEnded ||
                RallyManager.Instance.State == MatchState.MatchOver))
            {
                isMoving = false;
                awaitingHit = false;
                moveVelocity = Vector3.zero;
                return;
            }
            if (awaitingHit && ballController != null && Time.time >= movementStartTime && Time.time >= nextTrackingTime)
            {
                nextTrackingTime = Time.time + 0.08f;
                bool mustBounce = RallyManager.Instance != null && Sim.RallyRules.MustBounce(RallyManager.Instance.CurrentRallyCount);
                if (!ballController.CanContact(transform.position, SideId, missReachDistance, mustBounce))
                {
                    ballController.PlanContact(transform.position, SideId, mustBounce, moveSpeed,
                        missReachDistance, 0f, out targetMovePosition, out float arrival);
                    estimatedArrivalTime = Time.time + arrival;
                }
                else
                {
                    targetMovePosition = transform.position;
                }
            }
            if (isMoving && (!awaitingHit || Time.time >= movementStartTime))
            {
                transform.position = Vector3.SmoothDamp(transform.position, targetMovePosition,
                    ref moveVelocity, 0.07f, moveSpeed, Time.deltaTime);
            }

            if (awaitingHit && Time.time >= estimatedArrivalTime)
            {
                ExecuteReturnShot();
            }
        }

        public void ReactToPlayerShot(ShotData playerShot)
        {
            incomingShotType = playerShot.shotType;
            incomingWasServe = playerShot.shotType == ShotType.Serve;

            isMoving = true;
            awaitingHit = true;
            movementStartTime = Time.time + reactionDelay;

            // Both opening returns use a bounced contact; later balls can be intercepted.
            int nextShotIndex = (RallyManager.Instance != null ? RallyManager.Instance.CurrentRallyCount : 0) + 1;
            bool mustLetBounce = incomingWasServe || nextShotIndex <= 3;
            ballController.PlanContact(transform.position, SideId, mustLetBounce, moveSpeed,
                missReachDistance, reactionDelay, out targetMovePosition, out float arrival);
            estimatedArrivalTime = Time.time + arrival;

            // Dynamically adjust court anchor based on shot archetype
            if (playerShot.shotType == ShotType.Dink)
            {
                // Move forward to the kitchen line (Z ~ 3.0f)
                currentHomePosition = new Vector3(0f, 1.0f, 3.2f);
            }
            else if (playerShot.shotType == ShotType.Lob)
            {
                // Retreat deep to baseline (Z ~ 7.2f)
                currentHomePosition = new Vector3(0f, 1.0f, 7.2f);
            }
            else
            {
                currentHomePosition = homePosition;
            }
        }

        private void ExecuteReturnShot()
        {
            if (ballController == null || ShotSystem.Instance == null) return;
            if (RallyManager.Instance != null && !RallyManager.Instance.CanSideHit(SideId))
            {
                awaitingHit = false;
                return;
            }
            if (ballController.State != BallState.InFlight && ballController.State != BallState.Bouncing)
            {
                awaitingHit = false;
                return;
            }
            int nextShot = (RallyManager.Instance != null ? RallyManager.Instance.CurrentRallyCount : 0) + 1;
            // Wait for real contact. A timestamp alone must never let the AI hit across the court.
            if (!ballController.CanContact(transform.position, SideId, missReachDistance, nextShot <= 3)) return;
            awaitingHit = false;

            bool isNearKitchen = transform.position.z <= 3.8f;

            // Stay patient on low kitchen balls and attack high contacts.
            ShotType chosenShotType = ShotType.Flat;
            float rand = UnityEngine.Random.value;

            // Put-away: at the net with a high, slow ball coming in (a lob or a floaty dink), a
            // competent AI smashes rather than politely dinking it back. Weaker AIs miss the chance.
            bool canSmash = ballController.transform.position.y > 1.9f;
            if (canSmash && skill > 0.3f && UnityEngine.Random.value < Mathf.Lerp(0.35f, 0.98f, skill))
            {
                chosenShotType = ShotType.Smash;
            }
            else if (isNearKitchen)
            {
                float driveShare = ballController.transform.position.y > 1.05f
                    ? Mathf.Lerp(0.25f, 0.6f, skill) : 0.12f;
                if (rand < driveShare) chosenShotType = ShotType.Topspin;
                else if (rand < 0.92f) chosenShotType = ShotType.Dink;
                else chosenShotType = ShotType.Flat;
            }

            else
            {
                float driveShare = Mathf.Lerp(0.35f, 0.72f, skill);
                if (rand < driveShare) chosenShotType = ShotType.Topspin;
                else if (rand < driveShare + 0.25f) chosenShotType = ShotType.Flat;
                else if (rand < driveShare + 0.40f) chosenShotType = ShotType.Slice;
                else chosenShotType = ShotType.Lob;
            }

            // The third-shot drop creates a route from the baseline into a kitchen exchange.
            if (nextShot == 3 && UnityEngine.Random.value < Mathf.Lerp(0.35f, 0.7f, skill))
                chosenShotType = ShotType.Dink;

            // Tactical aiming: a skilled AI drives into the open corner away from the player; a weak
            // one often just pushes it straight back to where the player already is.
            float targetAimX = 0f;
            if (player != null)
            {
                float openSide = player.transform.position.x > 0f ? -1f : 1f;
                float aimMagnitude = Mathf.Lerp(1.3f, 3.4f, skill);
                bool straightBack = UnityEngine.Random.value < Mathf.Lerp(0.45f, 0f, skill);
                targetAimX = straightBack ? 0f : openSide * aimMagnitude;
            }
            targetAimX += UnityEngine.Random.Range(-aimJitter, aimJitter);
            targetAimX = Mathf.Clamp(targetAimX, -3.9f, 3.9f);

            // Depth: a skilled AI hits deep to the player's baseline (pinning them back), a weak one
            // floats it mid-court. For the AI (far side) a more-negative swipe Y = deeper toward the player.
            float depthBias = Mathf.Lerp(-0.45f, -0.92f, skill);
            float randomY = Mathf.Clamp(depthBias + UnityEngine.Random.Range(-0.18f, 0.18f), -0.98f, -0.2f);
            Vector2 aiSwipeVector = Vector2.ClampMagnitude(new Vector2(targetAimX / 4.2f, randomY), 1f);

            float actualTimingError = Mathf.Max(0f, timingError + UnityEngine.Random.Range(-timingError * 0.3f, timingError * 1.2f));

            // Unforced error: a weak AI sometimes just shanks an easy ball. Modelled as a very short,
            // low shot that fails to clear the net, so the fault falls out of the normal ball physics
            // (BallController's net-clearance check) rather than a special case here.
            bool shank = UnityEngine.Random.value < unforcedErrorChance;
            if (shank)
            {
                chosenShotType = ShotType.Flat;
                actualTimingError = 0.6f; // guaranteed Miss-quality
            }

            ShotData shot = ShotSystem.Instance.EvaluateAndBuildShot(
                hitterId: 1,
                startPos: ballController.transform.position,
                idealStrikePos: targetMovePosition,
                actualPlayerPos: transform.position,
                timingError: actualTimingError,
                swipeVector: aiSwipeVector,
                shotType: chosenShotType,
                shotIndexInRally: nextShot
            );

            if (shank)
            {
                // Flatten the arc so it dies in the net rather than merely landing short.
                shot.arcHeight = 0.5f;
                shot.targetPosition = new Vector3(shot.targetPosition.x, 0f, -0.6f);
                shot.duration = 0.7f;
            }

            // Paddle swing
            bool isForehand = (ballController.transform.position.x <= transform.position.x);
            PlaySwingVisual(isForehand, shot.shotType, aiSwipeVector.magnitude);

            ballController.LaunchShot(shot);
            OnShotHit?.Invoke(shot);

            // Move back toward court position
            float recoveryDepth = chosenShotType == ShotType.Dink || isNearKitchen ? 3.15f : 5.2f;
            currentHomePosition = new Vector3(shot.targetPosition.x * 0.28f, 1f, recoveryDepth);
            targetMovePosition = currentHomePosition;
            isMoving = true;
        }

        public void ResetPosition()
        {
            moveVelocity = Vector3.zero;
            currentHomePosition = homePosition;
            transform.position = currentHomePosition;
            targetMovePosition = currentHomePosition;
            isMoving = false;
            awaitingHit = false;
            incomingShotType = ShotType.Flat;
            incomingWasServe = false;

            // Only reached on a new serve, so a rematch doesn't open on the previous result pose.
            if (characterVisual != null) characterVisual.ResetResultPose();
        }
    }
}
