using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;
using Sim = Pickleball.Sim;

namespace Pickleball.UI
{
    /// <summary>
    /// Leagues: the player's current league and league points, progress toward the next league, and
    /// the promotion and relegation thresholds that move them -- then the whole ladder, so every
    /// threshold is visible. Only multiplayer moves league points; the screen says so.
    ///
    /// Panels sit on dark plates rather than directly on the pale backdrop: cream display type on the
    /// sky measured about 1.3:1.
    /// </summary>
    public static class LeagueScreen
    {
        private const float CtaTop = UITheme.NavBarHeight + UITheme.CtaBottomGap + UITheme.ButtonHeight;
        private const float HeaderHeight = 250f;
        private const float ProgressHeight = 250f;
        private const float RowHeight = 104f;
        private const float RowGap = 10f;

        public static GameObject Build(Transform parent, ScreenManager mgr)
        {
            GameObject root = UIBuilder.Child("LeagueScreen", parent);
            UIBuilder.Fill(root);
            PSKit.Backdrop(root.transform);
            Transform safe = UIBuilder.SafeArea(root.transform);

            UIBuilder.TopBar(safe, "LEAGUES", delegate { mgr.NavigateTab("HOME"); }, IconId.None, null);

            int points = MetaGameState.LeaguePoints;
            int index = MetaGameState.CurrentLeagueIndex;
            float top = UITheme.ContentTopInset;

            BuildCrest(safe, top, points, index);
            float progressTop = top + HeaderHeight + 18f;
            BuildProgress(safe, progressTop, points, index);

            // ---- The ladder ----
            RectTransform content;
            GameObject scroll = UIBuilder.ScrollView(safe, out content);
            UIBuilder.StretchRect(scroll, Vector2.zero, Vector2.one,
                new Vector2(UITheme.SpaceLg, CtaTop + 12f), new Vector2(-UITheme.SpaceLg, -(progressTop + ProgressHeight + 18f)));

            float y = 0f;
            Sim.League[] leagues = Sim.LeagueConfig.Leagues;
            for (int i = leagues.Length - 1; i >= 0; i--)
            {
                y += BuildRow(content, y, i, i == index, points);
            }
            content.sizeDelta = new Vector2(0, y + 8f);

            UIBuilder.ButtonRefs playBtn = UIBuilder.GreenButton(safe, "MULTIPLAYER", new Vector2(0, UITheme.ButtonHeight),
                UITheme.TypeButton, delegate { mgr.StartMultiplayerMatch(); });
            UIBuilder.StretchRect(playBtn.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceLg, UITheme.NavBarHeight + UITheme.CtaBottomGap), new Vector2(-UITheme.SpaceLg, CtaTop));

            UIBuilder.BottomNav(safe, "LEAGUE", delegate (string tab) { mgr.NavigateTab(tab); });
            return root;
        }

        /// <summary>League crest, name and point balance on a dark plate.</summary>
        private static void BuildCrest(Transform safe, float top, int points, int index)
        {
            GameObject crest = UIBuilder.Panel(safe, "LeaguePanel", new Vector2(0, HeaderHeight), UITheme.PanelDarkTop, UITheme.PanelDarkDeep);
            UIBuilder.StretchRect(crest, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceLg, -(top + HeaderHeight)), new Vector2(-UITheme.SpaceLg, -top));
            UIBuilder.ShineLayer(crest.transform, 0.36f);

            GameObject shield = UIBuilder.Child("ShieldHost", crest.transform);
            UIBuilder.Rect(shield, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34, 0), new Vector2(150, 150));
            UIBuilder.IconAt(shield.transform, IconId.Shield, 150f, LeagueColor(index), UITheme.OutlineNavy, new Vector2(0.5f, 0.5f), Vector2.zero);
            UIBuilder.IconAt(shield.transform, IconId.Star, 62f, Color.white, UITheme.OutlineNavy, new Vector2(0.5f, 0.5f), new Vector2(0, 8));

            Text name = UIBuilder.StrokedText(crest.transform, "League", TextAnchor.MiddleLeft,
                Sim.LeagueConfig.Leagues[index].Name + " LEAGUE", UITheme.Cream, 50);
            UIBuilder.Rect(name.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(204, -44), new Vector2(-234, 60));
            UIBuilder.ClampLine(name);

            GameObject pointsRow = UIBuilder.Child("PointsRow", crest.transform);
            UIBuilder.Rect(pointsRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(204, -16), new Vector2(560, 54));
            UIBuilder.IconAt(pointsRow.transform, IconId.Trophy, 44f, UITheme.GoldMid, UITheme.OutlineNavy, new Vector2(0f, 0.5f), new Vector2(22, 0));
            Text value = UIBuilder.StrokedText(pointsRow.transform, "Points", TextAnchor.MiddleLeft,
                points.ToString("N0") + " LEAGUE POINTS", Color.white, 38);
            UIBuilder.Rect(value.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(52, 0), new Vector2(500, 48));
            UIBuilder.ClampLine(value);

            Text rule = UIBuilder.Text(crest.transform, "Rule", TextAnchor.MiddleLeft,
                "Multiplayer win +" + Sim.LeagueConfig.WinPoints + "  ·  loss -" + Sim.LeagueConfig.LossPoints +
                "  ·  AI matches don't count", new Color(1f, 1f, 1f, 0.82f), 23, FontStyle.Bold);
            UIBuilder.Rect(rule.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(204, 22), new Vector2(-230, 34));
            UIBuilder.ClampLine(rule);
        }

        /// <summary>Progress toward promotion, and where relegation starts.</summary>
        private static void BuildProgress(Transform safe, float top, int points, int index)
        {
            GameObject panel = UIBuilder.Panel(safe, "ProgressPanel", new Vector2(0, ProgressHeight), UITheme.PanelDarkTop, UITheme.PanelDarkDeep);
            UIBuilder.StretchRect(panel, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceLg, -(top + ProgressHeight)), new Vector2(-UITheme.SpaceLg, -top));

            Sim.League[] leagues = Sim.LeagueConfig.Leagues;
            int promotion = Sim.LeagueRules.PromotionThreshold(index);
            int relegation = Sim.LeagueRules.RelegationThreshold(index);

            string headline = promotion < 0
                ? "TOP LEAGUE REACHED"
                : (promotion - points).ToString("N0") + " POINTS TO " + leagues[index + 1].Name;
            Text title = UIBuilder.Text(panel.transform, "Headline", TextAnchor.MiddleLeft, headline, Color.white, 30, FontStyle.Bold);
            UIBuilder.Rect(title.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(28, -20), new Vector2(-56, 40));
            UIBuilder.ClampLine(title);

            GameObject meter = UIBuilder.Child("MeterHost", panel.transform);
            UIBuilder.Rect(meter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -76), new Vector2(-56, 28));
            UIBuilder.MeterStretch(meter.transform, Sim.LeagueRules.Progress01(points), UITheme.Volt);

            Text left = UIBuilder.Text(panel.transform, "Floor", TextAnchor.MiddleLeft,
                leagues[index].Name + " · " + leagues[index].MinPoints.ToString("N0"), new Color(1, 1, 1, 0.8f), 21, FontStyle.Bold);
            UIBuilder.Rect(left.gameObject, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(28, -112), new Vector2(0, 30));
            Text right = UIBuilder.Text(panel.transform, "Next", TextAnchor.MiddleRight,
                promotion < 0 ? "HIGHEST LEAGUE" : leagues[index + 1].Name + " · " + promotion.ToString("N0"),
                UITheme.Volt, 21, FontStyle.Bold);
            UIBuilder.Rect(right.gameObject, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28, -112), new Vector2(0, 30));

            string promo = promotion < 0 ? "You're in the top league."
                : "Promotion: reach " + promotion.ToString("N0") + " points.";
            string releg = relegation < 0 ? "Relegation: none -- " + leagues[0].Name + " is the lowest league."
                : "Relegation: drop below " + relegation.ToString("N0") + " and you move down to " + leagues[index - 1].Name + ".";
            Text thresholds = UIBuilder.Text(panel.transform, "Thresholds", TextAnchor.UpperLeft,
                promo + "\n" + releg, new Color(1f, 1f, 1f, 0.9f), 23, FontStyle.Normal);
            thresholds.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIBuilder.StretchRect(thresholds.gameObject, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(28, 16), new Vector2(-28, -150));
        }

        private static float BuildRow(Transform content, float y, int index, bool isCurrent, int points)
        {
            Sim.League league = Sim.LeagueConfig.Leagues[index];

            GameObject go = UIBuilder.Child("League" + index, content);
            UIBuilder.StretchRect(go, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -(y + RowHeight)), new Vector2(0, -y));
            go.AddComponent<Image>();
            if (isCurrent)
                UIBuilder.OutlinedGradientFill(go, UIBuilder.RoundedSprite(18), UITheme.GoldTop, UITheme.GoldDeep, UITheme.GoldInk, UITheme.StrokeOutlineThin);
            else
                UIBuilder.OutlinedGradientFill(go, UIBuilder.RoundedSprite(18), UITheme.Panel, UITheme.Ink0, UITheme.Outline, UITheme.StrokeOutlineThin);

            Color ink = isCurrent ? UITheme.GoldInk : Color.white;

            UIBuilder.IconAt(go.transform, IconId.Shield, 60f, LeagueColor(index), UITheme.OutlineNavy, new Vector2(0f, 0.5f), new Vector2(56, 0));

            Text name = UIBuilder.Text(go.transform, "Name", TextAnchor.MiddleLeft, league.Name, ink, 30, FontStyle.Bold);
            UIBuilder.StretchRect(name.gameObject, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(110, -30), new Vector2(-330, 30));
            UIBuilder.ClampLine(name);

            string range = Sim.LeagueRules.IsTopLeague(index)
                ? league.MinPoints.ToString("N0") + "+ PTS"
                : league.MinPoints.ToString("N0") + " - " + (Sim.LeagueConfig.Leagues[index + 1].MinPoints - 1).ToString("N0") + " PTS";
            if (isCurrent) range = "YOU · " + points.ToString("N0");
            Text detail = UIBuilder.Text(go.transform, "Range", TextAnchor.MiddleRight, range, ink, 26, FontStyle.Bold);
            UIBuilder.StretchRect(detail.gameObject, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0, -30), new Vector2(-24, 30));
            UIBuilder.ClampLine(detail);

            return RowHeight + RowGap;
        }

        /// <summary>A crest colour per rung, climbing from bronze to the champion's blaze.</summary>
        private static Color LeagueColor(int index)
        {
            Color[] ramp =
            {
                UITheme.LeagueBronze,
                UITheme.LeagueSilver,
                UITheme.LeagueGold,
                UITheme.LeaguePlatinum,
                UITheme.LeagueDiamond,
                UITheme.LeagueChampion,
            };
            return ramp[Mathf.Clamp(index, 0, ramp.Length - 1)];
        }
    }
}
