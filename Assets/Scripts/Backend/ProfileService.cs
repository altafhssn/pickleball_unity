using System;
using UnityEngine;
using Pickleball.Data;

namespace Pickleball.Backend
{
    /// <summary>Owns the player's identity and profile lifecycle. MetaGameState is the read-through
    /// cache; this is what loads it at startup and flushes it back out -- Kitchen Line Netcode plan,
    /// phase 1. Picks PlayFabProfileBackend when PLAYFAB_ENABLED is defined and a titleId is configured,
    /// otherwise LocalProfileBackend -- so the game runs fully offline until a title is set up, but not
    /// after: once PlayFab is the backend it is the only copy of the profile, and a failed load leaves
    /// IsLoaded false, which parks boot on the ProfileRecovery screen (retry only) and makes every
    /// FlushSave a no-op. That is deliberate -- playing on defaults would risk overwriting the cloud
    /// profile with a blank one. Offline play with a live title would need a local write-through cache
    /// and a merge rule on reconnect, which does not exist yet.</summary>
    public class ProfileService : MonoBehaviour
    {
        public static ProfileService Instance { get; private set; }

        [Tooltip("Optional. Leave unassigned, or leave titleId blank, to run on the local (offline) backend.")]
        [SerializeField] private PlayFabConfig playFabConfig;

        private const string GuestIdKey = "pb_guest_id";
        private const float SaveDebounceSeconds = 2f;

        private IProfileBackend backend;
        private string guestId;
        private float saveRequestedAt = -1f;
        private bool saving;
        private bool saveAgain;
        private bool destroyed;

        public bool IsLoaded { get; private set; }
        public bool IsLoading { get; private set; }
        public string LoadError { get; private set; }
        public bool UsesCloudProfile => !(backend is LocalProfileBackend);
        public event Action OnProfileLoaded;

        /// <summary>The per-install guest identity backing this profile -- the same id LoginWithCustomID
        /// uses once PlayFab is configured. Settings shows a short form of this instead of a hardcoded
        /// placeholder.</summary>
        public string GuestId => guestId;

        private void Awake()
        {
            // Only the profile survives scene changes; keeping the shared Managers object alive
            // also kept old HUDs, audio sources and references to destroyed players alive.
            if (Instance == null && (transform.parent != null || GetComponents<MonoBehaviour>().Length > 1))
            {
                var host = new GameObject("PersistentProfileService");
                host.SetActive(false);
                var service = host.AddComponent<ProfileService>();
                service.playFabConfig = playFabConfig;
                host.SetActive(true);
                enabled = false;
                Destroy(this);
                return;
            }
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(this);
                return;
            }

            guestId = PlayerPrefs.GetString(GuestIdKey, string.Empty);
            if (string.IsNullOrEmpty(guestId))
            {
                guestId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(GuestIdKey, guestId);
                PlayerPrefs.Save();
            }

            backend = CreateBackend();
        }

        private IProfileBackend CreateBackend()
        {
#if PLAYFAB_ENABLED
            if (playFabConfig != null && !string.IsNullOrEmpty(playFabConfig.titleId))
                return new PlayFabProfileBackend(playFabConfig.titleId);
#endif
            return new LocalProfileBackend();
        }

        private void Start() { RetryLoad(); }

        public async void RetryLoad()
        {
            if (IsLoading || IsLoaded) return;
            IsLoading = true;
            LoadError = null;
            try
            {
                var load = backend.LoadAsync(guestId);
                if (await System.Threading.Tasks.Task.WhenAny(load, System.Threading.Tasks.Task.Delay(10000)) != load)
                    throw new TimeoutException("Profile load timed out");
                PlayerProfileData profile = await load;
                if (destroyed) return;
                if (profile == null) profile = MetaGameState.CaptureSnapshot();
                profile.Migrate();
                MetaGameState.ApplySnapshot(profile);
                IsLoaded = true;
                OnProfileLoaded?.Invoke();
            }
            catch (Exception e)
            {
                LoadError = "Unable to load your profile.";
                Debug.LogWarning("[ProfileService] Load failed: " + e.Message);
            }
            finally { IsLoading = false; }
        }

        private void Update()
        {
            if (saveRequestedAt >= 0f && Time.unscaledTime - saveRequestedAt >= SaveDebounceSeconds)
            {
                saveRequestedAt = -1f;
                FlushSave();
            }
        }

        /// <summary>Called by MetaGameState after any field mutation. Debounced so a burst of changes
        /// (spend coins, then equip gear, then...) writes once rather than once per field.</summary>
        public void RequestSave()
        {
            saveRequestedAt = Time.unscaledTime;
        }

        public async void FlushSave()
        {
            if (backend == null || !IsLoaded) return;
            if (saving) { saveAgain = true; return; }
            saving = true;
            try
            {
                do
                {
                    saveAgain = false;
                    var snapshot = MetaGameState.CaptureSnapshot();
                    await backend.SaveAsync(guestId, snapshot);
                } while (saveAgain && !destroyed);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ProfileService] Save failed; retrying shortly: " + e.Message);
                if (!destroyed) saveRequestedAt = Time.unscaledTime + 3f;
            }
            finally { saving = false; }
        }

        private void OnDestroy()
        {
            destroyed = true;
            if (Instance == this) Instance = null;
            OnProfileLoaded = null;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) FlushSave();
        }

        private void OnApplicationQuit()
        {
            FlushSave();
        }
    }
}
