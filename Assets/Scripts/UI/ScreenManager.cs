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

namespace Pickleball.UI
{
    public enum ScreenId
    {
        Boot,
        StartupStanding,
        StartupLeagueResult,
        Lobby,
        TourSelect,
        Matchmaking,
        MatchIntro,
        ChestOpening,
        GearLoadout,
        GearCatalog,
        GearDetail,
        GearUpgradeReveal,
        League,
        SeasonPass,
        Shop,
        Settings,
        Tutorial,
        SeasonComplete,
        PlayMode, BagInventory, Account, Purchase, ProfileRecovery,
        Practice
    }

    /// <summary>
    /// Owns the meta canvas (every screen except the in-match HUD, which is GameplayHUD's own
    /// separate canvas) and the navigation between screens: back-stack, bottom-nav tab switching,
    /// and the match/pause transitions that hand control over to GameplayHUD + RallyManager.
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
        private string selectedProduct;
        private int selectedBag = -1;
        private GameObject recoveryGO;
        private readonly Stack<ScreenId> backStack = new Stack<ScreenId>();
        private GearItem selectedGear;
        private GearType selectedGearType;
        /// <summary>The mode of the match being set up or played. Tours are online-only and practice
        /// is the only way to play the AI -- see Sim.MatchModes.</summary>
        private Sim.MatchMode matchMode = Sim.MatchMode.Practice;
        private INetworkTransport pendingMatchTransport;
        private MatchStartMessage pendingMatchStart;
        private bool pendingMatchIsPvP;
        private string pendingOpponentName = "R. VEGA";
        private string pendingOpponentRank = "";

        /// <summary>Tour entry coins charged in RunPreMatchChecks but not yet "spent for real" -- the
        /// fee used to be taken the moment matchmaking began, and CANCEL / hardware-back on the
        /// matchmaking screen refunded nothing. Cleared to 0 once a match actually starts (EnterMatch);
        /// refunded in ExitMatchToLobby while still nonzero.</summary>
        private int pendingTourEntryFee;
        private Sim.MatchMode pendingPostTutorialMode;

        private const string PracticeLevelKey = "ps_practice_level";

        /// <summary>The AI level practice matches are played at. A per-device preference, so it
        /// lives in PlayerPrefs rather than the synced profile.</summary>
        public static Sim.PracticeLevel SelectedPracticeLevel
        {
            get
            {
                return (Sim.PracticeLevel)Mathf.Clamp(PlayerPrefs.GetInt(PracticeLevelKey, 0),
                    0, (int)Sim.PracticeLevel.Elite);
            }
            set
            {
                PlayerPrefs.SetInt(PracticeLevelKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        public Sim.MatchMode CurrentMatchMode => matchMode;

        private const int BaseSortOrder = 50;
        private const int PauseSortOrder = 150;

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
                a => a == "-autotourmatch" || a == "-autorankedmatch"))
            {
                // QA hook for exercising real two-process PvP without manual UI navigation on a
                // standalone build -- e.g. `Game.exe -autotourmatch`. Skips the entry fee. Never
                // fires otherwise.
                StartMatchmakingInternal(Sim.MatchMode.Tour);
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

        public void NavigateTab(string tabId)
        {
            backStack.Clear();
            switch (tabId)
            {
                case "HOME": Show(ScreenId.Lobby, false); break;
                case "PLAY": Show(ScreenId.PlayMode, false); break;
                case "GEAR": Show(ScreenId.GearLoadout, false); break;
                case "LEAGUE": Show(ScreenId.League, false); break;
                case "SHOP": Show(ScreenId.Shop, false); break;
            }
        }

        public void ShowGearDetail(GearItem item)
        {
            selectedGear = item;
            Show(ScreenId.GearDetail);
        }

        public void RefreshGearDetail(GearItem item)
        {
            selectedGear = item;
            Show(ScreenId.GearDetail, false);
        }

        public void ShowGearCatalog(GearType type)
        {
            selectedGearType = type;
            Show(ScreenId.GearCatalog);
        }

        public void ShowGearUpgradeReveal(GearItem item)
        {
            selectedGear = item;
            Show(ScreenId.GearUpgradeReveal, false);
        }

        public void ShowChestOpening() { selectedBag = -1; Show(ScreenId.ChestOpening); }

        public void SelectBag(int index)
        {
            MetaGameState.RefreshBagTimers();
            if (index < 0 || index >= MetaGameState.BagSlots.Count) return;
            var slot = MetaGameState.BagSlots[index];
            if (slot.state == BagSlotState.Ready) { selectedBag = index; Show(ScreenId.ChestOpening); }
            else if (slot.state == BagSlotState.Unlocking) ShowSkipBagConfirm(index);
            else if (slot.state == BagSlotState.Sealed) MetaGameState.StartBagUnlock(index);
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
            if (matchMode == Sim.MatchMode.Practice)
            {
                ShowDecisionModal("LEAVE PRACTICE?", "Practice matches don't count, so nothing is lost.",
                    "KEEP PLAYING", "LEAVE", ResumeFromPause, ForfeitMatch, IconId.Warn);
                return;
            }
            ShowDecisionModal("QUIT MATCH?", "Leaving counts as a loss, with an additional " +
                Sim.MatchModes.ForfeitSurcharge + "-trophy penalty.",
                "KEEP PLAYING", "FORFEIT", ResumeFromPause, ForfeitMatch, IconId.Warn);
        }

        public void RetryProfileLoad()
        {
            if (ProfileService.Instance != null) ProfileService.Instance.RetryLoad();
            Show(ScreenId.Boot, false);
        }

        public void MatchmakingFailed()
        {
            ShowDecisionModal("CAN'T FIND A MATCH", "Check your connection and try again, or practise against the AI. You won't be charged for a match that didn't start.",
                "RETRY", "PRACTICE", () => { ExitMatchToLobby(); StartTourMatch(); },
                PracticeInstead, IconId.Warn);
        }

        /// <summary>Leaves an online search (refunding the entry fee, see ExitMatchToLobby) for the
        /// practice screen. Offered while a tour search is taking a while, and when one fails.</summary>
        public void PracticeInstead()
        {
            ExitMatchToLobby();
            Show(ScreenId.Practice);
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
            ContinueAfterBoot();
        }

        private IEnumerator WaitForProfileThenContinue()
        {
            float started = Time.unscaledTime;
            while (ProfileService.Instance != null && !ProfileService.Instance.IsLoaded && Time.unscaledTime - started < 12f)
                yield return null;
            if (ProfileService.Instance != null && !ProfileService.Instance.IsLoaded)
                Show(ScreenId.ProfileRecovery, false);
            else ContinueAfterBoot();
        }

        private void ContinueAfterBoot()
        {
            if (MaybeShowSeasonComplete()) return;
            Show(MetaGameState.ShouldShowStartupResults ? ScreenId.StartupStanding : ScreenId.Lobby, false);
        }

        /// <summary>
        /// Shows the season-complete screen once, the first time the app opens in a new season.
        ///
        /// It reports the rollover; it does not perform one. Resetting the tier, banking the final
        /// standing and paying end-of-season rewards is economy work that does not exist yet, and
        /// wiring a screen to silently zero a player's progress would be worse than the screen not
        /// existing. Returns true if it took over the boot flow.
        /// </summary>
        private bool MaybeShowSeasonComplete()
        {
            int current = MetaGameState.CurrentSeasonNumber;
            int lastSeen = PlayerPrefs.GetInt(LastSeenSeasonKey, current);
            PlayerPrefs.SetInt(LastSeenSeasonKey, current);
            PlayerPrefs.Save();

            if (lastSeen >= current) return false;
            Show(ScreenId.SeasonComplete, false);
            return true;
        }

        private const string LastSeenSeasonKey = "ps_last_seen_season";

        public void ShowStartupLeagueResult()
        {
            Show(ScreenId.StartupLeagueResult, false);
        }

        public void CompleteStartupResults()
        {
            if (!MetaGameState.ShouldShowStartupResults) { NavigateTab("HOME"); return; }
            if (MetaGameState.AreBagSlotsFull())
            {
                ShowDecisionModal("MAKE ROOM FOR YOUR REWARD", "Open a bag to make room for your weekly league chest. Your claim will remain available.",
                    "YOUR BAGS", "LATER", () => Show(ScreenId.BagInventory), () => NavigateTab("HOME"), IconId.Chest);
                return;
            }
            MetaGameState.MarkStartupResultsSeen();
            // This flow only ever runs once per week (gated by ShouldShowStartupResults, which this
            // marks seen above) -- the natural once-a-week trigger for League Chest
            // (Docs/GearProgression.md#6-acquisition-b), which had no grant point at all before this.
            MetaGameState.AddLeagueChest();
            Show(ScreenId.Lobby, false);
        }

        /// <summary>A tour match: the current tour's entry fee, then a live opponent over Photon via
        /// PhotonQuickMatch, landing on EnterPvPMatch once one is found. Tours are online-only.</summary>
        public void StartTourMatch() => RunPreMatchChecks(Sim.MatchMode.Tour, false, false);

        /// <summary>A free practice match against the AI at <see cref="SelectedPracticeLevel"/>. No
        /// search screen: there is nobody to search for.</summary>
        public void StartPracticeMatch() => RunPreMatchChecks(Sim.MatchMode.Practice, false, false);

        private void RunPreMatchChecks(Sim.MatchMode mode, bool starterAccepted, bool bagAccepted)
        {
            // The equipment and bag warnings are about what a match can win, and practice wins nothing.
            bool stakes = mode == Sim.MatchMode.Tour;
            if (stakes && !starterAccepted && !bagAccepted && MetaGameState.HasStarterEquipment() &&
                MetaGameState.AreBagSlotsFull())
            {
                ShowDecisionModal("MATCH CHECK",
                    "Starter equipment is equipped and your bag slots are full. You can play, but a win cannot award another bag.",
                    "PLAY ANYWAY", "EQUIPMENT",
                    delegate { RunPreMatchChecks(mode, true, true); },
                    delegate { NavigateTab("GEAR"); }, IconId.Warn);
                return;
            }

            if (stakes && !starterAccepted && MetaGameState.HasStarterEquipment())
            {
                ShowDecisionModal("STARTER ITEM EQUIPPED",
                    "You are entering a match with starter equipment. Play anyway?",
                    "PLAY", "EQUIPMENT",
                    delegate { RunPreMatchChecks(mode, true, bagAccepted); },
                    delegate { NavigateTab("GEAR"); }, IconId.Tape);
                return;
            }

            if (stakes && !bagAccepted && MetaGameState.AreBagSlotsFull())
            {
                ShowDecisionModal("BAG SLOTS FULL",
                    "There is no room for a match bag. You can still play, but a win cannot award another bag.",
                    "PLAY ANYWAY", "MANAGE BAGS",
                    delegate { RunPreMatchChecks(mode, true, true); },
                    delegate
                    {
                        Show(ScreenId.BagInventory);
                    }, IconId.Chest);
                return;
            }

            // First-ever match: show the how-to-play card before anything is charged. Its CONTINUE
            // button re-enters this method with tutorial_seen set, so the flow picks up here.
            if (!MetaGameState.IsFeatureUnlocked("tutorial_seen"))
            {
                tutorialReplay = false;
                pendingPostTutorialMode = mode;
                Show(ScreenId.Tutorial);
                return;
            }

            if (mode == Sim.MatchMode.Tour)
            {
                int fee = Sim.MatchModes.EntryFee(mode, MetaGameState.CurrentTour.entryCoins);
                if (MetaGameState.Coins < fee)
                {
                    ShowDecisionModal("NOT ENOUGH COINS",
                        "You need more coins to enter this tour.",
                        "SHOP", "CANCEL",
                        delegate { Show(ScreenId.Shop); },
                        delegate { }, IconId.Coin);
                    return;
                }
                MetaGameState.SpendCoins(fee);
                // Held as refundable until a match actually starts -- see pendingTourEntryFee.
                pendingTourEntryFee = fee;
            }

            StartMatchmakingInternal(mode);
        }

        /// <summary>The how-to-play card's CONTINUE button. Marks the tutorial seen and resumes the
        /// pre-match flow it interrupted.</summary>
        public void ContinueFromTutorial()
        {
            if (tutorialReplay) { tutorialReplay = false; GoBack(); return; }
            MetaGameState.UnlockFeature("tutorial_seen");
            RunPreMatchChecks(pendingPostTutorialMode, true, true);
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

        /// <summary>Shown when a real-money "$x.xx" store button is tapped. There is no payment flow
        /// wired (Unity IAP), and every such button used to just grant its contents for free and
        /// repeat on every revisit. Fails closed until IAP exists.</summary>
        public void ShowStoreUnavailable(string productName)
        {
            selectedProduct = string.IsNullOrEmpty(productName) ? "Store purchase" : productName;
            Show(ScreenId.Purchase);
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

        private GameObject BuildScreen(ScreenId id)
        {
            switch (id)
            {
                case ScreenId.Boot: return BootScreen.Build(metaRoot, this);
                case ScreenId.StartupStanding: return StartupResultsScreen.BuildStanding(metaRoot, this);
                case ScreenId.StartupLeagueResult: return StartupResultsScreen.BuildLeagueResult(metaRoot, this);
                case ScreenId.Lobby: return LobbyScreen.Build(metaRoot, this);
                case ScreenId.TourSelect: return TourSelectScreen.Build(metaRoot, this);
                case ScreenId.Matchmaking: return MatchmakingScreen.Build(metaRoot, this);
                case ScreenId.MatchIntro:
                    return MatchIntroScreen.Build(metaRoot, this, pendingOpponentName, pendingOpponentRank,
                        !pendingMatchIsPvP);
                case ScreenId.Practice: return FlowScreens.Practice(metaRoot, this);
                case ScreenId.ChestOpening: return ChestOpeningScreen.Build(metaRoot, this, selectedBag);
                case ScreenId.PlayMode: return FlowScreens.Play(metaRoot, this);
                case ScreenId.BagInventory: return FlowScreens.Bags(metaRoot, this);
                case ScreenId.Account: return FlowScreens.Account(metaRoot, this);
                case ScreenId.Purchase: return FlowScreens.Purchase(metaRoot, this, selectedProduct ?? "Store purchase");
                case ScreenId.ProfileRecovery: return FlowScreens.ProfileRecovery(metaRoot, this);
                case ScreenId.GearLoadout: return GearLoadoutScreen.Build(metaRoot, this);
                case ScreenId.GearCatalog: return GearCatalogScreen.Build(metaRoot, this, selectedGearType);
                case ScreenId.GearDetail: return GearDetailScreen.Build(metaRoot, this, selectedGear);
                case ScreenId.GearUpgradeReveal: return GearUpgradeRevealScreen.Build(metaRoot, this, selectedGear);
                case ScreenId.League: return LeagueScreen.Build(metaRoot, this);
                case ScreenId.SeasonPass: return SeasonPassScreen.Build(metaRoot, this);
                case ScreenId.Shop: return ShopScreen.Build(metaRoot, this);
                case ScreenId.Settings: return SettingsScreen.Build(metaRoot, this);
                case ScreenId.Tutorial: return TutorialScreen.Build(metaRoot, this);
                case ScreenId.SeasonComplete:
                    return SeasonCompleteScreen.Build(metaRoot, this, MetaGameState.CurrentSeasonNumber - 1,
                        MetaGameState.SeasonTier, MetaGameState.LeagueTierName(MetaGameState.Trophies));
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
        /// <summary>The practice match card: the AI, labelled with the level it will play at.</summary>
        public void BeginMatchIntro()
        {
            pendingMatchIsPvP = false;
            pendingMatchTransport = null;
            pendingMatchStart = default(MatchStartMessage);
            pendingOpponentName = "R. VEGA";
            pendingOpponentRank = Sim.MatchModes.SkillLabel(Sim.MatchModes.PracticeSkill(SelectedPracticeLevel));
            Show(ScreenId.MatchIntro, false);
        }

        public void BeginPvPMatchIntro(INetworkTransport transport, MatchStartMessage matchStart)
        {
            pendingMatchIsPvP = true;
            pendingMatchTransport = transport;
            pendingMatchStart = matchStart;
            pendingOpponentName = string.IsNullOrEmpty(matchStart.opponentName) ? "OPPONENT" : matchStart.opponentName;
            // Their league, from the trophy count they reported -- the card used to show the AI's
            // tour difficulty for a human opponent.
            pendingOpponentRank = MetaGameState.LeagueTierName(matchStart.opponentTrophies);

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

        public void EnterMatch()
        {
            // A match against the AI is practice by definition, however it was reached.
            matchMode = Sim.MatchMode.Practice;
            matchActive = true;
            matchFinished = false;
            Time.timeScale = 1f;
            backStack.Clear();
            ClearCurrentScreen();
            metaCanvasGO.SetActive(false);

            // The match is really starting now -- the entry fee is spent for good, no longer refundable.
            pendingTourEntryFee = 0;

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

        /// <summary>Sets the AI to the practice level the player picked. The AI only plays practice
        /// matches; tours are online.</summary>
        private void ConfigureOpponentForMatch()
        {
            OpponentAI ai = FindObjectOfType<OpponentAI>();
            if (ai == null) return;
            ai.ConfigureSkill(Sim.MatchModes.PracticeSkill(SelectedPracticeLevel));
        }

        /// <summary>PvP equivalent of EnterMatch, called once MatchmakingScreen's PhotonQuickMatch
        /// finds a real opponent. NetworkedMatchController lives on the same "Managers" object as the
        /// rest of the match-critical singletons; created on first use since it's PvP-only and most
        /// sessions never touch it.</summary>
        public void EnterPvPMatch(INetworkTransport transport, MatchStartMessage matchStart)
        {
            matchMode = Sim.MatchMode.Tour;
            matchActive = true;
            matchFinished = false;
            // As in EnterMatch: the match has started, so the entry fee is no longer refundable.
            pendingTourEntryFee = 0;
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

            // Left the flow before a match started (CANCEL / back on the matchmaking screen) -- give
            // the tour entry fee back. EnterMatch zeroes this once a match is actually under way, so a
            // completed or forfeited match keeps the fee.
            if (pendingTourEntryFee > 0)
            {
                MetaGameState.AddCoins(pendingTourEntryFee);
                pendingTourEntryFee = 0;
            }

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
            // A tour forfeit is a loss, scored through the same table as any other loss (was a flat
            // -19, which was cheaper than actually losing on the scoreboard -- so bailing out from
            // behind was the correct play), plus a small surcharge so it is never the painless option.
            // Leaving practice costs nothing.
            if (matchMode == Sim.MatchMode.Tour)
            {
                RallyManager rally = RallyManager.Instance;
                MatchReward reward = MatchRewards.Forfeit(matchMode, MetaGameState.CurrentTour,
                    rally != null ? rally.playerScore : 0, rally != null ? rally.opponentScore : 0);
                MetaGameState.AddTrophies(reward.trophies);
                MetaGameState.AddSeasonXp(reward.seasonXp);
            }

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
            int playerScore = rally != null ? rally.playerScore : 0;
            int opponentScore = rally != null ? rally.opponentScore : 0;
            int perfects = rally != null ? rally.PlayerPerfectCount : 0;
            int longestRally = rally != null ? rally.LongestRally : 0;

            MatchReward reward = MatchRewards.Compute(matchMode, playerWon, MetaGameState.CurrentTour,
                playerScore, opponentScore, perfects, longestRally);
            MetaGameState.LastMatchReward = reward;
            // Practice pays nothing: no coins, trophies, XP, bag, career win or tour progress.
            if (matchMode != Sim.MatchMode.Tour) return;

            MetaGameState.AddCoins(reward.coins);
            MetaGameState.AddTrophies(reward.trophies);
            MetaGameState.AddSeasonXp(reward.seasonXp);

            if (playerWon)
            {
                MetaGameState.RecordCareerWin();
                MetaGameState.AddMatchBag();
                MetaGameState.RecordTourWin();
            }
        }

        /// <summary>Coins a PLAY AGAIN tap costs right now: the current tour's entry fee after a tour
        /// match, nothing after practice. Drives the button's label.</summary>
        public int PendingRematchFee =>
            Sim.MatchModes.EntryFee(matchMode, MetaGameState.CurrentTour.entryCoins);

        /// <summary>The match-result modal's PLAY AGAIN button. A tour rematch is a fresh online
        /// search, through the same pre-match checks and entry fee as the first one (it used to
        /// restart the local simulation for free). Practice restarts straight away at the same level.</summary>
        public void RequestRematch()
        {
            if (matchMode == Sim.MatchMode.Tour)
            {
                ExitMatchToLobby();
                StartTourMatch();
                return;
            }

            if (GameplayHUD.Instance != null) GameplayHUD.Instance.HideMatchResult();
            matchFinished = false;
            if (RallyManager.Instance != null) RallyManager.Instance.RestartMatch();
        }

        /// <summary>Lobby taps an unlocking bag: confirm spending gems to finish its timer now.</summary>
        public void ShowSkipBagConfirm(int index)
        {
            if (index < 0 || index >= MetaGameState.BagSlots.Count) return;
            BagSlot slot = MetaGameState.BagSlots[index];
            int cost = MetaGameState.BagSkipGemCost(slot);
            if (cost <= 0) return;

            if (MetaGameState.Gems < cost)
            {
                ShowDecisionModal("NOT ENOUGH GEMS",
                    "Finishing this bag now costs " + cost + " gems.",
                    "SHOP", "CANCEL",
                    delegate { Show(ScreenId.Shop); }, delegate { }, IconId.Gem);
                return;
            }

            ShowDecisionModal("FINISH NOW?",
                "Open this bag slot immediately for " + cost + " gems.",
                "SPEND " + cost, "WAIT",
                delegate
                {
                    MetaGameState.TrySkipBagUnlock(index);
                    Show(ScreenId.BagInventory, false);
                },
                delegate { }, IconId.Gem);
        }

        /// <summary>Called by LobbyScreen on build. Pops the once-a-day login reward as a decision
        /// modal; claiming it queues the reward banner the lobby shows on its next build.</summary>
        public void MaybeShowDailyReward()
        {
            if (modalGO != null || !MetaGameState.CanClaimDailyReward) return;

            ShowDecisionModal("DAILY REWARD",
                "Your login reward is ready. Come back tomorrow to keep the streak going.",
                "CLAIM", "LATER",
                delegate
                {
                    MetaGameState.ClaimDailyReward();
                    Show(ScreenId.Lobby, false);
                },
                delegate { }, IconId.Chest);
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
