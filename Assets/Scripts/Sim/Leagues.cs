using System;

namespace Pickleball.Sim
{
    public struct League
    {
        public readonly string Name;
        /// <summary>The fewest league points that place a player in this league. Falling below it
        /// relegates to the league underneath.</summary>
        public readonly int MinPoints;

        public League(string name, int minPoints)
        {
            Name = name;
            MinPoints = minPoints;
        }
    }

    /// <summary>
    /// The league ladder and how multiplayer results move a player on it. The rule is fixed: a win
    /// adds points, a loss subtracts them, reaching the next league's threshold promotes and falling
    /// below the current league's minimum relegates. The names, thresholds, deltas, starting rank and
    /// floor below are balancing placeholders, not approved values.
    /// </summary>
    public static class LeagueConfig
    {
        /// <summary>Lowest first. Each league's MinPoints must be higher than the one before it.</summary>
        public static readonly League[] Leagues =
        {
            new League("BRONZE", 0),
            new League("SILVER", 250),
            new League("GOLD", 600),
            new League("PLATINUM", 1000),
            new League("DIAMOND", 1500),
            new League("CHAMPION", 2100),
        };

        public const int WinPoints = 25;
        public const int LossPoints = 20;

        /// <summary>Where a brand-new player starts.</summary>
        public const int StartingPoints = 0;

        /// <summary>The lowest balance a player can fall to.</summary>
        public const int MinimumPoints = 0;
    }

    public enum LeagueChange { None, Promoted, Relegated }

    public static class LeagueRules
    {
        public static int IndexFor(int points)
        {
            League[] leagues = LeagueConfig.Leagues;
            for (int i = leagues.Length - 1; i > 0; i--)
            {
                if (points >= leagues[i].MinPoints) return i;
            }
            return 0;
        }

        public static League For(int points) => LeagueConfig.Leagues[IndexFor(points)];

        public static bool IsTopLeague(int index) => index >= LeagueConfig.Leagues.Length - 1;

        /// <summary>Points needed to be promoted out of league <paramref name="index"/>, or -1 from the
        /// top league.</summary>
        public static int PromotionThreshold(int index) =>
            IsTopLeague(index) ? -1 : LeagueConfig.Leagues[index + 1].MinPoints;

        /// <summary>A balance below this relegates out of league <paramref name="index"/>, or -1 from
        /// the bottom league, which nobody can fall out of.</summary>
        public static int RelegationThreshold(int index) =>
            index <= 0 ? -1 : LeagueConfig.Leagues[index].MinPoints;

        /// <summary>How far through the current league toward the next, 0..1. Always 1 in the top league.</summary>
        public static float Progress01(int points)
        {
            int index = IndexFor(points);
            if (IsTopLeague(index)) return 1f;
            int floor = LeagueConfig.Leagues[index].MinPoints;
            int next = LeagueConfig.Leagues[index + 1].MinPoints;
            return SimMath.Clamp01((points - floor) / (float)Math.Max(1, next - floor));
        }

        /// <summary>A balance after a change of <paramref name="delta"/>, never below the floor.</summary>
        public static int Apply(int points, int delta) => Math.Max(LeagueConfig.MinimumPoints, points + delta);

        public static LeagueChange Compare(int pointsBefore, int pointsAfter)
        {
            int before = IndexFor(pointsBefore), after = IndexFor(pointsAfter);
            if (after > before) return LeagueChange.Promoted;
            if (after < before) return LeagueChange.Relegated;
            return LeagueChange.None;
        }
    }
}
