using UnityEngine;

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

    public static class MatchRewards
    {
        /// <param name="tour">The tour stage played, or null for a ranked match.</param>
        /// <param name="localTrophies">The player's trophy count going in (for the ranked Elo delta).</param>
        /// <param name="opponentTrophies">Opponent's trophy count (ranked only; 0 if unknown).</param>
        /// <param name="playerPerfects">Perfect-quality shots the player landed this match.</param>
        /// <param name="longestRally">Longest rally (total shots) this match.</param>
        public static MatchReward Compute(bool won, TourInfo tour, int localTrophies, int opponentTrophies,
            int playerScore, int opponentScore, int playerPerfects, int longestRally)
        {
            MatchReward r = new MatchReward();
            int margin = Mathf.Abs(playerScore - opponentScore);

            if (tour != null)
            {
                // Tour: pay the stage's own advertised reward, scaled slightly by how convincing the
                // win was. A loss costs roughly half the stage's trophy value, never a flat 19.
                if (won)
                {
                    float dominance = 1f + Mathf.Clamp01((margin - 2) / 6f) * 0.25f; // up to +25% for a 4+ margin
                    r.coins = Mathf.RoundToInt(Mathf.Max(1, tour.rewardCoins) * dominance);
                    r.trophies = Mathf.Max(1, tour.rewardTrophies);
                }
                else
                {
                    r.coins = 0;
                    r.trophies = -Mathf.RoundToInt(Mathf.Max(1, tour.rewardTrophies) * 0.55f);
                }
            }
            else
            {
                // Ranked: Elo-style trophy swing so beating a stronger opponent is worth more and
                // losing to a weaker one hurts more, instead of a flat +28 / -19.
                float expected = 1f / (1f + Mathf.Pow(10f, (opponentTrophies - localTrophies) / 400f));
                float actual = won ? 1f : 0f;
                int swing = Mathf.RoundToInt(34f * (actual - expected));
                r.trophies = won ? Mathf.Clamp(swing, 8, 40) : Mathf.Clamp(swing, -40, -8);
                r.coins = won ? Mathf.RoundToInt(170 * (1f + Mathf.Clamp01((margin - 2) / 6f) * 0.25f)) : 0;
            }

            // Performance bonus (win only): rewards clean, long rallies rather than just showing up.
            if (won)
            {
                int bonus = playerPerfects * 4 + (longestRally >= 8 ? 30 : 0);
                if (bonus > 0)
                {
                    r.coins += bonus;
                    r.hasPerformanceBonus = true;
                }
            }

            r.seasonXp = won ? 40 + playerPerfects : 10;
            return r;
        }
    }
}
