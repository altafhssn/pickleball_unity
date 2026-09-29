using System;
using System.Collections;
using UnityEngine;
using Pickleball.Systems;
using Pickleball.UI;
using Pickleball.VFX;
using Pickleball.Utils;
using Sim = Pickleball.Sim;

namespace Pickleball.Gameplay
{
    public enum MatchState
    {
        Serving,
        InRally,
        PointEnded,
        MatchOver
    }

    public class RallyManager : MonoBehaviour
    {
        public const float PointBreakSeconds = 1.5f;
        public static RallyManager Instance { get; private set; }

        [Header("Match Settings")]
        [SerializeField] private int pointsToWin = MatchConfig.PointsToWin;

        [Header("References")]
        [SerializeField] private PlayerController player;
        [SerializeField] private OpponentAI opponent;
        [SerializeField] private BallController ball;

        /// <summary>Built from player/opponent in EnsureReferences(). Interfaces can't be
        /// SerializeField'd (and the scene-setup tooling wires the concrete fields by name), so this
        /// is a derived view the match loop reads instead of touching player/opponent directly --
        /// what makes a third or fourth participant (doubles) an addition here rather than a rewrite.</summary>
        private IMatchParticipant[] sides;

        /// <summary>True once ConfigurePvP has run. EnsureReferences() must not clobber the PvP
        /// `sides` array (player + RemoteParticipant) back to [player, opponent] on its next call --
        /// and StartNewServe() calls EnsureReferences() every point.</summary>
        private bool isPvPMode = false;
        private bool isNetworkAuthority = true;
        private bool networkInputSuspended;

        /// <summary>Set by RequestFarSideTakeover when a disconnect takeover arrives mid-rally --
        /// swapping `sides[1]` out from under an in-flight ball would leave stale reach/timing state
        /// on whatever replaces it, so the swap is deferred to the next safe point boundary instead.</summary>
        private IMatchParticipant pendingFarSideTakeover;

        [Header("Scores")]
        public int playerScore = 0;
        public int opponentScore = 0;

        private bool isPlayerServing = true;
        private bool currentServeFromRight = true;
        private int lastHitterId = -1; // 0 = player, 1 = opponent
        private ShotType lastShotType = ShotType.Flat;
        private int currentRallyCount = 0;

        /// <summary>False from the moment a shot is launched until the ball's first bounce. Drives the
        /// two-bounce rule (shots 2-3 must be let bounce -- enforced in the controllers) and the
        /// kitchen no-volley fault below.</summary>
        private bool ballBouncedSinceLastHit = false;
        private readonly bool[] volleyMomentum = new bool[2];
        private readonly Vector3[] volleyPreviousPosition = new Vector3[2];
        private readonly float[] volleySettledTime = new float[2];

        private void LateUpdate()
        {
            if (State != MatchState.InRally || Time.timeScale <= 0f || (isPvPMode && !isNetworkAuthority) || sides == null) return;
            for (int side = 0; side < 2; side++)
            {
                if (!volleyMomentum[side] || sides[side] == null) continue;
                Vector3 position = sides[side].Position;
                if (Sim.RallyRules.IsInKitchen(position.x, position.z, side))
                {
                    if (GameplayHUD.Instance != null)
                        GameplayHUD.Instance.ShowShotFeedback("KITCHEN MOMENTUM FAULT", UITheme.QualityColor(ShotQuality.Miss));
                    AwardPoint(side == 1);
                    return;
                }
                float speed = Vector3.Distance(position, volleyPreviousPosition[side]) / Mathf.Max(0.001f, Time.deltaTime);
                volleySettledTime[side] = speed < 0.15f ? volleySettledTime[side] + Time.deltaTime : 0f;
                if (volleySettledTime[side] >= 0.12f) volleyMomentum[side] = false;
                volleyPreviousPosition[side] = position;
            }
        }

        // Per-match stats, read by ScreenManager to size the end-of-match reward.
        private int playerPerfectCount = 0;
        private int longestRally = 0;
        public int PlayerPerfectCount => playerPerfectCount;
        public int LongestRally => longestRally;

        public int PointsToWin => pointsToWin;
        public bool IsPvP => isPvPMode;

        public MatchState State { get; private set; } = MatchState.Serving;

        /// <summary>Shots resolved so far this rally (the serve counts as 1). Read by PlayerController
        /// to place its next shot on the rally-fatigue curve ShotSim.RallyFatigueMultiplier applies.
        /// Reset to 0 in StartNewServe.</summary>
        public int CurrentRallyCount => currentRallyCount;

        public event Action<int, int> OnScoreUpdated;
        public event Action<string> OnPointEnded;
        public event Action<bool> OnMatchEnded;
        /// <summary>Authority-only point result for the network protocol.</summary>
        public event Action<bool, int, int, bool> OnAuthoritativePointResolved;

        public bool IsPlayerServing => isPlayerServing;
        public bool CurrentServeFromRight => currentServeFromRight;

        private CourtLineRenderer courtLines;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            EnsureReferences();
        }

        private void Start()
        {
            EnsureReferences();

            if (player != null) player.OnShotHit += HandleShotFromSide;
            if (opponent != null) opponent.OnShotHit += HandleShotFromSide;
            if (ball != null)
            {
                ball.OnBallBounced += HandleBallBounced;
                ball.OnNetFault += HandleNetFault;
                ball.OnFlightCompleted += HandleBallDead;
            }

            if (ScreenManager.Instance == null)
            {
                Invoke(nameof(StartNewServe), 1.0f);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (player != null) player.OnShotHit -= HandleShotFromSide;
            if (opponent != null) opponent.OnShotHit -= HandleShotFromSide;
            if (ball != null)
            {
                ball.OnBallBounced -= HandleBallBounced;
                ball.OnNetFault -= HandleNetFault;
                ball.OnFlightCompleted -= HandleBallDead;
            }
        }

        public void EnsureReferences()
        {
            if (player == null) player = FindObjectOfType<PlayerController>();
            if (opponent == null) opponent = FindObjectOfType<OpponentAI>();
            if (ball == null) ball = FindObjectOfType<BallController>();
            if (courtLines == null) courtLines = FindObjectOfType<CourtLineRenderer>();

            if (!isPvPMode && player != null && opponent != null)
            {
                sides = new IMatchParticipant[] { player, opponent };
            }
        }

        /// <summary>Switches the match loop from local AI to a networked human opponent. Called by
        /// NetworkedMatchController once a MatchStart message has been received. player stays the
        /// concrete SerializeField'd reference (Inspector wiring still needs it); only the far side of
        /// `sides` changes, so HandleShotFromSide/AwardPoint/etc. need no PvP-specific branches at all.
        /// Start()/OnDestroy() only wire HandleShotFromSide to the concrete player/opponent fields, so
        /// this has to explicitly subscribe the new far side itself -- otherwise a RemoteParticipant's
        /// shots would fire OnShotHit into nothing.</summary>
        public void ConfigurePvP(IMatchParticipant remoteSide, bool localServesFirst, bool localIsAuthority, int networkPointsToWin)
        {
            if (opponent != null) opponent.OnShotHit -= HandleShotFromSide;
            if (isPvPMode && sides != null && sides.Length > 1 && sides[1] != null)
                sides[1].OnShotHit -= HandleShotFromSide;

            isPvPMode = true;
            isNetworkAuthority = localIsAuthority;
            networkInputSuspended = false;
            pointsToWin = Mathf.Max(1, networkPointsToWin);
            sides = new IMatchParticipant[] { player, remoteSide };
            sides[1].OnShotHit += HandleShotFromSide;
            isPlayerServing = localServesFirst;
        }

        /// <summary>PvP equivalent of RestartMatch -- resets scores/HUD and hands serve order to
        /// whichever side the match-start handshake decided, instead of RestartMatch's hardcoded
        /// isPlayerServing = true.</summary>
        public void StartPvPMatch(IMatchParticipant remoteSide, bool localServesFirst, bool localIsAuthority, int networkPointsToWin)
        {
            CancelInvoke(nameof(StartNewServe));
            playerScore = 0;
            opponentScore = 0;
            playerPerfectCount = 0;
            longestRally = 0;
            State = MatchState.Serving;

            if (GameplayHUD.Instance != null)
            {
                GameplayHUD.Instance.UpdateScore(0, 0);
                GameplayHUD.Instance.HideMatchResult();
            }

            ConfigurePvP(remoteSide, localServesFirst, localIsAuthority, networkPointsToWin);
            StartNewServe();
        }

        public void EndPvPMode()
        {
            CancelInvoke(nameof(ResolveUnreturnedNetworkBall));
            CancelInvoke(nameof(StartNewServe));
            if (isPvPMode && sides != null && sides.Length > 1 && sides[1] != null)
                sides[1].OnShotHit -= HandleShotFromSide;
            isPvPMode = false;
            isNetworkAuthority = true;
            networkInputSuspended = false;
            pendingFarSideTakeover = null;
            if (player != null && opponent != null)
            {
                sides = new IMatchParticipant[] { player, opponent };
                opponent.OnShotHit -= HandleShotFromSide;
                opponent.OnShotHit += HandleShotFromSide;
            }
        }

        /// <summary>Called by NetworkedMatchController when the peer disconnects and its grace window
        /// expires. Applied immediately if it's safe to do so right now (nothing mid-flight to leave
        /// in a stale state); otherwise deferred until StartNewServe's next call, at the boundary
        /// between points.</summary>
        public void RequestFarSideTakeover(IMatchParticipant newFarSide)
        {
            if (State == MatchState.InRally)
            {
                pendingFarSideTakeover = newFarSide;
            }
            else
            {
                ApplyFarSideTakeover(newFarSide);
            }
        }

        private void ApplyFarSideTakeover(IMatchParticipant newFarSide)
        {
            if (sides != null && sides.Length == 2 && sides[1] != null)
            {
                sides[1].OnShotHit -= HandleShotFromSide;
            }
            sides = new IMatchParticipant[] { player, newFarSide };
            sides[1].OnShotHit += HandleShotFromSide;
        }

        public void StartNewServe()
        {
            CancelInvoke(nameof(ResolveUnreturnedNetworkBall));
            EnsureReferences();

            if (pendingFarSideTakeover != null)
            {
                ApplyFarSideTakeover(pendingFarSideTakeover);
                pendingFarSideTakeover = null;
            }

            // opponent is only required outside PvP mode -- a PvP match's far side is `sides[1]`
            // (a RemoteParticipant), not the concrete OpponentAI field.
            if (player == null || ball == null || (!isPvPMode && opponent == null))
            {
                Debug.LogWarning("[RallyManager] Missing references!");
                return;
            }

            if (State == MatchState.MatchOver) return;

            State = MatchState.Serving;
            currentRallyCount = 0;
            lastHitterId = -1;
            lastShotType = ShotType.Flat;
            ballBouncedSinceLastHit = false;

            int serverSideId = isPlayerServing ? 0 : 1;
            Array.Clear(volleyMomentum, 0, volleyMomentum.Length);
            int serverScore = isPlayerServing ? playerScore : opponentScore;
            currentServeFromRight = Sim.ServeRules.ServesFromRight(serverScore);

            foreach (IMatchParticipant side in sides) side.ResetPosition();

            if (GameplayHUD.Instance != null)
            {
                GameplayHUD.Instance.UpdateServeState(isPlayerServing, currentServeFromRight);
                GameplayHUD.Instance.UpdateRallyCount(0);
            }

            if (courtLines != null)
                courtLines.ShowServiceCourt(serverSideId, currentServeFromRight,
                    isPlayerServing ? UITheme.GoldMid : UITheme.TextRivalRed);

            // Both sides now serve through the same IMatchParticipant entry point -- what used to be
            // a hand-rolled AI-serve branch here (building the ShotData and calling
            // ball.LaunchShot/player.PrepareForIncomingBall directly, bypassing HandleShotFromSide
            // entirely) is now OpponentAI.EnterServeMode(bool) firing OnShotHit like any other shot. This
            // is what makes a doubles server rotation an addition later rather than special-cased here.
            // Routing through `sides` rather than the concrete player/opponent fields is what makes
            // this work unchanged in PvP mode too, where sides[1] is a RemoteParticipant instead.
            sides[serverSideId].EnterServeMode(currentServeFromRight);
        }

        /// <summary>
        /// Replaces the old HandlePlayerShot/HandleOpponentShot pair -- both did the same five things
        /// (advance state, notify the other side, update HUD, trigger juice, play a sound) and only
        /// differed in which side was hardcoded on each end. Routing by shot.hitterId through `sides`
        /// is exactly what a doubles rotation needs later: this loop does not care how many
        /// participants are actually playing, only who hit last and who should receive next.
        ///
        /// One asymmetry is real and stays gated explicitly: shot-quality UI and the Perfect-shot
        /// sound reflect the human player's own swing. Showing them for the AI's shots would surface
        /// its hidden inaccuracy roll as if it were the player's skill.
        /// </summary>
        private void HandleShotFromSide(ShotData shot)
        {
            if (!CanSideHit(shot.hitterId))
            {
                Debug.LogWarning("[RallyManager] Rejected illegal shot from side " + shot.hitterId +
                    " while state=" + State + " lastHitter=" + lastHitterId);
                return;
            }
            CancelInvoke(nameof(ResolveUnreturnedNetworkBall));

            if ((State == MatchState.Serving) != (shot.shotType == ShotType.Serve) && (!isPvPMode || isNetworkAuthority))
            {
                AwardPoint(shot.hitterId == 1);
                return;
            }

            if (State == MatchState.InRally && Sim.RallyRules.MustBounce(currentRallyCount) &&
                !ballBouncedSinceLastHit && (!isPvPMode || isNetworkAuthority))
            {
                if (GameplayHUD.Instance != null)
                    GameplayHUD.Instance.ShowShotFeedback("TWO-BOUNCE FAULT", UITheme.QualityColor(ShotQuality.Miss));
                AwardPoint(shot.hitterId == 1);
                return;
            }

            // Kitchen (non-volley zone) fault: taking the ball out of the air while standing inside
            // the kitchen is illegal -- the tactical heart of pickleball. The serve is exempt (it is
            // struck from the baseline anyway) and shots 2-3 can't be volleys (two-bounce rule). The
            // authority resolves this in PvP, same as the out-of-bounds check.
            if (shot.shotType != ShotType.Serve && currentRallyCount >= 3 && !ballBouncedSinceLastHit
                && (!isPvPMode || isNetworkAuthority))
            {
                Vector3 hitterPos = (sides != null && shot.hitterId >= 0 && shot.hitterId < sides.Length)
                    ? sides[shot.hitterId].Position : shot.startPosition;
                bool inKitchen = Sim.RallyRules.IsInKitchen(hitterPos.x, hitterPos.z, shot.hitterId);
                if (inKitchen)
                {
                    if (GameplayHUD.Instance != null && shot.hitterId == 0)
                        GameplayHUD.Instance.ShowShotFeedback("KITCHEN FAULT", UITheme.QualityColor(ShotQuality.Miss));
                    AwardPoint(shot.hitterId == 1);
                    return;
                }
            }

            if (State == MatchState.InRally && !ballBouncedSinceLastHit && sides != null)
            {
                volleyMomentum[shot.hitterId] = true;
                volleyPreviousPosition[shot.hitterId] = sides[shot.hitterId].Position;
                volleySettledTime[shot.hitterId] = 0f;
            }
            State = MatchState.InRally;
            lastHitterId = shot.hitterId;
            lastShotType = shot.shotType;
            currentRallyCount++;
            ballBouncedSinceLastHit = false;
            if (currentRallyCount > longestRally) longestRally = currentRallyCount;
            if (shot.hitterId == 0 && shot.quality == ShotQuality.Perfect) playerPerfectCount++;

            IMatchParticipant receiver = (sides != null && shot.hitterId >= 0 && shot.hitterId < sides.Length)
                ? sides[1 - shot.hitterId] : null;
            if (receiver != null) receiver.PrepareForIncomingShot(shot);

            bool isPlayerShot = shot.hitterId == 0;

            if (GameplayHUD.Instance != null)
            {
                GameplayHUD.Instance.UpdateMatchState("RALLY");
                GameplayHUD.Instance.UpdateRallyCount(currentRallyCount);
                // Weak / Miss are covered by the failure-feedback callout ("TOO LATE", "NET", ...);
                // showing a neutral "MISS" quality on top of that is just noise.
                if (isPlayerShot && shot.quality <= ShotQuality.Good)
                    GameplayHUD.Instance.ShowShotQuality(shot.quality, shot.compositeScore, shot.shotType);
            }

            if (GameFeedbackManager.Instance != null)
                GameFeedbackManager.Instance.OnBallHit(shot.startPosition, shot);

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayRacketHit(shot.shotType, shot.compositeScore);
                if (isPlayerShot && shot.quality == ShotQuality.Perfect)
                    SoundManager.Instance.PlayPerfectShot();
            }
        }

        public bool CanSideHit(int sideId)
        {
            if (sideId < 0 || sideId > 1 || State == MatchState.PointEnded || State == MatchState.MatchOver)
                return false;
            if (networkInputSuspended && sideId == 0) return false;
            if (State == MatchState.Serving) return sideId == (isPlayerServing ? 0 : 1);
            return State == MatchState.InRally && sideId != lastHitterId;
        }

        /// <summary>Whether a touch starting now may later become this side's shot. Unlike CanSideHit
        /// this stays true through the opponent's swing and the gap between points, so a thumb resting
        /// on the glass is already being tracked when the ball arrives; CanSideHit still decides at
        /// release whether the finished swipe is a legal hit.</summary>
        public bool AcceptsGestureFrom(int sideId)
        {
            if (sideId < 0 || sideId > 1 || State == MatchState.MatchOver) return false;
            return !(networkInputSuspended && sideId == 0);
        }

        private void HandleBallBounced(Vector3 bouncePos)
        {
            if (State != MatchState.InRally) return;

            if (ball.BounceCount == 1) ballBouncedSinceLastHit = true;
            if (ball.BounceCount == 1 && courtLines != null) courtLines.HideServiceCourt();

            // Sound & VFX for bounce
            if (SoundManager.Instance != null) SoundManager.Instance.PlayCourtBounce();
            if (GameFeedbackManager.Instance != null) GameFeedbackManager.Instance.OnBallBounce(bouncePos);

            // Serve fault: the first bounce must land beyond the kitchen in the diagonally opposite
            // service box. This validates local, AI, and received network shots identically.
            if (ball.BounceCount == 1 && lastShotType == ShotType.Serve &&
                !Sim.ServeRules.IsInCorrectServiceCourt(
                    new System.Numerics.Vector3(bouncePos.x, bouncePos.y, bouncePos.z),
                    lastHitterId, currentServeFromRight) && (!isPvPMode || isNetworkAuthority))
            {
                if (GameplayHUD.Instance != null && lastHitterId == 0)
                    GameplayHUD.Instance.ShowShotFeedback("SERVE FAULT", UITheme.QualityColor(ShotQuality.Miss));
                AwardPoint(lastHitterId == 1);
                return;
            }

            // Out-of-bounds fault on the FIRST bounce: nothing previously checked whether a shot
            // actually landed inside the lines, so a wild shot from either side just kept the rally
            // going. The hitter of an out shot loses the point immediately, same as a net fault.
            if (ball.BounceCount == 1 && ((lastHitterId == 0 ? bouncePos.z <= 0f : bouncePos.z >= 0f) ||
                (ShotSystem.Instance != null && !ShotSystem.Instance.IsInBounds(bouncePos))))
            {
                if (isPvPMode && !isNetworkAuthority) return;
                if (GameplayHUD.Instance != null && lastHitterId == 0)
                    GameplayHUD.Instance.ShowShotFeedback("OUT", UITheme.QualityColor(ShotQuality.Miss));
                bool playerWinsOob = (lastHitterId == 1);
                AwardPoint(playerWinsOob);
                return;
            }

            // 1-bounce allowed rule:
            // BounceCount == 1 means first bounce (receiver should return)
            // BounceCount >= 2 means second bounce (point over)
            if (ball.BounceCount >= 2)
            {
                if (isPvPMode && !isNetworkAuthority) return;
                // Second bounce = point over. Last hitter wins.
                bool playerWins = (lastHitterId == 0);
                AwardPoint(playerWins);
            }
            // First bounce: ball is still in play, receiver should hit
        }

        private void HandleNetFault()
        {
            if (State != MatchState.InRally) return;
            if (isPvPMode && !isNetworkAuthority) return;

            if (courtLines != null) courtLines.HideServiceCourt();

            // Net fault: last hitter loses the point
            if (GameplayHUD.Instance != null && lastHitterId == 0)
                GameplayHUD.Instance.ShowShotFeedback("NET", UITheme.QualityColor(ShotQuality.Miss));
            bool playerWins = (lastHitterId == 1);
            AwardPoint(playerWins);
        }

        private void HandleBallDead()
        {
            // Ball has completed its full arc + bounce and stopped
            // If we're still in rally and nobody returned it, point is over
            if (State != MatchState.InRally) return;
            if (isPvPMode && !isNetworkAuthority) return;
            // The peer may hit just before its second bounce; allow that reliable intent time to
            // reach the authority before ending the point on the authority's clock.
            if (isPvPMode && lastHitterId == 0)
            {
                Invoke(nameof(ResolveUnreturnedNetworkBall), 0.35f);
                return;
            }

            // The ball died without being returned after bounce
            // Last hitter wins (their shot was unreturned)
            bool playerWins = (lastHitterId == 0);
            AwardPoint(playerWins);
        }

        private void ResolveUnreturnedNetworkBall()
        {
            if (isPvPMode && isNetworkAuthority && State == MatchState.InRally && lastHitterId == 0)
                AwardPoint(true);
        }

        private void AwardPoint(bool playerWins)
        {
            if (State == MatchState.PointEnded || State == MatchState.MatchOver) return;

            if (courtLines != null) courtLines.HideServiceCourt();

            bool scored = Sim.RallyRules.ResolveRally(playerWins, ref isPlayerServing,
                ref playerScore, ref opponentScore);

            // Check match end -- via Sim.RallyRules so a re-simulating server checks the exact same
            // win condition against a submitted match's final score.
            bool matchOver = Sim.RallyRules.IsMatchOver(playerScore, opponentScore, pointsToWin, out _);
            if (!matchOver)
                isPlayerServing = playerWins;
            PresentResolvedPoint(playerWins, matchOver, PointBreakSeconds, scored);

            if (isPvPMode && isNetworkAuthority)
                OnAuthoritativePointResolved?.Invoke(playerWins, playerScore, opponentScore, matchOver);
        }

        public void ApplyAuthoritativePoint(bool playerWins, int authoritativePlayerScore,
            int authoritativeOpponentScore, bool matchOver, bool nextPlayerServes, float nextServeDelay)
        {
            if (!isPvPMode || isNetworkAuthority) return;
            CancelInvoke(nameof(StartNewServe));
            bool scored = authoritativePlayerScore > playerScore || authoritativeOpponentScore > opponentScore;
            playerScore = authoritativePlayerScore;
            opponentScore = authoritativeOpponentScore;
            isPlayerServing = nextPlayerServes;
            PresentResolvedPoint(playerWins, matchOver, Mathf.Max(0.05f, nextServeDelay), scored);
        }

        public void ApplyAuthoritativeSnapshot(int authoritativePlayerScore, int authoritativeOpponentScore,
            bool playerServes, bool matchOver, bool hasActiveShot, int lastHitterLocalSide, float nextServeDelay,
            int shotSequence = 0, ShotType shotType = ShotType.Flat)
        {
            if (!isPvPMode || isNetworkAuthority) return;
            bool wasMatchOver = State == MatchState.MatchOver;
            CancelInvoke(nameof(StartNewServe));
            ball.StopBall();
            playerScore = authoritativePlayerScore;
            opponentScore = authoritativeOpponentScore;
            isPlayerServing = playerServes;
            lastHitterId = hasActiveShot ? lastHitterLocalSide : -1;
            currentRallyCount = hasActiveShot ? shotSequence : 0;
            lastShotType = shotType;
            ballBouncedSinceLastHit = false;
            State = matchOver ? MatchState.MatchOver : (hasActiveShot ? MatchState.InRally : MatchState.PointEnded);
            if (GameplayHUD.Instance != null)
            {
                GameplayHUD.Instance.UpdateScore(playerScore, opponentScore);
                GameplayHUD.Instance.UpdateServerHighlight(isPlayerServing);
                GameplayHUD.Instance.UpdateRallyCount(currentRallyCount);
            }
            if (matchOver && !wasMatchOver)
            {
                bool playerWins = playerScore > opponentScore;
                OnMatchEnded?.Invoke(playerWins);
                if (GameplayHUD.Instance != null) GameplayHUD.Instance.ShowMatchResult(playerWins);
            }
            if (!matchOver && !hasActiveShot) Invoke(nameof(StartNewServe), Mathf.Max(0.05f, nextServeDelay));
        }

        public void SetNetworkInputSuspended(bool suspended) => networkInputSuspended = suspended;

        public void ResolveNetworkForfeitWin()
        {
            if (!isPvPMode || State == MatchState.MatchOver) return;
            CancelInvoke(nameof(StartNewServe));
            ball.StopBall();
            State = MatchState.MatchOver;
            OnMatchEnded?.Invoke(true);
            if (GameplayHUD.Instance != null) GameplayHUD.Instance.ShowMatchResult(true);
        }

        private void PresentResolvedPoint(bool playerWins, bool matchOver, float nextServeDelay, bool scored)
        {
            State = MatchState.PointEnded;
            ball.StopBall();
            if (courtLines != null) courtLines.HideServiceCourt();
            string result = scored ? (playerWins ? "Player Scored!" : "Opponent Scored!") : "SIDE OUT";
            OnPointEnded?.Invoke(result);
            OnScoreUpdated?.Invoke(playerScore, opponentScore);

            if (GameplayHUD.Instance != null)
            {
                GameplayHUD.Instance.UpdateScore(playerScore, opponentScore);
                GameplayHUD.Instance.UpdateServerHighlight(isPlayerServing);
                GameplayHUD.Instance.UpdateMatchState(scored ? "POINT!" : "SIDE OUT");
            }
            if (GameFeedbackManager.Instance != null)
                GameFeedbackManager.Instance.OnPointScored(result);
            if (SoundManager.Instance != null) SoundManager.Instance.PlayPointScored();

            if (matchOver)
            {
                State = MatchState.MatchOver;
                OnMatchEnded?.Invoke(playerWins);
                if (GameplayHUD.Instance != null) GameplayHUD.Instance.ShowMatchResult(playerWins);
            }
            else
            {
                if (GameplayHUD.Instance != null)
                    GameplayHUD.Instance.ShowPointResult(playerWins, playerScore, opponentScore, scored);
                Invoke(nameof(StartNewServe), nextServeDelay);
            }
        }

        public void StopMatch()
        {
            CancelInvoke(nameof(ResolveUnreturnedNetworkBall));
            CancelInvoke(nameof(StartNewServe));
            if (ball != null) ball.StopBall();
            networkInputSuspended = true;
            State = MatchState.MatchOver;
        }

        public void RestartMatch()
        {
            networkInputSuspended = false;
            CancelInvoke(nameof(StartNewServe));
            playerScore = 0;
            opponentScore = 0;
            isPlayerServing = true;
            playerPerfectCount = 0;
            longestRally = 0;
            State = MatchState.Serving;

            if (GameplayHUD.Instance != null)
            {
                GameplayHUD.Instance.UpdateScore(0, 0);
                GameplayHUD.Instance.HideMatchResult();
            }

            StartNewServe();
        }
    }
}
