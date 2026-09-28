using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;

namespace Pickleball.UI
{
    /// <summary>
    /// Screen 03 — tour select. A ladder you climb: cleared tours get a green tick, the live tour is
    /// bigger with a gold rim, locked tours fade and state the trophy gate. Tour matches are played
    /// online against live opponents; the AI is practice only.
    ///
    /// The list is the screen, so it stretches between the header and the CTA rather than sitting in
    /// a fixed-height window. (It also used to render nothing at all — see the RectMask2D note in
    /// UIBuilder.ScrollView.)
    /// </summary>
    public static class TourSelectScreen
    {
        private const float CtaTop = UITheme.NavBarHeight + UITheme.CtaBottomGap + UITheme.ButtonHeight;

        public static GameObject Build(Transform parent, ScreenManager mgr)
        {
            GameObject root = UIBuilder.Child("TourSelectScreen", parent);
            UIBuilder.Fill(root);
            PSKit.Backdrop(root.transform);
            Transform safe = UIBuilder.SafeArea(root.transform);

            TourInfo current = MetaGameState.CurrentTour;
            UIBuilder.TopBar(safe, "TOURS", delegate { mgr.GoBack(); }, IconId.Trophy, MetaGameState.Trophies.ToString("N0"));

            RectTransform content;
            GameObject scroll = UIBuilder.ScrollView(safe, out content);
            UIBuilder.StretchRect(scroll, Vector2.zero, Vector2.one,
                new Vector2(UITheme.SpaceLg, CtaTop + 12f), new Vector2(-UITheme.SpaceLg, -UITheme.ContentTopInset));

            float y = 0f;
            const float gap = 16f;
            for (int i = 0; i < MetaGameState.Tours.Count; i++)
            {
                float h = BuildRow(content, mgr, i, y);
                y += h + gap;
            }
            content.sizeDelta = new Vector2(0, y);

            UIBuilder.ButtonRefs playBtn = UIBuilder.GreenButton(safe, "PLAY ONLINE  ·  " + current.entryCoins + " COINS", new Vector2(0, UITheme.ButtonHeight), UITheme.TypeButton, delegate { mgr.StartTourMatch(); });
            UIBuilder.StretchRect(playBtn.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceLg, UITheme.NavBarHeight + UITheme.CtaBottomGap), new Vector2(-UITheme.SpaceLg, CtaTop));
            UIBuilder.ClampLine(playBtn.label);

            UIBuilder.BottomNav(safe, "PLAY", delegate (string tab) { mgr.NavigateTab(tab); });
            return root;
        }

        private static float BuildRow(Transform content, ScreenManager mgr, int i, float y)
        {
            TourInfo tour = MetaGameState.Tours[i];
            bool isCurrent = i == MetaGameState.CurrentTourIndex;
            bool locked = tour.trophiesNeeded > 0 && MetaGameState.Trophies < tour.trophiesNeeded;
            float h = isCurrent ? 268f : 168f;

            GameObject card = UIBuilder.Panel(content, "TourRow" + i, new Vector2(0, h), UITheme.PanelDarkTop, UITheme.PanelDarkDeep);
            UIBuilder.StretchRect(card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -(y + h)), new Vector2(0, -y));

            if (isCurrent)
            {
                UIBuilder.OutlinedGradientFill(card, UIBuilder.PanelSprite, UITheme.PanelTop, UITheme.PanelDeep, UITheme.GoldMid, UITheme.StrokeOutline);
                GameObject tag = UIBuilder.Chip(card.transform, "NOW PLAYING", UITheme.GoldMid, 36f, 20);
                tag.transform.Find("Text").GetComponent<Text>().color = UITheme.GoldInk;
                UIBuilder.Rect(tag, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24, -16), new Vector2(190, 36));
            }
            else if (locked)
            {
                CanvasGroup cg = card.AddComponent<CanvasGroup>();
                cg.alpha = 0.62f;
            }

            // --- Icon ---
            float iconBox = isCurrent ? 128f : 112f;
            GameObject iconBg = UIBuilder.Child("Icon", card.transform);
            UIBuilder.Rect(iconBg, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26, -26), new Vector2(iconBox, iconBox));
            iconBg.AddComponent<Image>();

            if (locked)
            {
                UIBuilder.OutlinedFill(iconBg, UIBuilder.RoundedSprite(18), new Color(0.03f, 0.13f, 0.24f, 0.6f), UITheme.OutlineNavy, 4f);
                UIBuilder.IconAt(iconBg.transform, IconId.Lock, iconBox * 0.52f, new Color(1f, 1f, 1f, 0.85f), UITheme.OutlineNavy, new Vector2(0.5f, 0.5f), Vector2.zero);
            }
            else
            {
                UIBuilder.OutlinedGradientFill(iconBg, UIBuilder.RoundedSprite(18), UITheme.SkyMid, UITheme.CourtDeep, UITheme.OutlineNavy, 4f);
                UIBuilder.IconAt(iconBg.transform, tour.icon, iconBox * 0.58f, Color.white, UITheme.OutlineNavy, new Vector2(0.5f, 0.5f), Vector2.zero);
            }

            float textLeft = 26f + iconBox + 22f;

            Text label = UIBuilder.Text(card.transform, "Label", TextAnchor.MiddleLeft, "TOUR " + (i + 1), isCurrent ? UITheme.GoldTop : new Color(1, 1, 1, 0.65f), 21, FontStyle.Bold);
            UIBuilder.Rect(label.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(textLeft, -28), new Vector2(-(textLeft + 130), 26));

            Text name = UIBuilder.StrokedText(card.transform, "Name", TextAnchor.MiddleLeft, tour.name, Color.white, isCurrent ? 44 : 34);
            UIBuilder.Rect(name.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(textLeft, -66), new Vector2(-(textLeft + 130), 50));
            UIBuilder.ClampLine(name);

            // --- Status line ---
            if (locked)
            {
                GameObject need = UIBuilder.Child("Need", card.transform);
                UIBuilder.Rect(need, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(textLeft, 24), new Vector2(340, 32));
                Text needText = UIBuilder.Text(need.transform, "Text", TextAnchor.MiddleLeft, "NEEDS " + tour.trophiesNeeded.ToString("N0"), UITheme.GoldTop, 22, FontStyle.Bold);
                UIBuilder.Rect(needText.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(220, 28));
                UIBuilder.IconAt(need.transform, IconId.Trophy, 26f, UITheme.GoldMid, UITheme.OutlineNavy, new Vector2(0f, 0.5f), new Vector2(NeedIconX(tour), 0));
            }
            else if (tour.cleared)
            {
                GameObject check = UIBuilder.Child("Check", card.transform);
                UIBuilder.Rect(check, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30, 0), new Vector2(84, 84));
                check.AddComponent<Image>();
                UIBuilder.OutlinedGradientFill(check, UIBuilder.CircleSprite, UITheme.FillActionGreen, UITheme.FillActionGreenDeep, Hex(11, 61, 22), 4f);
                UIBuilder.IconAt(check.transform, IconId.Check, 44f, Color.white, Hex(11, 61, 22), new Vector2(0.5f, 0.5f), Vector2.zero);
            }
            else
            {
                GameObject reward = UIBuilder.Child("Reward", card.transform);
                UIBuilder.Rect(reward, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(textLeft, isCurrent ? 92 : 24), new Vector2(400, 32));
                UIBuilder.IconAt(reward.transform, IconId.Coin, 26f, UITheme.GoldMid, UITheme.OutlineNavy, new Vector2(0f, 0.5f), new Vector2(13, 0));
                Text coins = UIBuilder.Text(reward.transform, "Coins", TextAnchor.MiddleLeft, tour.rewardCoins.ToString(), new Color(1, 1, 1, 0.9f), 22, FontStyle.Bold);
                UIBuilder.Rect(coins.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(32, 0), new Vector2(90, 28));
                UIBuilder.IconAt(reward.transform, IconId.Trophy, 26f, UITheme.GoldMid, UITheme.OutlineNavy, new Vector2(0f, 0.5f), new Vector2(132, 0));
                Text trop = UIBuilder.Text(reward.transform, "Trophies", TextAnchor.MiddleLeft, "+" + tour.rewardTrophies, new Color(1, 1, 1, 0.9f), 22, FontStyle.Bold);
                UIBuilder.Rect(trop.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(151, 0), new Vector2(90, 28));
            }

            // --- Progress meter, live tour only ---
            if (isCurrent)
            {
                GameObject meterHost = UIBuilder.Child("MeterHost", card.transform);
                UIBuilder.Rect(meterHost, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 46), new Vector2(-(textLeft + 26), 28));
                UIBuilder.MeterStretch(meterHost.transform, (float)tour.winsCurrent / tour.winsRequired, UITheme.GoldMid);

                Text progress = UIBuilder.Text(card.transform, "Progress", TextAnchor.MiddleLeft, tour.winsCurrent + " / " + tour.winsRequired + " WINS", new Color(1, 1, 1, 0.88f), 21, FontStyle.Bold);
                UIBuilder.Rect(progress.gameObject, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(textLeft, 14), new Vector2(300, 26));
            }

            if (!locked)
            {
                Button rowBtn = card.AddComponent<Button>();
                rowBtn.transition = Selectable.Transition.None;
                rowBtn.targetGraphic = card.GetComponent<Image>();
                int capturedIndex = i;
                rowBtn.onClick.AddListener(delegate { MetaGameState.CurrentTourIndex = capturedIndex; mgr.Show(ScreenId.TourSelect, false); });
            }

            return h;
        }

        /// <summary>x offset for the trophy icon that follows the "NEEDS n,nnn" label.</summary>
        private static float NeedIconX(TourInfo tour)
        {
            return 13f + ("NEEDS " + tour.trophiesNeeded.ToString("N0")).Length * 13f + 16f;
        }

        private static Color Hex(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f);
        }
    }
}
