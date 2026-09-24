using UnityEngine;
using Pickleball.Data;
using Pickleball.Gameplay;

namespace Pickleball.Systems
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        private AudioSource audioSource;
        private AudioSource musicSource;
        private AudioSource[] voices;
        private int nextVoice;
        private readonly System.Random random = new System.Random(7821);
        private bool audioSuspended;

        /// <summary>The Effects slider in Settings persisted a value that nothing ever read -- every
        /// PlayOneShot below used its own hardcoded volume. Read live so a mid-session change on the
        /// settings screen takes effect on the next sound.</summary>
        private static float Fx => Mathf.Clamp01(MetaGameState.EffectsVolume);

        private AudioClip racketHitClip;
        private AudioClip dinkClip;
        private AudioClip driveClip;
        private AudioClip courtBounceClip;
        private AudioClip pointScoredClip;
        private AudioClip perfectShotClip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            voices = new AudioSource[8];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }
            audioSource = voices[0];
            GenerateClips();
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            musicSource.priority = 192;
            float[] music = Pickleball.Sim.AudioSynthesis.CreateMusicLoop();
            var clip = OwnedRuntimeAssets.Track(gameObject, AudioClip.Create("Kitchen Groove", music.Length, 1,
                Pickleball.Sim.AudioSynthesis.SampleRate, false));
            clip.SetData(music, 0);
            musicSource.clip = clip;
            musicSource.volume = 0f;
            musicSource.Play();
        }

        private void Update()
        {
            if (voices == null) return;
            foreach (var voice in voices) if (voice != null) voice.volume = Fx;
            bool inMatch = Pickleball.UI.ScreenManager.Instance != null && Pickleball.UI.ScreenManager.Instance.IsMatchActive;
            float target = Mathf.Clamp01(MetaGameState.MusicVolume) * (inMatch ? 0.18f : 0.32f);
            if (audioSuspended) target = 0f;
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, target, Time.unscaledDeltaTime * 0.8f);
        }

        private void PlayEffect(AudioClip clip, float volume, float pitch = 1f)
        {
            if (!isActiveAndEnabled || audioSuspended || Fx <= 0f || voices == null) return;
            audioSource = voices[nextVoice++ % voices.Length];
            audioSource.Stop();
            audioSource.pitch = pitch;
            audioSource.volume = Fx;
            audioSource.PlayOneShot(clip, volume);
        }

        private float Noise() => (float)random.NextDouble();

        private void OnApplicationPause(bool paused)
        {
            audioSuspended = paused;
            if (musicSource == null) return;
            if (paused)
            {
                musicSource.Pause();
                foreach (var voice in voices) voice.Stop();
            }
            else musicSource.UnPause();
        }

        private void OnDisable()
        {
            if (musicSource != null) musicSource.Pause();
            if (voices != null) foreach (var voice in voices) if (voice != null) voice.Stop();
        }
        private void OnEnable() { if (musicSource != null && !audioSuspended) musicSource.UnPause(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private static void FadeEdges(float[] samples, int sampleRate)
        {
            int length = Mathf.Min(samples.Length / 2, sampleRate / 200);
            for (int i = 0; i < length; i++)
            {
                float gain = i / (float)length;
                samples[i] *= gain;
                samples[samples.Length - 1 - i] *= gain;
            }
        }

        private void GenerateClips()
        {
            racketHitClip = CreateRacketHitClip();
            dinkClip = CreateDinkClip();
            driveClip = CreateDriveClip();
            courtBounceClip = CreateCourtBounceClip();
            pointScoredClip = CreatePointScoredClip();
            perfectShotClip = CreatePerfectShotClip();
        }

        private AudioClip CreateRacketHitClip()
        {
            int sampleRate = 44100;
            float duration = 0.12f;
            int sampleLength = (int)(sampleRate * duration);
            float[] samples = new float[sampleLength];

            for (int i = 0; i < sampleLength; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 35f);
                float noise = (Noise() * 2f - 1f) * (t < 0.02f ? 1f : 0f);
                float sine = Mathf.Sin(2f * Mathf.PI * 920f * t);
                samples[i] = (noise * 0.4f + sine * 0.6f) * envelope;
            }

            FadeEdges(samples, sampleRate);
            AudioClip clip = OwnedRuntimeAssets.Track(gameObject, AudioClip.Create("RacketHit", sampleLength, 1, sampleRate, false));
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateDinkClip()
        {
            int sampleRate = 44100;
            float duration = 0.08f;
            int sampleLength = (int)(sampleRate * duration);
            float[] samples = new float[sampleLength];

            for (int i = 0; i < sampleLength; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 50f);
                float sine = Mathf.Sin(2f * Mathf.PI * 520f * t);
                samples[i] = sine * envelope * 0.7f;
            }

            FadeEdges(samples, sampleRate);
            AudioClip clip = OwnedRuntimeAssets.Track(gameObject, AudioClip.Create("DinkHit", sampleLength, 1, sampleRate, false));
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateDriveClip()
        {
            int sampleRate = 44100;
            float duration = 0.16f;
            int sampleLength = (int)(sampleRate * duration);
            float[] samples = new float[sampleLength];

            for (int i = 0; i < sampleLength; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 28f);
                float noise = (Noise() * 2f - 1f) * (t < 0.035f ? 1f : 0f);
                float sine1 = Mathf.Sin(2f * Mathf.PI * 1100f * t);
                float sine2 = Mathf.Sin(2f * Mathf.PI * 2200f * t);
                samples[i] = (noise * 0.5f + sine1 * 0.35f + sine2 * 0.15f) * envelope;
            }

            FadeEdges(samples, sampleRate);
            AudioClip clip = OwnedRuntimeAssets.Track(gameObject, AudioClip.Create("DriveHit", sampleLength, 1, sampleRate, false));
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateCourtBounceClip()
        {
            int sampleRate = 44100;
            float duration = 0.10f;
            int sampleLength = (int)(sampleRate * duration);
            float[] samples = new float[sampleLength];

            for (int i = 0; i < sampleLength; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 45f);
                float noise = (Noise() * 2f - 1f) * (t < 0.015f ? 1f : 0f);
                float sine = Mathf.Sin(2f * Mathf.PI * 240f * t);
                samples[i] = (noise * 0.6f + sine * 0.4f) * envelope;
            }

            FadeEdges(samples, sampleRate);
            AudioClip clip = OwnedRuntimeAssets.Track(gameObject, AudioClip.Create("CourtBounce", sampleLength, 1, sampleRate, false));
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreatePointScoredClip()
        {
            int sampleRate = 44100;
            float duration = 0.45f;
            int sampleLength = (int)(sampleRate * duration);
            float[] samples = new float[sampleLength];

            for (int i = 0; i < sampleLength; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 5f);
                float freq = t < 0.15f ? 523.25f : 659.25f; // C5 to E5
                float sine = Mathf.Sin(2f * Mathf.PI * freq * t);
                samples[i] = sine * envelope * 0.5f;
            }

            FadeEdges(samples, sampleRate);
            AudioClip clip = OwnedRuntimeAssets.Track(gameObject, AudioClip.Create("PointScored", sampleLength, 1, sampleRate, false));
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreatePerfectShotClip()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int sampleLength = (int)(sampleRate * duration);
            float[] samples = new float[sampleLength];

            for (int i = 0; i < sampleLength; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 18f);
                float sine1 = Mathf.Sin(2f * Mathf.PI * 1320f * t);
                float sine2 = Mathf.Sin(2f * Mathf.PI * 2640f * t);
                samples[i] = (sine1 * 0.65f + sine2 * 0.35f) * envelope;
            }

            FadeEdges(samples, sampleRate);
            AudioClip clip = OwnedRuntimeAssets.Track(gameObject, AudioClip.Create("PerfectShot", sampleLength, 1, sampleRate, false));
            clip.SetData(samples, 0);
            return clip;
        }

        public void PlayRacketHit(float power)
        {
            PlayRacketHit(ShotType.Flat, power);
        }

        public void PlayRacketHit(ShotType shotType, float power)
        {
            AudioClip clip = racketHitClip;
            float basePitch = 1.0f;

            if (shotType == ShotType.Dink)
            {
                clip = dinkClip;
                basePitch = 1.1f;
            }
            else if (shotType == ShotType.Topspin || shotType == ShotType.Smash)
            {
                clip = driveClip;
                basePitch = 1.05f;
            }
            else if (shotType == ShotType.Lob)
            {
                clip = racketHitClip;
                basePitch = 0.9f;
            }

            float pitch = Mathf.Lerp(0.95f, 1.05f, Noise()) * (basePitch + power * 0.2f);
            float vol = Mathf.Clamp01(0.6f + power * 0.4f);
            if (shotType == ShotType.Dink) vol *= 0.6f;
            else if (shotType == ShotType.Lob) vol *= 0.75f;
            PlayEffect(clip, vol, pitch);
        }

        public void PlayCourtBounce()
        {
            PlayEffect(courtBounceClip, 0.42f, Mathf.Lerp(0.95f, 1.05f, Noise()));
        }

        public void PlayPointScored()
        {
            PlayEffect(pointScoredClip, 0.75f);
        }

        public void PlayPerfectShot()
        {
            PlayEffect(perfectShotClip, 0.65f);
        }
    }
}
