using System;
using UnityEngine;
using Pickleball.VFX;
using Sim = Pickleball.Sim;

namespace Pickleball.Gameplay
{
    /// <summary>
    /// A networked human opponent, driven by NetworkedMatchController rather than local input or AI.
    /// Proves IMatchParticipant is genuinely implementable by something that isn't a local
    /// MonoBehaviour -- RallyManager's match loop does not change to support this.
    /// </summary>
    public class RemoteParticipant : IMatchParticipant
    {
        private const float MoveSpeed = 5.5f;

        private readonly Vector3 homePosition;
        private Vector3 currentPosition;
        private Vector3 moveTarget;
        private float lastPoseLocalTime = -999f;

        private Transform visualTransform;
        private CharacterVisual visualCharacter;
        private PaddleVisual visualPaddle;

        public int SideId { get; }
        public Vector3 Position => currentPosition;

        public event Action<ShotData> OnShotHit;

        public RemoteParticipant(int sideId, Vector3 homePosition)
        {
            SideId = sideId;
            this.homePosition = homePosition;
            currentPosition = homePosition;
            moveTarget = homePosition;
        }

        /// <summary>Hands this participant the peer's own on-court avatar (the scene's existing
        /// Opponent rig) so its movement and swings are actually visible, instead of the peer's shots
        /// only ever showing up as motion on the local player's own model. CharacterVisual reads the
        /// transform's frame-to-frame position delta to drive its run animation, so just moving the
        /// transform here is enough -- no animation wiring needed on top.</summary>
        public void AttachVisual(Transform transform, CharacterVisual character, PaddleVisual paddle)
        {
            visualTransform = transform;
            visualCharacter = character;
            visualPaddle = paddle;
            if (visualTransform != null) visualTransform.position = homePosition;
            // The rig is shared with the AI opponent and may still hold the last match's result pose.
            if (visualCharacter != null) visualCharacter.ResetResultPose();
        }

        /// <summary>Called every frame by NetworkedMatchController -- this class isn't a MonoBehaviour,
        /// so it has no Update of its own to move the visual transform toward moveTarget.</summary>
        public void Tick(float deltaTime)
        {
            if (visualTransform == null) return;
            float distance = Vector3.Distance(visualTransform.position, moveTarget);
            if (distance > 3.5f) visualTransform.position = moveTarget;
            else if (Time.unscaledTime - lastPoseLocalTime < 0.5f)
                visualTransform.position = Vector3.Lerp(visualTransform.position, moveTarget,
                    1f - Mathf.Exp(-16f * Mathf.Max(0f, deltaTime)));
            else
                visualTransform.position = Vector3.MoveTowards(visualTransform.position, moveTarget, MoveSpeed * deltaTime);
            currentPosition = visualTransform.position;
        }

        public void SetNetworkPose(Vector3 position)
        {
            moveTarget = position;
            lastPoseLocalTime = Time.unscaledTime;
            if (visualTransform == null) currentPosition = position;
        }

        /// <summary>A human-controlled remote side serves on their own machine whenever they're ready;
        /// this client just waits for the resulting shot to arrive over the wire.</summary>
        public void EnterServeMode(bool serveFromRight)
        {
            System.Numerics.Vector3 simPosition = Sim.ServeRules.ServerPosition(SideId, serveFromRight);
            currentPosition = new Vector3(simPosition.X, simPosition.Y, simPosition.Z);
            moveTarget = currentPosition;
            if (visualTransform != null) visualTransform.position = currentPosition;
        }

        /// <summary>Moves the peer's avatar to intercept, mirroring OpponentAI.ReactToPlayerShot's own
        /// clamp so the remote avatar stays inside its half exactly like the AI one always did.</summary>
        public void PrepareForIncomingShot(ShotData incomingShot)
        {
            if (Time.unscaledTime - lastPoseLocalTime >= 0.5f)
            {
                currentPosition = incomingShot.targetPosition;
                moveTarget = new Vector3(
                    Mathf.Clamp(incomingShot.targetPosition.x, -4.6f, 4.6f),
                    1.0f,
                    Mathf.Clamp(incomingShot.targetPosition.z + 0.5f, 0.3f, 7.8f));
            }
        }

        public void ResetPosition()
        {
            currentPosition = homePosition;
            moveTarget = homePosition;
            lastPoseLocalTime = -999f;
            if (visualTransform != null) visualTransform.position = homePosition;
            // Same as PlayerController/OpponentAI.ResetPosition. Without it a finished match left
            // CharacterVisual refusing every swing, so the peer's avatar never animated a shot again.
            if (visualCharacter != null) visualCharacter.ResetResultPose();
        }

        /// <summary>Called by NetworkedMatchController once a ShotData arrives from the peer. The shot
        /// was already fully evaluated -- including its RNG-driven inaccuracy roll -- on the sender's
        /// own client; see INetworkTransport.SendShot for why this trusts the transmitted result
        /// rather than recomputing it.
        ///
        /// hitterId is overwritten to this participant's own SideId before firing. It arrives set to 0
        /// -- the sender's PlayerController always stamps its own shots that way, since PlayerController
        /// has no idea it's talking to a network peer -- but RallyManager.HandleShotFromSide routes by
        /// treating hitterId as an index into *this* client's own `sides` array (0 = local player,
        /// 1 = opponent), not as a globally-meaningful player identity. Trusting the wire value here
        /// would misroute every remote shot back at the local player instead of at the peer.</summary>
        public void ReceiveRemoteShot(ShotData shot)
        {
            Debug.Log("[RemoteParticipant] Received shot: type=" + shot.shotType + " quality=" + shot.quality +
                " target=" + shot.targetPosition + " wireHitterId=" + shot.hitterId + " correctedTo=" + SideId);
            shot.hitterId = SideId;

            // This avatar faces -Z, so its forehand (right-hand) side is world -X: the same test as
            // OpponentAI, mirrored from the near-side player's.
            bool isForehand = visualTransform != null ? shot.startPosition.x <= visualTransform.position.x : true;
            if (visualCharacter != null) visualCharacter.PlaySwing(isForehand, shot.shotType, shot.compositeScore);
            else if (visualPaddle != null) visualPaddle.PlaySwing(isForehand, shot.shotType, shot.compositeScore);

            // Mirrors OpponentAI.ExecuteReturnShot: once the peer has struck, their avatar heads back
            // toward home rather than lingering at the intercept point until the next incoming shot.
            if (Time.unscaledTime - lastPoseLocalTime >= 0.5f) moveTarget = homePosition;

            OnShotHit?.Invoke(shot);
        }
    }
}
