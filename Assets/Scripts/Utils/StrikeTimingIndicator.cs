using UnityEngine;
using Pickleball.Gameplay;

namespace Pickleball.Utils
{
    public class StrikeTimingIndicator : MonoBehaviour
    {
        private void OnDestroy() { if (Instance == this) Instance = null; }
        public static StrikeTimingIndicator Instance { get; private set; }

        private LineRenderer outerRing;
        private LineRenderer innerTimingRing;

        private Vector3 targetPosition;
        private float arrivalTimestamp;
        private float totalDuration;
        private bool isActive = false;
        private int ringSegments = 32;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            CreateRings();
            Hide();
        }

        private void CreateRings()
        {
            Shader unlitShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            Material ringMat = Pickleball.Systems.OwnedRuntimeAssets.Track(gameObject, new Material(unlitShader));

            // Outer Ring
            GameObject outerObj = new GameObject("OuterRing");
            outerObj.transform.SetParent(transform);
            outerRing = outerObj.AddComponent<LineRenderer>();
            outerRing.useWorldSpace = false;
            outerRing.positionCount = ringSegments + 1;
            outerRing.startWidth = 0.06f;
            outerRing.endWidth = 0.06f;
            outerRing.material = ringMat;
            outerRing.startColor = new Color(0.2f, 0.8f, 1.0f, 0.7f);
            outerRing.endColor = new Color(0.2f, 0.8f, 1.0f, 0.7f);
            outerRing.loop = true;
            GenerateCirclePoints(outerRing, 0.65f);

            // Inner Contracting Timing Ring
            GameObject innerObj = new GameObject("InnerTimingRing");
            innerObj.transform.SetParent(transform);
            innerTimingRing = innerObj.AddComponent<LineRenderer>();
            innerTimingRing.useWorldSpace = false;
            innerTimingRing.positionCount = ringSegments + 1;
            innerTimingRing.startWidth = 0.08f;
            innerTimingRing.endWidth = 0.08f;
            innerTimingRing.material = ringMat;
            innerTimingRing.startColor = new Color(1.0f, 0.85f, 0.0f, 0.9f);
            innerTimingRing.endColor = new Color(1.0f, 0.85f, 0.0f, 0.9f);
            innerTimingRing.loop = true;
        }

        private void GenerateCirclePoints(LineRenderer lr, float radius)
        {
            for (int i = 0; i <= ringSegments; i++)
            {
                float angle = (i / (float)ringSegments) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                lr.SetPosition(i, new Vector3(x, 0.02f, z));
            }
        }

        public void Show(Vector3 targetPos, float duration, float strikeTimestamp)
        {
            targetPosition = new Vector3(targetPos.x, 0.02f, targetPos.z);
            transform.position = targetPosition;
            arrivalTimestamp = strikeTimestamp;
            totalDuration = Mathf.Max(0.1f, duration);
            isActive = true;

            if (outerRing != null) outerRing.enabled = true;
            if (innerTimingRing != null) innerTimingRing.enabled = true;
        }

        public void Retarget(Vector3 position, float strikeTimestamp)
        {
            targetPosition = new Vector3(position.x, 0.02f, position.z);
            transform.position = targetPosition;
            arrivalTimestamp = strikeTimestamp;
        }

        public bool IsShowing => isActive;

        /// <summary>Moves the strike moment without moving the ring.</summary>
        public void Retime(float strikeTimestamp)
        {
            arrivalTimestamp = strikeTimestamp;
        }

        // Hide()/Show() only toggle the ring renderers, not the GameObject itself: this component
        // is attached to the shared "Managers" object alongside every other manager singleton, so
        // gameObject.SetActive(false) here used to deactivate the entire Managers object (and with
        // it RallyManager, ScreenManager, InputManager, etc.) the instant Awake() called Hide().
        public void Hide()
        {
            isActive = false;
            if (outerRing != null) outerRing.enabled = false;
            if (innerTimingRing != null) innerTimingRing.enabled = false;
        }

        private void Update()
        {
            if (!isActive) return;

            float timeRemaining = arrivalTimestamp - Pickleball.Systems.MatchClock.NowSeconds();
            float progress = 1.0f - Mathf.Clamp01(timeRemaining / totalDuration);

            // Keep the timing cue compact enough to read the ball and both players.
            float innerRadius = Mathf.Lerp(1.65f, 0.65f, progress);
            GenerateCirclePoints(innerTimingRing, innerRadius);

            // Color shift as timing sweet spot nears
            float absError = Mathf.Abs(timeRemaining);
            Color timingColor;
            if (absError < 0.08f)
            {
                timingColor = new Color(1.0f, 0.85f, 0.0f, 1.0f); // Gold / Perfect
                float pulse = 0.9f + Mathf.Sin(Time.time * 25f) * 0.1f;
                outerRing.transform.localScale = Vector3.one * pulse;
            }
            else if (absError < 0.18f)
            {
                timingColor = new Color(0.2f, 0.9f, 1.0f, 0.9f); // Cyan / Great
                outerRing.transform.localScale = Vector3.one;
            }
            else if (absError < 0.32f)
            {
                timingColor = new Color(0.3f, 0.9f, 0.3f, 0.8f); // Green / Good
                outerRing.transform.localScale = Vector3.one;
            }
            else
            {
                timingColor = new Color(1.0f, 0.3f, 0.2f, 0.6f); // Red / Early or Late
                outerRing.transform.localScale = Vector3.one;
            }

            innerTimingRing.startColor = timingColor;
            innerTimingRing.endColor = timingColor;

            // Auto-hide if far past arrival time
            if (timeRemaining < -0.4f)
            {
                Hide();
            }
        }
    }
}
