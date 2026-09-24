using System.Numerics;

namespace Pickleball.Sim
{
    /// <summary>
    /// Mirrors Pickleball.Gameplay.ShotData field-for-field, using System.Numerics.Vector3 in place
    /// of UnityEngine.Vector3 since this assembly has no engine reference. ShotSystem converts
    /// between the two at the boundary so the ~15 files that already consume Gameplay.ShotData don't
    /// need to change.
    /// </summary>
    public struct ShotData
    {
        public int hitterId;              // 0 for the near side, 1 for the far side
        public ShotType shotType;
        public Vector3 startPosition;
        public Vector3 targetPosition;
        public float arcHeight;
        public float duration;

        public float timingScore;         // 0 to 1 (1 = exact center of timing window)
        public float positionScore;       // 0 to 1 (1 = ideal strike position)
        public float swipeAccuracy;       // 0 to 1 (1 = perfect swipe gesture)
        public float compositeScore;
        public ShotQuality quality;

        public float bounceMultiplier;
        public float spinRate;

        public ShotData(int hitterId, ShotType shotType, Vector3 startPos, Vector3 targetPos, float arcHeight, float duration)
        {
            this.hitterId = hitterId;
            this.shotType = shotType;
            this.startPosition = startPos;
            this.targetPosition = targetPos;
            this.arcHeight = arcHeight;
            this.duration = duration;
            this.timingScore = 1f;
            this.positionScore = 1f;
            this.swipeAccuracy = 1f;
            this.compositeScore = 1f;
            this.quality = ShotQuality.Perfect;
            this.bounceMultiplier = 1f;
            this.spinRate = 0f;
        }
    }
}
