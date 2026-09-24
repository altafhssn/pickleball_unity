using UnityEngine;

namespace Pickleball.Systems
{
    internal static class MobileFrameRate
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            // Mobile otherwise defaults to 30 FPS, adding avoidable input and motion latency.
            if (Application.isMobilePlatform) Application.targetFrameRate = 60;
        }
    }
}
