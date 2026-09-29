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
    /// Screen 10 — gear detail and upgrade. The whole screen takes the rarity colour so there is no
    /// doubt what tier this item is. One decision: spend or leave.
    ///
    /// Laid out from the bottom up — the upgrade CTA is the point of the screen and must never be
    /// the thing that falls off a short display.
    /// </summary>
    public static class GearDetailScreen
    {
        private const float EquippedTop = 24f + UITheme.ButtonHeightGhost;
        private const float CtaBottom = EquippedTop + 18f;
        private const float CtaTop = CtaBottom + UITheme.ButtonHeight;
        private const float UpgradeBottom = CtaTop + 22f;
        private const float UpgradeHeight = 318f;
        private const float UpgradeTop = UpgradeBottom + UpgradeHeight;
        private const float CollectedBottom = UpgradeTop + 16f;
        private const float CollectedHeight = 148f;
        private const float CollectedTop = CollectedBottom + CollectedHeight;

        public static GameObject Build(Transform parent, ScreenManager mgr, GearItem item)
        {
            GameObject root = UIBuilder.Child("GearDetailScreen", parent);
            UIBuilder.Fill(root);

            if (item == null)
            {
                UIBuilder.SkyBackground(root.transform, UITheme.SkyTop, UITheme.SkyMid);
                Transform emptySafe = UIBuilder.SafeArea(root.transform);
                UIBuilder.TopBar(emptySafe, "GEAR", delegate { mgr.NavigateTab("GEAR"); }, IconId.None, null);
                Text empty = UIBuilder.Text(emptySafe, "Empty", TextAnchor.MiddleCenter, "No item selected.", UITheme.InkOnLight, 32, FontStyle.Bold);
                UIBuilder.Fill(empty.gameObject);
                return root;
            }

            Color rTop, rBottom;
            UIBuilder.RarityGradient(item.rarity, out rTop, out rBottom);
            Color skyBottom = new Color(rBottom.r * 0.5f, rBottom.g * 0.5f, rBottom.b * 0.5f, 1f);
            UIBuilder.SkyBackground(root.transform, rTop, skyBottom);
            Transform safe = UIBuilder.SafeArea(root.transform);

            UIBuilder.TopBar(safe, item.type.ToString().ToUpperInvariant(), delegate { mgr.GoBack(); }, IconId.Coin, MetaGameState.Coins.ToString("N0"));

            BuildHero(safe, item, rTop);
            BuildCollectedPanel(safe, item, rTop);
            BuildUpgradePanel(safe, item);
            BuildActions(safe, mgr, item);

            return root;
        }

        // ============================================================
        private static void BuildHero(Transform safe, GearItem item, Color rTop)
        {
            GameObject hero = UIBuilder.Child("Hero", safe);
            UIBuilder.StretchRect(hero, Vector2.zero, Vector2.one, new Vector2(0, CollectedTop + 12f), new Vector2(0, -UITheme.ContentTopInset));

            // The item art sits on a rarity-tinted disc so it has a ground to stand on instead of
            // floating as a lone glyph on the gradient.
            GameObject disc = UIBuilder.Child("Disc", hero.transform);
            UIBuilder.Rect(disc, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 44), new Vector2(300, 300));
            disc.AddComponent<Image>();
            UIBuilder.OutlinedGradientFill(disc, UIBuilder.CircleSprite, new Color(1f, 1f, 1f, 0.22f), new Color(1f, 1f, 1f, 0.05f), new Color(1f, 1f, 1f, 0.35f), 6f);
            UIBuilder.IconAt(disc.transform, item.icon, 200f, Color.white, UITheme.OutlineNavy, new Vector2(0.5f, 0.5f), Vector2.zero);

            Text name = UIBuilder.StrokedText(hero.transform, "Name", TextAnchor.MiddleCenter, item.name, Color.white, 56);
            UIBuilder.Rect(name.gameObject, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -140), new Vector2(-120, 76));
            UIBuilder.ClampLine(name);

            GameObject badge = UIBuilder.Chip(hero.transform, UIBuilder.RarityLabel(item.rarity) + " · LEVEL " + item.level, rTop, 58f, 25);
            UIBuilder.Rect(badge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -206), badge.GetComponent<RectTransform>().sizeDelta);
        }

        private static void BuildCollectedPanel(Transform safe, GearItem item, Color rTop)
        {
            GameObject panel = UIBuilder.Panel(safe, "CollectedPanel", new Vector2(0, CollectedHeight), UITheme.PanelDarkTop, UITheme.PanelDarkDeep);
            UIBuilder.StretchRect(panel, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceXl, CollectedBottom), new Vector2(-UITheme.SpaceXl, CollectedTop));

            Text label = UIBuilder.Text(panel.transform, "Label", TextAnchor.MiddleLeft, "CARDS COLLECTED", new Color(1, 1, 1, 0.88f), 24, FontStyle.Bold);
            UIBuilder.Rect(label.gameObject, new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(0f, 1f), new Vector2(28, -22), new Vector2(0, 34));

            bool maxLevel = MetaGameState.IsMaxLevel(item);
            // cardsNeeded is int.MaxValue at max level (GearCatalog.ApplyLevel) -- there is no next
            // rung to show progress toward, so the panel reads MAXED rather than "24 / 2,147,483,647".
            Text value = UIBuilder.Text(panel.transform, "Value", TextAnchor.MiddleRight,
                maxLevel ? "MAXED" : item.cardsCollected + " / " + item.cardsNeeded, rTop, 24, FontStyle.Bold);
            UIBuilder.Rect(value.gameObject, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28, -22), new Vector2(0, 34));

            GameObject meterHost = UIBuilder.Child("MeterHost", panel.transform);
            UIBuilder.Rect(meterHost, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(-56, 32));
            UIBuilder.MeterStretch(meterHost.transform, maxLevel ? 1f : (float)item.cardsCollected / item.cardsNeeded, rTop);
        }

        private static void BuildUpgradePanel(Transform safe, GearItem item)
        {
            GameObject panel = UIBuilder.Panel(safe, "UpgradePanel", new Vector2(0, UpgradeHeight), UITheme.PanelTop, UITheme.PanelDeep);
            UIBuilder.StretchRect(panel, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceXl, UpgradeBottom), new Vector2(-UITheme.SpaceXl, UpgradeTop));
            UIBuilder.ShineLayer(panel.transform, 0.34f);

            bool maxed = MetaGameState.IsMaxLevel(item);

            Text title = UIBuilder.Text(panel.transform, "Title", TextAnchor.MiddleLeft,
                maxed ? "LEVEL " + item.level + " · MAXED" : "LEVEL " + item.level + " → " + (item.level + 1),
                Color.white, 28, FontStyle.Bold);
            UIBuilder.Rect(title.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(28, -24), new Vector2(-56, 40));

            if (maxed)
            {
                Text maxedText = UIBuilder.Text(panel.transform, "MaxedText", TextAnchor.MiddleLeft,
                    "This item has reached its highest level.", new Color(1, 1, 1, 0.75f), 22, FontStyle.Normal);
                UIBuilder.Rect(maxedText.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(28, -84), new Vector2(-56, 60));
                return;
            }

            // Each slot only ever carries 3 of the game's 6 stats (Docs/GearProgression.md#2-slots) --
            // these are the 3 that item.type actually uses, not a fixed POWER/SPIN/SERVE regardless of
            // slot. "Next" is a pure preview one level ahead via the same curve TryUpgradeGear will
            // apply, not a duplicated flat delta.
            string[] labels = GearProgressionCurve.DisplayLabels(item.type);
            int[] currents = GearProgressionCurve.DisplayValues(item.type, GearProgressionCurve.StatsOf(item));
            int[] nextValues = GearProgressionCurve.DisplayValues(item.type, GearProgressionCurve.ComputeStats(item.type, item.tier, item.level + 1));
            float rowY = 92f;

            for (int i = 0; i < labels.Length; i++)
            {
                Text lbl = UIBuilder.Text(panel.transform, "Lbl" + i, TextAnchor.MiddleLeft, labels[i], new Color(1, 1, 1, 0.9f), 21, FontStyle.Bold);
                UIBuilder.Rect(lbl.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28, -rowY), new Vector2(140, 34));

                GameObject tickHost = UIBuilder.Child("TickHost" + i, panel.transform);
                UIBuilder.Rect(tickHost, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -rowY - 4), new Vector2(-336, 26));
                int filled = Mathf.Clamp(currents[i] / 10, 0, 10);
                int incoming = Mathf.Clamp(nextValues[i] / 10 - filled, 0, 10 - filled);
                UIBuilder.Ticks(tickHost.transform, 10, filled, incoming, UITheme.TickFilled, UITheme.FillActionGreen);

                Text value = UIBuilder.Text(panel.transform, "Val" + i, TextAnchor.MiddleRight, currents[i] + " → " + nextValues[i], UITheme.FillActionGreen, 25, FontStyle.Bold);
                UIBuilder.Rect(value.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28, -rowY), new Vector2(180, 34));
                rowY += 76f;
            }
        }

        private static void BuildActions(Transform safe, ScreenManager mgr, GearItem item)
        {
            bool maxLevel = MetaGameState.IsMaxLevel(item);
            bool canAfford = !maxLevel && MetaGameState.Coins >= item.upgradeCostCoins;
            bool hasCards = !maxLevel && item.cardsCollected >= item.cardsNeeded;
            bool canUpgrade = MetaGameState.CanUpgrade(item);

            UIBuilder.ButtonRefs upgradeBtn = canUpgrade
                ? UIBuilder.GreenButton(safe, "UPGRADE", new Vector2(0, UITheme.ButtonHeight), UITheme.TypeButton, delegate
                {
                    if (MetaGameState.TryUpgradeGear(item))
                    {
                        mgr.ShowGearUpgradeReveal(item);
                    }
                })
                : UIBuilder.GhostButton(safe, maxLevel ? "MAX LEVEL" : (hasCards ? "NEED MORE COINS" : "NEED MORE CARDS"), new Vector2(0, UITheme.ButtonHeight), UITheme.TypeButtonSm,
                    delegate { if (!canAfford && !maxLevel) mgr.Show(ScreenId.Shop); });

            UIBuilder.StretchRect(upgradeBtn.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceXl, CtaBottom), new Vector2(-UITheme.SpaceXl, CtaTop));

            Transform btnContent = upgradeBtn.root.transform.Find("Content");

            // Price sits inside the CTA as an icon + number pair, and lifts the main label so the
            // two do not overlap.
            UIBuilder.Rect(upgradeBtn.label.gameObject, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, 16), Vector2.zero);

            if (!maxLevel)
            {
                GameObject costRow = UIBuilder.Child("Cost", btnContent);
                UIBuilder.Rect(costRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(230, 32));
                UIBuilder.IconAt(costRow.transform, IconId.Coin, 28f, canAfford ? UITheme.GoldTop : new Color(1f, 1f, 1f, 0.6f), UITheme.OutlineNavy, new Vector2(0.5f, 0.5f), new Vector2(-44, 0));
                Text cost = UIBuilder.Text(costRow.transform, "Text", TextAnchor.MiddleLeft, item.upgradeCostCoins.ToString("N0"),
                    canAfford ? new Color(1, 1, 1, 0.95f) : new Color(1f, 1f, 1f, 0.7f), 23, FontStyle.Bold);
                UIBuilder.Rect(cost.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-24, 0), new Vector2(140, 30));
            }

            bool equipped = MetaGameState.IsEquipped(item);
            UIBuilder.ButtonRefs equippedBtn = equipped
                ? UIBuilder.GhostButton(safe, "", new Vector2(0, UITheme.ButtonHeightGhost), UITheme.TypeButtonSm, null)
                : UIBuilder.GoldButton(safe, "EQUIP", new Vector2(0, UITheme.ButtonHeightGhost), UITheme.TypeButtonSm, delegate
                {
                    MetaGameState.Equip(item);
                    mgr.RefreshGearDetail(item);
                });
            UIBuilder.StretchRect(equippedBtn.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceXl, 24), new Vector2(-UITheme.SpaceXl, EquippedTop));
            equippedBtn.button.interactable = !equipped;
            equippedBtn.label.gameObject.SetActive(!equipped);

            Transform eqContent = equippedBtn.root.transform.Find("Content");
            if (equipped)
            {
                GameObject eqRow = UIBuilder.Child("EquippedRow", eqContent);
                UIBuilder.Centered(eqRow, new Vector2(400, 60));
                UIBuilder.IconAt(eqRow.transform, IconId.Check, 32f, UITheme.FillActionGreen, UITheme.OutlineNavy, new Vector2(0f, 0.5f), new Vector2(48, 0));
                Text eqText = UIBuilder.StrokedText(eqRow.transform, "Text", TextAnchor.MiddleLeft, "EQUIPPED", Color.white, UITheme.TypeButtonSm);
                UIBuilder.Rect(eqText.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(76, 0), new Vector2(300, 60));
                UIButtonTypography.Attach(eqText);
            }
        }
    }
}
#endif
