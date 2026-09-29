namespace Pickleball.Sim
{
    /// <summary>
    /// The passive stats one side plays a match with. Each comes from exactly one gear slot (see
    /// <see cref="GearRules"/>): the paddle's Power, the shoes' Speed and the grip's Accuracy. Outfits
    /// are cosmetic and add nothing. This assembly has zero references (Pickleball.Sim.asmdef,
    /// noEngineReferences), so a re-simulating server can replay a match from a recorded value
    /// without MetaGameState or UnityEngine.
    ///
    /// All-zero (Neutral) is a true no-op through every diminishing-returns curve in ShotSim, so a
    /// caller with no gear (a trajectory preview) can simply omit the parameter.
    /// </summary>
    public struct LoadoutStats
    {
        /// <summary>Paddle. Shots arrive faster -- ShotSim.PowerDurationMultiplier.</summary>
        public float power;
        /// <summary>Grip. Shots land closer to where they were aimed -- ShotSim.AccuracyScatterMultiplier.</summary>
        public float accuracy;
        /// <summary>Shoes. The player covers the court faster and reaches wider -- ShotSim.SpeedMoveMultiplier.</summary>
        public float speed;

        public static readonly LoadoutStats Neutral = new LoadoutStats();
    }
}
