using System.Collections;
using UnityEngine;
using Pickleball.Gameplay;
using Pickleball.VFX;

namespace Pickleball.Systems
{
    public class GameFeedbackManager : MonoBehaviour
    {
        public static GameFeedbackManager Instance { get; private set; }

        private CameraController cameraController;
        private Coroutine hitStopCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            cameraController = FindObjectOfType<CameraController>();
        }

        private void Start()
        {
            if (cameraController == null) cameraController = FindObjectOfType<CameraController>();
        }

        public void OnBallHit(Vector3 hitPos, ShotData shotData)
        {
            // Emphasis effects (slow-mo, punch zoom) are the *player's* skill payoff -- an AI shot,
            // which is Perfect by default on its serve, shouldn't freeze the game. A light shake
            // still fires for both sides so every contact has weight.
            bool isPlayerShot = shotData.hitterId == 0;
            bool softShot = shotData.shotType == ShotType.Dink || shotData.shotType == ShotType.Lob;
            bool putAway = shotData.shotType == ShotType.Smash;

            if (cameraController != null)
            {
                float shakeIntensity = softShot ? 0.018f :
                    Mathf.Lerp(0.035f, putAway ? 0.16f : 0.075f, shotData.compositeScore);
                cameraController.Shake(shakeIntensity, putAway ? 0.12f : 0.07f);

                if (isPlayerShot && putAway && shotData.quality == ShotQuality.Perfect)
                {
                    TriggerHitStop(0.035f);
                    Haptics.Strong();
                }
                else if (isPlayerShot && !softShot && shotData.quality <= ShotQuality.Great) Haptics.Light();

                if (isPlayerShot && putAway && (shotData.quality == ShotQuality.Great || shotData.quality == ShotQuality.Perfect))
                {
                    cameraController.PunchZoom(1.2f, 0.14f);
                }
            }

            if (ImpactVFX.Instance != null)
            {
                ImpactVFX.Instance.SpawnHitVFX(hitPos, shotData.quality);
            }
        }

        /// <summary>Brief slow-mo on a Perfect shot. Single-owner: a second Perfect within the window
        /// restarts the timer rather than stacking a second coroutine that would slam timeScale back
        /// to 1 while the first still thinks it owns the slow-mo. Skipped entirely in PvP -- a local
        /// timeScale change desyncs the shared match clock both peers simulate against.</summary>
        private void TriggerHitStop(float realSeconds)
        {
            if (Pickleball.Gameplay.RallyManager.Instance != null &&
                Pickleball.Gameplay.RallyManager.Instance.IsPvP) return;

            if (hitStopCoroutine != null) StopCoroutine(hitStopCoroutine);
            hitStopCoroutine = StartCoroutine(HitStopRoutine(realSeconds));
        }

        private IEnumerator HitStopRoutine(float realSeconds)
        {
            Time.timeScale = 0.2f;
            yield return new WaitForSecondsRealtime(realSeconds);
            // Restore to full speed only if nothing else has taken ownership of timeScale in the
            // meantime (a pause sets it to 0). Blindly writing 1.0 here is how a Perfect shot could
            // un-pause the game -- the coroutine runs on realtime, unaffected by the freeze.
            if (Time.timeScale > 0f) Time.timeScale = 1.0f;
            hitStopCoroutine = null;
        }

        /// <summary>Ends any in-flight hit-stop immediately and restores full speed. Called by
        /// ScreenManager.ShowPause *before* it zeroes timeScale, so the two never race.</summary>
        public void CancelHitStop()
        {
            if (hitStopCoroutine != null)
            {
                StopCoroutine(hitStopCoroutine);
                hitStopCoroutine = null;
            }
            if (Time.timeScale > 0f && Time.timeScale < 1f) Time.timeScale = 1.0f;
        }

        private void OnDisable()
        {
            // Never leave the game frozen if this object is torn down mid-slowmo (match exit, scene change).
            if (hitStopCoroutine != null)
            {
                StopCoroutine(hitStopCoroutine);
                hitStopCoroutine = null;
                if (Time.timeScale > 0f) Time.timeScale = 1.0f;
            }
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void OnBallBounce(Vector3 bouncePos)
        {
            if (ImpactVFX.Instance != null)
            {
                ImpactVFX.Instance.SpawnCourtBounceVFX(bouncePos);
            }
        }

        public void OnPointScored(string message)
        {
            if (cameraController != null)
            {
                cameraController.PunchZoom(4f, 0.35f);
                cameraController.Shake(0.12f, 0.2f);
            }
            Haptics.Light();
        }
    }
}
