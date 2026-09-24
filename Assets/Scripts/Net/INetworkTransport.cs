using System;
using UnityEngine;
using Pickleball.Gameplay;

namespace Pickleball.Net
{
    [Serializable]
    public struct MatchStartMessage
    {
        public string matchId;
        public ulong seed;
        /// <summary>Stable match-space side: authority/master is 0, peer is 1.</summary>
        public int localSideId;
        public bool localServesFirst;
        public int pointsToWin;
        /// <summary>Photon server time at which gameplay becomes active on both clients.</summary>
        public double startServerTime;
        public string opponentName;
        public int opponentTrophies;
        public int opponentOverallRating;
    }

    [Serializable]
    public struct NetworkShotMessage
    {
        public string matchId;
        public int rallyId;
        public int sequence;
        public int hitterSideId;
        public double sentServerTime;
        public ShotData shot;
    }

    [Serializable]
    public struct NetworkPointMessage
    {
        public string matchId;
        public int rallyId;
        public int winnerSideId;
        public int side0Score;
        public int side1Score;
        public int nextServerSideId;
        public bool matchOver;
        public double nextServeServerTime;
    }

    [Serializable]
    public struct NetworkPoseMessage
    {
        public string matchId;
        public int sideId;
        public int sequence;
        public double sentServerTime;
        public Vector3 position;
    }

    [Serializable]
    public struct NetworkMatchSnapshot
    {
        public string matchId;
        public int rallyId;
        public int lastShotSequence;
        public int expectedHitterSideId;
        public int side0Score;
        public int side1Score;
        public int serverSideId;
        public bool matchOver;
        public bool hasActiveShot;
        public NetworkShotMessage activeShot;
        public double nextServeServerTime;
    }

    /// <summary>
    /// Match protocol boundary. Discrete gameplay decisions are reliable; cosmetic poses are
    /// unreliable. The transport never decides game rules -- NetworkedMatchController does.
    /// </summary>
    public interface INetworkTransport
    {
        bool IsAuthority { get; }
        int LocalSideId { get; }
        double NetworkTime { get; }

        event Action<NetworkShotMessage, float> OnShotIntentReceived;
        event Action<NetworkShotMessage, float> OnShotCommittedReceived;
        event Action<NetworkPointMessage> OnPointResolved;
        event Action<NetworkPoseMessage> OnPoseReceived;
        event Action OnForfeitReceived;
        event Action OnSnapshotRequested;
        event Action<NetworkMatchSnapshot> OnSnapshotReceived;
        event Action OnPeerDisconnected;
        event Action OnPeerReconnected;
        event Action OnLocalConnectionLost;
        event Action OnLocalReconnected;

        void SendShotIntent(NetworkShotMessage message);
        void SendShotCommit(NetworkShotMessage message);
        void SendPointResolved(NetworkPointMessage message);
        void SendPose(NetworkPoseMessage message);
        void SendForfeit();
        void RequestSnapshot();
        void SendSnapshot(NetworkMatchSnapshot snapshot);
        void Disconnect();
    }
}
