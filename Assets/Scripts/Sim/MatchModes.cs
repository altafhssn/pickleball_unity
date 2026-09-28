using System;

namespace Pickleball.Sim
{
    /// <summary>
    /// The two ways to play. Tours are the competitive structure and are played online against a
    /// live opponent: entry fee, stage progress, coins, trophies, bags. Practice is the only way to
    /// play the AI, and it is free and pays nothing -- AI wins are easy to repeat, so they cannot be
    /// allowed to feed the economy that online play is meant to earn.
    /// </summary>
    public enum MatchMode { Practice, Tour }

    /// <summary>The AI levels a practice match can be played at, weakest first.</summary>
    public enum PracticeLevel { Rookie, Club, Pro, Elite }

    /// <summary>What a finished match pays out. Trophies are signed -- negative on a loss.</summary>
    public struct MatchPayout
    {
        public int Coins;
        public int Trophies;
        public int SeasonXp;
        public bool HasPerformanceBonus;
    }

    public static class MatchModes
    {
        /// <summary>Extra trophies a forfeit costs on top of the loss, so quitting from behind is never
        /// cheaper than playing it out.</summary>
        public const int ForfeitSurcharge = 5;

        public static bool IsOnline(MatchMode mode) => mode == MatchMode.Tour;

        public static int EntryFee(MatchMode mode, int stageEntryCoins) =>
            mode == MatchMode.Tour ? Math.Max(0, stageEntryCoins) : 0;

        /// <summary>
        /// The payout for a finished match. A tour win pays the stage's advertised coins (more for a
        /// margin over 2 points, capped at +25%) and trophies; a tour loss costs about half the
        /// stage's trophy value. Winning also pays a performance bonus for perfect shots and long
        /// rallies. Practice pays nothing either way.
        /// </summary>
        public static MatchPayout Payout(MatchMode mode, bool won, int stageRewardCoins, int stageRewardTrophies,
            int playerScore, int opponentScore, int playerPerfects, int longestRally)
        {
            MatchPayout payout = new MatchPayout();
            if (mode != MatchMode.Tour) return payout;

            int margin = Math.Abs(playerScore - opponentScore);
            if (won)
            {
                float dominance = 1f + SimMath.Clamp01((margin - 2) / 6f) * 0.25f;
                payout.Coins = (int)Math.Round(Math.Max(1, stageRewardCoins) * dominance);
                payout.Trophies = Math.Max(1, stageRewardTrophies);

                int bonus = playerPerfects * 4 + (longestRally >= 8 ? 30 : 0);
                if (bonus > 0)
                {
                    payout.Coins += bonus;
                    payout.HasPerformanceBonus = true;
                }
            }
            else
            {
                payout.Trophies = -(int)Math.Round(Math.Max(1, stageRewardTrophies) * 0.55f);
            }

            payout.SeasonXp = won ? 40 + playerPerfects : 10;
            return payout;
        }

        /// <summary>Quitting a match. Leaving a tour match is a loss at the current score plus
        /// <see cref="ForfeitSurcharge"/>; leaving practice costs nothing.</summary>
        public static MatchPayout ForfeitPayout(MatchMode mode, int stageRewardTrophies, int playerScore, int opponentScore)
        {
            MatchPayout payout = Payout(mode, false, 0, stageRewardTrophies,
                playerScore, Math.Max(opponentScore, playerScore + 1), 0, 0);
            if (mode == MatchMode.Tour) payout.Trophies -= ForfeitSurcharge;
            return payout;
        }

        /// <summary>The AI skill (0..1) a practice level plays at. Each sits inside the band
        /// <see cref="SkillLabel"/> gives the same name, so the match card shows what was picked.</summary>
        public static float PracticeSkill(PracticeLevel level)
        {
            switch (level)
            {
                case PracticeLevel.Rookie: return 0.20f;
                case PracticeLevel.Club: return 0.42f;
                case PracticeLevel.Pro: return 0.78f;
                default: return 0.96f;
            }
        }

        /// <summary>Word label for an AI skill value.</summary>
        public static string SkillLabel(float skill)
        {
            if (skill < 0.30f) return "ROOKIE";
            if (skill < 0.50f) return "CLUB";
            if (skill < 0.70f) return "CONTENDER";
            if (skill < 0.88f) return "PRO";
            return "ELITE";
        }
    }
}
