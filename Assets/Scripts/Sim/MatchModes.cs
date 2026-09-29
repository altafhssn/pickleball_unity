namespace Pickleball.Sim
{
    /// <summary>
    /// The two ways to play, one per home-screen button. Play with AI is offline against an opponent
    /// matched to the player's own gear; it pays coins for a win and never touches league points.
    /// Multiplayer is ranked: a random opponent from the same league, a 70/30 coin split, and league
    /// points won or lost.
    /// </summary>
    public enum MatchMode { AI, Multiplayer }

    /// <summary>What a match pays out. LeaguePoints is signed -- negative on a loss.</summary>
    public struct MatchPayout
    {
        public int Coins;
        public int LeaguePoints;
    }

    public static class MatchModes
    {
        public static bool IsOnline(MatchMode mode) => mode == MatchMode.Multiplayer;

        /// <summary>Only multiplayer is ranked. AI results never change league points.</summary>
        public static bool AffectsLeague(MatchMode mode) => mode == MatchMode.Multiplayer;

        /// <summary>
        /// The payout for a completed match. Multiplayer shares the configured reward pool 70/30
        /// between winner and loser (see <see cref="Economy.SplitRewardPool"/>) and moves league
        /// points. An AI match pays the configured AI-win coins, or the AI-loss amount, and no points.
        /// </summary>
        public static MatchPayout Payout(MatchMode mode, bool won)
        {
            if (mode == MatchMode.Multiplayer)
            {
                Economy.SplitRewardPool(EconomyConfig.MultiplayerRewardPool, EconomyConfig.MultiplayerWinnerPercent,
                    out int winnerCoins, out int loserCoins);
                return new MatchPayout
                {
                    Coins = won ? winnerCoins : loserCoins,
                    LeaguePoints = won ? LeagueConfig.WinPoints : -LeagueConfig.LossPoints,
                };
            }
            return new MatchPayout { Coins = won ? EconomyConfig.AiWinCoins : EconomyConfig.AiLossCoins };
        }

        /// <summary>
        /// Leaving a match before it ends. A multiplayer forfeit counts as a loss for league points and
        /// earns nothing: coins are credited only for a completed match. Leaving an AI match costs and
        /// earns nothing.
        /// </summary>
        public static MatchPayout ForfeitPayout(MatchMode mode)
        {
            return mode == MatchMode.Multiplayer
                ? new MatchPayout { LeaguePoints = -LeagueConfig.LossPoints }
                : new MatchPayout();
        }
    }
}
