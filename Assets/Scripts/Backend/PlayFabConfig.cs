using UnityEngine;

namespace Pickleball.Backend
{
    /// <summary>Fill in titleId once a PlayFab title exists (playfab.com &gt; Game Manager &gt; title
    /// settings &gt; Title ID). ProfileService falls back to LocalProfileBackend whenever this is
    /// missing or PLAYFAB_ENABLED isn't defined, so the game runs fully offline until this is set up.</summary>
    [CreateAssetMenu(fileName = "PlayFabConfig", menuName = "Pickleball/PlayFab Config")]
    public class PlayFabConfig : ScriptableObject
    {
        public string titleId = "";
    }
}
