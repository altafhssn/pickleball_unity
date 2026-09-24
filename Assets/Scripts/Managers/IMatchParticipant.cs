using System;
using UnityEngine;

namespace Pickleball.Gameplay
{
    /// <summary>
    /// One side of a match, from RallyManager's point of view. PlayerController and OpponentAI both
    /// implement this; a future RemoteParticipant (a networked human) implements it identically, so
    /// RallyManager's shot-exchange loop does not need to know or care what is driving the other side
    /// of the net -- see RallyManager.sides. Concrete fields on RallyManager stay Unity-serializable
    /// (interfaces can't be SerializeField'd, and the scene-setup tooling wires them by field name),
    /// but the match loop itself talks only through this contract.
    /// </summary>
    public interface IMatchParticipant
    {
        /// <summary>0 for the near side (today's "Player"), 1 for the far side ("Opponent"). Matches
        /// ShotData.hitterId so a shot can be routed to the correct side without extra lookups.</summary>
        int SideId { get; }
        Vector3 Position { get; }

        /// <summary>Fired the instant this side strikes the ball, whether serving or returning.</summary>
        event Action<ShotData> OnShotHit;

        /// <summary>Begin this side's serve from its score-correct right/even or left/odd service
        /// position. A human-controlled side waits for the swipe gesture; an AI or scripted side may
        /// serve immediately and fire OnShotHit synchronously.</summary>
        void EnterServeMode(bool serveFromRight);

        /// <summary>The ball is inbound; move to intercept it and prepare to return the given shot.
        /// Unifies what were previously two differently-shaped methods (PrepareForIncomingBall on the
        /// player, ReactToPlayerShot on the AI) so RallyManager can call either side identically.</summary>
        void PrepareForIncomingShot(ShotData incomingShot);

        /// <summary>Return to the home position for a new point/serve.</summary>
        void ResetPosition();
    }
}
