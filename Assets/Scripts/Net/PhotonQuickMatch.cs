#if PHOTON_UNITY_NETWORKING
using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;
using Pickleball.Data;

namespace Pickleball.Net
{
    /// <summary>Ranked multiplayer matchmaking over Photon: connect, join a random open room in the
    /// player's own league (or open one), and once two players are present, have the room's
    /// MasterClient mint a match seed and hand it to the other player. The only filter is the league:
    /// the opponent is whoever else in that league is searching. There is no bot backfill and the
    /// league range never widens.</summary>
    public class PhotonQuickMatch : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        public const byte MatchStartEventCode = 1;
        /// <summary>v5: first-to-7 with no two-point margin, and league-bucketed rooms. Clients on
        /// different rules must never be paired, so the rule change bumps the version.</summary>
        private const string ProtocolVersion = "pickleball-pvp-v5";
        private const string PropProtocol = "protocol";
        /// <summary>Room property: the league index the room is for (Sim.LeagueRules.IndexFor).</summary>
        private const string PropLeague = "league";
        private const int PointsToWin = Pickleball.Gameplay.MatchConfig.PointsToWin;
        private const double StartLeadSeconds = 6.25;

        /// <summary>How long Photon keeps a disconnected player's room slot reserved for
        /// ReconnectAndRejoin, and (matched to it) how long this client waits before treating the
        /// disconnect as final -- see NetworkedMatchController's grace-window takeover.</summary>
        public const int PlayerTtlMs = 15000;

        private const string PropName = "name";
        /// <summary>The player's league points. The key keeps its old wire name.</summary>
        private const string PropLeaguePoints = "trophies";

        /// <summary>Fired once for whichever side of the handshake this client ended up on -- the
        /// caller (a match-start screen, eventually) hands both straight to
        /// NetworkedMatchController.StartMatch.</summary>
        public event Action<INetworkTransport, MatchStartMessage> OnMatchReady;

        /// <summary>Fired once this client has actually joined the shared room and is genuinely
        /// waiting for a second player -- distinct from still connecting/authenticating. Lets the UI
        /// show an honest "searching" state instead of claiming a match before one exists.</summary>
        public event Action OnSearching;
        public event Action<string> OnFailure;

        private bool isDestroyed;
        private int leagueIndex;

        private PhotonNetworkTransport transport;
        private bool matchStarted;
        private float searchStartedAt;
        private bool searchFailed;
        private string opponentUserId;

        public void BeginQuickMatch()
        {
            matchStarted = false;
            searchFailed = false;
            searchStartedAt = Time.realtimeSinceStartup;
            leagueIndex = MetaGameState.CurrentLeagueIndex;
            PhotonNetwork.AddCallbackTarget(this);

            PhotonNetwork.GameVersion = ProtocolVersion;
            PhotonNetwork.SendRate = 30;
            if (PhotonNetwork.AuthValues == null || string.IsNullOrEmpty(PhotonNetwork.AuthValues.UserId))
                PhotonNetwork.AuthValues = new AuthenticationValues(GetOrCreateNetworkUserId());
            PhotonNetwork.NickName = MetaGameState.PlayerName;
            SetLocalProperties();

            ClientState state = PhotonNetwork.NetworkClientState;
            if (state == ClientState.ConnectedToMasterServer) JoinQueue();
            else if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
            else if (state == ClientState.PeerCreated || state == ClientState.Disconnected) PhotonNetwork.ConnectUsingSettings();
            // Any other state (already connecting/authenticating/joining) means a handshake is
            // already in flight -- OnConnectedToMaster/OnJoinedRoom will carry it forward without a
            // duplicate call here.
        }

        /// <summary>Self-reported for display only -- see MatchStartMessage.opponentName.</summary>
        private static void SetLocalProperties()
        {
            var props = new Hashtable
            {
                { PropName, MetaGameState.PlayerName },
                { PropLeaguePoints, MetaGameState.LeaguePoints },
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        public override void OnConnectedToMaster()
        {
            JoinQueue();
        }

        private void JoinQueue()
        {
            if (isDestroyed || matchStarted || searchFailed) return;
            if (PhotonNetwork.NetworkClientState != ClientState.ConnectedToMasterServer) return;
            var roomOptions = new RoomOptions
            {
                MaxPlayers = 2,
                // Keeps a disconnected player's slot reserved so their own client can
                // ReconnectAndRejoin instead of the room forgetting them outright; matched to
                // NetworkedMatchController's own grace-window timer on the remaining player's side.
                PlayerTtl = PlayerTtlMs,
                EmptyRoomTtl = PlayerTtlMs,
                CleanupCacheOnLeave = true,
                CustomRoomProperties = new Hashtable { { PropProtocol, ProtocolVersion }, { PropLeague, leagueIndex } },
                CustomRoomPropertiesForLobby = new[] { PropProtocol, PropLeague },
            };
            // Only rooms for this league match; a new room is opened for it otherwise.
            var expected = new Hashtable { { PropProtocol, ProtocolVersion }, { PropLeague, leagueIndex } };
            PhotonNetwork.JoinRandomOrCreateRoom(expected, 2, MatchmakingMode.FillRoom,
                TypedLobby.Default, null, "pickleball-" + Guid.NewGuid().ToString("N"), roomOptions);
        }

        public override void OnJoinedRoom()
        {
            if (isDestroyed || searchFailed) { PhotonNetwork.LeaveRoom(false); return; }
            TryStartIfReady();
            if (!matchStarted && PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount < 2)
            {
                OnSearching?.Invoke();
            }
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            // Once matched, PhotonQuickMatch's own GameObject is about to be destroyed along with the
            // matchmaking screen (ScreenManager.EnterPvPMatch clears it) -- ongoing room-membership
            // monitoring for the rest of the match lives on PhotonNetworkTransport instead, which
            // registers itself independently and outlives this. See that class for why.
            if (matchStarted) return;
            TryStartIfReady();
        }

        private void TryStartIfReady()
        {
            if (matchStarted) return;
            if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.PlayerCount < 2) return;
            // Only the master client mints the seed and announces it -- otherwise both players would
            // race to pick one and disagree about which was authoritative.
            if (!PhotonNetwork.IsMasterClient) return;

            Player opponent = FindOpponent();
            if (opponent == null || opponent.IsInactive) return;

            matchStarted = true;
            ulong seed = unchecked((ulong)Guid.NewGuid().GetHashCode() ^ (ulong)DateTime.UtcNow.Ticks);
            double startServerTime = PhotonNetwork.Time + StartLeadSeconds;
            opponentUserId = opponent != null ? opponent.UserId : null;

            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.IsVisible = false;

            PhotonNetwork.RaiseEvent(MatchStartEventCode, new object[] { unchecked((long)seed), startServerTime },
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);

            BeginLocalMatch(seed, startServerTime, localSideId: 0, localServesFirst: true, opponent);
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != MatchStartEventCode || matchStarted) return;
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom.MasterClientId != photonEvent.Sender ||
                !(photonEvent.CustomData is object[] startData) || startData.Length != 2 ||
                !(startData[0] is long) || !(startData[1] is double) ||
                double.IsNaN((double)startData[1]) || double.IsInfinity((double)startData[1])) return;
            matchStarted = true;
            ulong seed = unchecked((ulong)(long)startData[0]);
            double startServerTime = (double)startData[1];
            // Each side determines its own opponent locally via PUN's room player list -- no need to
            // transmit identity, only the seed needs to come from the master.
            Player opponent = FindOpponent();
            opponentUserId = opponent != null ? opponent.UserId : null;
            BeginLocalMatch(seed, startServerTime, localSideId: 1, localServesFirst: false, opponent);
        }

        private static Player FindOpponent()
        {
            List<Player> others = PhotonNetwork.PlayerListOthers != null
                ? new List<Player>(PhotonNetwork.PlayerListOthers) : null;
            return others != null ? others.Find(player => !player.IsInactive) : null;
        }

        private void BeginLocalMatch(ulong seed, double startServerTime, int localSideId, bool localServesFirst, Player opponent)
        {
            int opponentActorNumber = opponent != null ? opponent.ActorNumber : 0;
            string roomName = PhotonNetwork.CurrentRoom.Name;
            transport = new PhotonNetworkTransport(opponentUserId, opponentActorNumber, localSideId, roomName);

            string oppName = "OPPONENT";
            int oppLeaguePoints = 0;
            if (opponent != null && opponent.CustomProperties != null)
            {
                if (opponent.CustomProperties.TryGetValue(PropName, out object n) && n is string s && !string.IsNullOrEmpty(s)) oppName = s;
                if (opponent.CustomProperties.TryGetValue(PropLeaguePoints, out object t) && t is int ti) oppLeaguePoints = ti;
            }

            var matchStart = new MatchStartMessage
            {
                matchId = roomName,
                seed = seed,
                localSideId = localSideId,
                localServesFirst = localServesFirst,
                pointsToWin = PointsToWin,
                startServerTime = startServerTime,
                opponentName = oppName,
                opponentLeaguePoints = oppLeaguePoints,
            };
            OnMatchReady?.Invoke(transport, matchStart);
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            if (!matchStarted) OnFailure?.Invoke("Unable to join match (" + returnCode + "). " + message);
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            if (!matchStarted) OnFailure?.Invoke("Matchmaking failed (" + returnCode + "). " + message);
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            if (!matchStarted) OnFailure?.Invoke("Unable to create match (" + returnCode + "). " + message);
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            if (!matchStarted) OnFailure?.Invoke("Connection lost: " + cause);
        }

        private static string GetOrCreateNetworkUserId()
        {
            const string key = "pickleball.network.userId";
            string id = PlayerPrefs.GetString(key, string.Empty);
            if (!string.IsNullOrEmpty(id)) return id;
            id = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(key, id);
            PlayerPrefs.Save();
            return id;
        }

        private void OnDestroy()
        {
            // Set before LeaveRoom below so a callback Photon invokes synchronously during teardown
            // (OnJoinedRoom) sees it and does nothing.
            isDestroyed = true;

            // If the screen this is attached to gets torn down (player hit Cancel) before a match was
            // found, leave the room rather than abandoning it silently occupied.
            if (!matchStarted && PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
            else if (!matchStarted && PhotonNetwork.NetworkClientState != ClientState.Disconnected &&
                PhotonNetwork.NetworkClientState != ClientState.ConnectedToMasterServer)
                PhotonNetwork.Disconnect();
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        private void Update()
        {
            if (matchStarted || searchFailed || Time.realtimeSinceStartup - searchStartedAt < 60f) return;
            searchFailed = true;
            OnFailure?.Invoke("No opponent found in time. Please try again.");
        }
    }
}
#endif
