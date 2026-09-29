using System;
using System.Collections.Generic;
using UnityEngine;
using Pickleball.Data;
using Pickleball.Systems;
using Sim = Pickleball.Sim;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pickleball.Gameplay
{
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Swipe Parameters (reference DPI 160 -- scaled to the device at runtime)")]
        [SerializeField] private float minSwipeDistance = 50f;
        [SerializeField] private float fullPowerSwipeSpeed = 1200f;

        // Raw pixel thresholds mean the same physical flick registers as a different shot on a dense
        // phone screen vs a tablet. Everything below is measured in "reference pixels" and divided by
        // this factor so a swipe of a given physical length behaves identically across devices.
        private float dpiScale = 1f;

        // The touch's path in reference pixels, timed in real (unscaled) seconds: finger speed is a
        // physical quantity, and the tick-quantized match clock is 16.7 ms coarse -- a large error on
        // an 80 ms flick. The release timestamp that scores timing still uses the match clock.
        private const int MaxSamples = 256;
        private readonly List<Sim.SwipeSample> samples = new List<Sim.SwipeSample>(MaxSamples);
        private float touchStartTime;
        private bool isSwiping = false;

        public bool IsSwiping => isSwiping;
        /// <summary>Aim direction with placement (depth, 0..1) as its length, for the stroke so far.</summary>
        public Vector2 CurrentSwipeVector { get; private set; }
        /// <summary>How hard the stroke so far would strike the ball, 0..1 (Sim.ShotSim.SwingPace).</summary>
        public float CurrentPace { get; private set; }
        public ShotType CurrentPreviewShot { get; private set; } = ShotType.Flat;

        public event Action OnSwipeStart;
        public event Action<Vector2> OnSwipeUpdate;
        public event Action<Vector2, float> OnSwipeCompleted;
        /// <summary>A finished shot gesture: aim (direction x placement), release time, shot type, pace.</summary>
        public event Action<Vector2, float, ShotType, float> OnShotGestureCompleted;
        /// <summary>Fired continuously while dragging with the shot the current gesture *would* throw,
        /// so the HUD/trajectory preview can show it before release.</summary>
        public event Action<ShotType> OnShotTypePreview;
        /// <summary>A touch lifted without moving far enough to be a swipe (and not a tap-serve).
        /// Used to be dropped silently.</summary>
        public event Action OnTouchTooShort;

        /// <summary>Height the ball is expected to be struck at, so the live preview and the released
        /// shot agree on whether a smash is on (see SwipeGesture.ForContactHeight). Set by
        /// PlayerController; unset means no restriction.</summary>
        public Func<float> ExpectedContactHeight { get; set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            float dpi = Screen.dpi;
            dpiScale = dpi > 40f ? dpi / 160f : 1f;
        }

        // Settings' "Swipe sensitivity" (0..1, default 0.5) used to round-trip through the save file
        // and be read by nothing. Higher sensitivity -> a shorter flick registers the same gesture.
        // ~1.0 at the default so shipping behaviour is unchanged; 0.55 at max, 1.6 at min.
        private static float SensScale => Mathf.Lerp(1.6f, 0.55f, Mathf.Clamp01(MetaGameState.SwipeSensitivity));

        // Every distance threshold in this class is measured against this: device DPI so a physical
        // flick feels the same everywhere, times the player's sensitivity preference.
        private float GestureScale => dpiScale * SensScale;

        private float PaceFor(System.Numerics.Vector2 referenceDelta, float duration) => Sim.ShotSim.SwingPace(
            referenceDelta.Length(), duration, fullPowerSwipeSpeed);

        private void Update()
        {
            if (Time.timeScale <= 0f) { CancelSwipe(); return; }
            HandleInput();
        }

        private void HandleInput()
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            if (pointer != null)
            {
                bool wasPressed = pointer.press.wasPressedThisFrame;
                bool isPressed = pointer.press.isPressed;
                bool wasReleased = pointer.press.wasReleasedThisFrame;
                Vector2 pointerPos = pointer.position.ReadValue();

                if (wasPressed) BeginSwipe(pointerPos);
                else if (isPressed && isSwiping) UpdateSwipe(pointerPos);
                else if (wasReleased && isSwiping) EndSwipe(pointerPos);
                return;
            }
#endif

#if !ENABLE_INPUT_SYSTEM || UNITY_EDITOR
            try
            {
                if (Input.GetMouseButtonDown(0)) BeginSwipe(Input.mousePosition);
                else if (Input.GetMouseButton(0) && isSwiping) UpdateSwipe(Input.mousePosition);
                else if (Input.GetMouseButtonUp(0) && isSwiping) EndSwipe(Input.mousePosition);
            }
            catch (InvalidOperationException)
            {
                // Swallowed if Legacy Input is disabled in project settings and Pointer.current handled it above
            }
#endif
        }

        private void BeginSwipe(Vector2 pos)
        {
            if (Time.timeScale <= 0f) return;
            // Track the touch even before it is the player's turn: a thumb put down while the
            // opponent is still swinging used to be discarded, so the flick that followed did nothing.
            // Whether the finished swipe is a legal hit is decided on release (PlayerController).
            if (RallyManager.Instance == null || !RallyManager.Instance.AcceptsGestureFrom(0)) return;
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                var pointerData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                    { position = pos };
                var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, hits);
                foreach (var hit in hits)
                    if (hit.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null) return;
            }
            CurrentSwipeVector = Vector2.zero;
            CurrentPace = 0f;
            CurrentPreviewShot = RallyManager.Instance.State == MatchState.Serving ? ShotType.Serve : ShotType.Flat;
            samples.Clear();
            AddSample(pos);
            touchStartTime = Time.unscaledTime;
            isSwiping = true;
            OnSwipeStart?.Invoke();
        }

        private void AddSample(Vector2 pos)
        {
            if (samples.Count >= MaxSamples) samples.RemoveAt(0);
            Vector2 reference = pos / GestureScale;
            samples.Add(new Sim.SwipeSample(new System.Numerics.Vector2(reference.x, reference.y), Time.unscaledTime));
        }

        /// <summary>The swing vector for the stroke measured so far: its angle is the aim direction
        /// and its length the placement (depth) -- both from the stroke's shape, never its speed.
        /// Speed is the separate pace (<see cref="PaceFor"/>).</summary>
        private static Vector2 SwingVector(System.Numerics.Vector2 stroke)
        {
            if (stroke.LengthSquared() < 0.0001f) return Vector2.zero;
            System.Numerics.Vector2 direction = Sim.SwipeGesture.AimDirection(stroke);
            float depth = Sim.ShotSim.SwingDepth(stroke.Length());
            // Screen-left always means court-left, whichever hand holds the device.
            return new Vector2(direction.X, direction.Y) * depth;
        }

        /// <summary>The rally shot a stroke plays, given where the ball will be met. An upward stroke
        /// that curves (<paramref name="bend"/>) is the lob -- the only way to play one.</summary>
        private ShotType RallyShotFor(System.Numerics.Vector2 stroke, float seconds, float bend)
        {
            var gesture = Sim.SwipeGesture.Classify(stroke, seconds, bend);
            float height = ExpectedContactHeight != null ? ExpectedContactHeight() : float.PositiveInfinity;
            return (ShotType)Sim.SwipeGesture.ForContactHeight(gesture, height);
        }

        private void UpdateSwipe(Vector2 pos)
        {
            AddSample(pos);
            Sim.SwipeGesture.MeasureStroke(samples, minSwipeDistance, out var stroke, out float seconds, out float bend);
            CurrentSwipeVector = SwingVector(stroke);
            CurrentPace = PaceFor(stroke, Mathf.Max(0.01f, seconds));

            CurrentPreviewShot = RallyManager.Instance != null && RallyManager.Instance.State == MatchState.Serving
                ? ShotType.Serve : RallyShotFor(stroke, seconds, bend);
            OnShotTypePreview?.Invoke(CurrentPreviewShot);
            OnSwipeUpdate?.Invoke(CurrentSwipeVector);
        }

        private void EndSwipe(Vector2 pos)
        {
            if (Time.timeScale <= 0f)
            {
                CancelSwipe();
                return;
            }
            AddSample(pos);
            Sim.SwipeGesture.MeasureStroke(samples, minSwipeDistance, out var stroke, out float strokeSeconds, out float bend);
            float touchDuration = Time.unscaledTime - touchStartTime;

            if (stroke.Length() >= minSwipeDistance)
            {
                Vector2 finalSwipeVector = SwingVector(stroke);
                float pace = PaceFor(stroke, Mathf.Max(0.01f, strokeSeconds));
                CurrentSwipeVector = finalSwipeVector;
                CurrentPace = pace;

                float releaseTimestamp = MatchClock.NowSeconds();
                ShotType detectedShot = RallyShotFor(stroke, strokeSeconds, bend);
                // Development builds only: what the gesture was read as, for on-device tuning
                // (adb logcat -s Unity | grep "\[Swing\]").
                if (Debug.isDebugBuild)
                    Debug.Log(string.Format("[Swing] stroke ({0:F0},{1:F0}) {2:F0}ms bend {3:F2} -> {4} depth {5:F2} pace {6:F2} aim {7:F2}",
                        stroke.X, stroke.Y, strokeSeconds * 1000f, bend, detectedShot,
                        finalSwipeVector.magnitude, pace, finalSwipeVector.x));

                OnSwipeCompleted?.Invoke(finalSwipeVector, releaseTimestamp);
                OnShotGestureCompleted?.Invoke(finalSwipeVector, releaseTimestamp, detectedShot, pace);
            }
            else if (RallyManager.Instance != null && RallyManager.Instance.State == MatchState.Serving &&
                RallyManager.Instance.IsPlayerServing && touchDuration < 0.4f)
            {
                // The court prompts "tap to serve"; a swipe still offers deliberate placement.
                Vector2 tapServe = new Vector2(0f, 0.65f);
                float timestamp = MatchClock.NowSeconds();
                OnSwipeCompleted?.Invoke(tapServe, timestamp);
                OnShotGestureCompleted?.Invoke(tapServe, timestamp, ShotType.Serve, 0.65f);
            }
            else
            {
                OnTouchTooShort?.Invoke();
            }

            isSwiping = false;
            CurrentSwipeVector = Vector2.zero;
            CurrentPace = 0f;
            CurrentPreviewShot = ShotType.Flat;
        }

        private void CancelSwipe()
        {
            isSwiping = false;
            samples.Clear();
            CurrentSwipeVector = Vector2.zero;
            CurrentPace = 0f;
            CurrentPreviewShot = ShotType.Flat;
        }

        private void OnDisable() => CancelSwipe();
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            OnSwipeStart = null;
            OnSwipeUpdate = null;
            OnSwipeCompleted = null;
            OnShotGestureCompleted = null;
            OnShotTypePreview = null;
            OnTouchTooShort = null;
            ExpectedContactHeight = null;
        }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelSwipe(); }
    }
}
