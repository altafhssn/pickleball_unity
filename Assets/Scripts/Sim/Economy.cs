using System;

namespace Pickleball.Sim
{
    /// <summary>
    /// Every coin amount the launch economy runs on, in one place. The design fixes the shape -- a
    /// 70/30 multiplayer split, coins for beating the AI, optional rewarded ads, coin-funded gear
    /// upgrades and no real-money coin purchases -- but not the amounts. These are balancing
    /// placeholders, not approved production values.
    /// </summary>
    public static class EconomyConfig
    {
        /// <summary>A brand-new player's balance -- enough for a first upgrade.</summary>
        public const int StartingCoins = 300;

        /// <summary>Coins one completed multiplayer match pays out in total, split between the two
        /// players. A reward pool funded by the game, not an entry stake: entry fees are not part of
        /// the design.</summary>
        public const int MultiplayerRewardPool = 100;

        /// <summary>The winner's share of <see cref="MultiplayerRewardPool"/>, in percent. The loser
        /// gets the rest.</summary>
        public const int MultiplayerWinnerPercent = 70;

        /// <summary>Coins for beating the AI. The 70/30 split does not apply to AI matches.</summary>
        public const int AiWinCoins = 40;

        /// <summary>Coins for losing to the AI.</summary>
        public const int AiLossCoins = 0;

        /// <summary>Coins for one fully watched rewarded ad.</summary>
        public const int AdRewardCoins = 50;

        /// <summary>Rewarded ads that can pay out per UTC day.</summary>
        public const int AdRewardsPerDay = 5;

        /// <summary>How many credited reward ids the ledger remembers. Far more than a day of play, so
        /// a replayed completion can never find its id already forgotten.</summary>
        public const int LedgerCapacity = 256;
    }

    public static class Economy
    {
        /// <summary>
        /// Splits a reward pool between winner and loser. Rounding policy: the loser's share is
        /// rounded down and the winner receives the remainder, so the two always add up to the whole
        /// pool and any rounding favours the winner by less than one coin. A pool divisible by 10
        /// splits exactly 70/30.
        /// </summary>
        public static void SplitRewardPool(int pool, int winnerPercent, out int winnerCoins, out int loserCoins)
        {
            pool = Math.Max(0, pool);
            int loserPercent = 100 - Math.Max(0, Math.Min(100, winnerPercent));
            loserCoins = (int)((long)pool * loserPercent / 100);
            winnerCoins = pool - loserCoins;
        }
    }
}
