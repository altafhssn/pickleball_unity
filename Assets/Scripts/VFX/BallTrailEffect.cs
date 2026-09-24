using UnityEngine;
using Pickleball.Gameplay;

namespace Pickleball.VFX
{
    public class BallTrailEffect : MonoBehaviour
    {
        private TrailRenderer trailRenderer;

        private void Awake()
        {
            trailRenderer = GetComponent<TrailRenderer>();
            if (trailRenderer == null)
            {
                trailRenderer = gameObject.AddComponent<TrailRenderer>();
            }

            // Short and thin: from the steep top-down camera a longer, wider trail projected to a
            // flat opaque beam that read as a laser rather than a moving ball. This is a wisp that
            // tapers away fast, so the eye tracks the ball itself.
            trailRenderer.time = 0.16f;
            trailRenderer.startWidth = 0.10f;
            trailRenderer.endWidth = 0f;
            trailRenderer.numCapVertices = 2;

            // Assign URP-compatible unlit material to prevent pink missing texture
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            trailRenderer.sharedMaterial = Pickleball.Systems.OwnedRuntimeAssets.Track(gameObject, new Material(shader));

            neutralColor = new Color(1f, 0.92f, 0.55f);
            ApplyGradient(neutralColor, 0.45f);
        }

        private Color neutralColor = new Color(1f, 0.92f, 0.55f);

        /// <summary>Wipe the ribbon so a shot that starts after the ball teleported (a serve, a new
        /// point) doesn't draw one long streak connecting the old and new positions.</summary>
        public void ClearTrail()
        {
            if (trailRenderer != null) trailRenderer.Clear();
        }

        private void ApplyGradient(Color head, float headAlpha)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(head, 0.0f), new GradientColorKey(head, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(headAlpha, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            trailRenderer.colorGradient = gradient;
        }

        public void SetTrailQuality(ShotQuality quality, float compositeScore = 0f)
        {
            // Only the two top tiers get a signature colour + a slightly fatter streak -- a reward
            // cue, not a per-shot readout. Everything else keeps the neutral wisp so a normal rally
            // doesn't strobe through five colours.
            switch (quality)
            {
                case ShotQuality.Perfect:
                    ApplyGradient(new Color(1.0f, 0.82f, 0.15f), 0.85f);
                    trailRenderer.startWidth = 0.16f;
                    break;
                case ShotQuality.Great:
                    ApplyGradient(new Color(0.35f, 0.9f, 1.0f), 0.7f);
                    trailRenderer.startWidth = 0.12f;
                    break;
                default:
                    ApplyGradient(neutralColor, 0.45f);
                    trailRenderer.startWidth = 0.10f;
                    break;
            }
        }
    }
}
