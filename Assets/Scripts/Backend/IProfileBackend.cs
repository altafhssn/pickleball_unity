using System.Threading.Tasks;

namespace Pickleball.Backend
{
    /// <summary>What ProfileService talks to. LocalProfileBackend and PlayFabProfileBackend both
    /// implement this identically, so which one is active is a config detail, not a code fork.</summary>
    public interface IProfileBackend
    {
        /// <summary>Returns null if this identity has no saved profile yet (first run).</summary>
        Task<PlayerProfileData> LoadAsync(string guestId);
        Task SaveAsync(string guestId, PlayerProfileData profile);
    }
}
