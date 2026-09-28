using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;

namespace Pickleball.UI
{
    /// <summary>
    /// Screen 11 — ranked league. Standings are the screen; the player's row flips to the same gold
    /// as the selected nav tab and the ready chest — this is you, this is now.
    ///
    /// The tier header sits on a dark plate rather than directly on the pale sky: white display type
    /// on #B9F0FF measured about 1.3:1, so the tier name and trophy count were legible only as
    /// shapes. The standings list also rendered completely empty before the scroll-view mask fix.
    /// </summary>
    public static class LeagueScreen
    {
        private const float CtaTop = UITheme.NavBarHeight + UITheme.CtaBottomGap + UITheme.ButtonHeight;
        private const float HeaderHeight = 250f;
        private const float PromoHeight = 122f;

        public static GameObject Build(Transform parent, ScreenManager mgr)
        {
            GameObject root = UIBuilder.Child("LeagueScreen", parent);
            UIBuilder.Fill(root);
            PSKit.Backdrop(root.transform);
            Transform safe = UIBuilder.SafeArea(root.transform);

            UIBuilder.TopBar(safe, "LEAGUE", delegate { mgr.NavigateTab("HOME"); }, IconId.None, null);

            float top = UITheme.ContentTopInset;

            // ---- Tier crest on a dark plate ----
            GameObject crest = UIBuilder.Panel(safe, "TierPanel", new Vector2(0, HeaderHeight), UITheme.PanelDarkTop, UITheme.PanelDarkDeep);
            UIBuilder.StretchRect(crest, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceLg, -(top + HeaderHeight)), new Vector2(-UITheme.SpaceLg, -top));
            UIBuilder.ShineLayer(crest.transform, 0.36f);

            GameObject shield = UIBuilder.Child("ShieldHost", crest.transform);
            UIBuilder.Rect(shield, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34, 0), new Vector2(150, 150));
            UIBuilder.IconAt(shield.transform, IconId.Shield, 150f, UITheme.LeaguePlatinum, UITheme.OutlineNavy, new Vector2(0.5f, 0.5f), Vector2.zero);
            UIBuilder.IconAt(shield.transform, IconId.Star, 62f, Color.white, UITheme.OutlineNavy, new Vector2(0.5f, 0.5f), new Vector2(0, 8));

            Text tierName = UIBuilder.StrokedText(crest.transform, "Tier", TextAnchor.MiddleLeft, MetaGameState.LeagueTierName(MetaGameState.Trophies), UITheme.Cream, 50);
            UIBuilder.Rect(tierName.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(204, -44), new Vector2(-234, 60));
            UIBuilder.ClampLine(tierName);

            GameObject tropRow = UIBuilder.Child("TrophyRow", crest.transform);
            UIBuilder.Rect(tropRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(204, -16), new Vector2(330, 54));
            UIBuilder.IconAt(tropRow.transform, IconId.Trophy, 44f, UITheme.GoldMid, UITheme.OutlineNavy, new Vector2(0f, 0.5f), new Vector2(22, 0));
            Text trophies = UIBuilder.StrokedText(tropRow.transform, "Trophies", TextAnchor.MiddleLeft, MetaGameState.Trophies.ToString("N0"), Color.white, 40);
            UIBuilder.Rect(trophies.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(52, 0), new Vector2(240, 48));

            // ---- Promotion progress ----
            float promoTop = top + HeaderHeight + 18f;
            GameObject promo = UIBuilder.Panel(safe, "PromoPanel", new Vector2(0, PromoHeight), UITheme.PanelDarkTop, UITheme.PanelDarkDeep);
            UIBuilder.StretchRect(promo, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceLg, -(promoTop + PromoHeight)), new Vector2(-UITheme.SpaceLg, -promoTop));

            // Progress through the current 200-trophy sub-band toward the next tier, all derived from
            // the live trophy count -- this panel was three hardcoded literals (0.58, "PLATINUM III",
            // "PLATINUM I · 2,300") that never moved.
            int myTrophies = MetaGameState.Trophies;
            int intoBand = myTrophies % 200;
            int nextCut = myTrophies - intoBand + 200;
            float promoT = Mathf.Clamp01(intoBand / 200f);

            GameObject promoMeter = UIBuilder.Child("MeterHost", promo.transform);
            UIBuilder.Rect(promoMeter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(-48, 28));
            UIBuilder.MeterStretch(promoMeter.transform, promoT, UITheme.QualityColor(Pickleball.Gameplay.ShotQuality.Great));

            Text promoLeft = UIBuilder.Text(promo.transform, "PromoLeft", TextAnchor.MiddleLeft, MetaGameState.LeagueTierName(myTrophies), new Color(1, 1, 1, 0.8f), 21, FontStyle.Bold);
            UIBuilder.Rect(promoLeft.gameObject, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(24, 18), new Vector2(0, 30));
            Text promoRight = UIBuilder.Text(promo.transform, "PromoRight", TextAnchor.MiddleRight, MetaGameState.LeagueTierName(nextCut) + " · " + nextCut.ToString("N0"), UITheme.Volt, 21, FontStyle.Bold);
            UIBuilder.Rect(promoRight.gameObject, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24, 18), new Vector2(0, 30));

            // ---- Standings ----
            RectTransform content;
            GameObject scroll = UIBuilder.ScrollView(safe, out content);
            UIBuilder.StretchRect(scroll, Vector2.zero, Vector2.one,
                new Vector2(UITheme.SpaceLg, CtaTop + 12f), new Vector2(-UITheme.SpaceLg, -(promoTop + PromoHeight + 18f)));

            float y = 0f;
            y += BuildBand(content, y, "PROMOTION — TOP 3", UITheme.FillActionGreenDeep, IconId.Play, 90f);

            // Sorted, with the player's row synced to the live trophy count -- the screen used to draw
            // the seed list verbatim, so "you" stayed frozen at 2,140 and never changed rank.
            System.Collections.Generic.List<LeagueRow> standings = MetaGameState.BuildLeagueStandings();
            for (int i = 0; i < standings.Count; i++)
            {
                y += BuildRow(content, y, i, standings[i]);
            }

            y += BuildBand(content, y, "DEMOTION — BOTTOM 2", UITheme.FillRivalRedBtnDeep, IconId.Play, -90f);
            content.sizeDelta = new Vector2(0, y + 8f);

            UIBuilder.ButtonRefs playBtn = UIBuilder.GreenButton(safe, "CHOOSE TOUR", new Vector2(0, UITheme.ButtonHeight), UITheme.TypeButton, delegate { mgr.Show(ScreenId.TourSelect); });
            UIBuilder.StretchRect(playBtn.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceLg, UITheme.NavBarHeight + UITheme.CtaBottomGap), new Vector2(-UITheme.SpaceLg, CtaTop));

            UIBuilder.BottomNav(safe, "LEAGUE", delegate (string tab) { mgr.NavigateTab(tab); });
            return root;
        }

        /// <summary>
        /// Promotion / demotion divider. Previously a bare text label with a loose coloured rectangle
        /// dropped behind it at a hand-guessed 300px width, which did not match the label and left a
        /// ragged block of colour in the list. This is one sized pill.
        /// </summary>
        private static float BuildBand(Transform content, float y, string label, Color color, IconId arrow, float arrowRotation)
        {
            const float h = 40f;
            GameObject band = UIBuilder.Child("Band", content);
            UIBuilder.Rect(band, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0, -y), new Vector2(label.Length * 15f + 72f, h));
            band.AddComponent<Image>();
            UIBuilder.OutlinedFill(band, UIBuilder.RoundedSprite(12), color, UITheme.OutlineNavy, 3f);

            GameObject arrowGO = UIBuilder.IconAt(band.transform, arrow, 22f, Color.white, UITheme.OutlineNavy, new Vector2(0f, 0.5f), new Vector2(22, 0));
            arrowGO.transform.localRotation = Quaternion.Euler(0, 0, arrowRotation);

            Text t = UIBuilder.Text(band.transform, "Text", TextAnchor.MiddleLeft, label, Color.white, 20, FontStyle.Bold);
            UIBuilder.StretchRect(t.gameObject, Vector2.zero, Vector2.one, new Vector2(40, 0), new Vector2(-14, 0));
            UIBuilder.ClampLine(t);

            return h + 10f;
        }

        private static float BuildRow(Transform content, float y, int index, LeagueRow row)
        {
            const float rowH = 104f, gap = 10f;

            GameObject go = UIBuilder.Child("Row" + index, content);
            UIBuilder.StretchRect(go, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -(y + rowH)), new Vector2(0, -y));
            go.AddComponent<Image>();

            if (row.isPlayer)
            {
                UIBuilder.OutlinedGradientFill(go, UIBuilder.RoundedSprite(18), UITheme.GoldTop, UITheme.GoldDeep, UITheme.GoldInk, UITheme.StrokeOutlineThin);
            }
            else
            {
                UIBuilder.OutlinedGradientFill(go, UIBuilder.RoundedSprite(18), UITheme.Panel, UITheme.Ink0, UITheme.Outline, UITheme.StrokeOutlineThin);
            }

            Color ink = row.isPlayer ? UITheme.GoldInk : Color.white;

            Text rank = UIBuilder.Text(go.transform, "Rank", TextAnchor.MiddleCenter, (index + 1).ToString(), row.isPlayer ? UITheme.Ink1 : UITheme.Volt, 34, FontStyle.Bold);
            UIBuilder.Rect(rank.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(38, 0), new Vector2(72, 80));

            GameObject avatar = UIBuilder.Child("Avatar", go.transform);
            UIBuilder.Rect(avatar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(90, 0), new Vector2(64, 64));
            avatar.AddComponent<Image>();
            UIBuilder.OutlinedGradientFill(avatar, UIBuilder.CircleSprite, UITheme.SkyMid, UITheme.CourtDeep,
                row.isPlayer ? UITheme.GoldInk : UITheme.OutlineNavy, 4f);
            UIBuilder.IconAt(avatar.transform, IconId.Player, 36f, Color.white, UITheme.OutlineNavy, new Vector2(0.5f, 0.5f), Vector2.zero);

            // Starts clear of the 64px avatar (which spans 90..154) and stops short of the trophy
            // column. The old 136px offset ran the name straight over the avatar disc.
            Text name = UIBuilder.Text(go.transform, "Name", TextAnchor.MiddleLeft, row.name, ink, 27, FontStyle.Bold);
            UIBuilder.StretchRect(name.gameObject, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(170, -30), new Vector2(-196, 30));
            UIBuilder.ClampLine(name);

            GameObject tropHost = UIBuilder.Child("Trophies", go.transform);
            UIBuilder.Rect(tropHost, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20, 0), new Vector2(170, 60));
            UIBuilder.IconAt(tropHost.transform, IconId.Trophy, 30f, row.isPlayer ? UITheme.GoldInk : UITheme.GoldMid, UITheme.OutlineNavy, new Vector2(0f, 0.5f), new Vector2(16, 0));
            Text trophies = UIBuilder.Text(tropHost.transform, "Text", TextAnchor.MiddleRight, row.trophies.ToString("N0"), ink, 28, FontStyle.Bold);
            UIBuilder.StretchRect(trophies.gameObject, Vector2.zero, Vector2.one, new Vector2(38, 0), new Vector2(-8, 0));

            return rowH + gap;
        }

        private static Color Hex(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f);
        }
    }
}
