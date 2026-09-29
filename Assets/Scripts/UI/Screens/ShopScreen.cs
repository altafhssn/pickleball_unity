// Retired from the launch build: this feature is outside the scope in Docs/LaunchScope.md
// (Pickleball Game Design Document v1.0). The source is kept for reference and is not compiled;
// PICKLEBALL_RETIRED_FEATURES is deliberately never defined.
#if PICKLEBALL_RETIRED_FEATURES
using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;

namespace Pickleball.UI
{
    /// <summary>
    /// Screen 13 — shop. One featured offer on a hot-orange panel, a daily-deal row reusing the gear
    /// card, and gem packs. No shop interstitial after defeat — entry is the tab and the contextual
    /// plus button on the lobby balances only.
    ///
    /// The whole catalogue now lives in a scroll view. It used to be pinned to absolute offsets down
    /// to y = -1600, so the gem row was already clipped at 19.5:9 and gone entirely on anything
    /// squarer, and there was no way to reach it.
    /// </summary>
    public static class ShopScreen
    {
        private static readonly IconId[] DealIcons = { IconId.Paddle, IconId.Shoe, IconId.Chest };
        private static readonly string[] DealNames = { "PADDLE x10", "SHOES x20", "EPIC CHEST" };
        private static readonly int[] DealCoinCost = { 900, 400, 0 };
        private static readonly int[] DealGemCost = { 0, 0, 90 };
        private static readonly Rarity[] DealRarities = { Rarity.Rare, Rarity.Common, Rarity.Epic };
        // Coin deals grant cards toward the player's equipped item in this slot; the count matches the
        // deal name ("PADDLE x10" = 10 paddle cards). Index 2 is the Epic Chest and uses neither.
        private static readonly GearType[] DealCardSlot = { GearType.Paddle, GearType.Shoes, GearType.Paddle };
        private static readonly int[] DealCardAmount = { 10, 20, 0 };

        private static readonly int[] GemAmounts = { 80, 500, 1200 };
        private static readonly string[] GemPrices = { "$0.99", "$4.99", "$9.99" };

        public static GameObject Build(Transform parent, ScreenManager mgr)
        {
            GameObject root = UIBuilder.Child("ShopScreen", parent);
            UIBuilder.Fill(root);
            PSKit.Backdrop(root.transform);
            Transform safe = UIBuilder.SafeArea(root.transform);

            BuildHeader(safe);

            RectTransform content;
            GameObject scroll = UIBuilder.ScrollView(safe, out content);
            UIBuilder.StretchRect(scroll, Vector2.zero, Vector2.one,
                new Vector2(UITheme.SpaceLg, UITheme.NavBarHeight + 10f), new Vector2(-UITheme.SpaceLg, -(UITheme.ContentTopInset + 46f)));

            float y = 0f;
            y += BuildFeatured(content, mgr, y);
            y += BuildSectionHeader(content, y, "DAILY DEALS", "RESETS 6h 14m");
            y += BuildDeals(content, mgr, y);
            y += BuildSectionHeader(content, y, "PREMIUM", "1 GUARANTEED LEGENDARY");
            y += BuildLegendarySection(content, mgr, y);
            y += BuildSectionHeader(content, y, "GEMS", null);
            y += BuildGemPacks(content, mgr, y);
            content.sizeDelta = new Vector2(0, y + 16f);

            UIBuilder.BottomNav(safe, "SHOP", delegate (string t) { mgr.NavigateTab(t); });
            return root;
        }

        private static void BuildHeader(Transform safe)
        {
            float barH = UITheme.TouchMin + 8;
            GameObject bar = UIBuilder.Child("TopBar", safe);
            UIBuilder.Rect(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -UITheme.HeaderTopOffset), new Vector2(-2 * UITheme.SpaceLg, barH));

            // Two balances sit side by side on one line instead of stacking two pills down the right
            // edge, where the lower one overlapped the featured card below.
            GameObject ribbon = UIBuilder.Ribbon(bar.transform, "SHOP", 0, barH, UITheme.TypeScreenTitle);
            UIBuilder.StretchRect(ribbon, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-404, 0));

            GameObject coinPill = UIBuilder.CurrencyPill(bar.transform, IconId.Coin, MetaGameState.Coins.ToString("N0"));
            UIBuilder.Rect(coinPill, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-202, 0), new Vector2(190, 66));

            GameObject gemPill = UIBuilder.CurrencyPill(bar.transform, IconId.Gem, MetaGameState.Gems.ToString("N0"));
            UIBuilder.Rect(gemPill, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4, 0), new Vector2(190, 66));
        }

        // ============================================================
        private static float BuildFeatured(Transform content, ScreenManager mgr, float y)
        {
            const float h = 420f;

            GameObject featured = UIBuilder.Panel(content, "Featured", new Vector2(0, h), UITheme.Bubblegum, UITheme.BubbleDeep);
            UIBuilder.StretchRect(featured, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -(y + h)), new Vector2(0, -y));
            UIBuilder.ShineLayer(featured.transform, 0.38f);

            GameObject tag = UIBuilder.Chip(featured.transform, "70% OFF", UITheme.Blaze, 46f, 20);
            UIBuilder.Rect(tag, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18, -18), tag.GetComponent<RectTransform>().sizeDelta);

            Text kicker = UIBuilder.Text(featured.transform, "Kicker", TextAnchor.MiddleLeft, "STARTER BUNDLE", UITheme.Cream, 22, FontStyle.Bold);
            UIBuilder.Rect(kicker.gameObject, new Vector2(0f, 1f), new Vector2(0.62f, 1f), new Vector2(0f, 1f), new Vector2(30, -34), new Vector2(-30, 30));

            Text title = UIBuilder.StrokedText(featured.transform, "Title", TextAnchor.MiddleLeft, "MIAMI PRO PACK", Color.white, 44);
            UIBuilder.Rect(title.gameObject, new Vector2(0f, 1f), new Vector2(0.62f, 1f), new Vector2(0f, 1f), new Vector2(30, -76), new Vector2(-30, 60));
            UIBuilder.ClampLine(title);

            // Clears the title block, which runs to -136.
            Text desc = UIBuilder.Text(featured.transform, "Desc", TextAnchor.UpperLeft, "1 LEGENDARY PADDLE", UITheme.Cream, 22, FontStyle.Bold);
            UIBuilder.Rect(desc.gameObject, new Vector2(0f, 1f), new Vector2(0.62f, 1f), new Vector2(0f, 1f), new Vector2(30, -144), new Vector2(-30, 30));

            // Bundle contents as icon + amount pairs — the old version wrote them as emoji inline in
            // the string, which rendered as "2,500 · 120" with two holes in it.
            GameObject contents = UIBuilder.Child("Contents", featured.transform);
            UIBuilder.Rect(contents, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30, -186), new Vector2(320, 44));
            UIBuilder.IconAt(contents.transform, IconId.Coin, 32f, UITheme.Gold, UITheme.Outline, new Vector2(0f, 0.5f), new Vector2(16, 0));
            Text coins = UIBuilder.StrokedText(contents.transform, "Coins", TextAnchor.MiddleLeft, "2,500", Color.white, 24);
            UIBuilder.Rect(coins.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(38, 0), new Vector2(110, 30));
            UIBuilder.IconAt(contents.transform, IconId.Gem, 32f, UITheme.Cream, UITheme.Outline, new Vector2(0f, 0.5f), new Vector2(160, 0));
            Text gems = UIBuilder.StrokedText(contents.transform, "Gems", TextAnchor.MiddleLeft, "120", Color.white, 24);
            UIBuilder.Rect(gems.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(182, 0), new Vector2(110, 30));

            UIBuilder.IconAt(featured.transform, IconId.Paddle, 190f, Color.white, UITheme.Outline, new Vector2(1f, 1f), new Vector2(-108, -140));

            // Real-money IAP. There is no payment flow (Unity IAP isn't wired) and nothing recorded
            // the purchase, so this used to grant a legendary paddle + 2,500 coins + 120 gems for
            // free, every time the button was tapped (audit 2026-08-27). Fails closed until IAP exists.
            UIBuilder.ButtonRefs buy = UIBuilder.GhostButton(featured.transform, "$4.99", new Vector2(0, 100), 34, delegate
            {
                mgr.ShowStoreUnavailable("Miami Pro Pack");
            });
            UIBuilder.StretchRect(buy.root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(26, 24), new Vector2(-26, 124));
            MarkComingSoon(buy.root);

            return h + 34f;
        }

        private static float BuildSectionHeader(Transform content, float y, string title, string note)
        {
            const float h = 48f;

            // Navy on the pale sky rather than white — "DAILY DEALS" in white measured about 1.4:1
            // against #9DE6FF and effectively disappeared.
            Text t = UIBuilder.Text(content, title + "Title", TextAnchor.MiddleLeft, title, UITheme.InkOnLight, 30, FontStyle.Bold);
            UIBuilder.Rect(t.gameObject, new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(0f, 1f), new Vector2(4, -y), new Vector2(0, h));

            if (!string.IsNullOrEmpty(note))
            {
                GameObject chip = UIBuilder.Chip(content, note, UITheme.PanelDarkDeep, 46f, 20);
                UIBuilder.Rect(chip, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4, -y - 1), chip.GetComponent<RectTransform>().sizeDelta);
            }

            return h + 10f;
        }

        private static float BuildDeals(Transform content, ScreenManager mgr, float y)
        {
            const float h = 306f;

            GameObject row = UIBuilder.Child("DealsRow", content);
            UIBuilder.StretchRect(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -(y + h)), new Vector2(0, -y));

            float slot = 1f / 3f;
            for (int i = 0; i < 3; i++)
            {
                GameObject cell = UIBuilder.Child("Deal" + i, row.transform);
                UIBuilder.StretchRect(cell, new Vector2(i * slot, 0f), new Vector2((i + 1) * slot, 1f), new Vector2(8, 0), new Vector2(-8, 0));

                GameObject card = UIBuilder.GearCard(cell.transform, new Vector2(300, h), DealIcons[i], DealNames[i], DealRarities[i], -1f);
                UIBuilder.Fill(card);

                // Captured per-iteration: `i` is the shared for-loop variable, so a delegate that
                // closes over it directly sees its final value (3) at click time -- which is exactly
                // why the old `if (i == 2)` Epic Chest grant below never fired.
                int idx = i;
                int coinCost = DealCoinCost[i];
                int gemCost = DealGemCost[i];
                bool costsGems = gemCost > 0;
                bool affordable = costsGems ? MetaGameState.Gems >= gemCost : MetaGameState.Coins >= coinCost;

                GameObject buyHost = UIBuilder.Child("BuyHost", card.transform);
                UIBuilder.Rect(buyHost, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(-22, 70));

                UIBuilder.ButtonRefs buy = affordable
                    ? UIBuilder.GoldButton(buyHost.transform, "", new Vector2(0, 70), 24, delegate
                    {
                        if (costsGems)
                        {
                            // idx == 2 is specifically the EPIC CHEST deal (DealGemCost is only nonzero
                            // there) -- gated explicitly rather than "any gem-cost deal grants a chest"
                            // so a future non-chest gem deal doesn't silently start granting one too.
                            if (MetaGameState.Gems >= gemCost)
                            {
                                MetaGameState.AddGems(-gemCost);
                                if (idx == 2) MetaGameState.AddEpicChest();
                                mgr.NavigateTab("HOME");
                            }
                        }
                        else if (MetaGameState.SpendCoins(coinCost))
                        {
                            // Was: spent coins and granted nothing -- no gear id was ever associated
                            // with these deals (Docs/GearProgression.md#16). Grants the advertised
                            // card count toward the player's equipped item in the deal's slot.
                            GearItem slotItem = MetaGameState.EquippedOrDefault(DealCardSlot[idx]);
                            RewardBundle bundle = new RewardBundle { chestName = "DEAL CLAIMED" };
                            if (slotItem != null)
                            {
                                bundle.entries.Add(new RewardEntry
                                {
                                    icon = slotItem.icon, name = slotItem.name, rarity = slotItem.rarity,
                                    amount = DealCardAmount[idx], gearId = slotItem.id
                                });
                            }
                            MetaGameState.GrantStoreBundle(bundle);
                            mgr.NavigateTab("HOME");
                        }
                    })
                    : UIBuilder.GhostButton(buyHost.transform, "", new Vector2(0, 70), 24, delegate { mgr.NavigateTab("SHOP"); });
                UIBuilder.Fill(buy.root);
                buy.label.gameObject.SetActive(false);

                // Price rendered as icon + number so the currency is unambiguous. Both are inked in
                // the dark GoldInk on the gold button face — a white or pale-cyan price on gold was
                // barely legible, which is the one number on the card that has to be.
                Transform btnContent = buy.root.transform.Find("Content");
                Color priceInk = affordable ? UITheme.GoldInk : new Color(1f, 1f, 1f, 0.75f);
                Color priceHalo = affordable ? new Color(1f, 1f, 1f, 0.55f) : UITheme.OutlineNavy;

                GameObject priceRow = UIBuilder.Child("Price", btnContent);
                UIBuilder.Centered(priceRow, new Vector2(180, 40));
                UIBuilder.IconAt(priceRow.transform, costsGems ? IconId.Gem : IconId.Coin, 32f, priceInk, priceHalo,
                    new Vector2(0f, 0.5f), new Vector2(30, 0));
                Text price = UIBuilder.Text(priceRow.transform, "Text", TextAnchor.MiddleLeft, (costsGems ? gemCost : coinCost).ToString("N0"), priceInk, 27, FontStyle.Bold);
                UIBuilder.Rect(price.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(52, 0), new Vector2(120, 40));
                UIButtonTypography.Attach(price, true);
            }

            return h + 34f;
        }

        // Legendary Chest (Docs/GearProgression.md#6-acquisition-b: "Legendary Chest (IAP)") is real
        // money like the Featured bundle above, not gems like Epic Chest below it -- ChestCatalog's
        // legendary_chest already guarantees its rarity outright (guaranteesLegendary), so this is the
        // shop's single top-tier standing offer rather than a daily-reset deal. It gets its own section
        // rather than a 4th slot in BuildDeals: that row is hardcoded to exactly 3 equal-width columns,
        // and a guaranteed-Legendary purchase reads as a different tier of offer than a coin/gem deal
        // that resets every 6 hours, not one more item in that row.
        private static float BuildLegendarySection(Transform content, ScreenManager mgr, float y)
        {
            const float h = 306f;

            GameObject row = UIBuilder.Child("LegendaryRow", content);
            UIBuilder.StretchRect(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -(y + h)), new Vector2(0, -y));

            // Same 300px card width BuildDeals uses -- GearCard sizes its internal text/icons as
            // fractions of the `size` passed in, not the final rendered width, so reusing that width
            // keeps this card's proportions identical to the daily-deal cards instead of guessing new
            // ones for a wider card. Centered rather than filling the row: one offer, not three.
            GameObject cell = UIBuilder.Child("LegendaryCell", row.transform);
            UIBuilder.Rect(cell, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, h));

            GameObject card = UIBuilder.GearCard(cell.transform, new Vector2(300, h), IconId.Chest, "LEGENDARY CHEST", Rarity.Legendary, -1f);
            card.transform.Find("Rarity").gameObject.SetActive(false);
            UIBuilder.Fill(card);

            // Same button geometry as a gem pack's buy button (14px side margins, 74px tall, 14px off
            // the bottom) -- a plain "$9.99" label needs no icon/price split the way DAILY DEALS' gem
            // and coin costs do, so GoldButton's own label is used directly instead of DealsRow's
            // hidden-label-plus-custom-price-row trick.
            UIBuilder.ButtonRefs buy = UIBuilder.GhostButton(card.transform, "$9.99", new Vector2(0, 74), 26, delegate
            {
                // Real-money IAP -- see the Featured bundle above. Used to grant a guaranteed
                // Legendary chest for free on every tap.
                mgr.ShowStoreUnavailable("Legendary Chest");
            });
            UIBuilder.StretchRect(buy.root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14, 14), new Vector2(-14, 88));
            UIBuilder.ClampLine(buy.label);
            MarkComingSoon(buy.root);

            return h + 34f;
        }

        private static float BuildGemPacks(Transform content, ScreenManager mgr, float y)
        {
            const float h = 360f;

            GameObject row = UIBuilder.Child("GemRow", content);
            UIBuilder.StretchRect(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -(y + h)), new Vector2(0, -y));

            float slot = 1f / 3f;
            for (int i = 0; i < 3; i++)
            {
                GameObject cell = UIBuilder.Child("Gem" + i, row.transform);
                // The "best value" pack needs headroom for its badge, so its cell starts lower.
                UIBuilder.StretchRect(cell, new Vector2(i * slot, 0f), new Vector2((i + 1) * slot, 1f), new Vector2(8, 0), new Vector2(-8, i == 1 ? -18 : 0));

                GameObject pack = UIBuilder.Panel(cell.transform, "GemPack" + i, new Vector2(0, 0), UITheme.PanelTop, UITheme.PanelDeep);
                UIBuilder.Fill(pack);
                UIBuilder.ShineLayer(pack.transform, 0.36f);

                if (i == 1)
                {
                    // Anchored inside the panel's top edge. It used to hang above it and was clipped
                    // by the section above.
                    GameObject best = UIBuilder.Chip(pack.transform, "+60%", UITheme.GoldMid, 42f, 18);
                    best.transform.Find("Text").GetComponent<Text>().color = UITheme.GoldInk;
                    UIBuilder.Rect(best, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -8), best.GetComponent<RectTransform>().sizeDelta);
                }

                UIBuilder.IconAt(pack.transform, IconId.Gem, 92f, UITheme.Volt, UITheme.Outline, new Vector2(0.5f, 1f), new Vector2(0, i == 1 ? -112 : -84));

                Text amount = UIBuilder.StrokedText(pack.transform, "Amt", TextAnchor.MiddleCenter, GemAmounts[i].ToString("N0"), Color.white, 38);
                UIBuilder.Rect(amount.gameObject, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -34), new Vector2(-16, 46));
                UIBuilder.ClampLine(amount);

                // Real-money IAP -- see the Featured bundle. Used to add the gem amount for free on
                // every tap, which trivially defeated every gem sink in the game.
                string packName = GemAmounts[i].ToString("N0") + " gems";
                UIBuilder.ButtonRefs buy = UIBuilder.GhostButton(pack.transform, GemPrices[i], new Vector2(0, 74), 26, delegate
                {
                    mgr.ShowStoreUnavailable(packName);
                });
                UIBuilder.StretchRect(buy.root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14, 14), new Vector2(-14, 88));
                UIBuilder.ClampLine(buy.label);
                MarkComingSoon(buy.root);
            }

            return h + 20f;
        }

        // The $-priced packs have no IAP flow behind them yet. They stay tappable so the tap can
        // explain why (ScreenManager.ShowStoreUnavailable), but they drop the full-gold "buy me" face
        // for a muted button dimmed to 60% with a COMING SOON badge above it -- so the shop doesn't
        // read as if it takes card payments today. Drop this once Unity IAP is wired.
        private static void MarkComingSoon(GameObject buyRoot)
        {
            CanvasGroup cg = buyRoot.GetComponent<CanvasGroup>();
            if (cg == null) cg = buyRoot.AddComponent<CanvasGroup>();
            cg.alpha = 0.6f;

            // Keep availability inside the action instead of floating over the product name.
            Text label = buyRoot.GetComponentInChildren<UIButtonTypography>().GetComponent<Text>();
            label.text += " \u00b7 SOON";
        }

        private static Color Hex(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f);
        }
    }
}
#endif
