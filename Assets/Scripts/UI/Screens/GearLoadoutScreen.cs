using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;

namespace Pickleball.UI
{
    /// <summary>
    /// Gear loadout with a two-by-two slot grid and six attribute rows in a scrolling body.
    /// The upgrade action and navigation remain reachable below the body on shorter screens.
    /// </summary>
    public static class GearLoadoutScreen
    {
        private const float CtaTop = UITheme.NavBarBottomGap + UITheme.NavBarHeight + 24f + UITheme.ButtonHeight;
        private const float StatsHeight = 430f;

        private static readonly string[] SlotNames = { "PADDLE", "SHOES", "GRIP", "EXTRA" };

        public static GameObject Build(Transform parent, ScreenManager mgr)
        {
            GameObject root = UIBuilder.Child("GearLoadoutScreen", parent);
            UIBuilder.Fill(root);
            PSKit.Backdrop(root.transform);
            Transform safe = UIBuilder.SafeArea(root.transform);

            UIBuilder.TopBar(safe, "LOADOUT", delegate { mgr.NavigateTab("HOME"); }, IconId.Star, MetaGameState.OverallRating.ToString());

            RectTransform content;
            GameObject scroll = UIBuilder.ScrollView(safe, out content);
            UIBuilder.StretchRect(scroll, Vector2.zero, Vector2.one,
                new Vector2(UITheme.SpaceLg, CtaTop + 24), new Vector2(-UITheme.SpaceLg, -UITheme.ContentTopInset));
            content.sizeDelta = new Vector2(0, 1000);
            BuildStage(content, mgr);
            BuildStatsPanel(content);

            int upgradeable = 0;
            foreach (GearItem g in MetaGameState.Gear) if (MetaGameState.CanUpgrade(g)) upgradeable++;

            UIBuilder.ButtonRefs upgradeBtn = UIBuilder.GreenButton(safe, "UPGRADE GEAR", new Vector2(0, UITheme.ButtonHeight), UITheme.TypeButton, delegate
            {
                GearItem target = MetaGameState.Gear.Find(g => MetaGameState.CanUpgrade(g));
                if (target != null) mgr.ShowGearDetail(target);
                else if (MetaGameState.Gear.Count > 0) mgr.ShowGearDetail(MetaGameState.Gear[0]);
            });
            UIBuilder.StretchRect(upgradeBtn.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceLg, UITheme.NavBarBottomGap + UITheme.NavBarHeight + 24f), new Vector2(-UITheme.SpaceLg, CtaTop));

            // Nudge the label up so the "n READY" sub-line has room instead of overlapping it.
            UIBuilder.Rect(upgradeBtn.label.gameObject, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, 14), Vector2.zero);
            Text sub = UIBuilder.Text(upgradeBtn.root.transform.Find("Content"), "Sub", TextAnchor.MiddleCenter,
                upgradeable + (upgradeable == 1 ? " READY" : " READY"), new Color(1, 1, 1, 0.9f), 22, FontStyle.Bold);
            UIBuilder.Rect(sub.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(240, 30));

            UIBuilder.BottomNav(safe, "GEAR", delegate (string tab) { mgr.NavigateTab(tab); });
            return root;
        }

        // ============================================================
        private static void BuildStage(Transform safe, ScreenManager mgr)
        {
            GameObject stage = UIBuilder.Child("Stage", safe);
            // Inset on the right so the slot column cannot run under the header's balance pill or off
            // the screen edge -- at 0 the paddle slot and its level badge were both clipped.
            UIBuilder.StretchRect(stage, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -480), new Vector2(0, 0));

            const float heroLift = 0f;

            GameObject disc = UIBuilder.HeroPlatformDisc(stage.transform, new Vector2(360, 360));
            UIBuilder.Rect(disc, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-90, heroLift), new Vector2(360, 360));
            disc.GetComponent<RectTransform>().anchoredPosition = new Vector2(-240, heroLift);
            var preview = UIReferenceArt.Draw(stage.transform, "home_player").gameObject;
            UIBuilder.Rect(preview, new Vector2(0.28f, 0.5f), new Vector2(0.28f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(350, 420));
            if (preview.GetComponent<Image>().sprite == null)
            {
                preview.SetActive(false);
                UIBuilder.IconAt(disc.transform, IconId.Player, 176f, Color.white, UITheme.OutlineNavy,
                    new Vector2(0.5f, 0.5f), Vector2.zero);
            }

            // Two rows keep every slot above the attributes panel.
            const float slotSize = 130f;
            // Wide enough for the slot's type label to sit under it without the next slot's level
            // badge — which overhangs its top-right corner — landing on top of the text.
            const float slotGap = 38f;

            for (int i = 0; i < 4; i++)
            {
                GearType type = (GearType)i;
                GearItem g = MetaGameState.GetEquipped(type);
                bool hasItem = g != null;

                GameObject slot = hasItem
                    ? UIBuilder.GearSlot(stage.transform, slotSize, g.icon, g.level, g.rarity, false)
                    : UIBuilder.GearSlot(stage.transform, slotSize, IconId.None, 0, Rarity.Common, true);

                float y = -40f - (i / 2) * (slotSize + slotGap + 42f);
                UIBuilder.Rect(slot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-24f - (1 - i % 2) * (slotSize + slotGap), y), new Vector2(slotSize, slotSize));
                UIBuilder.DropShadow(slot, -5f, 3f);

                // Slot type label — without it every slot is an anonymous square and there is no way
                // to tell which one takes shoes. Navy, because it sits below the slot on the pale
                // sky where white is unreadable.
                Text slotLabel = UIBuilder.Text(slot.transform, "SlotName", TextAnchor.UpperCenter, SlotNames[i],
                    UITheme.InkOnLight, 25, FontStyle.Bold);
                UIBuilder.Rect(slotLabel.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(0, 36));

                Button slotBtn = slot.AddComponent<Button>();
                slotBtn.transition = Selectable.Transition.None;
                slotBtn.targetGraphic = slot.GetComponent<Image>();
                if (hasItem)
                {
                    GearType capturedType = type;
                    slotBtn.onClick.AddListener(delegate { mgr.ShowGearCatalog(capturedType); });
                }
                else
                {
                    GearType capturedType = type;
                    slotBtn.onClick.AddListener(delegate { mgr.ShowGearCatalog(capturedType); });
                }
            }
        }

        // ============================================================
        private static void BuildStatsPanel(Transform safe)
        {
            // Shares MetaGameState.GetLoadoutStats() with the gameplay sim path rather than summing
            // equipped gear separately -- one aggregation, not two copies that could drift. Clamps each
            // of the 6 stats to 0-100 for this panel's 10-tick display only; the sim reads the same
            // call's real, unclamped totals.
            Pickleball.Sim.LoadoutStats loadout = MetaGameState.GetLoadoutStats();
            int power = Mathf.Clamp(Mathf.RoundToInt(loadout.power), 0, 100);
            int spin = Mathf.Clamp(Mathf.RoundToInt(loadout.spin), 0, 100);
            int control = Mathf.Clamp(Mathf.RoundToInt(loadout.control), 0, 100);
            int speed = Mathf.Clamp(Mathf.RoundToInt(loadout.speed), 0, 100);
            int serve = Mathf.Clamp(Mathf.RoundToInt(loadout.serve), 0, 100);
            int stamina = Mathf.Clamp(Mathf.RoundToInt(loadout.stamina), 0, 100);

            GameObject panel = UIBuilder.Panel(safe, "StatsPanel", new Vector2(0, StatsHeight), UITheme.PanelTop, UITheme.PanelDeep);
            UIBuilder.StretchRect(panel, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0, -480f - StatsHeight), new Vector2(0, -480f));
            UIBuilder.ShineLayer(panel.transform, 0.34f);

            Text title = UIBuilder.Text(panel.transform, "AttrTitle", TextAnchor.MiddleLeft, "ATTRIBUTES", Color.white, 30, FontStyle.Bold);
            UIBuilder.Rect(title.gameObject, new Vector2(0f, 1f), new Vector2(0.55f, 1f), new Vector2(0f, 1f), new Vector2(30, -26), new Vector2(0, 42));

            GearItem equippedPaddle = MetaGameState.GetEquipped(GearType.Paddle);
            if (equippedPaddle != null)
            {
                GameObject chip = UIBuilder.Chip(panel.transform, equippedPaddle.name, UITheme.FillActionGreenDeep, 50f, 20);
                UIBuilder.Rect(chip, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30, -24), chip.GetComponent<RectTransform>().sizeDelta);
            }

            string[] labels = { "POWER", "SPIN", "CONTROL", "SPEED", "SERVE", "STAMINA" };
            int[] values = { power, spin, control, speed, serve, stamina };
            float rowY = 86f;

            for (int i = 0; i < labels.Length; i++)
            {
                Text label = UIBuilder.Text(panel.transform, "Lbl" + i, TextAnchor.MiddleLeft, labels[i], new Color(1, 1, 1, 0.9f), 27, FontStyle.Bold);
                UIBuilder.Rect(label.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30, -rowY), new Vector2(150, 38));

                GameObject tickHost = UIBuilder.Child("TickHost" + i, panel.transform);
                UIBuilder.StretchRect(tickHost, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(250, -rowY - 34), new Vector2(-150, -rowY - 6));
                // Filled ticks lifted from the pale TextPlayerBlue to a solid cyan: at the old value
                // the filled and empty ticks were nearly the same brightness on this panel and the
                // bar read as a flat strip rather than as a score out of ten.
                UIBuilder.Ticks(tickHost.transform, 10, Mathf.RoundToInt(values[i] / 10f), 0, UITheme.TickFilled, UITheme.FillActionGreen);

                Text value = UIBuilder.StrokedText(panel.transform, "Val" + i, TextAnchor.MiddleRight, values[i].ToString(), Color.white, 30);
                UIBuilder.Rect(value.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30, -rowY), new Vector2(110, 38));
                rowY += 52f;
            }
        }
    }
}
