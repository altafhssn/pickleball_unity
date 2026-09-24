#if PLAYFAB_ENABLED
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace Pickleball.Backend
{
    /// <summary>Live once PLAYFAB_ENABLED is defined (Project Settings &gt; Player &gt; Scripting Define
    /// Symbols) and a PlayFabConfig asset carries a real titleId. Not server-authoritative -- writes go
    /// straight from the client to UserData. That's the correct scope for phase 1 (identity +
    /// persistence only, no PvP yet to protect); validating writes server-side via CloudScript is
    /// phase 4's job.</summary>
    public class PlayFabProfileBackend : IProfileBackend
    {
        private const string ProfileDataKey = "profile";

        public PlayFabProfileBackend(string titleId)
        {
            PlayFabSettings.staticSettings.TitleId = titleId;
        }

        public Task<PlayerProfileData> LoadAsync(string guestId)
        {
            var tcs = new TaskCompletionSource<PlayerProfileData>();

            PlayFabClientAPI.LoginWithCustomID(
                new LoginWithCustomIDRequest { CustomId = guestId, CreateAccount = true },
                loginResult =>
                {
                    PlayFabClientAPI.GetUserData(new GetUserDataRequest(), dataResult =>
                    {
                        PlayerProfileData profile = null;
                        if (dataResult.Data != null && dataResult.Data.TryGetValue(ProfileDataKey, out UserDataRecord record))
                        {
                            try { profile = JsonUtility.FromJson<PlayerProfileData>(record.Value); if (profile == null) throw new Exception("Empty stored profile"); }
                            catch (Exception e) { tcs.TrySetException(e); return; }
                        }
                        tcs.SetResult(profile);
                    }, error =>
                    {
                        Debug.LogWarning("[PlayFabProfileBackend] GetUserData failed: " + error.GenerateErrorReport());
                        tcs.TrySetException(new Exception("Profile service unavailable"));
                    });
                },
                error =>
                {
                    Debug.LogWarning("[PlayFabProfileBackend] LoginWithCustomID failed: " + error.GenerateErrorReport());
                    tcs.TrySetException(new Exception("Profile service unavailable"));
                });

            return tcs.Task;
        }

        public Task SaveAsync(string guestId, PlayerProfileData profile)
        {
            var tcs = new TaskCompletionSource<bool>();

            var request = new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string> { { ProfileDataKey, JsonUtility.ToJson(profile) } }
            };

            PlayFabClientAPI.UpdateUserData(request,
                _ => tcs.SetResult(true),
                error =>
                {
                    Debug.LogWarning("[PlayFabProfileBackend] UpdateUserData failed: " + error.GenerateErrorReport());
                    tcs.TrySetException(new Exception("Cloud save failed"));
                });

            return tcs.Task;
        }
    }
}
#endif
