// Retired from the launch build: this feature is outside the scope in Docs/LaunchScope.md
// (Pickleball Game Design Document v1.0). The source is kept for reference and is not compiled;
// PICKLEBALL_RETIRED_FEATURES is deliberately never defined.
#if PICKLEBALL_RETIRED_FEATURES
using Pickleball.Sim;

namespace Pickleball.Data
{
    /// <summary>The coins / trophies / season XP a finished match pays out. Was three hardcoded
    /// constants in ScreenManager.HandleMatchEnded (+220 / +28 / -19) that ignored the tour's own
    /// reward table, the opponent's strength, and how the match actually went. GameplayHUD's result
    /// modal hardcoded the same numbers separately.</summary>
    public struct MatchReward
    {
        public int coins;
        public int trophies;   // signed -- negative on a loss
        public int seasonXp;
        public bool hasPerformanceBonus;
    }

    /// <summary>Applies the engine-free rules in <see cref="MatchModes"/> to a tour stage. Practice
    /// matches pay nothing; see MatchModes for why.</summary>
    public static class MatchRewards
    {
        /// <param name="tour">The tour stage played. Ignored for practice.</param>
        /// <param name="playerPerfects">Perfect-quality shots the player landed this match.</param>
        /// <param name="longestRally">Longest rally (total shots) this match.</param>
        public static MatchReward Compute(MatchMode mode, bool won, TourInfo tour,
            int playerScore, int opponentScore, int playerPerfects, int longestRally)
        {
            return From(MatchModes.Payout(mode, won, tour != null ? tour.rewardCoins : 0,
                tour != null ? tour.rewardTrophies : 0, playerScore, opponentScore, playerPerfects, longestRally));
        }

        public static MatchReward Forfeit(MatchMode mode, TourInfo tour, int playerScore, int opponentScore)
        {
            return From(MatchModes.ForfeitPayout(mode, tour != null ? tour.rewardTrophies : 0,
                playerScore, opponentScore));
        }

        private static MatchReward From(MatchPayout payout)
        {
            return new MatchReward
            {
                coins = payout.Coins,
                trophies = payout.Trophies,
                seasonXp = payout.SeasonXp,
                hasPerformanceBonus = payout.HasPerformanceBonus
            };
        }
    }
}
#endif
