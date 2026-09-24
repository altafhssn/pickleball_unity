using UnityEngine;
using Pickleball.Data;

namespace Pickleball.Systems
{
    /// <summary>Thin wrapper over device vibration, gated on the Settings "Haptics" toggle -- which
    /// previously persisted a value that nothing in the game ever read. Base Unity only exposes an
    /// on/off <see cref="Handheld.Vibrate"/> with no amplitude or duration control, so `strong` just
    /// fires two short pulses; it's a hook for a richer implementation later, not the real thing.</summary>
    public static class Haptics
    {
        public static void Light()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (!Application.isMobilePlatform || !MetaGameState.HapticsOn) return;
            Handheld.Vibrate();
#endif
        }

        public static void Strong()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (!Application.isMobilePlatform || !MetaGameState.HapticsOn) return;
            Handheld.Vibrate();
#endif
        }
    }
}
