#if PHOTON_UNITY_NETWORKING
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;
using Pickleball.Gameplay;

namespace Pickleball.Net
{
    /// <summary>Photon implementation of the match protocol. Reliable events carry state changes;
    /// unreliable pose packets are cosmetic and may be dropped safely.</summary>
    public class PhotonNetworkTransport : INetworkTransport, IOnEventCallback, IInRoomCallbacks,
        IConnectionCallbacks, IMatchmakingCallbacks
    {
        public const byte ShotIntentEventCode = 2;
        public const byte ShotCommitEventCode = 3;
        public const byte PointEventCode = 4;
        public const byte PoseEventCode = 5;
        public const byte ForfeitEventCode = 6;
        public const byte SnapshotRequestEventCode = 7;
        public const byte SnapshotEventCode = 8;

        private readonly string opponentUserId;
        private readonly int opponentActorNumber;
        private readonly string matchId;
        private readonly int authorityActorNumber;
        private bool disposed;
        private bool reconnecting;
        private bool applicationPaused;

        public bool IsAuthority { get; }
        public int LocalSideId { get; }
        public double NetworkTime => PhotonNetwork.Time;

        public event System.Action<NetworkShotMessage, float> OnShotIntentReceived;
        public event System.Action<NetworkShotMessage, float> OnShotCommittedReceived;
        public event System.Action<NetworkPointMessage> OnPointResolved;
        public event System.Action<NetworkPoseMessage> OnPoseReceived;
        public event System.Action OnForfeitReceived;
        public event System.Action OnSnapshotRequested;
        public event System.Action<NetworkMatchSnapshot> OnSnapshotReceived;
        public event System.Action OnPeerDisconnected;
        public event System.Action OnPeerReconnected;
        public event System.Action OnLocalConnectionLost;
        public event System.Action OnLocalReconnected;

        public PhotonNetworkTransport(string opponentUserId, int opponentActorNumber, int localSideId, string matchId)
        {
            this.opponentUserId = opponentUserId;
            this.opponentActorNumber = opponentActorNumber;
            this.matchId = matchId;
            authorityActorNumber = localSideId == 0 ? PhotonNetwork.LocalPlayer.ActorNumber : opponentActorNumber;
            LocalSideId = localSideId;
            IsAuthority = localSideId == 0;
            PhotonNetwork.AddCallbackTarget(this);
        }

        public void SendShotIntent(NetworkShotMessage message)
        {
            PhotonNetwork.RaiseEvent(ShotIntentEventCode, PackShot(message),
                new RaiseEventOptions { TargetActors = new[] { authorityActorNumber } }, SendOptions.SendReliable);
            PhotonNetwork.SendAllOutgoingCommands();
        }

        public void SendShotCommit(NetworkShotMessage message)
        {
            PhotonNetwork.RaiseEvent(ShotCommitEventCode, PackShot(message),
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
            PhotonNetwork.SendAllOutgoingCommands();
        }

        public void SendPointResolved(NetworkPointMessage message)
        {
            object[] data =
            {
                message.matchId, message.rallyId, message.winnerSideId, message.side0Score,
                message.side1Score, message.nextServerSideId, message.matchOver, message.nextServeServerTime
            };
            PhotonNetwork.RaiseEvent(PointEventCode, data,
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
        }

        public void SendPose(NetworkPoseMessage message)
        {
            object[] data = { message.matchId, message.sideId, message.sequence, message.sentServerTime, message.position };
            PhotonNetwork.RaiseEvent(PoseEventCode, data,
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendUnreliable);
        }

        public void SendForfeit()
        {
            PhotonNetwork.RaiseEvent(ForfeitEventCode, matchId,
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
            PhotonNetwork.SendAllOutgoingCommands();
        }

        public void RequestSnapshot()
        {
            PhotonNetwork.RaiseEvent(SnapshotRequestEventCode, matchId,
                new RaiseEventOptions { TargetActors = new[] { authorityActorNumber } }, SendOptions.SendReliable);
        }

        public void SendSnapshot(NetworkMatchSnapshot snapshot)
        {
            object[] data =
            {
                snapshot.matchId, snapshot.rallyId, snapshot.lastShotSequence, snapshot.expectedHitterSideId,
                snapshot.side0Score, snapshot.side1Score, snapshot.serverSideId, snapshot.matchOver,
                snapshot.hasActiveShot, PackShot(snapshot.activeShot), snapshot.nextServeServerTime
            };
            PhotonNetwork.RaiseEvent(SnapshotEventCode, data,
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
        }

        public void Disconnect()
        {
            if (disposed) return;
            disposed = true;
            PhotonNetwork.RemoveCallbackTarget(this);
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
        }

        public void OnEvent(EventData photonEvent)
        {
            if (disposed || !IsExpectedSender(photonEvent.Sender)) return;
            try
            {
            switch (photonEvent.Code)
            {
                case ShotIntentEventCode:
                {
                    NetworkShotMessage message = UnpackShot((object[])photonEvent.CustomData);
                    if (!Matches(message.matchId)) return;
                    float elapsed = (float)System.Math.Max(0.0, PhotonNetwork.Time - message.sentServerTime);
                    OnShotIntentReceived?.Invoke(message, elapsed);
                    break;
                }
                case ShotCommitEventCode:
                {
                    NetworkShotMessage message = UnpackShot((object[])photonEvent.CustomData);
                    if (!Matches(message.matchId)) return;
                    float elapsed = (float)System.Math.Max(0.0, PhotonNetwork.Time - message.sentServerTime);
                    OnShotCommittedReceived?.Invoke(message, elapsed);
                    break;
                }
                case PointEventCode:
                {
                    object[] data = (object[])photonEvent.CustomData;
                    var message = new NetworkPointMessage
                    {
                        matchId = (string)data[0], rallyId = (int)data[1], winnerSideId = (int)data[2],
                        side0Score = (int)data[3], side1Score = (int)data[4], nextServerSideId = (int)data[5],
                        matchOver = (bool)data[6], nextServeServerTime = (double)data[7]
                    };
                    if (Matches(message.matchId)) OnPointResolved?.Invoke(message);
                    break;
                }
                case PoseEventCode:
                {
                    object[] data = (object[])photonEvent.CustomData;
                    var message = new NetworkPoseMessage
                    {
                        matchId = (string)data[0], sideId = (int)data[1], sequence = (int)data[2],
                        sentServerTime = (double)data[3], position = (Vector3)data[4]
                    };
                    if (Matches(message.matchId)) OnPoseReceived?.Invoke(message);
                    break;
                }
                case ForfeitEventCode:
                    if (Matches(photonEvent.CustomData as string)) OnForfeitReceived?.Invoke();
                    break;
                case SnapshotRequestEventCode:
                    if (Matches(photonEvent.CustomData as string)) OnSnapshotRequested?.Invoke();
                    break;
                case SnapshotEventCode:
                {
                    object[] data = (object[])photonEvent.CustomData;
                    var snapshot = new NetworkMatchSnapshot
                    {
                        matchId = (string)data[0], rallyId = (int)data[1], lastShotSequence = (int)data[2],
                        expectedHitterSideId = (int)data[3], side0Score = (int)data[4], side1Score = (int)data[5],
                        serverSideId = (int)data[6], matchOver = (bool)data[7], hasActiveShot = (bool)data[8],
                        activeShot = UnpackShot((object[])data[9]), nextServeServerTime = (double)data[10]
                    };
                    if (Matches(snapshot.matchId)) OnSnapshotReceived?.Invoke(snapshot);
                    break;
                }
            }
            }
            catch (System.Exception exception) when (exception is System.InvalidCastException ||
                exception is System.IndexOutOfRangeException || exception is System.NullReferenceException || exception is System.ArgumentException)
            {
                Debug.LogWarning("[PhotonTransport] Ignored malformed event " + photonEvent.Code);
            }
        }

        private bool Matches(string incomingMatchId) => !string.IsNullOrEmpty(incomingMatchId) && incomingMatchId == matchId;
        private bool IsExpectedSender(int actorNumber) => opponentActorNumber <= 0 || actorNumber == opponentActorNumber;

        private static object[] PackShot(NetworkShotMessage message)
        {
            ShotData shot = message.shot;
            return new object[]
            {
                message.matchId, message.rallyId, message.sequence, message.hitterSideId, message.sentServerTime,
                shot.hitterId, (byte)shot.shotType, shot.startPosition, shot.targetPosition, shot.arcHeight,
                shot.duration, shot.timingScore, shot.positionScore, shot.swipeAccuracy, shot.compositeScore,
                (byte)shot.quality, shot.bounceMultiplier, shot.spinRate
            };
        }

        private static NetworkShotMessage UnpackShot(object[] data)
        {
            var shot = new ShotData((int)data[5], (ShotType)(byte)data[6], (Vector3)data[7],
                (Vector3)data[8], (float)data[9], (float)data[10])
            {
                timingScore = (float)data[11], positionScore = (float)data[12], swipeAccuracy = (float)data[13],
                compositeScore = (float)data[14], quality = (ShotQuality)(byte)data[15],
                bounceMultiplier = (float)data[16], spinRate = (float)data[17]
            };
            return new NetworkShotMessage
            {
                matchId = (string)data[0], rallyId = (int)data[1], sequence = (int)data[2],
                hitterSideId = (int)data[3], sentServerTime = (double)data[4], shot = shot
            };
        }

        public void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (otherPlayer.ActorNumber == opponentActorNumber || otherPlayer.UserId == opponentUserId)
                OnPeerDisconnected?.Invoke();
        }

        public void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (newPlayer.ActorNumber == opponentActorNumber || newPlayer.UserId == opponentUserId)
                OnPeerReconnected?.Invoke();
        }

        public void OnDisconnected(DisconnectCause cause)
        {
            if (disposed) return;
            reconnecting = true;
            OnLocalConnectionLost?.Invoke();
            RetryReconnect();
        }

        public void RetryReconnect()
        {
            if (!disposed && !applicationPaused && reconnecting && PhotonNetwork.NetworkClientState == ClientState.Disconnected)
                PhotonNetwork.ReconnectAndRejoin();
        }

        public void SetApplicationPaused(bool paused)
        {
            if (disposed) return;
            applicationPaused = paused;
            if (paused)
            {
                // PUN otherwise acknowledges packets for up to a minute while mobile gameplay is
                // suspended. Disconnect promptly so the other phone can show its recovery timer.
                reconnecting = true;
                PhotonNetwork.Disconnect();
            }
            else RetryReconnect();
        }

        public void OnJoinedRoom()
        {
            if (!reconnecting) return;
            reconnecting = false;
            OnLocalReconnected?.Invoke();
            if (!IsAuthority) RequestSnapshot();
        }

        public void OnJoinRoomFailed(short returnCode, string message)
        {
            if (reconnecting) Debug.LogWarning("[PhotonTransport] Rejoin failed " + returnCode + ": " + message);
        }

        public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) { }
        public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) { }
        public void OnMasterClientSwitched(Player newMasterClient) { }
        public void OnConnected() { }
        public void OnConnectedToMaster() { }
        public void OnRegionListReceived(RegionHandler regionHandler) { }
        public void OnCustomAuthenticationResponse(Dictionary<string, object> data) { }
        public void OnCustomAuthenticationFailed(string debugMessage) { }
        public void OnFriendListUpdate(List<FriendInfo> friendList) { }
        public void OnCreatedRoom() { }
        public void OnCreateRoomFailed(short returnCode, string message) { }
        public void OnJoinRandomFailed(short returnCode, string message) { }
        public void OnLeftRoom() { }
    }
}
#endif
