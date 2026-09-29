using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;
using Sim = Pickleball.Sim;

namespace Pickleball.UI
{
    /// <summary>
    /// Gear: the three stat slots and the outfit. Each slot improves exactly one passive stat -- the
    /// paddle Power, the shoes Speed, the grip Accuracy -- and is upgraded with coins, one level at a
    /// time, right here. The outfit is cosmetic: picking one changes the player's kit and nothing else,
    /// and the section says so.
    /// </summary>
    public static class GearLoadoutScreen
    {
        private const float SlotHeight = 300f;
        private const float SlotGap = 22f;
        private const float OutfitTileHeight = 250f;

        private static readonly Sim.GearSlot[] Slots = { Sim.GearSlot.Paddle, Sim.GearSlot.Shoes, Sim.GearSlot.Grip };

        public static GameObject Build(Transform parent, ScreenManager mgr)
        {
            GameObject root = UIBuilder.Child("GearLoadoutScreen", parent);
            UIBuilder.Fill(root);
            PSKit.Backdrop(root.transform);
            Transform safe = UIBuilder.SafeArea(root.transform);

            UIBuilder.TopBar(safe, "GEAR", delegate { mgr.NavigateTab("HOME"); }, IconId.Coin, MetaGameState.Coins.ToString("N0"));

            RectTransform content;
            GameObject scroll = UIBuilder.ScrollView(safe, out content);
            UIBuilder.StretchRect(scroll, Vector2.zero, Vector2.one,
                new Vector2(UITheme.SpaceLg, UITheme.NavBarBottomGap + UITheme.NavBarHeight + 24f),
                new Vector2(-UITheme.SpaceLg, -UITheme.ContentTopInset));

            float y = 0f;
            foreach (Sim.GearSlot slot in Slots)
            {
                BuildSlot(content, y, slot, mgr);
                y += SlotHeight + SlotGap;
            }
            y = BuildOutfits(content, y + 12f, mgr);
            content.sizeDelta = new Vector2(0, y + 16f);

            UIBuilder.BottomNav(safe, "GEAR", delegate (string tab) { mgr.NavigateTab(tab); });
            return root;
        }

        private static string SlotName(Sim.GearSlot slot)
        {
            switch (slot)
            {
                case Sim.GearSlot.Paddle: return "PADDLE";
                case Sim.GearSlot.Shoes: return "SHOES";
                default: return "GRIP";
            }
        }

        private static IconId SlotIcon(Sim.GearSlot slot)
        {
            switch (slot)
            {
                case Sim.GearSlot.Paddle: return IconId.Paddle;
                case Sim.GearSlot.Shoes: return IconId.Shoe;
                default: return IconId.Tape;
            }
        }

        private static string SlotEffect(Sim.GearSlot slot)
        {
            switch (slot)
            {
                case Sim.GearSlot.Paddle: return "Harder, faster shots";
                case Sim.GearSlot.Shoes: return "Quicker to the ball, wider reach";
                default: return "Shots land closer to your aim";
            }
        }

        // ============================================================
        // STAT SLOTS
        // ============================================================
        private static void BuildSlot(Transform content, float y, Sim.GearSlot slot, ScreenManager mgr)
        {
            int level = MetaGameState.GearLevel(slot);
            bool maxed = Sim.GearRules.IsMaxLevel(level);
            int cost = Sim.GearRules.UpgradeCost(level);
            bool affordable = !maxed && MetaGameState.Coins >= cost;
            string stat = Sim.GearRules.StatName(slot);

            GameObject card = UIBuilder.Panel(content, "Slot_" + slot, new Vector2(0, SlotHeight), UITheme.PanelDarkTop, UITheme.PanelDarkDeep);
            UIBuilder.StretchRect(card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -(y + SlotHeight)), new Vector2(0, -y));
            UIBuilder.ShineLayer(card.transform, 0.30f);

            // Icon well with the level on it.
            GameObject well = UIBuilder.Child("IconWell", card.transform);
            UIBuilder.Rect(well, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26, -26), new Vector2(150, 150));
            well.AddComponent<Image>();
            UIBuilder.OutlinedGradientFill(well, UIBuilder.RoundedSprite(24), UITheme.SkyMid, UITheme.CourtDeep, UITheme.OutlineNavy, 5f);
            UIBuilder.IconAt(well.transform, SlotIcon(slot), 110f, Color.white, UITheme.OutlineNavy, new Vector2(0.5f, 0.5f), Vector2.zero);
            GameObject badge = UIBuilder.Chip(well.transform, "LV " + level, UITheme.PanelDarkDeep, 44f, 22);
            UIBuilder.Rect(badge, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), badge.GetComponent<RectTransform>().sizeDelta);

            Text name = UIBuilder.StrokedText(card.transform, "Name", TextAnchor.MiddleLeft, SlotName(slot), UITheme.Cream, 44);
            UIBuilder.Rect(name.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(200, -24), new Vector2(-220, 56));
            UIBuilder.ClampLine(name);

            Text statLine = UIBuilder.Text(card.transform, "Stat", TextAnchor.MiddleLeft,
                stat + " " + Sim.GearRules.StatAtLevel(level) + "  ·  LEVEL " + level + " / " + Sim.GearConfig.MaxLevel,
                UITheme.Volt, 25, FontStyle.Bold);
            UIBuilder.Rect(statLine.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(200, -84), new Vector2(-220, 34));
            UIBuilder.ClampLine(statLine);

            Text effect = UIBuilder.Text(card.transform, "Effect", TextAnchor.MiddleLeft, SlotEffect(slot),
                new Color(1f, 1f, 1f, 0.82f), 23, FontStyle.Normal);
            UIBuilder.Rect(effect.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(200, -122), new Vector2(-220, 32));
            UIBuilder.ClampLine(effect);

            GameObject ticks = UIBuilder.Child("Ticks", card.transform);
            UIBuilder.StretchRect(ticks, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(200, -186), new Vector2(-30, -158));
            UIBuilder.Ticks(ticks.transform, Sim.GearConfig.MaxLevel, level, maxed ? 0 : 1, UITheme.TickFilled, new Color(1f, 1f, 1f, 0.35f));

            // The upgrade itself.
            Sim.GearSlot captured = slot;
            UIBuilder.ButtonRefs button;
            if (maxed)
            {
                button = UIBuilder.GhostButton(card.transform, "MAX LEVEL", new Vector2(0, 86), 28, null);
                button.button.interactable = false;
            }
            else
            {
                string label = "UPGRADE  +" + Sim.GearConfig.StatPerLevel + " " + stat;
                button = affordable
                    ? UIBuilder.GreenButton(card.transform, label, new Vector2(0, 86), 28, delegate { Upgrade(captured, mgr); })
                    : UIBuilder.GhostButton(card.transform, label, new Vector2(0, 86), 28, delegate { Upgrade(captured, mgr); });
            }
            UIBuilder.StretchRect(button.root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(26, 22), new Vector2(-26, 108));

            if (!maxed)
            {
                // Price inside the button, right-aligned, so the label keeps the middle.
                Transform face = button.root.transform.Find("Content");
                GameObject price = UIBuilder.Child("Price", face != null ? face : button.root.transform);
                UIBuilder.Rect(price, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18, 0), new Vector2(190, 50));
                UIBuilder.IconAt(price.transform, IconId.Coin, 34f, affordable ? UITheme.GoldTop : new Color(1f, 1f, 1f, 0.6f),
                    UITheme.OutlineNavy, new Vector2(0f, 0.5f), new Vector2(18, 0));
                Text priceText = UIBuilder.Text(price.transform, "Text", TextAnchor.MiddleLeft, cost.ToString("N0"),
                    affordable ? UITheme.Ink1 : new Color(1f, 1f, 1f, 0.7f), 26, FontStyle.Bold);
                UIBuilder.StretchRect(priceText.gameObject, Vector2.zero, Vector2.one, new Vector2(42, 0), Vector2.zero);
                RectTransform labelRt = button.label.GetComponent<RectTransform>();
                labelRt.offsetMax = new Vector2(labelRt.offsetMax.x - 170f, labelRt.offsetMax.y);
            }
        }

        private static void Upgrade(Sim.GearSlot slot, ScreenManager mgr)
        {
            int level = MetaGameState.GearLevel(slot);
            int cost = Sim.GearRules.UpgradeCost(level);
            switch (MetaGameState.TryUpgradeGear(slot))
            {
                case Sim.PurchaseResult.Purchased:
                    if (Pickleball.Systems.SoundManager.Instance != null) Pickleball.Systems.SoundManager.Instance.PlayPerfectShot();
                    mgr.RefreshCurrentScreen();
                    break;
                case Sim.PurchaseResult.NotEnoughCoins:
                    mgr.ShowNotice("NOT ENOUGH COINS",
                        "This upgrade costs " + cost.ToString("N0") + " coins. Win matches, or watch an ad from the home screen, to earn more.");
                    break;
            }
        }

        // ============================================================
        // OUTFITS
        // ============================================================
        private static float BuildOutfits(Transform content, float y, ScreenManager mgr)
        {
            Text heading = UIBuilder.StrokedText(content, "OutfitHeading", TextAnchor.MiddleLeft, "OUTFIT", UITheme.Cream, 44);
            UIBuilder.StretchRect(heading.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8, -(y + 56)), new Vector2(0, -y));
            Text note = UIBuilder.Text(content, "OutfitNote", TextAnchor.MiddleRight, "COSMETIC ONLY · NO STATS",
                UITheme.InkOnLight, 24, FontStyle.Bold);
            UIBuilder.StretchRect(note.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -(y + 56)), new Vector2(-8, -y));
            y += 72f;

            const int columns = 3;
            const float gap = 18f;
            Outfit[] outfits = OutfitCatalog.All;
            string selected = MetaGameState.OutfitId;
            for (int i = 0; i < outfits.Length; i++)
            {
                int col = i % columns, row = i / columns;
                float top = y + row * (OutfitTileHeight + gap);
                GameObject cell = UIBuilder.Child("Outfit_" + outfits[i].id, content);
                UIBuilder.StretchRect(cell, new Vector2(col / (float)columns, 1f), new Vector2((col + 1) / (float)columns, 1f),
                    new Vector2(col == 0 ? 0 : gap * 0.5f, -(top + OutfitTileHeight)),
                    new Vector2(col == columns - 1 ? 0 : -gap * 0.5f, -top));
                BuildOutfitTile(cell, outfits[i], outfits[i].id == selected, mgr);
            }
            int rows = (outfits.Length + columns - 1) / columns;
            return y + rows * (OutfitTileHeight + gap);
        }

        private static void BuildOutfitTile(GameObject cell, Outfit outfit, bool selected, ScreenManager mgr)
        {
            cell.AddComponent<Image>();
            if (selected)
                UIBuilder.OutlinedGradientFill(cell, UIBuilder.RoundedSprite(22), UITheme.Volt, UITheme.VoltDeep, UITheme.Outline, UITheme.StrokeOutlineThin);
            else
                UIBuilder.OutlinedGradientFill(cell, UIBuilder.RoundedSprite(22), UITheme.Panel, UITheme.Ink0, UITheme.Outline, UITheme.StrokeOutlineThin);

            // Shirt over shorts: the two colours the kit changes.
            GameObject shirt = UIBuilder.Child("Shirt", cell.transform);
            UIBuilder.Rect(shirt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -26), new Vector2(120, 86));
            shirt.AddComponent<Image>();
            UIBuilder.OutlinedFill(shirt, UIBuilder.RoundedSprite(16), outfit.shirt, UITheme.Outline, 5f);
            GameObject shorts = UIBuilder.Child("Shorts", cell.transform);
            UIBuilder.Rect(shorts, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -114), new Vector2(96, 50));
            shorts.AddComponent<Image>();
            UIBuilder.OutlinedFill(shorts, UIBuilder.RoundedSprite(12), outfit.shorts, UITheme.Outline, 5f);

            Text label = UIBuilder.Text(cell.transform, "Name", TextAnchor.MiddleCenter, selected ? outfit.name + " ✓" : outfit.name,
                selected ? UITheme.Ink1 : UITheme.Cream, 25, FontStyle.Bold);
            UIBuilder.StretchRect(label.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8, 16), new Vector2(-8, 58));
            UIBuilder.ClampLine(label);

            Button button = cell.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = cell.GetComponent<Image>();
            string id = outfit.id;
            button.onClick.AddListener(delegate
            {
                MetaGameState.SelectOutfit(id);
                mgr.RefreshCurrentScreen();
            });
        }
    }
}
