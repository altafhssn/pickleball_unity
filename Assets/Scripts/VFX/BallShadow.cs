using UnityEngine;
using Pickleball.Gameplay;

namespace Pickleball.VFX
{
    public class BallShadow : MonoBehaviour
    {
        private BallController ball;
        private Renderer rend;
        private Texture2D softDisc;
        private Material shadowMaterial;

        private void Start()
        {
            ball = GetComponentInParent<BallController>();

            rend = GetComponent<Renderer>();
            if (rend != null)
            {
                // Sprites/Default is always alpha-blended and multiplies texture alpha by the tint --
                // exactly what a soft shadow needs, and it dodges per-pipeline transparency setup.
                Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                shadowMaterial = Pickleball.Systems.OwnedRuntimeAssets.Track(gameObject, new Material(s));
                rend.sharedMaterial = shadowMaterial;
                Texture2D tex = SoftDisc();
                shadowMaterial.mainTexture = tex;
                if (shadowMaterial.HasProperty("_BaseMap")) shadowMaterial.SetTexture("_BaseMap", tex);
                shadowMaterial.color = new Color(0f, 0f, 0f, 0.5f);
            }
        }

        private Texture2D SoftDisc()
        {
            if (softDisc != null) return softDisc;
            const int N = 64;
            softDisc = Pickleball.Systems.OwnedRuntimeAssets.Track(gameObject,
                new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp });
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N - 0.5f, dy = (y + 0.5f) / N - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;      // 0 at centre, 1 at edge
                    float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.55f, 1f, d));
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            softDisc.SetPixels32(px);
            softDisc.Apply(false, true);
            return softDisc;
        }

        private void Update()
        {
            if (ball == null)
            {
                ball = GetComponentInParent<BallController>();
                if (ball == null) return;
            }

            Vector3 ballPos = ball.transform.position;
            // Don't rotate/scale with the parent ball -- keep the shadow a flat disc pinned to the floor.
            transform.position = new Vector3(ballPos.x, 0.02f, ballPos.z);
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // The shadow is the primary height cue on a near-top-down court, so drive it hard: a
            // ball on the deck casts a tight, dark disc; a lob overhead casts a big faint one. The
            // old y/5 curve barely moved for anything under a lob.
            float h = Mathf.Clamp01(ballPos.y / 3.2f);
            float scale = Mathf.Lerp(0.9f, 2.6f, h);
            // Divide out the parent ball's scale so the shadow's on-court size is independent of it.
            float parentScale = transform.parent != null ? Mathf.Max(0.01f, transform.parent.lossyScale.x) : 1f;
            float s = scale / parentScale;
            transform.localScale = new Vector3(s, s, 1f);

            if (shadowMaterial != null)
            {
                Color c = shadowMaterial.color;
                c.a = Mathf.Lerp(0.55f, 0.12f, h);
                shadowMaterial.color = c;
            }
        }
    }
}
