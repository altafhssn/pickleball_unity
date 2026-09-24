using UnityEngine;

namespace Pickleball.Net
{
    /// <summary>Fill in appId once a Photon "Realtime" app exists (photonengine.com &gt; Dashboard &gt;
    /// your app &gt; App ID). Only meaningful once PHOTON_ENABLED is defined and the Photon SDK is
    /// imported; nothing reads this until then.</summary>
    [CreateAssetMenu(fileName = "PhotonConfig", menuName = "Pickleball/Photon Config")]
    public class PhotonConfig : ScriptableObject
    {
        public string appId = "";
    }
}
