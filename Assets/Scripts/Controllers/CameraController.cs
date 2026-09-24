using UnityEngine;

namespace Pickleball.Gameplay
{
    public class CameraController : MonoBehaviour
    {
        // Framing is tuned for the 1080x1920 portrait target. The court's play area is 9 wide by 18
        // deep, which is almost exactly the screen's 9:16 ratio, so a high, steeply-pitched view fits
        // the whole court with very little waste. The previous setup (y 9.5, z -14, pitch 32, FOV 45)
        // sat far too low and shallow: the top ~40% of the frame was empty background past the far
        // baseline, the near baseline at z=-9 was off the bottom of the screen entirely, and at the
        // player's own end the camera only covered x +/-2.75 -- so the player, who auto-runs to
        // intercept shots out to x +/-4.5, would regularly run straight off the side of the screen.
        // Pitch is the main lever on how three-dimensional the court reads: steeper flattens toward a
        // top-down plan view, shallower gives depth but foreshortens the far court and eventually
        // strands the near baseline partway up the screen. 54 degrees is the shallowest setting that
        // still keeps every in-bounds landing spot and both players fully on screen.
        [Header("Camera Positioning")]
        [SerializeField] private Vector3 offsetFromCourt = new Vector3(0f, 22.0f, -15.5f);
        [SerializeField] private Vector3 lookAtRotation = new Vector3(54f, 0f, 0f);
        [SerializeField] private float xTrackingFactor = 0.10f;
        // Hard cap on the sideways pan. The framing only has ~0.4 units of horizontal slack past the
        // sidelines, so an unbounded pan would push the far sideline off screen.
        [SerializeField] private float maxHorizontalPan = 0.9f;
        [SerializeField] private float smoothSpeed = 4.0f;

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Transform ballTransform;
        
        private Camera cam;

        // Shake -- a smoothed offset chased toward a fresh random target a few times a second, rather
        // than a raw random displacement every frame (which read as jitter and fought the position
        // smoothing below).
        private float shakeTimer = 0f;
        private float shakeDuration = 0f;
        private float shakeIntensity = 0f;
        private Vector3 shakeOffset = Vector3.zero;
        private Vector3 shakeTarget = Vector3.zero;
        private float shakeRetargetTimer = 0f;
        private Vector3 smoothedPosition;

        // Punch Zoom
        private float punchTimer = 0f;
        private float punchDuration = 0f;
        private float punchAmount = 0f;
        // 42 -> 48: the previous value framed the court perfectly but left everything past the far
        // baseline entirely outside the frustum (see Stadium under Court Environment) -- the top edge
        // of the view crossed ground level at z~18.4, short of the stadium wall at z=20.4, so no
        // amount of scenery back there could ever be seen. 48 is the smallest bump that brings the
        // wall and its gold trim into frame while keeping the 54-degree pitch and position that keep
        // every in-bounds landing spot and both players on screen (see offsetFromCourt/lookAtRotation
        // above).
        private float defaultFOV = 48f;
        private bool venueIntroPlaying;
        private float venueIntroStartedAt;
        private float venueIntroDuration;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.fieldOfView = defaultFOV;
            }

            if (playerTransform == null)
            {
                var player = FindObjectOfType<PlayerController>();
                if (player != null) playerTransform = player.transform;
            }

            if (ballTransform == null)
            {
                var ball = FindObjectOfType<BallController>();
                if (ball != null) ballTransform = ball.transform;
            }
        }

        private void Start()
        {
            SetInitialCameraPosition();
        }

        private void LateUpdate()
        {
            if (playerTransform == null)
            {
                var player = FindObjectOfType<PlayerController>();
                if (player != null) playerTransform = player.transform;
            }

            if (ballTransform == null)
            {
                var ball = FindObjectOfType<BallController>();
                if (ball != null) ballTransform = ball.transform;
            }

            Vector3 desiredPosition = new Vector3(ComputeTargetX(), offsetFromCourt.y, offsetFromCourt.z);

            if (venueIntroPlaying)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - venueIntroStartedAt) / Mathf.Max(0.1f, venueIntroDuration));
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                Vector3 introStart = new Vector3(0f, 36f, 5f);
                smoothedPosition = Vector3.LerpUnclamped(introStart, desiredPosition, eased);
                transform.position = smoothedPosition;
                transform.rotation = Quaternion.SlerpUnclamped(Quaternion.Euler(82f, 0f, 0f), Quaternion.Euler(lookAtRotation), eased);
                if (cam != null) cam.fieldOfView = Mathf.Lerp(62f, defaultFOV, eased);
                if (t >= 1f) venueIntroPlaying = false;
                return;
            }

            // Smooth the *base* position in its own field; the shake is added only on the way out to
            // the transform and never read back, so it can't compound into a runaway offset (which it
            // did badly at low timeScale, where the correction lerp shrinks but the shake doesn't).
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            smoothedPosition = Vector3.Lerp(smoothedPosition, desiredPosition, Mathf.Clamp01(smoothSpeed * dt));

            if (shakeTimer > 0f)
            {
                float strength = shakeIntensity * (shakeTimer / Mathf.Max(0.0001f, shakeDuration));
                shakeRetargetTimer -= dt;
                if (shakeRetargetTimer <= 0f)
                {
                    shakeTarget = Random.insideUnitSphere * strength;
                    shakeTarget.z *= 0.4f; // less depth wobble -- it reads worst on this steep camera
                    shakeRetargetTimer = 0.035f;
                }
                shakeOffset = Vector3.Lerp(shakeOffset, shakeTarget, Mathf.Clamp01(20f * dt));
                shakeTimer -= dt;
            }
            else
            {
                shakeOffset = Vector3.Lerp(shakeOffset, Vector3.zero, Mathf.Clamp01(14f * dt));
            }

            transform.position = smoothedPosition + shakeOffset;
            transform.rotation = Quaternion.Euler(lookAtRotation);

            if (cam != null)
            {
                if (punchTimer > 0f)
                {
                    float t = punchTimer / punchDuration;
                    cam.fieldOfView = defaultFOV - (punchAmount * t);
                    punchTimer -= Time.deltaTime;
                }
                else
                {
                    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, defaultFOV, Time.deltaTime * smoothSpeed);
                }
            }
        }

        /// <summary>
        /// Sideways pan, weighted toward the player with the ball as a lighter secondary influence.
        /// This used to hardcode 0.3/0.15 and ignore the serialized xTrackingFactor entirely; with the
        /// current framing that much pan would swing the camera up to 2 units and drag the far
        /// sideline off screen, so it is now driven by the field and clamped.
        /// </summary>
        private float ComputeTargetX()
        {
            float playerX = playerTransform != null ? playerTransform.position.x : 0f;
            float ballX = ballTransform != null ? ballTransform.position.x : 0f;

            float targetX = (playerX * xTrackingFactor) + (ballX * xTrackingFactor * 0.5f);
            return Mathf.Clamp(targetX, -maxHorizontalPan, maxHorizontalPan);
        }

        public void SetInitialCameraPosition()
        {
            smoothedPosition = new Vector3(ComputeTargetX(), offsetFromCourt.y, offsetFromCourt.z);
            shakeOffset = Vector3.zero;
            transform.position = smoothedPosition;
            transform.rotation = Quaternion.Euler(lookAtRotation);
        }

        public void BeginVenueIntro(float duration)
        {
            venueIntroDuration = Mathf.Max(0.5f, duration);
            venueIntroStartedAt = Time.unscaledTime;
            venueIntroPlaying = true;
        }

        public void Shake(float intensity, float duration)
        {
            shakeIntensity = intensity;
            shakeDuration = duration;
            shakeTimer = duration;
        }

        public void PunchZoom(float amount, float duration)
        {
            punchAmount = amount;
            punchDuration = duration;
            punchTimer = duration;
        }
    }
}
