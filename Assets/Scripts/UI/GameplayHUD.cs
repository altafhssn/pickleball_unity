using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Pickleball.Gameplay;
using Pickleball.Data;

namespace Pickleball.UI
{
    /// <summary>
    /// The in-match HUD: score plates, serve/rally badge, pause button, shot-quality callout and the
    /// match-result modal. Owns its own canvas at sortingOrder 100, above the meta canvas.
    ///
    /// Rebuilt on the shared UIBuilder toolkit. The previous version carried its own private copies
    /// of the rect/text/sprite helpers, which had drifted from the kit — different corner radii,
    /// different outline handling — and every icon in it was an emoji codepoint that rendered as
    /// blank space on the plates, the badge and the result title.
    /// </summary>
    public class GameplayHUD : MonoBehaviour
    {
        private void OnDestroy()
        {
            if (coachPlayer != null) coachPlayer.OnShotHit -= HandleCoachedShot;
            if (Instance == this) Instance = null;
        }
        public static GameplayHUD Instance { get; private set; }

        // The single source is RallyManager (which PvP overrides with the negotiated length). Falls
        // back to the shared constant when the HUD builds before RallyManager's Awake has run --
        // both are the same value, so the pip rows are never wrong.
        private int TargetPoints => RallyManager.Instance != null ? RallyManager.Instance.PointsToWin : MatchConfig.PointsToWin;

        private Image playerServeKeyline;
        private Image opponentServeKeyline;
        private Text playerScoreText;
        private Text opponentScoreText;
        private Text opponentNameText;
        private Text playerNameText;
        private Text statusBadgeText;
        private GameObject statusBadgeIcon;
        private Image statusBadgeFill;

        private Text shotQualityText;
        private GameObject shotQualityHost;

        private Text shotFeedbackText;
        private GameObject shotFeedbackHost;
        private Coroutine shotFeedbackCoroutine;

        private Image[] pipsPlayerHUD;
        private Image[] pipsOpponentHUD;

        private GameObject winLoseOverlay;
        private Text playAgainLabel;
        private Text winLoseTitleText;
        private GameObject winLoseTitleIcon;
        private Text winLoseScoreText;
        private Text rewardCoinText;
        private Text rewardTrophyText;
        private Transform modalPlayerPipsRoot;
        private Transform modalOpponentPipsRoot;

        private Coroutine shotQualityCoroutine;
        private Coroutine pointOverlayCoroutine;
        private GameObject pointOverlay;
        private Text pointOverlayTitle;
        private Text pointOverlayScore;
        private GameObject canvasRoot;
        private Transform safeRoot;
        private Text powerText;
        private Image powerFill;
        private GameObject powerHost;
        private GameObject coachHost;
        private Text coachText;
        private PlayerController coachPlayer;
        private int coachStep;
        private int coachedPlayerShots;
        private bool coachEnabled;
        private Coroutine coachFinishCoroutine;

        private const string GameplayCoachFeature = "gameplay_coach_complete";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            CreateHUD();
        }

        private void CreateHUD()
        {
            GameObject canvasGO = new GameObject("GameplayCanvas");
            canvasGO.transform.SetParent(transform, false);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            // Match width, same as the meta canvas. At 0.5 the HUD scaled on a different curve from
            // every other screen, so the plates changed size relative to the buttons underneath them
            // as the aspect ratio changed.
            scaler.matchWidthOrHeight = 0f;

            canvasGO.AddComponent<GraphicRaycaster>();

            canvasRoot = canvasGO;
            safeRoot = UIBuilder.SafeArea(canvasGO.transform);

            BuildScorePlates(safeRoot);
            BuildServeBadge(safeRoot);
            BuildPauseButton(safeRoot);
            BuildShotQualityCallout(safeRoot);
            BuildShotFeedbackCallout(safeRoot);
            BuildPowerMeter(safeRoot);
            BuildGameplayCoach(safeRoot);
            BuildPointOverlay(canvasGO.transform);
            BuildResultModal(canvasGO.transform);

            // Hidden until ScreenManager hands control over via SetVisible(true) on match start —
            // otherwise this canvas (sortingOrder 100) would draw over every meta screen on boot.
            canvasRoot.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            if (canvasRoot != null && canvasRoot.activeSelf != visible)
            {
                // Match time is measured from the moment the HUD is handed control, which is the
                // same moment the countdown ends and play actually starts.
                if (visible)
                {
                    matchStartedAt = Time.unscaledTime;
                    BeginGameplayCoach();
                }
                else if (coachHost != null)
                {
                    coachHost.SetActive(false);
                }
                canvasRoot.SetActive(visible);
            }
        }

        private float matchStartedAt;

        private void BuildPowerMeter(Transform parent)
        {
            powerHost = UIBuilder.Child("ShotPower", parent);
            UIBuilder.Rect(powerHost, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -300), new Vector2(360, 78));
            var background = powerHost.AddComponent<Image>();
            background.color = new Color(0.02f, 0.08f, 0.14f, 0.85f);
            background.raycastTarget = false;
            powerText = UIBuilder.StrokedText(powerHost.transform, "Label", TextAnchor.MiddleCenter,
                "POWER 0%", Color.white, 28);
            UIBuilder.Rect(powerText.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(340, 38));
            powerText.raycastTarget = false;
            var track = UIBuilder.Child("Track", powerHost.transform);
            UIBuilder.Rect(track, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(320, 14));
            track.AddComponent<Image>().color = new Color(1, 1, 1, 0.15f);
            track.GetComponent<Image>().raycastTarget = false;
            var fill = UIBuilder.Child("Fill", track.transform);
            UIBuilder.StretchRect(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            powerFill = fill.AddComponent<Image>();
            powerFill.raycastTarget = false;
            powerHost.SetActive(false);
        }

        private void Update()
        {
            TryConnectGameplayCoach();
            RefreshGameplayCoach();

            var input = InputManager.Instance;
            var rally = RallyManager.Instance;
            bool visible = Time.timeScale > 0f && input != null && input.IsSwiping &&
                rally != null && rally.CanSideHit(0);
            if (powerHost == null) return;
            powerHost.SetActive(visible);
            if (!visible) return;
            float power = Mathf.Clamp01(input.CurrentSwipeVector.magnitude);
            powerText.text = ShotLabel(input.CurrentPreviewShot) + "  ·  " +
                Mathf.RoundToInt(power * 100f) + "% POWER";
            powerFill.rectTransform.anchorMax = new Vector2(power, 1f);
            powerFill.color = PreviewShotColor(input.CurrentPreviewShot);
        }

        private static string ShotLabel(ShotType shot)
        {
            switch (shot)
            {
                case ShotType.Topspin: return "TOPSPIN";
                case ShotType.Slice: return "SLICE";
                case ShotType.Lob: return "LOB";
                case ShotType.Dink: return "DINK";
                case ShotType.Smash: return "SMASH";
                case ShotType.Serve: return "SERVE";
                default: return "FLAT";
            }
        }

        private static Color PreviewShotColor(ShotType shot)
        {
            switch (shot)
            {
                case ShotType.Dink: return new Color(0.42f, 0.95f, 0.55f);
                case ShotType.Lob: return new Color(0.72f, 0.60f, 1f);
                case ShotType.Smash: return UITheme.Blaze;
                case ShotType.Topspin: return UITheme.Volt;
                case ShotType.Slice: return new Color(0.35f, 0.82f, 1f);
                case ShotType.Serve: return UITheme.GoldTop;
                default: return Color.white;
            }
        }

        private void BuildGameplayCoach(Transform parent)
        {
            coachHost = UIBuilder.Child("GameplayCoach", parent);
            UIBuilder.Rect(coachHost, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0, UITheme.F(108f)),
                new Vector2(UITheme.F(310f), UITheme.F(50f)));

            Image panel = coachHost.AddComponent<Image>();
            panel.sprite = UIBuilder.RoundedSprite(24);
            panel.type = Image.Type.Sliced;
            panel.color = new Color(0.02f, 0.08f, 0.15f, 0.92f);
            panel.raycastTarget = false;

            coachText = UIBuilder.StrokedText(coachHost.transform, "CoachText", TextAnchor.MiddleCenter,
                "", UITheme.Cream, 31);
            UIBuilder.StretchRect(coachText.gameObject, Vector2.zero, Vector2.one,
                new Vector2(UITheme.F(10f), UITheme.F(5f)), new Vector2(-UITheme.F(10f), -UITheme.F(5f)));
            coachText.raycastTarget = false;
            UIBuilder.ClampLine(coachText, 21);
            coachHost.SetActive(false);
        }

        private void BeginGameplayCoach()
        {
            coachEnabled = !MetaGameState.IsFeatureUnlocked(GameplayCoachFeature);
            if (!coachEnabled || coachHost == null) return;
            coachHost.SetActive(true);
            RefreshGameplayCoach(true);
            TryConnectGameplayCoach();
        }

        private void TryConnectGameplayCoach()
        {
            if (!coachEnabled || coachPlayer != null) return;
            coachPlayer = FindObjectOfType<PlayerController>();
            if (coachPlayer != null) coachPlayer.OnShotHit += HandleCoachedShot;
        }

        private void HandleCoachedShot(ShotData shot)
        {
            if (!coachEnabled || shot.hitterId != 0) return;
            coachedPlayerShots++;

            if (coachStep == 0)
            {
                coachStep = 1;
            }
            else if (coachStep == 1 && shot.shotType != ShotType.Serve)
            {
                coachStep = 2;
            }
            else if (coachStep == 2 && (shot.shotType == ShotType.Dink || coachedPlayerShots >= 4))
            {
                CompleteGameplayCoach();
                return;
            }
            RefreshGameplayCoach(true);
        }

        private void RefreshGameplayCoach(bool force = false)
        {
            if (!coachEnabled || coachHost == null || coachText == null || !coachHost.activeSelf) return;

            string prompt;
            if (coachStep == 0)
            {
                bool canServe = RallyManager.Instance != null && RallyManager.Instance.State == MatchState.Serving &&
                    RallyManager.Instance.IsPlayerServing;
                prompt = canServe ? "TAP OR SWIPE UP TO SERVE" :
                    "WATCH THE BOUNCE · SWIPE WHEN IT REACHES YOU";
            }
            else if (coachStep == 1)
            {
                prompt = "ANGLE YOUR SWIPE LEFT OR RIGHT TO AIM";
            }
            else
            {
                prompt = "TRY A SHORT, GENTLE DOWN SWIPE FOR A DINK";
            }

            if (force || coachText.text != prompt) coachText.text = prompt;
        }

        private void CompleteGameplayCoach()
        {
            coachEnabled = false;
            MetaGameState.UnlockFeature(GameplayCoachFeature);
            if (coachFinishCoroutine != null) StopCoroutine(coachFinishCoroutine);
            coachFinishCoroutine = StartCoroutine(FinishGameplayCoach());
        }

        private IEnumerator FinishGameplayCoach()
        {
            if (coachHost == null || coachText == null) yield break;
            coachText.text = "NICE · YOU CONTROL THE SHOT, YOUR PLAYER MOVES";
            yield return new WaitForSecondsRealtime(1.35f);
            coachHost.SetActive(false);
            coachFinishCoroutine = null;
        }

        /// <summary>Seconds of wall-clock match time, for the pause card. Unscaled, so it does not
        /// stop counting the way a Time.time reading would while the game is frozen -- the number is
        /// "how long you have been in this match", not "how long the simulation ran".</summary>
        public float ElapsedSeconds
        {
            get { return matchStartedAt > 0f ? Time.unscaledTime - matchStartedAt : 0f; }
        }

        /// <summary>The opponent's display name as the HUD is showing it.</summary>
        public string OpponentName
        {
            get { return opponentNameText != null ? opponentNameText.text : "OPPONENT"; }
        }

        // ============================================================
        // PAUSE BUTTON
        // ============================================================
        /// <summary>
        /// Centred between the two score plates, as the board draws it. It used to sit under the
        /// player plate on the right edge, in the same corner the thumb rests on during a rally --
        /// the one place a control that stops the match should never be.
        /// </summary>
        private void BuildPauseButton(Transform parent)
        {
            float size = UITheme.F(32f);

            GameObject btn = UIBuilder.Child("PauseButton", parent);
            UIBuilder.Rect(btn, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -(UITheme.HeaderTopOffset + PlateHeight * 0.5f)), new Vector2(size, size));
            UIBuilder.ExpandHitArea(btn, new Vector2(size, size));

            Image outline = btn.AddComponent<Image>();
            outline.sprite = UIBuilder.CircleSprite;
            outline.color = UITheme.Outline;

            GameObject face = UIBuilder.Child("Face", btn.transform);
            UIBuilder.StretchRect(face, Vector2.zero, Vector2.one, new Vector2(7, 7), new Vector2(-7, -7));
            Image faceImg = face.AddComponent<Image>();
            faceImg.sprite = UIBuilder.CircleSprite;
            faceImg.color = UITheme.PlateFill;

            // Two bars rather than the icon set's pause glyph: at 36 board px the glyph's outline
            // pass closes the gap between the bars and it reads as one solid block.
            for (int i = 0; i < 2; i++)
            {
                GameObject bar = UIBuilder.Child("Bar" + i, face.transform);
                UIBuilder.Rect(bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(i == 0 ? -UITheme.F(4.5f) : UITheme.F(4.5f), 0),
                    new Vector2(UITheme.F(5f), UITheme.F(16f)));
                Image barImg = bar.AddComponent<Image>();
                barImg.color = UITheme.Cream;
                barImg.raycastTarget = false;
            }

            Button button = btn.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = outline;
            button.onClick.AddListener(delegate
            {
                if (ScreenManager.Instance != null) ScreenManager.Instance.ShowPause();
            });
        }

        // ============================================================
        // SCORE PLATES
        // ============================================================
        // Board metrics (Docs/Figma/INGAME-1.svg), in Figma px on the 390-wide board.
        private static readonly float PlateWidth = UITheme.F(116f);
        private static readonly float PlateHeight = UITheme.F(40f);
        private static readonly float PipSize = UITheme.F(7.5f);
        private static readonly float PipGap = UITheme.F(2f);

        /// <summary>
        /// Two translucent plates hugging the top corners. The volt keyline follows the server;
        /// names and fixed positions identify each player.
        ///
        /// The plates are semi-transparent (<see cref="UITheme.PlateFill"/>) because they sit over
        /// live play; the previous opaque blue and red slabs read as two more objects on the court.
        /// </summary>
        private void BuildScorePlates(Transform parent)
        {
            CreatePlate(parent, "OpponentPlate", false, new Vector2(0f, 1f), UITheme.F(23.5f),
                "OPPONENT", out opponentScoreText, out pipsOpponentHUD, out opponentNameText);

            CreatePlate(parent, "PlayerPlate", true, new Vector2(1f, 1f), -UITheme.F(21f),
                "You", out playerScoreText, out pipsPlayerHUD, out playerNameText);
            UpdateServerHighlight(RallyManager.Instance == null || RallyManager.Instance.IsPlayerServing);
        }

        private void CreatePlate(Transform parent, string goName, bool isPlayer, Vector2 anchor, float xOffset,
            string displayName, out Text scoreText, out Image[] pips, out Text displayNameText)
        {
            GameObject plate = UIBuilder.Child(goName, parent);
            UIBuilder.Rect(plate, anchor, anchor, anchor, new Vector2(xOffset, -UITheme.HeaderTopOffset),
                new Vector2(PlateWidth, PlateHeight));

            Image outline = plate.AddComponent<Image>();
            outline.sprite = UIBuilder.RoundedSprite(46);
            outline.type = Image.Type.Sliced;
            outline.color = UITheme.Outline;

            GameObject face = UIBuilder.Child("Face", plate.transform);
            UIBuilder.StretchRect(face, Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
            Image faceImg = face.AddComponent<Image>();
            faceImg.sprite = UIBuilder.RoundedSprite(42);
            faceImg.type = Image.Type.Sliced;
            faceImg.color = UITheme.PlateFill;

            // The serving player's keyline is drawn over the face: the
            // face is only 74% opaque, so anything painted underneath tints the whole plate olive
            // instead of reading as a border.
            {
                GameObject keyline = UIBuilder.Child("Keyline", plate.transform);
                UIBuilder.StretchRect(keyline, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
                Image keyImg = keyline.AddComponent<Image>();
                keyImg.sprite = UIBuilder.RoundedRingSprite(43, 8);
                keyImg.type = Image.Type.Sliced;
                keyImg.color = UITheme.VoltBright;
                keyImg.raycastTarget = false;
                if (isPlayer) playerServeKeyline = keyImg;
                else opponentServeKeyline = keyImg;
            }

            float pad = UITheme.F(10f);
            TextAnchor nameAlign = isPlayer ? TextAnchor.UpperRight : TextAnchor.UpperLeft;

            Text nameText = PSKit.Body(face.transform, "Name", nameAlign, displayName, UITheme.Cream, 26);
            UIBuilder.StretchRect(nameText.gameObject, new Vector2(0f, 0.5f), Vector2.one,
                new Vector2(pad, 0), new Vector2(-pad, -UITheme.F(3f)));
            UIBuilder.ClampLine(nameText, 20);
            displayNameText = nameText;

            // Score digit on the outer edge, pips filling the rest of the lower band.
            Vector2 scoreAnchor = isPlayer ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
            scoreText = PSKit.Display(face.transform, "Score", TextAnchor.MiddleCenter, "0",
                UITheme.Cream, 48);
            UIBuilder.Rect(scoreText.gameObject, scoreAnchor, scoreAnchor, scoreAnchor,
                new Vector2(isPlayer ? -pad : pad, UITheme.F(1f)), new Vector2(UITheme.F(26f), UITheme.F(28f)));

            UIBuilder.ClampLine(scoreText, 32); // Deuce can produce two- or three-digit scores.
            pips = CreatePipRow(face.transform, isPlayer, pad + UITheme.F(30f));
        }

        /// <summary>
        /// Round pips, filling toward the player's own edge: the opponent's run left-to-right and
        /// yours right-to-left, so both rows grow outward from the net between you.
        /// </summary>
        private Image[] CreatePipRow(Transform faceT, bool isPlayer, float edgeInset)
        {
            int target = TargetPoints;
            float scale = Mathf.Min(1f, 7f / target);
            float pipSize = PipSize * scale;
            float pipGap = PipGap * scale;
            float totalW = target * pipSize + (target - 1) * pipGap;

            Vector2 anchor = isPlayer ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
            GameObject row = UIBuilder.Child("Pips", faceT);
            UIBuilder.Rect(row, anchor, anchor, anchor,
                new Vector2(isPlayer ? -edgeInset : edgeInset, UITheme.F(6f)), new Vector2(totalW, pipSize));

            Image[] pips = new Image[target];
            for (int i = 0; i < target; i++)
            {
                // Index 0 is always the first point scored, so it sits nearest the outer edge.
                float x = isPlayer
                    ? totalW - pipSize - i * (pipSize + pipGap)
                    : i * (pipSize + pipGap);

                GameObject pipGO = UIBuilder.Child("Pip" + i, row.transform);
                UIBuilder.Rect(pipGO, Vector2.zero, Vector2.zero, Vector2.zero,
                    new Vector2(x, 0), new Vector2(pipSize, pipSize));
                Image pipImg = pipGO.AddComponent<Image>();
                pipImg.sprite = UIBuilder.CircleSprite;
                pipImg.color = EmptyPip;
                pips[i] = pipImg;
            }
            return pips;
        }

        /// <summary>An unwon point. Near-black rather than a tinted slot: on the board the empty
        /// pips sink into the plate and only the won ones light up.</summary>
        private static readonly Color EmptyPip = new Color(0f, 0f, 0f, 0.85f);

        private void SetPips(Image[] pips, int filled, Color filledColor)
        {
            if (pips == null) return;
            int clamped = Mathf.Clamp(filled, 0, pips.Length);
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null) continue;
                pips[i].color = i < clamped ? filledColor : EmptyPip;
            }
        }

        // ============================================================
        // SERVE / RALLY BANNER
        // ============================================================
        /// <summary>
        /// A volt light-bar across the middle of the court with the prompt over it -- "TAP TO SERVE",
        /// "IN RALLY", the rally counter. It replaces the chip that used to sit under the plates:
        /// during a rally the player is watching the ball near the net, not the top corner, and this
        /// is the one place a prompt is actually read.
        ///
        /// The bar fades out at both ends so it never reads as a solid object lying on the court.
        /// UIGradient interpolates two stops, so the fade is built as two mirrored halves.
        /// </summary>
        private void BuildServeBadge(Transform parent)
        {
            GameObject badge = UIBuilder.Child("ServeBanner", parent);
            // Board y 434 on an 844-tall board: 12px below centre.
            UIBuilder.Rect(badge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -UITheme.F(12f)), new Vector2(UITheme.F(238f), UITheme.F(24f)));

            for (int half = 0; half < 2; half++)
            {
                GameObject band = UIBuilder.Child(half == 0 ? "BandL" : "BandR", badge.transform);
                UIBuilder.StretchRect(band,
                    new Vector2(half == 0 ? 0f : 0.5f, 0f), new Vector2(half == 0 ? 0.5f : 1f, 1f),
                    Vector2.zero, Vector2.zero);
                Image img = band.AddComponent<Image>();
                img.raycastTarget = false;
                band.AddComponent<UIGradient>().direction = GradientDirection.Horizontal;
                if (half == 0) statusBadgeFill = img;
            }

            statusBadgeText = PSKit.Display(badge.transform, "Text", TextAnchor.MiddleCenter,
                "TAP TO SERVE", UITheme.Ink1, 34);
            UIBuilder.Fill(statusBadgeText.gameObject);
            UIBuilder.ClampLine(statusBadgeText, 24);
            Outline stroke = statusBadgeText.GetComponent<Outline>();
            if (stroke != null) Destroy(stroke);

            statusBadgeIcon = null;
            SetBandColor(UITheme.Volt);
        }

        /// <summary>Tints both halves of the banner. The bar carries the state colour; the label
        /// stays ink so it stays legible whatever the bar is doing.</summary>
        private void SetBandColor(Color color)
        {
            if (statusBadgeFill == null) return;
            Transform badge = statusBadgeFill.transform.parent;

            Color solid = new Color(color.r, color.g, color.b, 0.9f);
            Color clear = new Color(color.r, color.g, color.b, 0f);

            for (int i = 0; i < badge.childCount; i++)
            {
                Transform child = badge.GetChild(i);
                UIGradient grad = child.GetComponent<UIGradient>();
                if (grad == null) continue;
                bool left = child.name == "BandL";
                grad.topColor = left ? clear : solid;
                grad.bottomColor = left ? solid : clear;
                Graphic g = child.GetComponent<Graphic>();
                if (g != null) g.SetVerticesDirty();
            }
        }

        // ============================================================
        // SHOT QUALITY CALLOUT
        // ============================================================
        private void BuildShotQualityCallout(Transform parent)
        {
            shotQualityHost = UIBuilder.Child("ShotQualityCallout", parent);
            UIBuilder.Rect(shotQualityHost, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 120), new Vector2(900, 150));

            shotQualityText = UIBuilder.StrokedText(shotQualityHost.transform, "Text", TextAnchor.MiddleCenter, "", Color.white, UITheme.TypeCallout);
            UIBuilder.Fill(shotQualityText.gameObject);

            CanvasGroup cg = shotQualityHost.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            // Never eat a tap: the callout sits right over the play area during a rally.
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        // ============================================================
        // SHOT FEEDBACK CALLOUT (failures: "OUT OF REACH", "TOO LATE", "NET", ...)
        // Separate host from the quality callout, and lower, so a Miss can show *both* the neutral
        // quality readout and the reason it went wrong without the two overwriting each other.
        // ============================================================
        private void BuildShotFeedbackCallout(Transform parent)
        {
            shotFeedbackHost = UIBuilder.Child("ShotFeedbackCallout", parent);
            UIBuilder.Rect(shotFeedbackHost, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -30), new Vector2(900, 120));

            shotFeedbackText = UIBuilder.StrokedText(shotFeedbackHost.transform, "Text", TextAnchor.MiddleCenter, "", Color.white, 58);
            UIBuilder.Fill(shotFeedbackText.gameObject);

            CanvasGroup cg = shotFeedbackHost.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        public void ShowShotFeedback(string text, Color color)
        {
            if (shotFeedbackHost == null) return;
            if (shotFeedbackCoroutine != null) StopCoroutine(shotFeedbackCoroutine);
            shotFeedbackCoroutine = StartCoroutine(AnimateShotFeedback(text, color));
        }

        private IEnumerator AnimateShotFeedback(string text, Color color)
        {
            CanvasGroup cg = shotFeedbackHost.GetComponent<CanvasGroup>();
            RectTransform rt = shotFeedbackHost.GetComponent<RectTransform>();
            shotFeedbackText.text = text;
            shotFeedbackText.color = color;

            float duration = 1.0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float scale = t < 0.16f ? Mathf.Lerp(0.5f, 1.15f, t / 0.16f)
                            : t < 0.32f ? Mathf.Lerp(1.15f, 1f, (t - 0.16f) / 0.16f) : 1f;
                rt.localScale = new Vector3(scale, scale, 1f);
                cg.alpha = Mathf.Clamp01((1f - t) * 2.2f);
                yield return null;
            }
            cg.alpha = 0f;
            shotFeedbackText.text = "";
            rt.localScale = Vector3.one;
            shotFeedbackCoroutine = null;
        }

        // ============================================================
        // BETWEEN-POINT OVERLAY
        // ============================================================
        private void BuildPointOverlay(Transform canvasT)
        {
            pointOverlay = UIBuilder.Child("PointOverlay", canvasT);
            UIBuilder.Fill(pointOverlay);
            Image scrim = pointOverlay.AddComponent<Image>();
            scrim.color = new Color(0.02f, 0.08f, 0.16f, 0.34f);
            scrim.raycastTarget = false;

            Transform safe = UIBuilder.SafeArea(pointOverlay.transform);
            pointOverlayTitle = UIBuilder.StrokedText(safe, "Title", TextAnchor.MiddleCenter,
                "YOU SCORE", UITheme.GoldTop, 58);
            UIBuilder.Rect(pointOverlayTitle.gameObject, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 110), new Vector2(-90, 100));

            pointOverlayScore = UIBuilder.StrokedText(safe, "Score", TextAnchor.MiddleCenter, "1  VS  0", Color.white, 70);
            UIBuilder.Rect(pointOverlayScore.gameObject, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -30), new Vector2(-90, 120));

            CanvasGroup group = pointOverlay.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            pointOverlay.SetActive(false);
        }

        public void ShowPointResult(bool playerWonPoint, int playerScore, int opponentScore, bool scored = true)
        {
            if (pointOverlay == null) return;
            if (pointOverlayCoroutine != null)
            {
                StopCoroutine(pointOverlayCoroutine);
                RestoreGameplayPrompts();
            }
            pointOverlayCoroutine = StartCoroutine(AnimatePointOverlay(playerWonPoint, playerScore, opponentScore, scored));
        }

        private void RestoreGameplayPrompts()
        {
            if (statusBadgeFill != null) statusBadgeFill.transform.parent.gameObject.SetActive(true);
            if (coachEnabled && coachHost != null) coachHost.SetActive(true);
        }

        private IEnumerator AnimatePointOverlay(bool playerWonPoint, int playerScore, int opponentScore, bool scored)
        {
            GameObject serveBanner = statusBadgeFill != null ? statusBadgeFill.transform.parent.gameObject : null;
            if (serveBanner != null) serveBanner.SetActive(false);
            if (coachHost != null) coachHost.SetActive(false);
            pointOverlay.SetActive(true);
            CanvasGroup group = pointOverlay.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            pointOverlayTitle.text = scored ? (playerWonPoint ? "YOU SCORE" : "OPPONENT SCORES") :
                "SIDE OUT";
            pointOverlayTitle.color = playerWonPoint ? UITheme.GoldTop : UITheme.TextRivalRed;
            pointOverlayScore.text = playerScore + "  VS  " + opponentScore;

            float t = 0f;
            while (t < 0.12f)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(t / 0.12f);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.82f);

            t = 0f;
            while (t < 0.24f)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(t / 0.24f);
                yield return null;
            }
            pointOverlay.SetActive(false);
            RestoreGameplayPrompts();
            pointOverlayCoroutine = null;
        }

        public void SetOpponentName(string displayName)
        {
            if (opponentNameText == null) return;
            opponentNameText.text = string.IsNullOrEmpty(displayName) ? "OPPONENT" : displayName.ToUpperInvariant();
            UIBuilder.ClampLine(opponentNameText);
        }

        // ============================================================
        // MATCH RESULT MODAL
        // ============================================================
        private void BuildResultModal(Transform canvasT)
        {
            winLoseOverlay = UIBuilder.Child("WinLoseOverlay", canvasT);
            UIBuilder.Fill(winLoseOverlay);
            Image scrim = winLoseOverlay.AddComponent<Image>();
            scrim.color = UITheme.ModalScrim;

            Transform safe = UIBuilder.SafeArea(winLoseOverlay.transform);

            GameObject card = UIBuilder.Panel(safe, "ResultCard", new Vector2(0, 0), UITheme.PanelTop, UITheme.PanelDeep);
            RectTransform cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0f, 0.5f);
            cardRt.anchorMax = new Vector2(1f, 0.5f);
            cardRt.offsetMin = new Vector2(UITheme.SpaceLg, -450);
            cardRt.offsetMax = new Vector2(-UITheme.SpaceLg, 450);
            UIBuilder.ShineLayer(card.transform, 0.30f);

            // Vertical stack, all measured from the card's top edge with a centred pivot:
            //   title      -40 .. -140
            //   "FINAL"   -152 .. -184
            //   digits    -196 .. -292
            //   pip rows  -318 .. -344   (must clear the digits; they used to be laid at -326 while
            //                             the 110px-tall score box still ran to -356, so the two
            //                             pip stacks were drawn straight through "7 - 4")
            //   rewards   -370 .. -478
            // and the button stack is anchored up from the bottom, topping out at -572.

            // --- Title ---
            GameObject titleRow = UIBuilder.Child("TitleRow", card.transform);
            UIBuilder.Rect(titleRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(700, 100));
            winLoseTitleIcon = UIBuilder.IconAt(titleRow.transform, IconId.Trophy, 78f, UITheme.GoldMid, UITheme.OutlineNavy, new Vector2(0f, 0.5f), new Vector2(160, 0));
            winLoseTitleText = UIBuilder.StrokedText(titleRow.transform, "Title", TextAnchor.MiddleLeft, "VICTORY!", UITheme.GoldMid, 68);
            UIBuilder.Rect(winLoseTitleText.gameObject, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(212, 0), new Vector2(-226, 92));

            // --- Score ---
            Text scoreLabel = UIBuilder.Text(card.transform, "ScoreLabel", TextAnchor.MiddleCenter, "FINAL SCORE", new Color(1f, 1f, 1f, 0.8f), 24, FontStyle.Bold);
            UIBuilder.Rect(scoreLabel.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -168), new Vector2(400, 32));

            winLoseScoreText = UIBuilder.StrokedText(card.transform, "Score", TextAnchor.MiddleCenter, "7 - 4", Color.white, 82);
            UIBuilder.Rect(winLoseScoreText.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -244), new Vector2(600, 96));

            GameObject playerPips = UIBuilder.Child("PlayerPipsRoot", card.transform);
            UIBuilder.Rect(playerPips, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-176, -331), Vector2.zero);
            modalPlayerPipsRoot = playerPips.transform;

            GameObject oppPips = UIBuilder.Child("OpponentPipsRoot", card.transform);
            UIBuilder.Rect(oppPips, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(176, -331), Vector2.zero);
            modalOpponentPipsRoot = oppPips.transform;

            // --- Rewards ---
            GameObject rewards = UIBuilder.Child("Rewards", card.transform);
            UIBuilder.Rect(rewards, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -370), new Vector2(620, 108));
            rewards.AddComponent<Image>();
            UIBuilder.OutlinedFill(rewards, UIBuilder.PanelSprite, UITheme.PanelDarkDeep, UITheme.OutlineNavy, UITheme.StrokeOutlineThin);

            GameObject coinHost = UIBuilder.Child("Coins", rewards.transform);
            UIBuilder.StretchRect(coinHost, new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            UIBuilder.IconAt(coinHost.transform, IconId.Coin, 44f, UITheme.GoldMid, UITheme.OutlineNavy, new Vector2(0.5f, 0.5f), new Vector2(-58, 0));
            rewardCoinText = UIBuilder.StrokedText(coinHost.transform, "Text", TextAnchor.MiddleLeft, "+0", UITheme.GoldTop, 32);
            UIBuilder.Rect(rewardCoinText.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-26, 0), new Vector2(150, 44));

            GameObject tropHost = UIBuilder.Child("Trophies", rewards.transform);
            UIBuilder.StretchRect(tropHost, new Vector2(0.5f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            UIBuilder.IconAt(tropHost.transform, IconId.Trophy, 44f, UITheme.GoldMid, UITheme.OutlineNavy, new Vector2(0.5f, 0.5f), new Vector2(-52, 0));
            rewardTrophyText = UIBuilder.StrokedText(tropHost.transform, "Text", TextAnchor.MiddleLeft, "+0", Color.white, 32);
            UIBuilder.Rect(rewardTrophyText.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-20, 0), new Vector2(150, 44));

            // --- Actions ---
            // There was no way out of this modal except PLAY AGAIN: the result screen was a dead end
            // that could only be escaped by starting another match and then forfeiting it.
            UIBuilder.ButtonRefs playAgain = UIBuilder.GreenButton(card.transform, "PLAY AGAIN", new Vector2(0, UITheme.ButtonHeight), UITheme.TypeButton, delegate
            {
                // Routed through ScreenManager so a tour rematch re-charges the entry fee, the same
                // way the first attempt did. Ranked/PvP rematches stay free. Local restart is only a
                // fallback for when the manager somehow isn't present.
                if (ScreenManager.Instance != null)
                {
                    ScreenManager.Instance.RequestRematch();
                    return;
                }
                HideMatchResult();
                RallyManager rally = FindObjectOfType<RallyManager>();
                if (rally != null) rally.RestartMatch();
            });
            playAgainLabel = playAgain.label;
            UIBuilder.StretchRect(playAgain.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(48, 44 + UITheme.ButtonHeightGhost + 16), new Vector2(-48, 44 + UITheme.ButtonHeightGhost + 16 + UITheme.ButtonHeight));

            UIBuilder.ButtonRefs lobby = UIBuilder.GhostButton(card.transform, "BACK TO LOBBY", new Vector2(0, UITheme.ButtonHeightGhost), UITheme.TypeButtonSm, delegate
            {
                HideMatchResult();
                if (ScreenManager.Instance != null) ScreenManager.Instance.ExitMatchToLobby();
            });
            UIBuilder.StretchRect(lobby.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(48, 44), new Vector2(-48, 44 + UITheme.ButtonHeightGhost));

            winLoseOverlay.SetActive(false);
        }

        // ============================================================
        // PUBLIC API
        // ============================================================
        public void UpdateScore(int playerScore, int opponentScore)
        {
            if (playerScoreText != null) playerScoreText.text = playerScore.ToString();
            if (opponentScoreText != null) opponentScoreText.text = opponentScore.ToString();
            // Both score rows use volt pips; the separate keyline indicates who serves.
            SetPips(pipsPlayerHUD, playerScore, UITheme.VoltBright);
            SetPips(pipsOpponentHUD, opponentScore, UITheme.VoltBright);
        }

        public void UpdateMatchState(string state)
        {
            if (statusBadgeText == null) return;

            if (state.Contains("SERVING") || state.Contains("SERVE"))
            {
                SetBadge("TAP TO SERVE", UITheme.Volt);
            }
            else if (state.Contains("RALLY"))
            {
                SetBadge("IN RALLY", UITheme.QualityColor(ShotQuality.Great));
            }
            else
            {
                SetBadge(state, UITheme.Cream);
            }
        }

        public void UpdateServerHighlight(bool playerServing)
        {
            if (playerServeKeyline != null) playerServeKeyline.enabled = playerServing;
            if (opponentServeKeyline != null) opponentServeKeyline.enabled = !playerServing;
        }

        public void UpdateServeState(bool playerServing, bool serveFromRight)
        {
            UpdateServerHighlight(playerServing);
            // The board says TAP TO SERVE, which is an instruction; on the opponent's serve there is
            // nothing to tap, so that copy would be a lie. The side matters either way -- it is
            // where the ball is about to come from.
            string side = serveFromRight ? "RIGHT" : "LEFT";
            SetBadge(playerServing ? "TAP TO SERVE · " + side : "OPPONENT SERVES · " + side,
                playerServing ? UITheme.Volt : UITheme.Bubblegum);
        }

        public void UpdateRallyCount(int count)
        {
            if (count > 1) SetBadge("RALLY " + count, UITheme.QualityColor(ShotQuality.Weak));
        }

        /// <summary>Sets the banner copy and the colour of the light-bar behind it.</summary>
        private void SetBadge(string text, Color color)
        {
            if (statusBadgeText != null) statusBadgeText.text = text;
            SetBandColor(color);
        }

        public void ShowShotQuality(ShotQuality quality, float compositeScore, ShotType shotType = ShotType.Flat)
        {
            if (shotQualityCoroutine != null) StopCoroutine(shotQualityCoroutine);
            shotQualityCoroutine = StartCoroutine(AnimateShotQuality(quality, shotType));
        }

        private IEnumerator AnimateShotQuality(ShotQuality quality, ShotType shotType)
        {
            if (shotQualityText == null || shotQualityHost == null) yield break;

            CanvasGroup cg = shotQualityHost.GetComponent<CanvasGroup>();
            RectTransform rt = shotQualityHost.GetComponent<RectTransform>();

            string label = UITheme.QualityLabel(quality);
            if (shotType == ShotType.Dink) label += " DINK";
            else if (shotType == ShotType.Topspin) label += " DRIVE";
            else if (shotType == ShotType.Lob) label += " LOB";
            else if (shotType == ShotType.Slice) label += " SLICE";
            else if (shotType == ShotType.Smash) label += " SMASH";

            shotQualityText.text = label;
            shotQualityText.color = UITheme.QualityColor(quality);

            bool softShot = shotType == ShotType.Dink || shotType == ShotType.Lob;
            float emphasis = shotType == ShotType.Smash ? 0.85f : (softShot ? 0.5f : 0.65f);
            float duration = softShot ? 0.48f : 0.65f;
            float elapsed = 0f;
            float tilt = UITheme.QualityTilt(quality);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float scale;
                if (t < 0.15f) scale = Mathf.Lerp(0.4f, 1.35f, t / 0.15f);
                else if (t < 0.35f) scale = Mathf.Lerp(1.35f, 1.0f, (t - 0.15f) / 0.2f);
                else scale = 1f;

                rt.localScale = new Vector3(scale * emphasis, scale * emphasis, 1f);
                rt.localRotation = Quaternion.Euler(0, 0, (1f - t) * tilt);
                cg.alpha = Mathf.Clamp01((1f - t) * 1.8f);

                yield return null;
            }

            cg.alpha = 0f;
            shotQualityText.text = "";
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        public void ShowMatchResult(bool playerWon)
        {
            if (winLoseOverlay == null) return;

            winLoseOverlay.SetActive(true);
            UIFadeIn.Attach(winLoseOverlay, UITheme.ModalEnterTime, 0f, 0f, 0.94f);

            int finalPlayer = 0, finalOpponent = 0;
            if (playerScoreText != null) int.TryParse(playerScoreText.text, out finalPlayer);
            if (opponentScoreText != null) int.TryParse(opponentScoreText.text, out finalOpponent);

            if (winLoseTitleText != null)
            {
                winLoseTitleText.text = playerWon ? "VICTORY!" : "DEFEAT";
                winLoseTitleText.color = playerWon ? UITheme.GoldMid : UITheme.TextRivalRed;
            }

            if (winLoseTitleIcon != null)
            {
                IconId icon = playerWon ? IconId.Trophy : IconId.Shield;
                Color tint = playerWon ? UITheme.GoldMid : UITheme.TextRivalRed;

                Transform fill = winLoseTitleIcon.transform.Find("Fill");
                if (fill != null)
                {
                    Image img = fill.GetComponent<Image>();
                    img.sprite = UIIcon.Fill(icon);
                    img.color = tint;
                }
                Transform outline = winLoseTitleIcon.transform.Find("Outline");
                if (outline != null) outline.GetComponent<Image>().sprite = UIIcon.Outline(icon);
            }

            if (winLoseScoreText != null) winLoseScoreText.text = finalPlayer + " - " + finalOpponent;

            // The real payout ScreenManager.HandleMatchEnded computed and applied (tour reward table /
            // Elo swing / performance bonus), not a hardcoded copy.
            MatchReward reward = MetaGameState.LastMatchReward;
            if (rewardCoinText != null)
            {
                rewardCoinText.text = "+" + reward.coins;
                UICountUp.Attach(rewardCoinText, 0, reward.coins, 0.7f, "+");
            }
            if (rewardTrophyText != null)
            {
                rewardTrophyText.text = (reward.trophies >= 0 ? "+" : "") + reward.trophies;
                rewardTrophyText.color = reward.trophies >= 0 ? Color.white : UITheme.TextRivalRed;
            }

            RebuildModalPips(modalPlayerPipsRoot, finalPlayer, UITheme.TextPlayerBlue);
            RebuildModalPips(modalOpponentPipsRoot, finalOpponent, UITheme.TextRivalRed);

            // A tour rematch costs the entry fee again -- show it on the button so the charge isn't a
            // surprise. Ranked/PvP rematches are free and keep the plain label.
            if (playAgainLabel != null)
            {
                int rematchFee = ScreenManager.Instance != null ? ScreenManager.Instance.PendingRematchFee : 0;
                playAgainLabel.text = rematchFee > 0 ? "PLAY AGAIN  (-" + rematchFee + ")" : "PLAY AGAIN";
            }
        }

        private void RebuildModalPips(Transform root, int filled, Color filledColor)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);

            const float pipW = 24f, pipH = 26f, gap = 6f;
            int target = TargetPoints;
            int clamped = Mathf.Clamp(filled, 0, target);

            for (int i = 0; i < target; i++)
            {
                GameObject pipGO = UIBuilder.Child("Pip" + i, root);
                UIBuilder.Rect(pipGO, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2((i - (target - 1) * 0.5f) * (pipW + gap), 0), new Vector2(pipW, pipH));
                pipGO.AddComponent<Image>();
                UIBuilder.OutlinedFill(pipGO, UIBuilder.RoundedSprite(5), i < clamped ? filledColor : EmptyPip, UITheme.OutlineNavy, 2f);
            }
        }

        public void HideMatchResult()
        {
            if (winLoseOverlay != null) winLoseOverlay.SetActive(false);
            if (pointOverlayCoroutine != null)
            {
                StopCoroutine(pointOverlayCoroutine);
                pointOverlayCoroutine = null;
            }
            if (pointOverlay != null) pointOverlay.SetActive(false);
            RestoreGameplayPrompts();
        }
    }
}
