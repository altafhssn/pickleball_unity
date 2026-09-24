namespace Pickleball.Sim
{
    /// <summary>
    /// Mirrors Pickleball.Gameplay.ShotType exactly (same members, same order). Kept as a separate
    /// enum rather than shared: this assembly has no UnityEngine reference and must stay that way,
    /// while Gameplay.ShotType is used unqualified across ~15 MonoBehaviour files that aren't being
    /// touched in this pass. ShotSystem converts between the two at the boundary -- see
    /// ShotSystem.ToSim / ShotSystem.FromSim.
    /// </summary>
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

    /// <summary>Mirrors Pickleball.Gameplay.ShotQuality exactly. See ShotType for why this is a
    /// separate, structurally-identical enum rather than a shared one.</summary>
    public enum ShotQuality
    {
        Perfect,
        Great,
        Good,
        Weak,
        Miss
    }
}
