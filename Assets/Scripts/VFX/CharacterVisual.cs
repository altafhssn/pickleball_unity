using UnityEngine;
using Pickleball.Gameplay;

namespace Pickleball.VFX
{
    /// <summary>
    /// Drives the rigged character that stands in for a player or opponent.
    ///
    /// The gameplay controllers own position entirely -- they lerp the transform toward an intercept
    /// point -- so this component never moves anything. It only reads the resulting motion back out
    /// and translates it into Animator parameters, plus exposes the one-shot swing/result triggers.
    /// Root motion is force-disabled for the same reason: the run clips carry motion of their own and
    /// would otherwise fight the controller for control of the transform.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;

        [Header("Setup")]
        [SerializeField] private bool isOpponent;

        /// <summary>Speed that maps to a fully-extended run blend. Matched to the owning controller's
        /// moveSpeed so the character isn't still leaning into a jog at its actual top speed.</summary>
        [SerializeField] private float referenceSpeed = 6f;

        /// <summary>Exponential smoothing rate on the blend values. Raw frame-to-frame velocity is
        /// jittery -- the controllers use MoveTowards and snap to zero on arrival -- and feeding that
        /// straight in makes the legs stutter.</summary>
        [SerializeField] private float blendSharpness = 10f;

        private static readonly int PMoveX = Animator.StringToHash("MoveX");
        private static readonly int PMoveZ = Animator.StringToHash("MoveZ");
        private static readonly int PSpeed = Animator.StringToHash("Speed");
        private static readonly int PSmashLeft = Animator.StringToHash("SmashLeft");
        private static readonly int PSmashRight = Animator.StringToHash("SmashRight");
        private static readonly int PVictory = Animator.StringToHash("Victory");
        private static readonly int PDefeat = Animator.StringToHash("Defeat");
        // State names (same strings as the triggers, but state hashes, not parameter hashes).
        private static readonly int SLocomotion = Animator.StringToHash("Locomotion");
        private static readonly int SVictory = Animator.StringToHash("Victory");
        private static readonly int SDefeat = Animator.StringToHash("Defeat");

        private Vector3 lastPosition;
        private Vector2 blend;
        private bool matchEndedPlayed;
        private bool subscribed;
        private BallController trackedBall;
        private Quaternion neutralRotation;
        private float swingSpeed = 1f;
        private float swingUntil;

        public void Configure(bool opponent, Animator anim, float ownerMoveSpeed)
        {
            isOpponent = opponent;
            animator = anim;
            referenceSpeed = Mathf.Max(0.01f, ownerMoveSpeed);
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
            lastPosition = transform.position;
            neutralRotation = transform.rotation;
            trackedBall = FindObjectOfType<BallController>();
        }

        private void OnEnable()
        {
            // Without this a re-enable reads one enormous bogus velocity from the position it held
            // when it was switched off.
            lastPosition = transform.position;
            blend = Vector2.zero;
        }

        private void Start()
        {
            TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (subscribed) return;
            if (RallyManager.Instance == null) return;
            RallyManager.Instance.OnMatchEnded += HandleMatchEnded;
            subscribed = true;
        }

        private void OnDestroy()
        {
            if (subscribed && RallyManager.Instance != null)
            {
                RallyManager.Instance.OnMatchEnded -= HandleMatchEnded;
            }
        }

        private void Update()
        {
            // RallyManager may not have existed yet when this ran its Start (both are created in the
            // same scene load), so keep trying until the hook lands.
            if (!subscribed) TrySubscribe();

            if (animator == null) return;

            animator.speed = Time.time < swingUntil && !matchEndedPlayed ? swingSpeed : 1f;
            if (trackedBall == null) trackedBall = FindObjectOfType<BallController>();
            if (!matchEndedPlayed && trackedBall != null && trackedBall.State != BallState.Idle)
            {
                Vector3 direction = trackedBall.transform.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.1f)
                {
                    Vector3 localDirection = Quaternion.Inverse(neutralRotation) * direction;
                    if (isOpponent) localDirection = -localDirection;
                    float yaw = Mathf.Clamp(Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg, -65f, 65f);
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        neutralRotation * Quaternion.Euler(0, yaw, 0), 1f - Mathf.Exp(-10f * Time.deltaTime));
                }
            }

            float dt = Time.deltaTime;
            Vector3 worldVelocity = dt > 0.0001f ? (transform.position - lastPosition) / dt : Vector3.zero;
            lastPosition = transform.position;
            worldVelocity.y = 0f;

            Vector3 localVelocity = Quaternion.Inverse(transform.rotation) * worldVelocity;
            Vector2 target = ComputeMoveBlend(localVelocity, isOpponent, referenceSpeed);

            blend = Vector2.Lerp(blend, target, 1f - Mathf.Exp(-blendSharpness * dt));
            if (blend.sqrMagnitude < 0.0001f) blend = Vector2.zero;

            animator.SetFloat(PMoveX, blend.x);
            animator.SetFloat(PMoveZ, blend.y);
            animator.SetFloat(PSpeed, blend.magnitude);
        }

        /// <summary>
        /// World velocity to blend-tree coordinates, as a pure function so the sign convention can be
        /// checked without a running player loop.
        ///
        /// The owner objects only ever translate -- they are never rotated -- so "forward" is the
        /// fixed direction the rig was placed facing: +Z for the player, -Z for the opponent. That
        /// makes the opponent a straight negation on both axes rather than a real transform.
        /// </summary>
        public static Vector2 ComputeMoveBlend(Vector3 worldVelocity, bool isOpponent, float referenceSpeed)
        {
            worldVelocity.y = 0f;
            float facingSign = isOpponent ? -1f : 1f;
            Vector2 target = new Vector2(worldVelocity.x * facingSign, worldVelocity.z * facingSign)
                             / Mathf.Max(0.01f, referenceSpeed);
            return Vector2.ClampMagnitude(target, 1f);
        }

        /// <summary>Mirrors PaddleVisual.PlaySwing so the controllers can drive either visual.</summary>
        public void PlaySwing(bool isForehand, ShotType shotType, float power)
        {
            if (animator == null || matchEndedPlayed) return;
            float shotTempo = 1f;
            switch (shotType)
            {
                case ShotType.Dink: shotTempo = 0.72f; break;
                case ShotType.Lob: shotTempo = 0.84f; break;
                case ShotType.Serve: shotTempo = 0.92f; break;
                case ShotType.Topspin: shotTempo = 1.12f; break;
                case ShotType.Smash: shotTempo = 1.28f; break;
            }
            // The supplied pack only has left/right contact clips. Tempo gives soft, lifting and
            // attacking shots distinct weight while the paddle and ball trajectory carry the shape.
            swingSpeed = Mathf.Clamp(Mathf.Lerp(0.78f, 1.28f, Mathf.Clamp01(power)) * shotTempo,
                0.62f, 1.55f);
            swingUntil = Time.time + 0.45f / swingSpeed;
            animator.SetTrigger(isForehand ? PSmashRight : PSmashLeft);
        }

        public void PlayVictory()
        {
            if (animator == null) return;
            matchEndedPlayed = true;
            animator.SetTrigger(PVictory);
        }

        public void PlayDefeat()
        {
            if (animator == null) return;
            matchEndedPlayed = true;
            animator.SetTrigger(PDefeat);
        }

        /// <summary>Cleared on the next serve so a rematch doesn't start in a celebration pose.</summary>
        public void ResetResultPose()
        {
            matchEndedPlayed = false;
            transform.rotation = neutralRotation;
            swingUntil = 0f;
            if (animator == null) return;
            animator.ResetTrigger(PVictory);
            animator.ResetTrigger(PDefeat);
            // Victory and Defeat are terminal states (they hold the pose), so clearing the flag alone
            // left the character sliding around the court in that pose until a swing trigger happened
            // to pull it out -- and a rig that never got a swing through (the online opponent) stayed
            // frozen in it for a whole match. Only interrupt a result pose, never a swing in progress.
            if (!animator.isActiveAndEnabled) return;
            int current = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            int next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0).shortNameHash : 0;
            if (current == SVictory || current == SDefeat || next == SVictory || next == SDefeat)
                animator.CrossFadeInFixedTime(SLocomotion, 0.2f, 0);
        }

        private void HandleMatchEnded(bool playerWon)
        {
            bool thisSideWon = isOpponent ? !playerWon : playerWon;
            if (thisSideWon) PlayVictory();
            else PlayDefeat();
        }
    }
}
