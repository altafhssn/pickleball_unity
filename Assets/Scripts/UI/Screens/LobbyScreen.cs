using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;
using Pickleball.Systems;

namespace Pickleball.UI
{
    /// <summary>
    /// Home. Built on the Pickle Smash board (Docs/Figma/Frame-2.svg) with the launch design's two
    /// actions:
    ///
    ///     top      identity chip, coin pill, league-points pill  -- who I am, what I have
    ///     under    league card                                    -- where I stand, what's next
    ///     right    the character art
    ///     bottom   PLAY WITH AI, MULTIPLAYER, then the four-tab nav
    ///
    /// The watch-an-ad offer sits under the coin balance, and only when an ad can actually pay.
    /// Vertical placement is measured off the 390x844 board and converted with
    /// <see cref="UITheme.F"/>. The action stack is anchored to the bottom edge so it survives aspect
    /// ratios the board was never drawn at; only the header block counts down from the top.
    /// </summary>
    public static class LobbyScreen
    {
        // ---------- Header block, measured from the top of the safe area ----------
        private const float HeaderTop = UITheme.HeaderTopOffset;
        /// <summary>Avatar disc diameter -- board 39.6px.</summary>
        private static readonly float ChipHeight = UITheme.F(39.6f);
        /// <summary>Board: name pill 29px tall, currency pills the same.</summary>
        private static readonly float PillHeight = UITheme.F(29f);
        /// <summary>The ad offer hangs under the coin pill, and the league card clears it.</summary>
        private static readonly float AdTop = HeaderTop + ChipHeight + UITheme.F(7f);
        private static readonly float AdHeight = UITheme.F(26f);
        private static readonly float CardTop = AdTop + AdHeight + UITheme.F(7f);

        // ---------- Actions, measured up from the bottom edge ----------
        private const float MultiplayerTop = UITheme.CtaStackHeight;
        private const float ActionGap = 30f;
        private static readonly float AiTop = MultiplayerTop + ActionGap + UITheme.ButtonHeight;

        // ---------- Character, board rect x 146..385, y 176..793 ----------
        private static readonly float CharWidth = UITheme.F(239f);
        private static readonly float CharHeight = UITheme.F(617f);
        private static readonly float CharBottom = UITheme.F(844f - 793f);
        private static readonly float CharRightInset = UITheme.F(390f - 385f);

        public static GameObject Build(Transform parent, ScreenManager mgr)
        {
            GameObject root = UIBuilder.Child("LobbyScreen", parent);
            UIBuilder.Fill(root);

            // Use the supplied stadium variant; the artwork fills the physical screen,
            // while the interactive composition fits inside the device safe area.
            UIReferenceArt.Backdrop(root.transform, "stadium");
            Transform safe = PSKit.BoardHost(UIBuilder.SafeArea(root.transform)).transform;

            // First child of the safe area, so every card, the CTA and the nav draw over him.
            BuildCharacter(safe);

            BuildHeaderRow(safe, mgr);
            BuildLeagueCard(safe, mgr);
            BuildAdOffer(safe, mgr);
            BuildActions(safe, mgr);

            PSKit.BottomNav(safe, "HOME", delegate (string tab) { mgr.NavigateTab(tab); });
            return root;
        }

        // ============================================================
        // CHARACTER
        // ============================================================

        /// <summary>The original supplied player artwork, composited behind the live controls.</summary>
        private static void BuildCharacter(Transform safe)
        {
            Image player = UIReferenceArt.Draw(safe, "home_player", false);
            UIBuilder.Rect(player.gameObject, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-CharRightInset, CharBottom), new Vector2(CharWidth, CharHeight));
            UIFadeIn.Attach(player.gameObject, 0.36f, 0.04f, 26f);
        }

        // ============================================================
        // HEADER ROW -- identity, coins, league points
        // ============================================================
        private static void BuildHeaderRow(Transform safe, ScreenManager mgr)
        {
            float rowCenter = -(HeaderTop + ChipHeight * 0.5f);

            GameObject chip = PSKit.IdentityChip(safe, MetaGameState.PlayerName,
                MetaGameState.CurrentLeague.Name, PillHeight, UITheme.F(116f),
                0, delegate { mgr.NavigateTab("SETTINGS"); });
            UIBuilder.Rect(chip, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(UITheme.F(21f), rowCenter), new Vector2(UITheme.F(116f), ChipHeight));

            // League points sit outermost, coins inboard of them: the board's order, and it keeps the
            // "+" affordance away from the screen edge where a thumb rests.
            GameObject pointsPill = PSKit.CurrencyPill(safe, IconId.Trophy,
                MetaGameState.LeaguePoints.ToString("N0"), new Vector2(UITheme.F(88f), PillHeight), null);
            UIBuilder.Rect(pointsPill, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-UITheme.F(18.8f), rowCenter), new Vector2(UITheme.F(88f), PillHeight));

            // The "+" is the watch-an-ad offer: coins are earned, never bought.
            GameObject coinPill = PSKit.CurrencyPill(safe, IconId.Coin,
                MetaGameState.Coins.ToString("N0"), new Vector2(UITheme.F(99f), PillHeight),
                delegate { mgr.ShowAdOffer(); });
            UIBuilder.Rect(coinPill, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-UITheme.F(18.8f + 88f + 13f), rowCenter), new Vector2(UITheme.F(99f), PillHeight));
        }

        // ============================================================
        // AD OFFER -- under the coin balance
        // ============================================================
        private static void BuildAdOffer(Transform safe, ScreenManager mgr)
        {
            if (!RewardedAds.IsOfferAvailable) return;

            Vector2 size = new Vector2(UITheme.F(118f), AdHeight);
            PSKit.ButtonStack offer = PSKit.PressStack(safe, "AdOffer", size, UITheme.RadiusChip, true,
                UITheme.GrassDeep, UITheme.Grass, UITheme.SheenVolt, delegate { mgr.ShowAdOffer(); });
            offer.label = PSKit.Display(offer.stack.face.transform, "Label", TextAnchor.MiddleCenter,
                "WATCH AD +" + Sim.EconomyConfig.AdRewardCoins, UITheme.Ink1, 26);
            UIBuilder.Fill(offer.label.gameObject);
            Outline outline = offer.label.GetComponent<Outline>();
            if (outline != null) Object.Destroy(outline);
            UIBuilder.ClampLine(offer.label, 18);
            UIBuilder.Rect(offer.root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-UITheme.F(18.8f + 88f + 13f), -AdTop), size);
            UIFadeIn.Attach(offer.root, 0.26f, 0.12f, 20f);
        }

        // ============================================================
        // LEAGUE CARD
        // ============================================================
        private static void BuildLeagueCard(Transform safe, ScreenManager mgr)
        {
            int points = MetaGameState.LeaguePoints;
            int index = MetaGameState.CurrentLeagueIndex;
            int next = Sim.LeagueRules.PromotionThreshold(index);

            GameObject card = UIBuilder.Child("LeagueCard", safe);
            UIBuilder.Rect(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(UITheme.F(19f), -CardTop), new Vector2(UITheme.F(352f), UITheme.F(96f)));
            Image skin = UIReferenceArt.Draw(card.transform, "season_card", false);
            skin.raycastTarget = true;
            Button button = card.AddComponent<Button>();
            button.targetGraphic = skin;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(delegate { mgr.NavigateTab("LEAGUE"); });

            Text title = PSKit.Body(card.transform, "LeagueName", TextAnchor.MiddleLeft,
                MetaGameState.CurrentLeague.Name + " LEAGUE", UITheme.Cream, 34);
            UIBuilder.Rect(title.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(UITheme.F(48f), -UITheme.F(30f)), new Vector2(UITheme.F(266f), UITheme.F(20f)));
            UIBuilder.ClampLine(title, 22);

            GameObject meter = UIBuilder.Child("LeagueProgress", card.transform);
            UIBuilder.Rect(meter, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(UITheme.F(48f), -UITheme.F(51.5f)), new Vector2(UITheme.F(266f), UITheme.F(15f)));
            BuildMeter(meter, Sim.LeagueRules.Progress01(points));

            string progress = next < 0
                ? string.Format("{0:N0} PTS · TOP LEAGUE", points)
                : string.Format("{0:N0} / {1:N0} PTS · NEXT {2}", points, next, Sim.LeagueConfig.Leagues[index + 1].Name);
            Text detail = PSKit.Body(card.transform, "Progress", TextAnchor.MiddleLeft, progress, UITheme.Cream, 30);
            UIBuilder.Rect(detail.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(UITheme.F(48f), -UITheme.F(74f)), new Vector2(UITheme.F(266f), UITheme.F(20f)));
            UIBuilder.ClampLine(detail, 20);
        }

        private static void BuildMeter(GameObject host, float fill01)
        {
            // Pure black rather than the panel's own Ink0: on a #12171C card an almost-black track
            // was invisible, so the volt fill read as a floating bar with no scale behind it.
            Image track = host.AddComponent<Image>();
            track.sprite = UIBuilder.CapsuleSprite;
            track.gameObject.AddComponent<UICapsuleFit>();
            track.type = Image.Type.Sliced;
            track.color = Color.black;

            GameObject fill = UIBuilder.Child("Fill", host.transform);
            UIBuilder.StretchRect(fill, Vector2.zero, new Vector2(Mathf.Clamp01(fill01), 1f),
                new Vector2(8, 8), new Vector2(-8, -8));
            Image fillImg = fill.AddComponent<Image>();
            fillImg.sprite = UIBuilder.CapsuleSprite;
            fillImg.gameObject.AddComponent<UICapsuleFit>();
            fillImg.type = Image.Type.Sliced;
            fillImg.color = UITheme.Volt;
        }

        // ============================================================
        // ACTIONS -- the two ways to play
        // ============================================================
        private static void BuildActions(Transform safe, ScreenManager mgr)
        {
            PSKit.ButtonStack ai = PSKit.SecondaryCta(safe, "PLAY WITH AI",
                new Vector2(0, UITheme.ButtonHeight), UITheme.TypeButton, delegate { mgr.StartAiMatch(); });
            UIBuilder.StretchRect(ai.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.ScreenPad, AiTop - UITheme.ButtonHeight),
                new Vector2(-UITheme.ScreenPad, AiTop));
            UIFadeIn.Attach(ai.root, 0.26f, 0.08f, 40f);

            PSKit.ButtonStack multiplayer = PSKit.PrimaryCta(safe, "MULTIPLAYER",
                new Vector2(0, UITheme.ButtonHeight), UITheme.TypeButton, delegate { mgr.StartMultiplayerMatch(); });
            UIBuilder.StretchRect(multiplayer.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.ScreenPad, MultiplayerTop - UITheme.ButtonHeight),
                new Vector2(-UITheme.ScreenPad, MultiplayerTop));
            UIFadeIn.Attach(multiplayer.root, 0.26f, 0.12f, 40f);
        }
    }
}
