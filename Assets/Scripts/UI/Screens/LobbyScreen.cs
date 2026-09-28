using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;

namespace Pickleball.UI
{
    /// <summary>
    /// Screen 02 â€” Home. Rebuilt against the Pickle Smash board (Docs/Figma/Frame-2.svg).
    ///
    /// The old lobby put nine competing elements on one screen: identity, a season strip, a hero
    /// disc, a recent-form row, an overall-rating card, three gear slots, a tour card, four chest
    /// slots and the CTA. The board keeps five, and gives the whole middle of the screen back to
    /// the character:
    ///
    ///     top     identity chip, coin pill, trophy pill        â€” who I am, what I have
    ///     under    season card, volt-bracketed                 â€” what I am climbing
    ///     middle   empty, for the 3D character behind the UI
    ///     left     next-bag timer, then the ready gift         â€” what is cooking, what to tap
    ///     bottom   LETS PLAY, then the four-tab nav            â€” the one action, then everywhere else
    ///
    /// Vertical placement is measured off the 390x844 board and converted with
    /// <see cref="UITheme.F"/>. Everything from the gift down is anchored to the BOTTOM edge so the
    /// stack survives aspect ratios the board was never drawn at; only the header block counts down
    /// from the top.
    /// </summary>
    public static class LobbyScreen
    {
        // ---------- Header block, measured from the top of the safe area ----------
        private const float HeaderTop = UITheme.HeaderTopOffset;
        /// <summary>Avatar disc diameter â€” board 39.6px.</summary>
        private static readonly float ChipHeight = UITheme.F(39.6f);
        /// <summary>Board: name pill 29px tall, currency pills the same.</summary>
        private static readonly float PillHeight = UITheme.F(29f);
        /// <summary>Board: season card spans y 97..185, the header row starts at y 48.</summary>
        private static readonly float SeasonTop = HeaderTop + UITheme.F(97f - 48f);
        private static readonly float SeasonHeight = UITheme.F(88f);

        // ---------- Left column and CTA, measured up from the bottom edge ----------
        private static readonly float CtaTop = UITheme.CtaStackHeight;
        /// <summary>Board: the CLAIM banner ends at y 574.9, the CTA starts at y 616.</summary>
        private static readonly float GiftBottom = CtaTop + UITheme.F(616f - 574.9f);
        private static readonly float GiftHeight = UITheme.F(574.9f - 499f);
        private static readonly float TimerBottom = GiftBottom + GiftHeight + UITheme.F(499f - 458.6f);
        private static readonly float TimerHeight = UITheme.F(458.6f - 382f);
        /// <summary>Board: the left column is 92px wide, inset 21px from the edge.</summary>
        private static readonly float LeftColWidth = UITheme.F(92f);
        private static readonly float LeftColInset = UITheme.F(21f);

        // ---------- Character, board rect x 146..385, y 176..793 ----------
        // Bottom-anchored with everything else below the fold, so he stands on the same ground line
        // the CTA and nav are measured from rather than drifting up on a taller phone. The board
        // runs him behind both of them; that is the depth order, not an overlap to fix.
        private static readonly float CharWidth = UITheme.F(239f);
        private static readonly float CharHeight = UITheme.F(617f);
        private static readonly float CharBottom = UITheme.F(844f - 793f);
        private static readonly float CharRightInset = UITheme.F(390f - 385f);

        /// <summary>
        /// The board names the live season "SUMMER SEASON". There is no season-name field on
        /// <see cref="MetaGameState"/> to read it from â€” the meta only tracks tier and XP â€” so the
        /// label is authored here until one exists.
        /// </summary>
        private static string SeasonName => "SEASON " + MetaGameState.CurrentSeasonNumber;

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
            BuildSeasonCard(safe, mgr);
            BuildLeftColumn(safe, mgr);
            Text bags = PSKit.TextButton(safe, "YOUR BAGS", UITheme.Cream, 30, () => mgr.Show(ScreenId.BagInventory));
            PSKit.BoardRow(bags.transform.parent.gameObject, 595, 32, 100);

            PSKit.ButtonStack play = PSKit.PrimaryCta(safe, "LET'S PLAY",
                new Vector2(0, UITheme.ButtonHeight), UITheme.TypeButton,
                delegate { mgr.Show(ScreenId.PlayMode); });
            UIBuilder.StretchRect(play.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.ScreenPad, CtaTop - UITheme.ButtonHeight),
                new Vector2(-UITheme.ScreenPad, CtaTop));
            UIFadeIn.Attach(play.root, 0.26f, 0.10f, 40f);

            PSKit.BottomNav(safe, "HOME", delegate (string tab) { mgr.NavigateTab(tab); });

            BuildRewardFeedback(root, safe);
            mgr.MaybeShowDailyReward();
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
        // HEADER ROW â€” identity, coins, trophies
        // ============================================================
        private static void BuildHeaderRow(Transform safe, ScreenManager mgr)
        {
            float rowCenter = -(HeaderTop + ChipHeight * 0.5f);

            // Unread count is the number of bags waiting to be opened â€” the one thing on this
            // screen that is genuinely "new since you left".
            int ready = 0;
            foreach (BagSlot slot in MetaGameState.BagSlots)
            {
                if (slot != null && slot.state == BagSlotState.Ready) ready++;
            }

            GameObject chip = PSKit.IdentityChip(safe, MetaGameState.PlayerName,
                MetaGameState.LeagueTierName(MetaGameState.Trophies), PillHeight, UITheme.F(116f),
                ready, delegate { mgr.Show(ScreenId.Settings); });
            UIBuilder.Rect(chip, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(LeftColInset, rowCenter), new Vector2(UITheme.F(116f), ChipHeight));

            // Trophies sit outermost, coins inboard of them: the board's order, and it keeps the
            // "+" affordance away from the screen edge where a thumb rests.
            GameObject trophyPill = PSKit.CurrencyPill(safe, IconId.Trophy,
                MetaGameState.Trophies.ToString("N0"), new Vector2(UITheme.F(88f), PillHeight), null);
            UIBuilder.Rect(trophyPill, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-UITheme.F(18.8f), rowCenter), new Vector2(UITheme.F(88f), PillHeight));

            GameObject coinPill = PSKit.CurrencyPill(safe, IconId.Coin,
                MetaGameState.Coins.ToString("N0"), new Vector2(UITheme.F(99f), PillHeight),
                delegate { mgr.NavigateTab("SHOP"); });
            UIBuilder.Rect(coinPill, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-UITheme.F(18.8f + 88f + 13f), rowCenter), new Vector2(UITheme.F(99f), PillHeight));
        }

        // ============================================================
        // SEASON CARD
        // ============================================================
        private static void BuildSeasonCard(Transform safe, ScreenManager mgr)
        {
            GameObject card = UIBuilder.Child("SeasonCard", safe);
            UIBuilder.Rect(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(UITheme.F(19f), -UITheme.F(93f)), new Vector2(UITheme.F(352f), UITheme.F(96f)));
            Image skin = UIReferenceArt.Draw(card.transform, "season_card", false);
            skin.raycastTarget = true;
            Button button = card.AddComponent<Button>();
            button.targetGraphic = skin;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(delegate { mgr.Show(ScreenId.SeasonPass); });
            Text title = PSKit.Body(card.transform, "SeasonName", TextAnchor.MiddleLeft,
                SeasonName, UITheme.Cream, 34);
            UIBuilder.Rect(title.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(UITheme.F(48f), -UITheme.F(30f)), new Vector2(UITheme.F(266f), UITheme.F(20f)));
            GameObject meter = UIBuilder.Child("SeasonProgress", card.transform);
            UIBuilder.Rect(meter, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(UITheme.F(48f), -UITheme.F(51.5f)), new Vector2(UITheme.F(266f), UITheme.F(15f)));
            BuildMeter(meter, MetaGameState.SeasonTierProgress01);
            Text tier = PSKit.Body(card.transform, "Tier", TextAnchor.MiddleLeft,
                string.Format("TIER {0} \u00b7 {1:N0} / {2:N0}", MetaGameState.SeasonTier,
                    MetaGameState.SeasonXP, MetaGameState.SeasonXPPerTier), UITheme.Cream, 30);
            UIBuilder.Rect(tier.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(UITheme.F(48f), -UITheme.F(74f)), new Vector2(UITheme.F(266f), UITheme.F(20f)));
            UIBuilder.ClampLine(tier, 22);
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
        // LEFT COLUMN â€” next bag timer, then the ready gift
        // ============================================================
        private static void BuildLeftColumn(Transform safe, ScreenManager mgr)
        {
            MetaGameState.RefreshBagTimers();

            int readyIndex = -1, unlockingIndex = -1, sealedIndex = -1;
            for (int i = 0; i < MetaGameState.BagSlots.Count; i++)
            {
                BagSlot slot = MetaGameState.BagSlots[i];
                if (slot == null) continue;
                if (slot.state == BagSlotState.Ready && readyIndex < 0) readyIndex = i;
                else if (slot.state == BagSlotState.Unlocking && unlockingIndex < 0) unlockingIndex = i;
                else if (slot.state == BagSlotState.Sealed && sealedIndex < 0) sealedIndex = i;
            }

            BuildTimerCard(safe, mgr, unlockingIndex, sealedIndex);
            BuildGift(safe, mgr, readyIndex, sealedIndex);
        }

        /// <summary>
        /// The countdown card. Blaze timer over a cream two-line reward name â€” the board's shape for
        /// "something is cooking". Falls back to the next sealed bag when nothing is unlocking, so
        /// the slot never renders empty.
        /// </summary>
        private static void BuildTimerCard(Transform safe, ScreenManager mgr, int unlockingIndex, int sealedIndex)
        {
            int index = unlockingIndex >= 0 ? unlockingIndex : sealedIndex;
            if (index < 0) return;

            BagSlot slot = MetaGameState.BagSlots[index];
            PSKit.Stack card = PSKit.DarkCard(safe, "BagTimer",
                new Vector2(LeftColWidth, TimerHeight), 34);
            UIBuilder.Rect(card.root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(LeftColInset, TimerBottom), new Vector2(LeftColWidth, TimerHeight));
            UIFadeIn.Attach(card.root, 0.26f, 0.16f, 30f);

            Text timer = PSKit.Body(card.face.transform, "Timer", TextAnchor.UpperCenter,
                MetaGameState.BagTimerLabel(slot), UITheme.Blaze, 28);
            UIBuilder.StretchRect(timer.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(10, -UITheme.F(20f)), new Vector2(-10, -UITheme.F(6f)));
            UIBuilder.ClampLine(timer, 18);

            Text reward = PSKit.Body(card.face.transform, "Reward", TextAnchor.LowerRight,
                BagRewardLabel(slot), UITheme.Cream, 24);
            UIBuilder.StretchRect(reward.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.F(48f), UITheme.F(20f)), new Vector2(-UITheme.F(4f), UITheme.F(52f)));
            // "LEAGUE" is wider than the space beside the shoe at 24 and used to run into the timer
            // above it; shrink to fit rather than spill.
            UIBuilder.Clamp(reward, 16);

            Image shoe = UIReferenceArt.Draw(card.root.transform, "shoe");
            UIBuilder.Rect(shoe.gameObject, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(UITheme.F(2f), UITheme.F(5f)), new Vector2(UITheme.F(55f), UITheme.F(53f)));

            Button btn = card.root.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = card.root.GetComponent<Image>();
            int captured = index;
            btn.onClick.AddListener(delegate { mgr.Show(ScreenId.BagInventory); });
        }

        /// <summary>
        /// The gift: a gold box on a dark plinth with a blaze CLAIM banner straddling its bottom
        /// edge. Only drawn when there is genuinely something to open â€” an empty gift with a live
        /// CLAIM button is the one thing on a home screen that must never lie.
        /// </summary>
        private static void BuildGift(Transform safe, ScreenManager mgr, int readyIndex, int sealedIndex)
        {
            bool hasReady = readyIndex >= 0;
            bool canClaimDaily = MetaGameState.CanClaimDailyReward;
            if (!hasReady && !canClaimDaily) return;

            GameObject host = UIBuilder.Child("Gift", safe);
            UIBuilder.Rect(host, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(LeftColInset, GiftBottom), new Vector2(LeftColWidth, GiftHeight));
            UIFadeIn.Attach(host, 0.26f, 0.22f, 30f);

            // Dark plinth, gift on it, CLAIM straddling its bottom edge â€” the board's three layers.
            // The plinth stops short of the bottom so the banner has somewhere to sit without
            // covering the gift itself.
            float plinthHeight = GiftHeight * 0.62f;
            PSKit.Stack plinth = PSKit.DarkCard(host.transform, "Plinth",
                new Vector2(LeftColWidth * 0.88f, plinthHeight), 30);
            UIBuilder.Rect(plinth.root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -GiftHeight * 0.10f), new Vector2(LeftColWidth * 0.88f, plinthHeight));

            Image gift = UIReferenceArt.Draw(host.transform, "gift");
            UIBuilder.Rect(gift.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(UITheme.F(82f), UITheme.F(74f)));

            // Tall enough for the compact button type UIButtonTypography enforces (32): at the
            // board's 20px the face was ~35 units high, the line didn't fit, and Truncate drew nothing.
            float claimHeight = UITheme.F(24f);
            PSKit.ButtonStack claim = PSKit.PressStack(host.transform, "Claim",
                new Vector2(LeftColWidth, claimHeight), 18, false,
                UITheme.BlazeDeep, UITheme.Blaze, UITheme.SheenBlaze,
                delegate { if (hasReady) mgr.Show(ScreenId.BagInventory); else mgr.MaybeShowDailyReward(); });
            claim.label = PSKit.Display(claim.stack.face.transform, "Label", TextAnchor.MiddleCenter,
                "CLAIM", UITheme.Cream, 26);
            UIBuilder.Fill(claim.label.gameObject);
            UIButtonTypography.Attach(claim.label, true);
            UIBuilder.Rect(claim.root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 0), new Vector2(LeftColWidth, claimHeight));
            claim.root.AddComponent<UIPulse>();
        }

        /// <summary>Two-line reward hint for the timer card â€” the board shows "FREE / SHOES".</summary>
        private static string BagRewardLabel(BagSlot slot)
        {
            if (slot == null) return "FREE\nBAG";
            switch (slot.bagId)
            {
                case "legendary_chest": return "FREE\nLEGEND";
                case "epic_chest":      return "FREE\nEPIC";
                case "league_chest":    return "LEAGUE\nCHEST";
                case "tour_crate":      return "TOUR\nCRATE";
                default:                return "FREE\nBAG";
            }
        }

        // ============================================================
        // REWARD FEEDBACK BANNER
        // ============================================================
        private static void BuildRewardFeedback(GameObject screenRoot, Transform safe)
        {
            RewardBundle bundle = MetaGameState.ConsumeHomeRewardFeedback();
            if (bundle == null) return;

            int coins = 0, gems = 0, cards = 0;
            foreach (RewardEntry reward in bundle.entries)
            {
                if (reward.icon == IconId.Coin) coins += reward.amount;
                else if (reward.icon == IconId.Gem) gems += reward.amount;
                else cards += reward.amount;
            }

            string bannerTitle = string.IsNullOrEmpty(bundle.chestName)
                ? "BAG REWARDS ADDED" : bundle.chestName;

            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
            if (coins > 0) parts.Add("+" + coins + " COINS");
            if (gems > 0) parts.Add("+" + gems + " GEMS");
            if (cards > 0) parts.Add("+" + cards + " CARDS");
            string totalsText = parts.Count > 0 ? string.Join("   ", parts.ToArray()) : "REWARDS ADDED";

            PSKit.Stack banner = PSKit.BuildStack(safe, "RewardApplied", new Vector2(0, 150),
                UITheme.RadiusPanel, false, UITheme.VoltDeep, UITheme.Volt, UITheme.SheenVolt);
            UIBuilder.StretchRect(banner.root, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(UITheme.ScreenPad, -(SeasonTop + SeasonHeight + 174f)),
                new Vector2(-UITheme.ScreenPad, -(SeasonTop + SeasonHeight + 24f)));

            Text title = PSKit.Body(banner.face.transform, "Title", TextAnchor.UpperCenter,
                bannerTitle, UITheme.Ink1, 30);
            UIBuilder.Rect(title.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -16), new Vector2(-40, 40));
            UIBuilder.ClampLine(title, 20);

            Text totals = PSKit.Body(banner.face.transform, "Totals", TextAnchor.MiddleCenter,
                totalsText, UITheme.Ink1, 26);
            UIBuilder.Rect(totals.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 18), new Vector2(-40, 44));
            UIBuilder.ClampLine(totals, 16);

            UIFadeIn.Attach(banner.root, 0.32f, 0.15f, 60f, 0.92f);
            UIBuilder.RunOnScreen(screenRoot, DismissRewardBanner(banner.root));
        }

        private static IEnumerator DismissRewardBanner(GameObject banner)
        {
            yield return new WaitForSecondsRealtime(3.2f);
            if (banner != null) Object.Destroy(banner);
        }
    }
}

