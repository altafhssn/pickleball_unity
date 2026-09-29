using System;
using UnityEngine;
using Pickleball.Data;

namespace Pickleball.Systems
{
    /// <summary>
    /// One rewarded-ad network. The ad provider is not chosen yet; whichever SDK is picked gets a
    /// small adapter implementing this and assigns it to <see cref="RewardedAds.Provider"/> at startup.
    /// </summary>
    public interface IRewardedAdProvider
    {
        /// <summary>Whether an ad is loaded and could be shown right now.</summary>
        bool IsReady { get; }

        /// <summary>Shows one ad and reports how it ended: true only if the viewer watched it to the
        /// end (the network's own verified-completion callback), false if it was skipped, closed
        /// early or failed to play.</summary>
        void Show(string placementId, Action<bool> onFinished);
    }

    /// <summary>
    /// The optional watch-an-ad-for-coins offer. Coins are credited only after the provider reports a
    /// completed view, exactly once per ad: each offer gets its own placement id, the first completion
    /// report for it is the only one that counts, and MetaGameState's reward ledger refuses an id it
    /// has already paid. Skipped or failed ads pay nothing. Also capped per day (Sim.EconomyConfig).
    ///
    /// With no provider there is no offer at all. Editor and development builds fall back to
    /// <see cref="UI.SimulatedAdProvider"/> so the flow can be tested; a release build never shows it.
    /// </summary>
    public static class RewardedAds
    {
        private static IRewardedAdProvider provider;
        private static bool showing;

        public static IRewardedAdProvider Provider
        {
            get
            {
                if (provider == null && (Application.isEditor || Debug.isDebugBuild))
                    provider = new UI.SimulatedAdProvider();
                return provider;
            }
            set { provider = value; }
        }

        /// <summary>Whether the offer should be shown: an ad is ready, none is already playing, and
        /// today's allowance isn't used up.</summary>
        public static bool IsOfferAvailable =>
            !showing && Provider != null && Provider.IsReady && MetaGameState.AdRewardsRemainingToday > 0;

        /// <summary>Plays one ad and reports whether it paid: true only for a completed view that was
        /// credited.</summary>
        public static void WatchForCoins(Action<bool> onDone)
        {
            if (!IsOfferAvailable)
            {
                onDone?.Invoke(false);
                return;
            }

            showing = true;
            string placementId = Guid.NewGuid().ToString("N");
            bool settled = false;
            Provider.Show(placementId, delegate (bool completed)
            {
                // A network that reports twice is ignored the second time, before the ledger even sees it.
                if (settled) return;
                settled = true;
                showing = false;
                bool credited = completed && MetaGameState.CreditAdReward(placementId);
                onDone?.Invoke(credited);
            });
        }
    }
}
