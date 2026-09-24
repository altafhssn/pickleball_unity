using UnityEngine;
using Pickleball.Gameplay;

namespace Pickleball.VFX
{
    /// <summary>A ground ring at the spot the ball is about to land, tightening as the ball nears it.
    /// On a near-top-down court the ball reads as a small dot with little depth cue; a "it lands
    /// here, now" marker is what lets a player commit to a position and a swing. Created by
    /// BallController itself so it needs no scene wiring.</summary>
    public class BallLandingMarker : MonoBehaviour
    {
        private BallController ball;
        private LineRenderer ring;
        private const int Segments = 40;

        public void Bind(BallController owner)
        {
            ball = owner;
        }

        private void Awake()
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(transform, false);
            ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = Segments;
            ring.startWidth = ring.endWidth = 0.05f;
            ring.numCornerVertices = 2;
            // Sprites/Default respects the LineRenderer's per-vertex colour (URP/Unlit ignores it).
            Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            ring.sharedMaterial = Pickleball.Systems.OwnedRuntimeAssets.Track(gameObject, new Material(s));
            ring.enabled = false;
        }

        private void LateUpdate()
        {
            if (ball == null) { ring.enabled = false; return; }

            bool show = ball.State == BallState.InFlight;
            ring.enabled = show;
            if (!show) return;

            Vector3 c = ball.CurrentShot.targetPosition;
            c.y = 0.03f;

            float p = Mathf.Clamp01(ball.FlightProgress);
            // Shot silhouette survives release: a lob announces a broad hang-time zone, a dink a
            // compact soft target, and an attacking ball a tighter destination.
            float startRadius = ball.CurrentShot.shotType == ShotType.Lob ? 1.75f :
                ball.CurrentShot.shotType == ShotType.Dink ? 0.95f :
                ball.CurrentShot.shotType == ShotType.Smash ? 1.1f : 1.4f;
            float radius = Mathf.Lerp(startRadius, 0.28f, p);

            Color col = ball.CurrentShot.hitterId == 0 ? ShotColor(ball.CurrentShot.shotType) :
                new Color(1f, 0.55f, 0.35f, 0.95f); // incoming opponent shot stays urgent and consistent
            ring.startWidth = ring.endWidth = ball.CurrentShot.shotType == ShotType.Smash ? 0.085f :
                ball.CurrentShot.shotType == ShotType.Dink ? 0.04f : 0.055f;
            col.a *= Mathf.Lerp(0.35f, 1f, p);
            ring.startColor = ring.endColor = col;

            for (int i = 0; i < Segments; i++)
            {
                float a = (i / (float)Segments) * Mathf.PI * 2f;
                ring.SetPosition(i, c + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
        }

        private static Color ShotColor(ShotType type)
        {
            switch (type)
            {
                case ShotType.Dink: return new Color(0.40f, 0.95f, 0.55f, 0.95f);
                case ShotType.Lob: return new Color(0.72f, 0.60f, 1f, 0.95f);
                case ShotType.Smash: return new Color(1f, 0.38f, 0.15f, 1f);
                case ShotType.Topspin: return new Color(0.84f, 0.98f, 0.12f, 0.95f);
                case ShotType.Slice: return new Color(0.35f, 0.82f, 1f, 0.95f);
                case ShotType.Serve: return new Color(1f, 0.82f, 0.35f, 0.95f);
                default: return new Color(0.35f, 0.75f, 1f, 0.9f);
            }
        }
    }
}
