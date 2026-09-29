using UnityEngine;
using UnityEngine.UI;
using Pickleball.Gameplay;

namespace Pickleball.UI
{
    /// <summary>
    /// Screen 06 — pause, rebuilt against the Pickle Smash board (Docs/Figma/INGAME.svg).
    ///
    /// A volt-edged night card over the frozen match: title, elapsed time, a rule, the live score on
    /// its own inset row, then one filled button to come back, one neutral to change something, and
    /// the way out as plain crimson type. The previous version stacked three filled buttons in
    /// green, blue and red, which gave FORFEIT the same visual weight as RESUME on a screen a
    /// player reaches by accident more often than on purpose.
    ///
    /// The card uses the cooler night palette rather than the meta screens' near-black, because it
    /// sits over live 3D — see <see cref="UITheme.NightCard"/>.
    /// </summary>
    public static class PauseScreen
    {
        // Board coordinates (390x844).
        private const float CardTop = 224f;
        private const float CardBottom = 630f;
        private const float CardSidePad = 19f;
        private const float TitleY = 268f;
        private const float ElapsedY = 300f;
        private const float RuleY = 325.5f;
        private const float ScoreRowY = 372.5f;
        private const float ScoreRowH = 53f;
        private const float ResumeY = 451f;
        private const float SettingsY = 528f;
        private const float QuitY = 596f;
        private const float ButtonH = 64.7f;

        public static GameObject Build(Transform parent, ScreenManager mgr)
        {
            GameObject root = UIBuilder.Child("PauseScreen", parent);
            UIBuilder.Fill(root);

            GameObject scrim = UIBuilder.Child("Scrim", root.transform);
            UIBuilder.Fill(scrim);
            Image scrimImg = scrim.AddComponent<Image>();
            scrimImg.color = new Color(0f, 0f, 0f, 0.63f);
            // Tapping the scrim resumes — the gesture players try first on a modal.
            Button scrimBtn = scrim.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.targetGraphic = scrimImg;
            scrimBtn.onClick.AddListener(delegate { mgr.ResumeFromPause(); });

            Transform safe = UIBuilder.SafeArea(root.transform);
            GameObject board = PSKit.BoardHost(safe);

            // ---- Card: night fill, volt keyline ----
            GameObject card = UIBuilder.Child("PauseCard", board.transform);
            PSKit.BoardRow(card, (CardTop + CardBottom) * 0.5f, CardBottom - CardTop, CardSidePad);
            Image cardImg = card.AddComponent<Image>();
            cardImg.sprite = UIBuilder.RoundedSprite(56);
            cardImg.type = Image.Type.Sliced;
            cardImg.color = UITheme.Volt;

            GameObject cardFill = UIBuilder.Child("Fill", card.transform);
            UIBuilder.StretchRect(cardFill, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            Image fillImg = cardFill.AddComponent<Image>();
            fillImg.sprite = UIBuilder.RoundedSprite(52);
            fillImg.type = Image.Type.Sliced;
            fillImg.color = UITheme.NightCard;
            UIFadeIn.Attach(card, UITheme.ModalEnterTime, 0f, 50f, 0.94f);

            Transform host = cardFill.transform;

            Text title = PSKit.Display(host, "Title", TextAnchor.MiddleCenter, "MATCH PAUSED",
                UITheme.Volt, UITheme.TypeHeroTitle);
            CardRow(title.gameObject, TitleY, 66f, 16f);
            UIBuilder.ClampLine(title, 40);

            Text elapsed = PSKit.Body(host, "Elapsed", TextAnchor.MiddleCenter,
                "ELAPSED TIME: " + FormatElapsed(), UITheme.Slate, 32);
            CardRow(elapsed.gameObject, ElapsedY, 32f, 16f);
            UIBuilder.ClampLine(elapsed, 20);

            GameObject rule = UIBuilder.Child("Rule", host);
            CardRow(rule, RuleY, 1.2f, 24f);
            Image ruleImg = rule.AddComponent<Image>();
            ruleImg.color = UITheme.NightRule;
            ruleImg.raycastTarget = false;

            BuildScoreRow(host, mgr);

            PSKit.ButtonStack resume = PSKit.PrimaryCta(host, "Resume Match",
                new Vector2(0, UITheme.F(ButtonH)), 66, delegate { mgr.ResumeFromPause(); });
            CardRow(resume.root, ResumeY, ButtonH, 24f);

            PSKit.ButtonStack settings = PSKit.SecondaryCta(host, "Settings",
                new Vector2(0, UITheme.F(ButtonH)), 62, delegate { mgr.ShowSettingsFromPause(); });
            CardRow(settings.root, SettingsY, ButtonH, 24f);

            // Forfeit is the only irreversible thing on this screen, so it says what it costs and it
            // is the one control with no fill behind it — you have to mean it.
            Text quit = PSKit.TextButton(host,
                mgr.CurrentMatchMode == Pickleball.Sim.MatchMode.AI ? "LEAVE MATCH" : "QUIT MATCH",
                UITheme.Crimson, 52,
                delegate { mgr.ConfirmForfeit(); });
            CardRow(quit.transform.parent.gameObject, QuitY, 44f, 24f);

            return root;
        }

        /// <summary>Places a row inside the card using the board's own y coordinate.</summary>
        private static void CardRow(GameObject go, float boardY, float boardHeight, float sidePad = 0f)
        {
            UIBuilder.Rect(go, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -UITheme.F(boardY - CardTop)),
                new Vector2(-UITheme.F(sidePad) * 2f, UITheme.F(boardHeight)));
        }

        /// <summary>
        /// Inset score row: opponent on the left, you on the right, your digit in volt. Volt means
        /// "you" everywhere else in the match — the score plates, the serve indicator — so it means
        /// it here too rather than marking whoever happens to be ahead.
        /// </summary>
        private static void BuildScoreRow(Transform host, ScreenManager mgr)
        {
            int playerScore = 0, opponentScore = 0;
            if (RallyManager.Instance != null)
            {
                playerScore = RallyManager.Instance.playerScore;
                opponentScore = RallyManager.Instance.opponentScore;
            }

            GameObject row = UIBuilder.Child("ScoreRow", host);
            CardRow(row, ScoreRowY, ScoreRowH, 24f);
            Image rowImg = row.AddComponent<Image>();
            rowImg.sprite = UIBuilder.RoundedSprite(22);
            rowImg.type = Image.Type.Sliced;
            rowImg.color = UITheme.NightRow;

            Text rival = PSKit.Body(row.transform, "Rival", TextAnchor.MiddleLeft,
                RivalName(), UITheme.Cream, 34);
            UIBuilder.StretchRect(rival.gameObject, new Vector2(0f, 0f), new Vector2(0.35f, 1f),
                new Vector2(UITheme.F(13f), 0), Vector2.zero);
            UIBuilder.ClampLine(rival, 20);

            Text you = PSKit.Body(row.transform, "You", TextAnchor.MiddleRight, "You", UITheme.Slate, 34);
            UIBuilder.StretchRect(you.gameObject, new Vector2(0.65f, 0f), new Vector2(1f, 1f),
                Vector2.zero, new Vector2(-UITheme.F(13f), 0));

            GameObject digits = UIBuilder.Child("Digits", row.transform);
            UIBuilder.Rect(digits, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(UITheme.F(90f), UITheme.F(40f)));

            Text rivalDigit = PSKit.Display(digits.transform, "RivalScore", TextAnchor.MiddleRight,
                opponentScore.ToString(), UITheme.Cream, 46);
            UIBuilder.StretchRect(rivalDigit.gameObject, Vector2.zero, new Vector2(0.38f, 1f), Vector2.zero, Vector2.zero);

            Text dash = PSKit.Body(digits.transform, "Dash", TextAnchor.MiddleCenter, "–", UITheme.Slate, 36);
            UIBuilder.StretchRect(dash.gameObject, new Vector2(0.38f, 0f), new Vector2(0.62f, 1f), Vector2.zero, Vector2.zero);

            Text playerDigit = PSKit.Display(digits.transform, "PlayerScore", TextAnchor.MiddleLeft,
                playerScore.ToString(), UITheme.VoltInk, 46);
            UIBuilder.StretchRect(playerDigit.gameObject, new Vector2(0.62f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
        }

        /// <summary>The HUD already holds the opponent's display name, whether it came from the
        /// AI defaults or from a real PvP handshake. Reading it back keeps the two surfaces from
        /// disagreeing about who you are playing.</summary>
        private static string RivalName()
        {
            return GameplayHUD.Instance != null ? GameplayHUD.Instance.OpponentName : "OPPONENT";
        }

        private static string FormatElapsed()
        {
            float seconds = GameplayHUD.Instance != null ? GameplayHUD.Instance.ElapsedSeconds : 0f;
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return string.Format("{0:00}:{1:00}", total / 60, total % 60);
        }
    }
}
