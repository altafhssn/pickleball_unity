namespace Pickleball.Sim
{
    /// <summary>
    /// Aggregated gear stats for one side of a shot, in the raw units GearProgressionCurve produces
    /// (Docs/GearProgression.md#1-stats) -- summed across a side's 4 equipped items by
    /// MetaGameState.GetLoadoutStats. Deliberately just six floats: this assembly has zero references
    /// (see Pickleball.Sim.asmdef, noEngineReferences) so a re-simulating server can replay a match
    /// from a recorded LoadoutStats value without ever touching MetaGameState, GearCatalog, or
    /// UnityEngine.
    ///
    /// All-zero (Neutral) is a true no-op through every diminishing-returns curve in ShotSim -- a
    /// stat of 0 always yields a 1x (or 0x-addition) multiplier -- so a caller with no real gear (the
    /// AI, a trajectory preview) can simply omit the parameter rather than special-case itself out.
    /// </summary>
    public struct LoadoutStats
    {
        public float power;
        public float spin;
        public float control;
        public float speed;
        public float serve;
        public float stamina;

        public static readonly LoadoutStats Neutral = new LoadoutStats();
    }
}
