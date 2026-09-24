using System.Collections.Generic;
using UnityEngine;

namespace Pickleball.Systems
{
    /// <summary>Owns generated native assets. Do not register imported assets or shared UI caches.</summary>
    public sealed class OwnedRuntimeAssets : MonoBehaviour
    {
        private readonly List<Object> assets = new List<Object>();
        public static T Track<T>(GameObject owner, T asset) where T : Object
        {
            if (asset == null) return null;
            var lifetime = owner.GetComponent<OwnedRuntimeAssets>();
            if (lifetime == null) lifetime = owner.AddComponent<OwnedRuntimeAssets>();
            lifetime.assets.Add(asset);
            return asset;
        }
        private void OnDestroy()
        {
            foreach (var asset in assets)
            {
                if (asset == null) continue;
                if (Application.isPlaying) Destroy(asset);
                else DestroyImmediate(asset);
            }
            assets.Clear();
        }
    }
}
