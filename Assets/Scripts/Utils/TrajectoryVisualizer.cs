using UnityEngine;
using Pickleball.Gameplay;
using Pickleball.Systems;
using Pickleball.UI;

namespace Pickleball.Utils
{
    [RequireComponent(typeof(LineRenderer))]
    public class TrajectoryVisualizer : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [SerializeField] private int lineSegmentCount = 20;
        [SerializeField] private PlayerController player;
        [SerializeField] private Transform landingTargetIndicator;

        private LineRenderer lineRenderer;
        private ShotType previewShot = ShotType.Flat;
        private BallController ball;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.positionCount = lineSegmentCount;
            lineRenderer.useWorldSpace = true;
            lineRenderer.numCornerVertices = 3;
            lineRenderer.numCapVertices = 3;
            lineRenderer.enabled = false;

            if (player == null) player = FindObjectOfType<PlayerController>();
            ball = FindObjectOfType<BallController>();
        }

        private void Start()
        {
            if (player == null) player = FindObjectOfType<PlayerController>();

            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnSwipeStart += HandleSwipeStart;
                InputManager.Instance.OnSwipeUpdate += HandleSwipeUpdate;
                InputManager.Instance.OnSwipeCompleted += HandleSwipeEnd;
                InputManager.Instance.OnShotTypePreview += HandleShotTypePreview;
            }
        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnSwipeStart -= HandleSwipeStart;
                InputManager.Instance.OnSwipeUpdate -= HandleSwipeUpdate;
                InputManager.Instance.OnSwipeCompleted -= HandleSwipeEnd;
                InputManager.Instance.OnShotTypePreview -= HandleShotTypePreview;
            }
        }

        private void HandleShotTypePreview(ShotType type)
        {
            previewShot = type;
            ApplyPreviewStyle(Vector2.one * 0.5f);
        }

        private void HandleSwipeStart()
        {
            previewShot = ShotType.Flat;
            if (lineRenderer != null) lineRenderer.enabled = true;
            if (landingTargetIndicator != null) landingTargetIndicator.gameObject.SetActive(true);
        }

        private void HandleSwipeUpdate(Vector2 swipeVector)
        {
            if (player == null) player = FindObjectOfType<PlayerController>();
            if (player == null || ShotSystem.Instance == null || lineRenderer == null) return;
            // A touch can now begin before it is the player's turn (LateUpdate keeps the preview
            // hidden until then), so bring the preview up as soon as the swipe becomes playable.
            if (RallyManager.Instance != null && !RallyManager.Instance.CanSideHit(0)) return;
            lineRenderer.enabled = true;
            if (landingTargetIndicator != null) landingTargetIndicator.gameObject.SetActive(true);

            Vector3 startPos = ball != null && ball.CurrentShot.hitterId != 0 && ball.transform.position.z < 0f
                ? ball.transform.position : player.transform.position;
            // RNG-free preview: shows the intended target, never touches the match's deterministic stream.
            Vector3 targetPos = ShotSystem.Instance.PredictTargetPosition(startPos, swipeVector, true, previewShot);

            float arc = ShotSystem.Instance.PredictArcHeight(startPos, targetPos, previewShot);
            if (RallyManager.Instance != null && RallyManager.Instance.State == MatchState.Serving)
            {
                float power = Mathf.Clamp01(swipeVector.magnitude);
                float depth = power;
                var target = Pickleball.Sim.ServeRules.BuildTarget(0, RallyManager.Instance.CurrentServeFromRight, swipeVector.x, depth);
                targetPos = new Vector3(target.X, target.Y, target.Z);
                startPos = player.transform.position + Vector3.up * 0.2f;
                arc = Mathf.Lerp(0.7f, 1.25f, power);
            }
            for (int i = 0; i < lineSegmentCount; i++)
            {
                float t = i / (float)(lineSegmentCount - 1);
                lineRenderer.SetPosition(i, CalculateArcPoint(startPos, targetPos, arc, t));
            }

            ApplyPreviewStyle(swipeVector);

            if (landingTargetIndicator != null)
            {
                landingTargetIndicator.position = targetPos;
                landingTargetIndicator.localScale = Vector3.one * PreviewMarkerScale(previewShot);
            }
        }

        private void HandleSwipeEnd(Vector2 swipeVector, float timestamp)
        {
            if (lineRenderer != null) lineRenderer.enabled = false;
            if (landingTargetIndicator != null)
            {
                landingTargetIndicator.gameObject.SetActive(false);
                landingTargetIndicator.localScale = Vector3.one;
            }
        }

        private void LateUpdate()
        {
            if (InputManager.Instance != null && (!InputManager.Instance.IsSwiping ||
                (RallyManager.Instance != null && !RallyManager.Instance.CanSideHit(0))))
                HandleSwipeEnd(Vector2.zero, 0f);
        }

        private static Color PreviewColor(ShotType type)
        {
            switch (type)
            {
                case ShotType.Smash: return new Color(1f, 0.45f, 0.2f, 0.9f);
                case ShotType.Lob: return new Color(0.7f, 0.6f, 1f, 0.9f);
                case ShotType.Dink: return new Color(0.4f, 0.95f, 0.55f, 0.9f);
                case ShotType.Slice: return new Color(0.4f, 0.85f, 1f, 0.9f);
                case ShotType.Topspin: return new Color(0.84f, 0.98f, 0.12f, 0.95f);
                case ShotType.Serve: return new Color(1f, 0.82f, 0.35f, 0.95f);
                default: return new Color(0.2f, 0.9f, 1f, 0.9f);
            }
        }

        private void ApplyPreviewStyle(Vector2 swipeVector)
        {
            if (lineRenderer == null) return;
            Color c = PreviewColor(previewShot);
            float baseWidth = previewShot == ShotType.Smash ? 0.13f :
                previewShot == ShotType.Dink ? 0.055f : previewShot == ShotType.Lob ? 0.075f : 0.09f;
            float power = Mathf.Clamp01(swipeVector.magnitude);
            lineRenderer.startWidth = baseWidth * Mathf.Lerp(0.75f, 1.2f, power);
            lineRenderer.endWidth = previewShot == ShotType.Lob ? lineRenderer.startWidth * 0.8f :
                lineRenderer.startWidth * 0.38f;
            lineRenderer.startColor = c;
            lineRenderer.endColor = new Color(c.r, c.g, c.b, 0.18f);
        }

        private static float PreviewMarkerScale(ShotType type)
        {
            switch (type)
            {
                case ShotType.Dink: return 0.72f;
                case ShotType.Lob: return 1.3f;
                case ShotType.Smash: return 0.85f;
                default: return 1f;
            }
        }

        private Vector3 CalculateArcPoint(Vector3 start, Vector3 target, float arcHeight, float t)
        {
            Vector3 groundPos = Vector3.Lerp(start, target, t);
            float baseHeight = Mathf.Lerp(start.y, target.y, t);
            float addedHeight = 4f * arcHeight * t * (1f - t);
            return new Vector3(groundPos.x, baseHeight + addedHeight, groundPos.z);
        }
    }
}
