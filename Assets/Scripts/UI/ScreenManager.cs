using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Pickleball.Backend;
using Pickleball.Data;
using Pickleball.Gameplay;
using Pickleball.Net;
using Pickleball.Systems;

namespace Pickleball.UI
{
    public enum ScreenId
    {
        Boot,
        Lobby,
        Matchmaking,
        MatchIntro,
        GearLoadout,
        League,
        Settings,
        Tutorial,
        Account,
        ProfileRecovery,
    }

    /// <summary>
    /// Owns the meta canvas (every screen except the in-match HUD, which is GameplayHUD's own
    /// separate canvas) and the navigation between screens: back-stack, bottom-nav tab switching,
    /// and the match/pause transitions that hand control over to GameplayHUD + RallyManager.
    ///
    /// The game has two ways to play, one per home-screen button: Play with AI and Multiplayer (see
    /// Sim.MatchModes). The bottom nav has four destinations: Home, Leagues, Gear and Settings.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager Instance { get; private set; }

        private GameObject metaCanvasGO;
        private Canvas metaCanvas;
        private Transform metaRoot;

        private GameObject currentScreenGO;
        private GameObject modalGO;
        private ScreenId currentScreen;
        private bool inPause;
        private bool matchActive;
        private bool matchFinished;
        private bool settingsFromPause;
        private bool accountFromPause;
        private bool tutorialReplay;
        private GameObject recoveryGO;
        private readonly Stack<ScreenId> backStack = new Stack<ScreenId>();

        /// <summary>The mode of the match being set up or played. An AI match is always
        /// Sim.MatchMode.AI and a networked one always Multiplayer.</summary>
        private Sim.MatchMode matchMode = Sim.MatchMode.AI;
        /// <summary>The id the current match is credited under (MetaGameState's reward ledger), so a
        /// match pays out once however its end is reported. The Photon room name for multiplayer, a
        /// fresh id per AI match.</summary>
        private string currentMatchId;
        private INetworkTransport pendingMatchTransport;
        private MatchStartMessage pendingMatchStart;
        private bool pendingMatchIsPvP;
        private string pendingOpponentName = "AI";
        private string pendingOpponentRank = "";
        private Sim.MatchMode pendingPostTutorialMode;

        public Sim.MatchMode CurrentMatchMode => matchMode;

        private const int BaseSortOrder = 50;
        private const int PauseSortOrder = 150;

        private const string AiOpponentName = "AI RIVAL";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureEventSystem();
            BuildRootCanvas();
        }

        /// <summary>Runtime-built UI has no Editor-inserted EventSystem, so without this, no
        /// Button/ScrollRect anywhere in the game ever receives a click or tap. The project is
        /// configured for the new Input System only (activeInputHandler in ProjectSettings), so
        /// this must be InputSystemUIInputModule, not the legacy StandaloneInputModule.</summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        [Header("Testing & Flow")]
        [SerializeField] private bool directMatchOnStart = false;

        private void Start()
        {
            if (RallyManager.Instance != null)
            {
                RallyManager.Instance.OnMatchEnded += HandleMatchEnded;
            }

            if (directMatchOnStart)
            {
                EnterMatch();
            }
            else if (System.Array.Exists(System.Environment.GetCommandLineArgs(),
                a => a == "-automultiplayermatch" || a == "-autorankedmatch"))
            {
                // QA hook for exercising real two-process PvP without manual UI navigation on a
                // standalone build -- e.g. `Game.exe -automultiplayermatch`. Never fires otherwise.
                StartMatchmakingInternal(Sim.MatchMode.Multiplayer);
            }
            else
            {
                Show(ScreenId.Boot, false);
            }
        }

        /// <summary>
        /// Hardware / Escape back. On Android the system back button is the primary navigation
        /// gesture and a game that ignores it drops the player straight out to the home screen.
        /// </summary>
        private void Update()
        {
            if (BackPressedThisFrame()) HandleBack();
        }

        public bool IsMatchActive => matchActive;

        public void HandleBack()
        {
            if (recoveryGO != null) return;
            if (modalGO != null) { DismissModal(); return; }
            if (currentScreen == ScreenId.ProfileRecovery) return;
            if (currentScreen == ScreenId.Account && accountFromPause) { CloseAccount(); return; }
            if (inPause)
            {
                if (settingsFromPause) { ShowPause(); return; }
                ResumeFromPause();
                return;
            }

            if (matchActive) { if (matchFinished) ExitMatchToLobby(); else ShowPause(); return; }
            // Nothing to go back to from the lobby, and backing out of a boot or matchmaking
            // sequence mid-flight would leave those coroutines to resolve into a dead screen.
            if (currentScreen == ScreenId.Lobby || currentScreen == ScreenId.Boot) return;
            if (currentScreen == ScreenId.Matchmaking || currentScreen == ScreenId.MatchIntro) { ExitMatchToLobby(); return; }

            GoBack();
        }

        private static bool BackPressedThisFrame()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;
            return false;
        }

        private void BuildRootCanvas()
        {
            metaCanvasGO = new GameObject("MetaCanvas");
            metaCanvasGO.transform.SetParent(transform, false);
            metaCanvas = metaCanvasGO.AddComponent<Canvas>();
            metaCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            metaCanvas.sortingOrder = BaseSortOrder;

            CanvasScaler scaler = metaCanvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;

            metaCanvasGO.AddComponent<GraphicRaycaster>();
            metaRoot = metaCanvasGO.transform;
        }

        // ============================================================
        // CORE NAVIGATION
        // ============================================================
        public void Show(ScreenId id, bool pushHistory = true)
        {
            if (pushHistory && currentScreenGO != null) backStack.Push(currentScreen);
            DismissModal();
            ClearCurrentScreen();

            inPause = false;
            metaCanvasGO.SetActive(true);
            metaCanvas.sortingOrder = BaseSortOrder;

            currentScreen = id;
            currentScreenGO = BuildScreen(id);

            // Navigation was a bare Destroy + rebuild, which lands as a single-frame pop and reads
            // as a glitch. A short fade-and-rise gives the change a direction without delaying input.
            if (currentScreenGO != null)
            {
                UIFadeIn.Attach(currentScreenGO, UITheme.ScreenEnterTime, 0f, 26f);
            }
        }

        public void GoBack()
        {
            if (backStack.Count > 0)
            {
                Show(backStack.Pop(), false);
            }
            else
            {
                Show(ScreenId.Lobby, false);
            }
        }

        /// <summary>The bottom nav: HOME, LEAGUE, GEAR, SETTINGS (see PSKit.Tabs).</summary>
        public void NavigateTab(string tabId)
        {
            backStack.Clear();
            switch (tabId)
            {
                case "HOME": Show(ScreenId.Lobby, false); break;
                case "LEAGUE": Show(ScreenId.League, false); break;
                case "GEAR": Show(ScreenId.GearLoadout, false); break;
                case "SETTINGS": Show(ScreenId.Settings, false); break;
            }
        }

        /// <summary>Rebuilds the current screen in place, for a change the screen displays (a
        /// purchase, an outfit pick, coins from an ad).</summary>
        public void RefreshCurrentScreen()
        {
            if (inPause || matchActive || currentScreenGO == null) return;
            ScreenId id = currentScreen;
            DismissModal();
            ClearCurrentScreen();
            currentScreenGO = BuildScreen(id);
        }

        public void ShowNotice(string title, string message)
        {
            ShowDecisionModal(title, message, "OK", "CLOSE", () => {}, () => {}, IconId.Warn);
        }

        public void ShowAccount()
        {
            accountFromPause = inPause;
            if (accountFromPause)
            {
                ClearCurrentScreen();
                currentScreen = ScreenId.Account;
                currentScreenGO = FlowScreens.Account(metaRoot, this);
            }
            else Show(ScreenId.Account);
        }

        public void CloseAccount()
        {
            if (accountFromPause) { accountFromPause = false; ShowSettingsFromPause(); }
            else GoBack();
        }

        public void ShowHelp() { tutorialReplay = true; Show(ScreenId.Tutorial); }

        public void ConfirmForfeit()
        {
            if (matchMode == Sim.MatchMode.AI)
            {
                ShowDecisionModal("LEAVE MATCH?", "Matches against the AI don't affect your league, so nothing is lost.",
                    "KEEP PLAYING", "LEAVE", ResumeFromPause, ForfeitMatch, IconId.Warn);
                return;
            }
            ShowDecisionModal("QUIT MATCH?", "Leaving counts as a loss: " + Sim.LeagueConfig.LossPoints +
                " league points and no coins.",
                "KEEP PLAYING", "FORFEIT", ResumeFromPause, ForfeitMatch, IconId.Warn);
        }

        public void RetryProfileLoad()
        {
            if (ProfileService.Instance != null) ProfileService.Instance.RetryLoad();
            Show(ScreenId.Boot, false);
        }

        public void MatchmakingFailed()
        {
            ShowDecisionModal("CAN'T FIND A MATCH", "Check your connection and try again, or play the AI instead. Matches against the AI don't affect your league.",
                "RETRY", "PLAY WITH AI", () => { ExitMatchToLobby(); StartMultiplayerMatch(); },
                PlayAiInstead, IconId.Warn);
        }

        /// <summary>Leaves a multiplayer search and starts a match against the AI instead. Offered
        /// while a search is taking a while and when one fails -- the player's choice, never automatic.</summary>
        public void PlayAiInstead()
        {
            ExitMatchToLobby();
            StartAiMatch();
        }

        public void ShowConnectionRecovery(bool local)
        {
            if (matchFinished) return;
            DismissModal();
            if (recoveryGO != null) Destroy(recoveryGO);
            recoveryGO = ConnectionRecoveryOverlay.Build(metaRoot, this, local);
            metaCanvasGO.SetActive(true);
            metaCanvas.sortingOrder = PauseSortOrder + 10;
        }

        public void HideConnectionRecovery()
        {
            if (recoveryGO != null) Destroy(recoveryGO);
            recoveryGO = null;
            metaCanvas.sortingOrder = inPause ? PauseSortOrder : BaseSortOrder;
            if (matchActive && !inPause) metaCanvasGO.SetActive(false);
        }

        public void CompleteBoot()
        {
            if (ProfileService.Instance != null && !ProfileService.Instance.IsLoaded)
            {
                StartCoroutine(WaitForProfileThenContinue());
                return;
            }
            Show(ScreenId.Lobby, false);
        }

        private IEnumerator WaitForProfileThenContinue()
        {
            float started = Time.unscaledTime;
            while (ProfileService.Instance != null && !ProfileService.Instance.IsLoaded && Time.unscaledTime - started < 12f)
                yield return null;
            if (ProfileService.Instance != null && !ProfileService.Instance.IsLoaded)
                Show(ScreenId.ProfileRecovery, false);
            else Show(ScreenId.Lobby, false);
        }

        // ============================================================
        // THE TWO MODES
        // ============================================================

        /// <summary>Home's PLAY WITH AI: a singles match against an AI matched to the player's own
        /// gear. Straight to the match card -- there is nobody to search for.</summary>
        public void StartAiMatch() => RunPreMatchChecks(Sim.MatchMode.AI);

        /// <summary>Home's MULTIPLAYER: a ranked singles match against a random opponent from the
        /// player's league, found by PhotonQuickMatch on the matchmaking screen.</summary>
        public void StartMultiplayerMatch() => RunPreMatchChecks(Sim.MatchMode.Multiplayer);

        private void RunPreMatchChecks(Sim.MatchMode mode)
        {
            // First-ever match: show the how-to-play card first. Its CONTINUE button re-enters the
            // flow with tutorial_seen set.
            if (!MetaGameState.IsFeatureUnlocked("tutorial_seen"))
            {
                tutorialReplay = false;
                pendingPostTutorialMode = mode;
                Show(ScreenId.Tutorial);
                return;
            }
            StartMatchmakingInternal(mode);
        }

        /// <summary>The how-to-play card's CONTINUE button. Marks the tutorial seen and resumes the
        /// pre-match flow it interrupted.</summary>
        public void ContinueFromTutorial()
        {
            if (tutorialReplay) { tutorialReplay = false; GoBack(); return; }
            MetaGameState.UnlockFeature("tutorial_seen");
            StartMatchmakingInternal(pendingPostTutorialMode);
        }

        private void ShowDecisionModal(string title, string body, string primaryLabel, string secondaryLabel,
            Action primary, Action secondary, IconId icon)
        {
            DismissModal();
            modalGO = DecisionModal.Build(metaRoot, title, body, primaryLabel, secondaryLabel,
                delegate { DismissModal(); primary?.Invoke(); },
                delegate { DismissModal(); secondary?.Invoke(); }, icon);
            UIFadeIn.Attach(modalGO, UITheme.ModalEnterTime, 0f, 0f, 0.92f);
        }

        public void DismissModal()
        {
            if (modalGO == null) return;
            Destroy(modalGO);
            modalGO = null;
        }

        private void StartMatchmakingInternal(Sim.MatchMode mode)
        {
            backStack.Clear();
            matchMode = mode;
            // The HUD canvas sorts above the meta canvas, so if it is still visible from a previous
            // match its score plates draw straight over the matchmaking screen.
            if (GameplayHUD.Instance != null)
            {
                GameplayHUD.Instance.HideMatchResult();
                GameplayHUD.Instance.SetVisible(false);
            }
            if (Sim.MatchModes.IsOnline(mode)) Show(ScreenId.Matchmaking, false);
            else BeginMatchIntro();
        }

        // ============================================================
        // REWARDED ADS
        // ============================================================

        /// <summary>The optional watch-an-ad offer, from the home screen beside the coin balance.</summary>
        public void ShowAdOffer()
        {
            if (!RewardedAds.IsOfferAvailable)
            {
                ShowNotice("NO ADS RIGHT NOW", MetaGameState.AdRewardsRemainingToday <= 0
                    ? "You've collected today's ad rewards. Come back tomorrow."
                    : "There's no ad ready yet. Try again in a moment.");
                return;
            }
            int left = MetaGameState.AdRewardsRemainingToday;
            ShowDecisionModal("FREE COINS",
                "Watch a short ad to earn " + Sim.EconomyConfig.AdRewardCoins + " coins. " +
                left + (left == 1 ? " ad" : " ads") + " left today. Optional -- skipping costs nothing.",
                "WATCH AD", "NOT NOW", WatchAdForCoins, null, IconId.Coin);
        }

        private void WatchAdForCoins()
        {
            RewardedAds.WatchForCoins(delegate (bool credited)
            {
                RefreshCurrentScreen();
                if (credited)
                    ShowNotice("+" + Sim.EconomyConfig.AdRewardCoins + " COINS",
                        "Thanks for watching. Your balance is now " + MetaGameState.Coins.ToString("N0") + ".");
                else
                    ShowNotice("NO COINS THIS TIME", "The ad didn't finish, so nothing was added.");
            });
        }

        private GameObject BuildScreen(ScreenId id)
        {
            switch (id)
            {
                case ScreenId.Boot: return BootScreen.Build(metaRoot, this);
                case ScreenId.Lobby: return LobbyScreen.Build(metaRoot, this);
                case ScreenId.Matchmaking: return MatchmakingScreen.Build(metaRoot, this);
                case ScreenId.MatchIntro:
                    return MatchIntroScreen.Build(metaRoot, this, pendingOpponentName, pendingOpponentRank,
                        !pendingMatchIsPvP);
                case ScreenId.Account: return FlowScreens.Account(metaRoot, this);
                case ScreenId.ProfileRecovery: return FlowScreens.ProfileRecovery(metaRoot, this);
                case ScreenId.GearLoadout: return GearLoadoutScreen.Build(metaRoot, this);
                case ScreenId.League: return LeagueScreen.Build(metaRoot, this);
                case ScreenId.Settings: return SettingsScreen.Build(metaRoot, this);
                case ScreenId.Tutorial: return TutorialScreen.Build(metaRoot, this);
                default: return null;
            }
        }

        private void ClearCurrentScreen()
        {
            if (currentScreenGO != null)
            {
                Destroy(currentScreenGO);
                currentScreenGO = null;
            }
        }

        // ============================================================
        // MATCH / PAUSE HAND-OFF
        // ============================================================
        /// <summary>The AI match card.</summary>
        public void BeginMatchIntro()
        {
            pendingMatchIsPvP = false;
            pendingMatchTransport = null;
            pendingMatchStart = default(MatchStartMessage);
            pendingOpponentName = AiOpponentName;
            pendingOpponentRank = "MATCHED TO YOUR GEAR";
            Show(ScreenId.MatchIntro, false);
        }

        public void BeginPvPMatchIntro(INetworkTransport transport, MatchStartMessage matchStart)
        {
            pendingMatchIsPvP = true;
            pendingMatchTransport = transport;
            pendingMatchStart = matchStart;
            pendingOpponentName = string.IsNullOrEmpty(matchStart.opponentName) ? "OPPONENT" : matchStart.opponentName;
            pendingOpponentRank = MetaGameState.LeagueName(matchStart.opponentLeaguePoints) + " LEAGUE";

            GameObject managers = GameObject.Find("Managers");
            if (managers == null)
            {
                Debug.LogWarning("[ScreenManager] BeginPvPMatchIntro: no 'Managers' object in scene.");
                transport?.Disconnect();
                pendingMatchTransport = null;
                Show(ScreenId.Lobby, false);
                return;
            }
            NetworkedMatchController nmc = managers.GetComponent<NetworkedMatchController>();
            if (nmc == null) nmc = managers.AddComponent<NetworkedMatchController>();
            nmc.PrepareMatch(transport, matchStart);
            Show(ScreenId.MatchIntro, false);
        }

        public float GetPreparedMatchStartDelay()
        {
            if (!pendingMatchIsPvP) return 4f;
            GameObject managers = GameObject.Find("Managers");
            NetworkedMatchController nmc = managers != null ? managers.GetComponent<NetworkedMatchController>() : null;
            return nmc != null ? Mathf.Max(0f, (float)(nmc.ScheduledStartServerTime - nmc.NetworkTime)) : 4f;
        }

        public bool HasPreparedPvPMatch => pendingMatchIsPvP;

        public void EnterPreparedMatch()
        {
            if (GameplayHUD.Instance != null) GameplayHUD.Instance.SetOpponentName(pendingOpponentName);
            if (pendingMatchIsPvP)
            {
                INetworkTransport transport = pendingMatchTransport;
                MatchStartMessage start = pendingMatchStart;
                pendingMatchTransport = null;
                EnterPvPMatch(transport, start);
            }
            else EnterMatch();
        }

        /// <summary>Starts a match against the AI.</summary>
        public void EnterMatch()
        {
            matchMode = Sim.MatchMode.AI;
            currentMatchId = "ai-" + Guid.NewGuid().ToString("N");
            matchActive = true;
            matchFinished = false;
            Time.timeScale = 1f;
            backStack.Clear();
            ClearCurrentScreen();
            metaCanvasGO.SetActive(false);

            if (GameplayHUD.Instance != null)
            {
                GameplayHUD.Instance.HideMatchResult();
                GameplayHUD.Instance.SetVisible(true);
            }

            ConfigureOpponentForMatch();

            if (RallyManager.Instance != null)
            {
                RallyManager.Instance.RestartMatch();
            }
        }

        /// <summary>Matches the AI to the player's current gear -- same stats, same base movement.
        /// Done at every AI match start, so an upgrade bought since the last match counts.</summary>
        private void ConfigureOpponentForMatch()
        {
            OpponentAI ai = FindObjectOfType<OpponentAI>();
            if (ai == null) return;
            PlayerController player = FindObjectOfType<PlayerController>();
            ai.ConfigureForPlayer(MetaGameState.GetLoadoutStats(),
                player != null ? player.BaseMoveSpeed : 6f, player != null ? player.BaseReach : 1.6f);
        }

        /// <summary>PvP equivalent of EnterMatch, called once MatchmakingScreen's PhotonQuickMatch
        /// finds a real opponent. NetworkedMatchController lives on the same "Managers" object as the
        /// rest of the match-critical singletons; created on first use since it's PvP-only and most
        /// sessions never touch it.</summary>
        public void EnterPvPMatch(INetworkTransport transport, MatchStartMessage matchStart)
        {
            matchMode = Sim.MatchMode.Multiplayer;
            currentMatchId = "mp-" + (string.IsNullOrEmpty(matchStart.matchId) ? Guid.NewGuid().ToString("N") : matchStart.matchId);
            matchActive = true;
            matchFinished = false;
            Time.timeScale = 1f;
            backStack.Clear();
            ClearCurrentScreen();
            metaCanvasGO.SetActive(false);

            if (GameplayHUD.Instance != null)
            {
                GameplayHUD.Instance.HideMatchResult();
                GameplayHUD.Instance.SetVisible(true);
            }

            GameObject managers = GameObject.Find("Managers");
            if (managers == null)
            {
                Debug.LogWarning("[ScreenManager] EnterPvPMatch: no 'Managers' object in scene, cannot host NetworkedMatchController.");
                return;
            }
            NetworkedMatchController nmc = managers.GetComponent<NetworkedMatchController>();
            if (nmc == null) nmc = managers.AddComponent<NetworkedMatchController>();
            if (nmc.HasActiveMatch) nmc.StartPreparedMatch();
            else nmc.StartMatch(transport, matchStart);
        }

        public void ExitMatchToLobby()
        {
            matchActive = false;
            accountFromPause = false;
            settingsFromPause = false;
            HideConnectionRecovery();
            Time.timeScale = 1f;
            inPause = false;

            if (pendingMatchTransport != null)
            {
                pendingMatchTransport.Disconnect();
                pendingMatchTransport = null;
            }
            pendingMatchIsPvP = false;
            if (GameplayHUD.Instance != null)
            {
                GameplayHUD.Instance.HideMatchResult();
                GameplayHUD.Instance.SetVisible(false);
            }

            // Covers both a natural match end and a forfeit (ForfeitMatch calls straight through to
            // here without going via RallyManager.OnMatchEnded) -- either way, an active PvP match's
            // Photon room membership and PUN callback registration need to be torn down, not just
            // left dangling until the app closes.
            GameObject managers = GameObject.Find("Managers");
            NetworkedMatchController nmc = managers != null ? managers.GetComponent<NetworkedMatchController>() : null;
            nmc?.EndMatch();
            RallyManager.Instance?.StopMatch();

            Show(ScreenId.Lobby, false);
        }

        /// <summary>Called by GameplayHUD's pause button. Overlays Pause on top of the frozen match.</summary>
        public void ShowPause()
        {
            if (matchFinished || recoveryGO != null) return;
            GameObject managers = GameObject.Find("Managers");
            NetworkedMatchController nmc = managers != null ? managers.GetComponent<NetworkedMatchController>() : null;
            // End any in-flight hit-stop first -- otherwise its realtime timer fires a beat later and
            // writes timeScale back to 1, un-pausing the game underneath the pause menu.
            if (Pickleball.Systems.GameFeedbackManager.Instance != null)
                Pickleball.Systems.GameFeedbackManager.Instance.CancelHitStop();
            // A local freeze cannot pause an online opponent. Keep simulation running in PvP while
            // the overlay blocks local input; single-player retains its real pause.
            Time.timeScale = nmc != null && nmc.HasActiveMatch ? 1f : 0f;
            if (nmc != null && nmc.HasActiveMatch) nmc.SetLocalInputSuspended(true);
            settingsFromPause = false;
            inPause = true;
            ClearCurrentScreen();
            metaCanvasGO.SetActive(true);
            metaCanvas.sortingOrder = PauseSortOrder;
            currentScreenGO = PauseScreen.Build(metaRoot, this);
            UIFadeIn.Attach(currentScreenGO, UITheme.ModalEnterTime, 0f, 0f);
        }

        public void ResumeFromPause()
        {
            accountFromPause = false;
            settingsFromPause = false;
            DismissModal();
            Time.timeScale = 1f;
            GameObject managers = GameObject.Find("Managers");
            NetworkedMatchController nmc = managers != null ? managers.GetComponent<NetworkedMatchController>() : null;
            nmc?.SetLocalInputSuspended(false);
            inPause = false;
            ClearCurrentScreen();
            metaCanvasGO.SetActive(false);
            metaCanvas.sortingOrder = BaseSortOrder;
        }

        /// <summary>Settings opened from the Pause overlay stays layered above the frozen match
        /// (same elevated sort order as Pause) and returns to Pause instead of the lobby.</summary>
        public void ShowSettingsFromPause()
        {
            settingsFromPause = true;
            currentScreen = ScreenId.Settings;
            inPause = true;
            ClearCurrentScreen();
            metaCanvasGO.SetActive(true);
            metaCanvas.sortingOrder = PauseSortOrder;
            currentScreenGO = SettingsScreen.Build(metaRoot, this, ShowPause);
        }

        public void ForfeitMatch()
        {
            if (matchFinished) { ExitMatchToLobby(); return; }
            // A multiplayer forfeit is a loss for league points and pays no coins; leaving an AI
            // match costs nothing. Settled through the ledger like a finished match, so the same
            // match can never be settled twice.
            RallyManager rally = RallyManager.Instance;
            MetaGameState.ForfeitMatch(currentMatchId, matchMode,
                rally != null ? rally.playerScore : 0, rally != null ? rally.opponentScore : 0);

            GameObject managers = GameObject.Find("Managers");
            NetworkedMatchController nmc = managers != null ? managers.GetComponent<NetworkedMatchController>() : null;
            nmc?.SendForfeit();
            inPause = false;
            metaCanvas.sortingOrder = BaseSortOrder;
            ExitMatchToLobby();
        }

        private void HandleMatchEnded(bool playerWon)
        {
            if (!matchActive || matchFinished) return;
            matchFinished = true;
            HideConnectionRecovery();
            if (inPause) ResumeFromPause();
            RallyManager rally = RallyManager.Instance;
            // Runs before GameplayHUD.ShowMatchResult (RallyManager raises OnMatchEnded first), so the
            // result card reads the settled numbers.
            MetaGameState.CompleteMatch(currentMatchId, matchMode, playerWon,
                rally != null ? rally.playerScore : 0, rally != null ? rally.opponentScore : 0);
        }

        /// <summary>The result card's PLAY AGAIN. Multiplayer searches for a new opponent; an AI
        /// match restarts straight away as a new match (credited under a new id).</summary>
        public void RequestRematch()
        {
            if (matchMode == Sim.MatchMode.Multiplayer)
            {
                ExitMatchToLobby();
                StartMultiplayerMatch();
                return;
            }

            if (GameplayHUD.Instance != null) GameplayHUD.Instance.HideMatchResult();
            matchFinished = false;
            currentMatchId = "ai-" + Guid.NewGuid().ToString("N");
            ConfigureOpponentForMatch();
            if (RallyManager.Instance != null) RallyManager.Instance.RestartMatch();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (RallyManager.Instance != null)
            {
                RallyManager.Instance.OnMatchEnded -= HandleMatchEnded;
            }
        }
    }
}
