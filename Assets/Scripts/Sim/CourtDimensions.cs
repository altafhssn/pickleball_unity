using System.Numerics;

namespace Pickleball.Sim
{
    /// <summary>
    /// Canonical court geometry for rendering, shot bounds, faults, and service placement. Keeping
    /// these values in the engine-free simulation assembly ensures that local play, PvP validation,
    /// and any future authoritative server all use the same painted lines.
    /// </summary>
    public static class CourtDimensions
    {
        public const float HalfWidth = 5f;
        public const float HalfLength = 9f;
        public const float KitchenDepth = 2.5f;
        public const float NetLineMargin = 0.05f;

        /// <summary>Keeps generated targets visibly clear of a line while line-contact validation
        /// itself remains inclusive, except for the kitchen line on a serve.</summary>
        public const float ServiceAimMargin = 0.25f;

        public const float ServerBaselineOffset = 0.35f;
        public const float ServerLateralPosition = HalfWidth * 0.5f;

        public static CourtBounds PlayBounds => new CourtBounds(
            new Vector2(-HalfWidth, NetLineMargin),
            new Vector2(HalfWidth, HalfLength));
    }

    /// <summary>Pure singles service-side and target-box rules shared by every match driver.</summary>
    public static class ServeRules
    {
        private const float LineTolerance = 0.001f;

        public static bool ServesFromRight(int serverScore) => (serverScore & 1) == 0;

        /// <summary>World-space server position. Side 0 faces +Z; side 1 faces -Z, so each side's
        /// personal right maps to the opposite world-X sign.</summary>
        public static Vector3 ServerPosition(int serverSideId, bool serveFromRight, float y = 1f)
        {
            bool worldPositiveX = serverSideId == 0 ? serveFromRight : !serveFromRight;
            float x = (worldPositiveX ? 1f : -1f) * CourtDimensions.ServerLateralPosition;
            float z = (serverSideId == 0 ? -1f : 1f) *
                (CourtDimensions.HalfLength + CourtDimensions.ServerBaselineOffset);
            return new Vector3(x, y, z);
        }

        /// <summary>Whether the diagonally opposite receiving service court is on +X.</summary>
        public static bool TargetCourtIsPositiveX(int serverSideId, bool serveFromRight)
        {
            bool serverIsPositiveX = serverSideId == 0 ? serveFromRight : !serveFromRight;
            return !serverIsPositiveX;
        }

        /// <summary>Builds a legal target inside the diagonally opposite service box. Horizontal
        /// input still aims naturally left/right, but cannot cross the center line into the wrong box.</summary>
        public static Vector3 BuildTarget(int serverSideId, bool serveFromRight,
            float horizontalInput, float depth01)
        {
            bool positiveX = TargetCourtIsPositiveX(serverSideId, serveFromRight);
            float margin = CourtDimensions.ServiceAimMargin;
            float minX = positiveX ? margin : -CourtDimensions.HalfWidth + margin;
            float maxX = positiveX ? CourtDimensions.HalfWidth - margin : -margin;
            float centerX = (minX + maxX) * 0.5f;
            float halfRange = (maxX - minX) * 0.5f;
            float targetX = centerX + SimMath.Clamp(horizontalInput, -1f, 1f) * halfRange;

            float absZ = SimMath.Lerp(CourtDimensions.KitchenDepth + margin,
                CourtDimensions.HalfLength - margin, depth01);
            float targetZ = serverSideId == 0 ? absZ : -absZ;
            return new Vector3(targetX, 0f, targetZ);
        }

        /// <summary>Validates the first bounce of a serve. Sideline, baseline, and center-line contact
        /// are in; the kitchen line is part of the non-volley zone and therefore a serve fault.</summary>
        public static bool IsInCorrectServiceCourt(Vector3 landing, int serverSideId, bool serveFromRight)
        {
            float absX = SimMath.Abs(landing.X);
            float absZ = SimMath.Abs(landing.Z);
            if (absX > CourtDimensions.HalfWidth + LineTolerance ||
                absZ > CourtDimensions.HalfLength + LineTolerance ||
                absZ <= CourtDimensions.KitchenDepth + LineTolerance)
                return false;

            if (serverSideId == 0 && landing.Z <= 0f) return false;
            if (serverSideId != 0 && landing.Z >= 0f) return false;

            return TargetCourtIsPositiveX(serverSideId, serveFromRight)
                ? landing.X >= -LineTolerance
                : landing.X <= LineTolerance;
        }
    }
}
