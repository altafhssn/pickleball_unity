namespace Pickleball.Gameplay
{
    /// <summary>
    /// Shared match constants. Previously the target score lived as a private const in both
    /// RallyManager and GameplayHUD, so changing one silently desynced the pips / "FIRST TO N" disc
    /// from the actual win condition. The kitchen (non-volley zone) geometry lived nowhere -- it was
    /// painted on the court and enforced by nothing.
    /// </summary>
    public static class MatchConfig
    {
        /// <summary>Points needed to win -- first to this many, no two-point margin (Sim.RallyRules). Kept as the
        /// single source both RallyManager's default and GameplayHUD read.</summary>
        public const int PointsToWin = 7;

        public const float CourtHalfWidth = Pickleball.Sim.CourtDimensions.HalfWidth;
        public const float CourtHalfLength = Pickleball.Sim.CourtDimensions.HalfLength;

        /// <summary>Depth of the non-volley zone from the net, in world units, on each side. A shot
        /// taken out of the air (a volley) while the hitter is standing inside this band is a fault --
        /// the tactical core of pickleball. The net sits at z = 0; the player's kitchen is
        /// z in [-KitchenDepth, 0), the opponent's is (0, KitchenDepth].</summary>
        public const float KitchenDepth = Pickleball.Sim.CourtDimensions.KitchenDepth;
    }
}
