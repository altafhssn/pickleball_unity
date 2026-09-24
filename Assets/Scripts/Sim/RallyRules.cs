namespace Pickleball.Sim
{
    /// <summary>
    /// The match win-condition, extracted so a re-simulating server can validate a submitted match's
    /// final score against the exact same rule a live client used, without needing any
    /// MonoBehaviour/RallyManager context. Per-point fault rules (out-of-bounds, net fault, unreturned
    /// ball) are deliberately left in RallyManager: each is a single, context-dependent line ("the
    /// last hitter loses") rather than a reusable decision, and extracting them would add an
    /// abstraction with nothing to share.
    /// </summary>
    public static class RallyRules
    {
        /// <summary>Singles side-out scoring: only the server can earn a point.</summary>
        public static bool ResolveRally(bool playerWon, ref bool playerServing,
            ref int playerScore, ref int opponentScore)
        {
            bool scored = playerWon == playerServing;
            if (scored)
            {
                if (playerWon) playerScore++;
                else opponentScore++;
            }
            playerServing = playerWon;
            return scored;
        }

        public static bool MustBounce(int completedShots) => completedShots == 1 || completedShots == 2;

        public static bool IsInKitchen(float x, float z, int side)
        {
            float depth = z * (side == 0 ? -1f : 1f);
            return SimMath.Abs(x) <= CourtDimensions.HalfWidth && depth >= 0f &&
                depth <= CourtDimensions.KitchenDepth;
        }
        /// <summary>True win-by-two: reaching pointsToWin is not enough while the lead is only one.
        /// There is deliberately no hidden cap, matching the rule shown to the player.</summary>
        public static bool IsMatchOver(int playerScore, int opponentScore, int pointsToWin, out bool playerWonMatch)
        {
            int lead = playerScore - opponentScore;

            if ((playerScore >= pointsToWin || opponentScore >= pointsToWin) && (lead >= 2 || lead <= -2))
            {
                playerWonMatch = lead >= 2;
                return true;
            }

            playerWonMatch = false;
            return false;
        }
    }
}
