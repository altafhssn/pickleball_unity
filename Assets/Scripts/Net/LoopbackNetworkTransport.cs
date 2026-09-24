using System;
using UnityEngine;
using Pickleball.Gameplay;

namespace Pickleball.Net
{
    /// <summary>In-process transport: two instances cross-wired so protocol messages from one fire
    /// on the other with zero networking. This lets the PvP wiring
    /// -- wire messages, RemoteParticipant, RallyManager's PvP mode -- be verified end to end today,
    /// without needing Photon's SDK or an App ID. Swap in PhotonNetworkTransport once those exist;
    /// NetworkedMatchController does not change.</summary>
    public class LoopbackNetworkTransport : INetworkTransport
    {
        private LoopbackNetworkTransport peer;
        private readonly int localSideId;

        public bool IsAuthority => localSideId == 0;
        public int LocalSideId => localSideId;
        public double NetworkTime => Time.realtimeSinceStartupAsDouble;

        public event Action<NetworkShotMessage, float> OnShotIntentReceived;
        public event Action<NetworkShotMessage, float> OnShotCommittedReceived;
        public event Action<NetworkPointMessage> OnPointResolved;
        public event Action<NetworkPoseMessage> OnPoseReceived;
        public event Action OnForfeitReceived;
        public event Action OnSnapshotRequested;
        public event Action<NetworkMatchSnapshot> OnSnapshotReceived;
        public event Action OnPeerDisconnected;
        public event Action OnPeerReconnected { add { } remove { } }
        public event Action OnLocalConnectionLost { add { } remove { } }
        public event Action OnLocalReconnected { add { } remove { } }

        private LoopbackNetworkTransport(int localSideId)
        {
            this.localSideId = localSideId;
        }

        public static (LoopbackNetworkTransport a, LoopbackNetworkTransport b) CreatePair()
        {
            var a = new LoopbackNetworkTransport(0);
            var b = new LoopbackNetworkTransport(1);
            a.peer = b;
            b.peer = a;
            return (a, b);
        }

        public void SendShotIntent(NetworkShotMessage message) => peer?.OnShotIntentReceived?.Invoke(message, 0f);
        public void SendShotCommit(NetworkShotMessage message) => peer?.OnShotCommittedReceived?.Invoke(message, 0f);
        public void SendPointResolved(NetworkPointMessage message) => peer?.OnPointResolved?.Invoke(message);
        public void SendPose(NetworkPoseMessage message) => peer?.OnPoseReceived?.Invoke(message);
        public void SendForfeit() => peer?.OnForfeitReceived?.Invoke();
        public void RequestSnapshot() => peer?.OnSnapshotRequested?.Invoke();
        public void SendSnapshot(NetworkMatchSnapshot snapshot) => peer?.OnSnapshotReceived?.Invoke(snapshot);

        public void Disconnect() => peer?.OnPeerDisconnected?.Invoke();
    }
}
