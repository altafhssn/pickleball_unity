using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Pickleball.Backend
{
    /// <summary>Default backend: no account, no network, no PlayFab title required. This is what makes
    /// AI matches and the meta screens work fully offline (Kitchen Line Netcode plan, phase 1) and what
    /// the game runs on before a PlayFab title is configured at all.</summary>
    public class LocalProfileBackend : IProfileBackend
    {
        private static string FilePath => Path.Combine(Application.persistentDataPath, "profile.json");

        public Task<PlayerProfileData> LoadAsync(string guestId)
        {
            if (!File.Exists(FilePath) && !File.Exists(FilePath + ".bak")) return Task.FromResult<PlayerProfileData>(null);

            try
            {
                string json = File.ReadAllText(FilePath);
                var profile = JsonUtility.FromJson<PlayerProfileData>(json);
                if (profile == null) throw new InvalidDataException("Empty profile");
                return Task.FromResult(profile);
            }
            catch (Exception e)
            {
                if (File.Exists(FilePath + ".bak"))
                {
                    try
                    {
                        var recovered = JsonUtility.FromJson<PlayerProfileData>(File.ReadAllText(FilePath + ".bak"));
                        if (recovered != null)
                        {
                            Debug.LogWarning("[LocalProfileBackend] " + FilePath + " was unreadable (" + e.Message + "); recovered the previous complete save.");
                            return Task.FromResult(recovered);
                        }
                    }
                    catch (Exception backupError) { Debug.LogWarning("[LocalProfileBackend] The backup save was unreadable too: " + backupError.Message); }
                }
                Debug.LogWarning("[LocalProfileBackend] Could not read a valid local save from " + FilePath + ": " + e.Message);
                return Task.FromException<PlayerProfileData>(e);
            }
        }

        public Task SaveAsync(string guestId, PlayerProfileData profile)
        {
            try
            {
                Pickleball.Sim.AtomicFileStore.Write(FilePath, JsonUtility.ToJson(profile));
            }
            catch (Exception e)
            {
                return Task.FromException(e);
            }
            return Task.CompletedTask;
        }
    }
}
