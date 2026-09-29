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
    /// Screen 17 — season complete. New: the board (Docs/Figma/Frame.svg) existed with no screen
    /// behind it, so the end of a season simply rolled over with nothing marking it.
    ///
    /// One moment, one message: the tier you finished on, the league it puts you in, and a single
    /// way forward. Everything on this screen is a statement of fact about a season that is already
    /// over — there is nothing to decide, so there is nothing to choose between.
    ///
    /// Shown by <see cref="ScreenManager.MaybeShowSeasonComplete"/> at startup when the stored
    /// season index is behind the live one, and it claims the rollover as it goes so it cannot fire
    /// twice for the same season.
    /// </summary>
    public static class SeasonCompleteScreen
    {
        // Board coordinates (390x844).
        private const float TitleY = 99f;
        private const float RankLabelY = 313f;
        private const float LeagueY = 344f;
        private const float ContinueY = 584f;
        private const float ContinueH = 70f;

        public static GameObject Build(Transform parent, ScreenManager mgr, int seasonNumber, int finalTier, string leagueName)
        {
            GameObject root = UIBuilder.Child("SeasonCompleteScreen", parent);
            UIBuilder.Fill(root);
            PSKit.Backdrop(root.transform);
            Transform safe = UIBuilder.SafeArea(root.transform);
            GameObject board = PSKit.BoardHost(safe);

            Text title = PSKit.Display(board.transform, "Title", TextAnchor.MiddleCenter,
                "SEASON " + seasonNumber + " COMPLETE", UITheme.Volt, 62);
            PSKit.BoardRow(title.gameObject, TitleY, 56f, 16f);
            UIBuilder.ClampLine(title, 30);

            BuildBadge(board.transform, finalTier);

            Text rankLabel = PSKit.Body(board.transform, "RankLabel", TextAnchor.MiddleCenter,
                "FINAL RANK", UITheme.InkOnLightMute, 26);
            PSKit.BoardRow(rankLabel.gameObject, RankLabelY, 26f, 16f);

            Text league = PSKit.Display(board.transform, "League", TextAnchor.MiddleCenter,
                leagueName, UITheme.InkOnLight, 48);
            PSKit.BoardRow(league.gameObject, LeagueY, 44f, 16f);
            UIBuilder.ClampLine(league, 26);

            PSKit.ButtonStack cont = PSKit.PrimaryCta(board.transform, "CONTINUE",
                new Vector2(0, UITheme.F(ContinueH)), UITheme.TypeButton,
                delegate { mgr.NavigateTab("HOME"); });
            PSKit.BoardRow(cont.root, ContinueY, ContinueH, 29f);

            UIFadeIn.Attach(board, 0.3f, 0f, 40f);
            return root;
        }

        /// <summary>
        /// The tier medal: a gold laurel wreath, a gold ring, a volt face, the tier number.
        /// </summary>
        private static void BuildBadge(Transform board, int tier)
        {
            Image medal = UIReferenceArt.Draw(board, "season_medal");
            UIBuilder.Rect(medal.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -UITheme.F(203.5f)), new Vector2(UITheme.F(315f), UITheme.F(189f)));
            Text number = PSKit.Display(board, "Tier", TextAnchor.MiddleCenter,
                tier.ToString(), UITheme.Volt, 210);
            PSKit.BoardRow(number.gameObject, 207f, 100f, 135f);
        }
    }
}
#endif
