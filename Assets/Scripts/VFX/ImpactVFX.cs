using UnityEngine;
using Pickleball.Gameplay;
using Pickleball.Systems;

namespace Pickleball.VFX
{
    public class ImpactVFX : MonoBehaviour
    {
        public static ImpactVFX Instance { get; private set; }
        private ParticleSystem hits;
        private ParticleSystem bounces;
        private readonly System.Random random = new System.Random(31415);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            var texture = OwnedRuntimeAssets.Track(gameObject,
                new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "ImpactDisc_Runtime", wrapMode = TextureWrapMode.Clamp });
            var pixels = new Color32[32 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float radius = new Vector2((x + 0.5f) / 16f - 1f, (y + 0.5f) / 16f - 1f).magnitude;
                    pixels[y * 32 + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01((1f - radius) * 4f)));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var material = OwnedRuntimeAssets.Track(gameObject, new Material(shader) { name = "ImpactShared_Runtime", mainTexture = texture });
            hits = CreateEmitter("HitParticles", material);
            bounces = CreateEmitter("BounceParticles", material);
        }

        private ParticleSystem CreateEmitter(string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 128;
            main.gravityModifier = 0.35f;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return ps;
        }

        private void Emit(ParticleSystem emitter, Vector3 position, Color color, int count, float speed)
        {
            if (emitter == null || !isActiveAndEnabled) return;
            for (int i = 0; i < count; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float velocity = speed * (0.5f + (float)random.NextDouble() * 0.5f);
                emitter.Emit(new ParticleSystem.EmitParams
                {
                    position = position + Vector3.up * 0.03f,
                    velocity = new Vector3(Mathf.Cos(angle), 0.6f, Mathf.Sin(angle)) * velocity,
                    startColor = color, startSize = 0.08f + (float)random.NextDouble() * 0.05f,
                    startLifetime = 0.18f + (float)random.NextDouble() * 0.12f
                }, 1);
            }
        }

        public void SpawnCourtBounceVFX(Vector3 position) => Emit(bounces, position, new Color(0.8f, 0.9f, 0.87f, 0.5f), 5, 1.1f);
        public void SpawnHitVFX(Vector3 position, ShotQuality quality) =>
            Emit(hits, position, Pickleball.UI.UITheme.QualityColor(quality), quality == ShotQuality.Perfect ? 10 : 6, 2.2f);
        private void OnEnable()
        {
            if (hits != null) hits.Play();
            if (bounces != null) bounces.Play();
        }
        private void OnDisable()
        {
            if (hits != null) hits.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (bounces != null) bounces.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
