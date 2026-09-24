using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pickleball.Gameplay;
using Pickleball.Systems;

namespace Pickleball.Net
{
    /// <summary>
    /// Owns the authoritative PvP protocol. Side 0 (the Photon MasterClient at match creation)
    /// validates and commits shots, resolves points, and publishes snapshots. Side 1 predicts its
    /// own swing for responsiveness but accepts side 0's committed rally/score state.
    /// </summary>
    public class NetworkedMatchController : MonoBehaviour
    {
        private const float GraceWindowSeconds = 15f;
        private const double PoseIntervalSeconds = 1.0 / 20.0;
        // Shared with the local match presentation so PvP peers resume on the same, shorter beat.
        private const float PointBreakSeconds = RallyManager.PointBreakSeconds;

        [SerializeField] private RallyManager rallyManager;
        [SerializeField] private PlayerController player;
        [SerializeField] private BallController ball;

        private INetworkTransport transport;
        private MatchStartMessage matchStart;
        private RemoteParticipant remote;
        private OpponentAI opponentVisualOwner;
        private Coroutine graceWindowCoroutine;
        private Coroutine localRecoveryCoroutine;
        private bool connectionSuspended;
        private bool menuInputSuspended;
        private bool awaitingStartSnapshot;
        private bool pendingPeerForfeit;
        private readonly Queue<NetworkShotMessage> bufferedCommits = new Queue<NetworkShotMessage>();

        private bool prepared;
        private bool gameplayActive;
        private int rallyId;
        private int lastShotSequence;
        private int expectedHitterSideId;
        private int serverSideId;
        private int side0Score;
        private int side1Score;
        private int poseSequence;
        private int lastRemotePoseSequence;
        private double nextPoseServerTime;
        private double nextServeServerTime;
        private bool hasActiveShot;
        private NetworkShotMessage activeShot;

        public RemoteParticipant Remote => remote;
        public bool HasActiveMatch => prepared;
        public double NetworkTime => transport != null ? transport.NetworkTime : Time.realtimeSinceStartupAsDouble;
        public double ScheduledStartServerTime => matchStart.startServerTime;

        /// <summary>Attaches network callbacks before the court intro starts, closing the old window
        /// in which an early first serve could arrive with no gameplay subscriber.</summary>
        public void PrepareMatch(INetworkTransport newTransport, MatchStartMessage start)
        {
            TeardownCurrentMatch();
            transport = newTransport;
            matchStart = start;

            if (rallyManager == null) rallyManager = FindAnyObjectByType<RallyManager>();
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (ball == null) ball = FindAnyObjectByType<BallController>();
            if (transport == null || rallyManager == null || player == null || ball == null)
            {
                Debug.LogError("[NetworkedMatchController] Cannot prepare PvP match: required reference is missing.");
                return;
            }

            remote = new RemoteParticipant(1, new Vector3(0f, 1f, 6f));
            opponentVisualOwner = FindAnyObjectByType<OpponentAI>();
            if (opponentVisualOwner != null)
            {
                opponentVisualOwner.enabled = false;
                remote.AttachVisual(opponentVisualOwner.transform,
                    opponentVisualOwner.GetComponent<Pickleball.VFX.CharacterVisual>(),
                    opponentVisualOwner.GetComponent<Pickleball.VFX.PaddleVisual>());
            }

            transport.OnShotIntentReceived += HandleShotIntent;
            transport.OnShotCommittedReceived += HandleShotCommit;
            transport.OnPointResolved += HandlePointResolved;
            transport.OnPoseReceived += HandlePose;
            transport.OnForfeitReceived += HandleForfeit;
            transport.OnSnapshotRequested += HandleSnapshotRequested;
            transport.OnSnapshotReceived += HandleSnapshot;
            transport.OnPeerDisconnected += HandlePeerDisconnected;
            transport.OnPeerReconnected += HandlePeerReconnected;
            transport.OnLocalConnectionLost += HandleLocalConnectionLost;
            transport.OnLocalReconnected += HandleLocalReconnected;

            rallyManager.OnAuthoritativePointResolved += HandleAuthoritativePoint;
            rallyManager.SetNetworkInputSuspended(true);
            if (ShotSystem.Instance != null) ShotSystem.Instance.SeedMatch(start.seed);

            rallyId = 1;
            lastShotSequence = 0;
            serverSideId = start.localServesFirst ? start.localSideId : 1 - start.localSideId;
            expectedHitterSideId = serverSideId;
            side0Score = 0;
            side1Score = 0;
            poseSequence = 0;
            lastRemotePoseSequence = 0;
            hasActiveShot = false;
            nextServeServerTime = start.startServerTime;
            prepared = true;

            Debug.Log("[NetworkedMatchController] Prepared authoritative PvP match " + start.matchId +
                " side=" + start.localSideId + " authority=" + transport.IsAuthority +
                " startsAt=" + start.startServerTime.ToString("F3"));
        }

        public void StartPreparedMatch()
        {
            if (!prepared || gameplayActive) return;
            gameplayActive = true;
            player.OnShotHit += HandleLocalShot;
            rallyManager.StartPvPMatch(remote, matchStart.localServesFirst, transport.IsAuthority, matchStart.pointsToWin);

            while (bufferedCommits.Count > 0)
            {
                NetworkShotMessage buffered = bufferedCommits.Dequeue();
                HandleShotCommit(buffered, (float)System.Math.Max(0.0, transport.NetworkTime - buffered.sentServerTime));
            }
            if (pendingPeerForfeit) { pendingPeerForfeit = false; HandleForfeit(); }
            // The intro may have stalled while the authority already completed a point. Fetch a
            // current state after configuring RallyManager; pre-game snapshots cannot restore it.
            if (!transport.IsAuthority && rallyManager.State != MatchState.MatchOver)
            {
                awaitingStartSnapshot = true;
                rallyManager.SetNetworkInputSuspended(true);
                transport.RequestSnapshot();
            }
        }

        /// <summary>Backwards-compatible entry point for callers that do not use the prepared intro.</summary>
        public void StartMatch(INetworkTransport newTransport, MatchStartMessage start)
        {
            PrepareMatch(newTransport, start);
            StartPreparedMatch();
        }

        private void Update()
        {
            remote?.Tick(Time.unscaledDeltaTime);
            if (!gameplayActive || transport == null || player == null) return;
            rallyManager.SetNetworkInputSuspended(connectionSuspended || menuInputSuspended || awaitingStartSnapshot || transport.NetworkTime < matchStart.startServerTime);
            if (transport.NetworkTime < nextPoseServerTime) return;

            nextPoseServerTime = transport.NetworkTime + PoseIntervalSeconds;
            transport.SendPose(new NetworkPoseMessage
            {
                matchId = matchStart.matchId,
                sideId = matchStart.localSideId,
                sequence = ++poseSequence,
                sentServerTime = transport.NetworkTime,
                position = player.transform.position
            });
        }

        private void HandleLocalShot(ShotData shot)
        {
            if (!gameplayActive || transport == null || rallyManager.State != MatchState.InRally) return;
            int localSide = matchStart.localSideId;
            int sequence = lastShotSequence + 1;
            if (expectedHitterSideId != localSide)
            {
                Debug.LogWarning("[NetworkedMatchController] Rejected local out-of-turn shot.");
                return;
            }

            var message = new NetworkShotMessage
            {
                matchId = matchStart.matchId,
                rallyId = rallyId,
                sequence = sequence,
                hitterSideId = localSide,
                sentServerTime = transport.NetworkTime,
                shot = shot
            };

            if (transport.IsAuthority)
            {
                AcceptCommittedShot(message);
                transport.SendShotCommit(message);
            }
            else
            {
                transport.SendShotIntent(message);
            }
        }

        private void HandleShotIntent(NetworkShotMessage message, float elapsedSeconds)
        {
            if (!gameplayActive || transport == null || !transport.IsAuthority) return;
            if (!ValidateNextShot(message, requireRemoteHitter: true)) return;

            ShotData local = MirrorToLocalFrame(message.shot);
            local.hitterId = 1;
            AcceptCommittedShot(message);
            transport.SendShotCommit(message);
            LaunchRemoteShot(local, elapsedSeconds);
        }

        private void HandleShotCommit(NetworkShotMessage message, float elapsedSeconds)
        {
            if (!prepared || transport == null || transport.IsAuthority) return;
            if (!gameplayActive)
            {
                bufferedCommits.Enqueue(message);
                return;
            }
            if (!ValidateNextShot(message, requireRemoteHitter: false)) return;

            if (message.hitterSideId != matchStart.localSideId)
            {
                ShotData local = MirrorToLocalFrame(message.shot);
                local.hitterId = 1; // Correct before BallController stores CurrentShot.
                AcceptCommittedShot(message);
                LaunchRemoteShot(local, elapsedSeconds);
                return;
            }
            AcceptCommittedShot(message);
        }

        private bool ValidateNextShot(NetworkShotMessage message, bool requireRemoteHitter)
        {
            if (message.matchId != matchStart.matchId || message.rallyId != rallyId ||
                message.sequence != lastShotSequence + 1 || message.hitterSideId != expectedHitterSideId)
            {
                Debug.LogWarning("[NetworkedMatchController] Rejected stale/out-of-order shot: rally=" +
                    message.rallyId + " seq=" + message.sequence + " hitter=" + message.hitterSideId);
                return false;
            }
            if (requireRemoteHitter && message.hitterSideId == matchStart.localSideId) return false;
            if (double.IsNaN(message.sentServerTime) || double.IsInfinity(message.sentServerTime) ||
                message.sentServerTime > transport.NetworkTime + .5 || transport.NetworkTime - message.sentServerTime > 5 ||
                !IsFiniteAndPlausible(message.shot))
            {
                Debug.LogWarning("[NetworkedMatchController] Rejected malformed shot payload.");
                return false;
            }
            return true;
        }

        private static bool IsFiniteAndPlausible(ShotData shot)
        {
            return IsFinite(shot.startPosition) && IsFinite(shot.targetPosition) &&
                Mathf.Abs(shot.startPosition.x) <= 8f && Mathf.Abs(shot.startPosition.z) <= 12f &&
                Mathf.Abs(shot.targetPosition.x) <= 6f && Mathf.Abs(shot.targetPosition.z) <= 11f &&
                shot.duration >= 0.3f && shot.duration <= 3f && shot.arcHeight >= 0.1f && shot.arcHeight <= 7f &&
                shot.startPosition.y >= 0f && shot.startPosition.y <= 8f && shot.targetPosition.y >= 0f && shot.targetPosition.y <= 2f &&
                shot.bounceMultiplier >= .1f && shot.bounceMultiplier <= 3f &&
                shot.spinRate >= -10f && shot.spinRate <= 10f &&
                shot.compositeScore >= 0f && shot.compositeScore <= 1f &&
                shot.timingScore >= 0f && shot.timingScore <= 1f && shot.positionScore >= 0f && shot.positionScore <= 1f &&
                shot.swipeAccuracy >= 0f && shot.swipeAccuracy <= 1f &&
                System.Enum.IsDefined(typeof(ShotType), shot.shotType) && System.Enum.IsDefined(typeof(ShotQuality), shot.quality);
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private void AcceptCommittedShot(NetworkShotMessage message)
        {
            lastShotSequence = message.sequence;
            expectedHitterSideId = 1 - message.hitterSideId;
            activeShot = message;
            hasActiveShot = true;
        }

        private void LaunchRemoteShot(ShotData localShot, float elapsedSeconds)
        {
            ball.LaunchShot(localShot);
            remote.ReceiveRemoteShot(localShot);
            ball.AdvanceToElapsed(elapsedSeconds);
            if (rallyManager.State == MatchState.InRally && ball.State != BallState.Idle && ball.State != BallState.Bounced)
                player.PrepareForIncomingShot(localShot);
        }

        private void HandleAuthoritativePoint(bool playerWins, int playerScore, int opponentScore, bool matchOver)
        {
            if (!gameplayActive || transport == null || !transport.IsAuthority) return;
            side0Score = matchStart.localSideId == 0 ? playerScore : opponentScore;
            side1Score = matchStart.localSideId == 1 ? playerScore : opponentScore;
            int winnerSide = playerWins ? matchStart.localSideId : 1 - matchStart.localSideId;
            serverSideId = rallyManager.IsPlayerServing ? matchStart.localSideId : 1 - matchStart.localSideId;
            nextServeServerTime = matchOver ? 0.0 : transport.NetworkTime + PointBreakSeconds;
            hasActiveShot = false;

            transport.SendPointResolved(new NetworkPointMessage
            {
                matchId = matchStart.matchId,
                rallyId = rallyId,
                winnerSideId = winnerSide,
                side0Score = side0Score,
                side1Score = side1Score,
                nextServerSideId = serverSideId,
                matchOver = matchOver,
                nextServeServerTime = nextServeServerTime
            });

            if (!matchOver)
            {
                rallyId++;
                lastShotSequence = 0;
                expectedHitterSideId = serverSideId;
            }
        }

        private void HandlePointResolved(NetworkPointMessage message)
        {
            if (!gameplayActive || transport == null || transport.IsAuthority ||
                message.matchId != matchStart.matchId || message.rallyId != rallyId) return;

            side0Score = message.side0Score;
            side1Score = message.side1Score;
            serverSideId = message.nextServerSideId;
            nextServeServerTime = message.nextServeServerTime;
            hasActiveShot = false;
            bool playerWins = message.winnerSideId == matchStart.localSideId;
            int localScore = matchStart.localSideId == 0 ? side0Score : side1Score;
            int remoteScore = matchStart.localSideId == 0 ? side1Score : side0Score;
            float delay = message.matchOver ? 0f : (float)System.Math.Max(0.05, message.nextServeServerTime - transport.NetworkTime);

            rallyManager.ApplyAuthoritativePoint(playerWins, localScore, remoteScore, message.matchOver,
                message.nextServerSideId == matchStart.localSideId, delay);

            if (!message.matchOver)
            {
                rallyId++;
                lastShotSequence = 0;
                expectedHitterSideId = serverSideId;
            }
        }

        private void HandlePose(NetworkPoseMessage message)
        {
            if (!gameplayActive || message.matchId != matchStart.matchId ||
                message.sideId == matchStart.localSideId || message.sequence <= lastRemotePoseSequence ||
                !IsFinite(message.position)) return;
            lastRemotePoseSequence = message.sequence;
            remote.SetNetworkPose(new Vector3(-message.position.x, message.position.y, -message.position.z));
        }

        private void HandleSnapshotRequested()
        {
            if (transport == null || !transport.IsAuthority) return;
            if (gameplayActive && hasActiveShot && rallyManager.State == MatchState.InRally)
            {
                // Unity clamps deltaTime after a background pause. Catch the authority up to the
                // same network clock the receiving phone uses before publishing recovery state.
                ball.AdvanceToElapsed((float)System.Math.Max(0.0, transport.NetworkTime - activeShot.sentServerTime));
                if (hasActiveShot && ball.State != BallState.Idle && ball.State != BallState.Bounced &&
                    activeShot.hitterSideId != matchStart.localSideId)
                    player.PrepareForIncomingShot(ball.CurrentShot);
            }
            transport.SendSnapshot(new NetworkMatchSnapshot
            {
                matchId = matchStart.matchId,
                rallyId = rallyId,
                lastShotSequence = lastShotSequence,
                expectedHitterSideId = expectedHitterSideId,
                side0Score = side0Score,
                side1Score = side1Score,
                serverSideId = serverSideId,
                matchOver = rallyManager.State == MatchState.MatchOver,
                hasActiveShot = hasActiveShot,
                activeShot = activeShot,
                nextServeServerTime = nextServeServerTime
            });
        }

        private void HandleSnapshot(NetworkMatchSnapshot snapshot)
        {
            if (!gameplayActive || transport == null || transport.IsAuthority || snapshot.matchId != matchStart.matchId) return;
            if (rallyManager.State == MatchState.MatchOver && !snapshot.matchOver) return;
            if (snapshot.rallyId < rallyId || (snapshot.rallyId == rallyId && snapshot.lastShotSequence < lastShotSequence)) return;
            rallyId = snapshot.rallyId;
            lastShotSequence = snapshot.lastShotSequence;
            expectedHitterSideId = snapshot.expectedHitterSideId;
            side0Score = snapshot.side0Score;
            side1Score = snapshot.side1Score;
            serverSideId = snapshot.serverSideId;
            nextServeServerTime = snapshot.nextServeServerTime;
            hasActiveShot = snapshot.hasActiveShot;
            activeShot = snapshot.activeShot;

            int localScore = matchStart.localSideId == 0 ? side0Score : side1Score;
            int remoteScore = matchStart.localSideId == 0 ? side1Score : side0Score;
            float delay = (float)System.Math.Max(0.05, snapshot.nextServeServerTime - transport.NetworkTime);
            int lastHitterLocalSide = snapshot.hasActiveShot && snapshot.activeShot.hitterSideId == matchStart.localSideId ? 0 : 1;
            rallyManager.ApplyAuthoritativeSnapshot(localScore, remoteScore,
                snapshot.serverSideId == matchStart.localSideId, snapshot.matchOver, snapshot.hasActiveShot,
                lastHitterLocalSide, delay, snapshot.lastShotSequence, snapshot.hasActiveShot ? snapshot.activeShot.shot.shotType : ShotType.Flat);

            if (snapshot.hasActiveShot)
            {
                bool localWasHitter = snapshot.activeShot.hitterSideId == matchStart.localSideId;
                ShotData local = localWasHitter ? snapshot.activeShot.shot : MirrorToLocalFrame(snapshot.activeShot.shot);
                local.hitterId = localWasHitter ? 0 : 1;
                ball.LaunchShot(local);
                ball.AdvanceToElapsed((float)System.Math.Max(0.0, transport.NetworkTime - snapshot.activeShot.sentServerTime));
                if (localWasHitter) remote.PrepareForIncomingShot(local);
                else player.PrepareForIncomingShot(local);
            }
            FinishRecovery();
        }

        private void HandleForfeit()
        {
            if (!gameplayActive) { pendingPeerForfeit = prepared; return; }
            rallyManager.ResolveNetworkForfeitWin();
        }

        public void SendForfeit()
        {
            if (prepared && transport != null) transport.SendForfeit();
        }

        public void SetLocalInputSuspended(bool suspended)
        {
            menuInputSuspended = suspended;
            if (prepared) rallyManager?.SetNetworkInputSuspended(suspended || connectionSuspended || awaitingStartSnapshot);
        }

        private void HandleLocalConnectionLost()
        {
            connectionSuspended = true;
            Pickleball.UI.ScreenManager.Instance?.ShowConnectionRecovery(true);
            rallyManager?.SetNetworkInputSuspended(true);
            if (localRecoveryCoroutine == null) localRecoveryCoroutine = StartCoroutine(LocalRecoveryTimeout());
            Debug.LogWarning("[NetworkedMatchController] Connection interrupted; attempting rejoin.");
        }

        private void HandleLocalReconnected()
        {
            Debug.Log("[NetworkedMatchController] Rejoined room; requesting authoritative snapshot.");
            if (transport != null && transport.IsAuthority)
            {
                HandleSnapshotRequested();
                FinishRecovery();
            }
        }

        private void HandlePeerDisconnected()
        {
            if (graceWindowCoroutine != null) return;
            connectionSuspended = true;
            Pickleball.UI.ScreenManager.Instance?.ShowConnectionRecovery(false);
            rallyManager?.SetNetworkInputSuspended(true);
            graceWindowCoroutine = StartCoroutine(GraceWindowThenResolveDisconnect());
        }

        private void HandlePeerReconnected()
        {
            if (graceWindowCoroutine != null)
            {
                StopCoroutine(graceWindowCoroutine);
                graceWindowCoroutine = null;
            }
            if (transport != null && transport.IsAuthority)
            {
                HandleSnapshotRequested();
                FinishRecovery();
            }
        }

        private IEnumerator GraceWindowThenResolveDisconnect()
        {
            yield return new WaitForSecondsRealtime(GraceWindowSeconds);
            graceWindowCoroutine = null;
            Debug.LogWarning("[NetworkedMatchController] Reconnect grace expired; disconnected player forfeits.");
            rallyManager.ResolveNetworkForfeitWin();
        }

        private static ShotData MirrorToLocalFrame(ShotData shot)
        {
            shot.startPosition = new Vector3(-shot.startPosition.x, shot.startPosition.y, -shot.startPosition.z);
            shot.targetPosition = new Vector3(-shot.targetPosition.x, shot.targetPosition.y, -shot.targetPosition.z);
            return shot;
        }

        private void FinishRecovery()
        {
            awaitingStartSnapshot = false;
            connectionSuspended = false;
            if (localRecoveryCoroutine != null) StopCoroutine(localRecoveryCoroutine);
            localRecoveryCoroutine = null;
            Pickleball.UI.ScreenManager.Instance?.HideConnectionRecovery();
            rallyManager?.SetNetworkInputSuspended(menuInputSuspended);
        }

        private IEnumerator LocalRecoveryTimeout()
        {
            float deadline = Time.realtimeSinceStartup + GraceWindowSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForSecondsRealtime(1f);
#if PHOTON_UNITY_NETWORKING
                (transport as PhotonNetworkTransport)?.RetryReconnect();
#endif
            }
            localRecoveryCoroutine = null;
            Pickleball.UI.ScreenManager.Instance?.ForfeitMatch();
        }

        public void EndMatch() => TeardownCurrentMatch();

        private void TeardownCurrentMatch()
        {
            if (localRecoveryCoroutine != null) StopCoroutine(localRecoveryCoroutine);
            localRecoveryCoroutine = null;
            connectionSuspended = false;
            menuInputSuspended = false;
            awaitingStartSnapshot = false;
            pendingPeerForfeit = false;
            if (graceWindowCoroutine != null)
            {
                StopCoroutine(graceWindowCoroutine);
                graceWindowCoroutine = null;
            }
            bufferedCommits.Clear();
            if (player != null) player.OnShotHit -= HandleLocalShot;
            if (rallyManager != null)
            {
                rallyManager.OnAuthoritativePointResolved -= HandleAuthoritativePoint;
                if (prepared) rallyManager.EndPvPMode();
            }
            if (transport != null)
            {
                transport.OnShotIntentReceived -= HandleShotIntent;
                transport.OnShotCommittedReceived -= HandleShotCommit;
                transport.OnPointResolved -= HandlePointResolved;
                transport.OnPoseReceived -= HandlePose;
                transport.OnForfeitReceived -= HandleForfeit;
                transport.OnSnapshotRequested -= HandleSnapshotRequested;
                transport.OnSnapshotReceived -= HandleSnapshot;
                transport.OnPeerDisconnected -= HandlePeerDisconnected;
                transport.OnPeerReconnected -= HandlePeerReconnected;
                transport.OnLocalConnectionLost -= HandleLocalConnectionLost;
                transport.OnLocalReconnected -= HandleLocalReconnected;
                transport.Disconnect();
            }
            transport = null;
            remote = null;
            prepared = false;
            gameplayActive = false;
            if (opponentVisualOwner != null) opponentVisualOwner.enabled = true;
            opponentVisualOwner = null;
        }

        private void OnDestroy() => TeardownCurrentMatch();

        private void OnApplicationPause(bool paused)
        {
#if PHOTON_UNITY_NETWORKING
            if (prepared) (transport as PhotonNetworkTransport)?.SetApplicationPaused(paused);
#endif
        }
    }
}
