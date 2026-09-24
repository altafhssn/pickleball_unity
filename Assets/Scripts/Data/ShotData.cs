using UnityEngine;

namespace Pickleball.Gameplay
{
    public enum ShotType
    {
        Flat,
        Topspin,
        Slice,
        Lob,
        Dink,
        Smash,
        Serve
    }

    public enum ShotQuality
    {
        Perfect,
        Great,
        Good,
        Weak,
        Miss
    }

    [System.Serializable]
    public struct ShotData
    {
        public int hitterId;              // 0 for Player, 1 for Opponent
        public ShotType shotType;
        public Vector3 startPosition;
        public Vector3 targetPosition;
        public float arcHeight;
        public float duration;
        
        // Quality metrics
        public float timingScore;         // 0 to 1 (1 = exact center of timing window)
        public float positionScore;       // 0 to 1 (1 = ideal strike position)
        public float swipeAccuracy;       // 0 to 1 (1 = perfect swipe gesture)
        public float compositeScore;      // ShotScore formula output
        public ShotQuality quality;

        // Visual / Physics properties
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
