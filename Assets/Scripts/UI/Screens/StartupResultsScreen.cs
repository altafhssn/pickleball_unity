// Retired from the launch build: this feature is outside the scope in Docs/LaunchScope.md
// (Pickleball Game Design Document v1.0). The source is kept for reference and is not compiled;
// PICKLEBALL_RETIRED_FEATURES is deliberately never defined.
#if PICKLEBALL_RETIRED_FEATURES
using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;

namespace Pickleball.UI
{
    /// <summary>Weekly-login result sequence shown after bootstrap and before the home lobby.</summary>
    public static class StartupResultsScreen
    {
        public static GameObject BuildStanding(Transform parent, ScreenManager mgr)
        {
            GameObject root = Base(rootName: "StartupStanding", parent: parent, title: "LEAGUE STANDING");
            Transform safe = root.transform.Find("SafeArea");

            GameObject league = UIBuilder.Panel(safe, "League", new Vector2(0, 150), UITheme.PanelTop, UITheme.PanelDeep);
            UIBuilder.StretchRect(league, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceLg, -310), new Vector2(-UITheme.SpaceLg, -160));
            UIBuilder.IconAt(league.transform, IconId.Shield, 88f, UITheme.TextPlayerBlue, UITheme.OutlineNavy,
                new Vector2(0f, 0.5f), new Vector2(76, 0));
            Text leagueName = UIBuilder.StrokedText(league.transform, "Name", TextAnchor.MiddleLeft, MetaGameState.LeagueTierName(MetaGameState.Trophies), Color.white, 42);
            UIBuilder.StretchRect(leagueName.gameObject, Vector2.zero, Vector2.one, new Vector2(150, 0), new Vector2(-30, 0));

            GameObject table = UIBuilder.Child("Table", safe);
            UIBuilder.StretchRect(table, Vector2.zero, Vector2.one,
                new Vector2(UITheme.SpaceLg, 220), new Vector2(-UITheme.SpaceLg, -340));
            System.Collections.Generic.List<LeagueRow> standings = MetaGameState.BuildLeagueStandings();
            float rowHeight = 116f;
            for (int i = 0; i < standings.Count; i++)
            {
                LeagueRow row = standings[i];
                bool isPlayerRow = row.isPlayer;
                GameObject rowGO = UIBuilder.Panel(table.transform, "Row" + i, new Vector2(0, rowHeight - 10),
                    isPlayerRow ? UITheme.GoldTop : UITheme.PanelDarkTop,
                    isPlayerRow ? UITheme.GoldDeep : UITheme.PanelDarkDeep);
                UIBuilder.StretchRect(rowGO, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0, -(i + 1) * rowHeight), new Vector2(0, -i * rowHeight - 10));
                Text rank = UIBuilder.StrokedText(rowGO.transform, "Rank", TextAnchor.MiddleCenter, (i + 1).ToString(), Color.white, 30);
                UIBuilder.Rect(rank.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(54, 0), new Vector2(70, 48));
                Text name = UIBuilder.Text(rowGO.transform, "Name", TextAnchor.MiddleLeft, row.name, Color.white, 27, FontStyle.Bold);
                UIBuilder.StretchRect(name.gameObject, Vector2.zero, Vector2.one, new Vector2(110, 0), new Vector2(-230, 0));
                Text trophies = UIBuilder.Text(rowGO.transform, "Trophies", TextAnchor.MiddleRight, row.trophies.ToString("N0"), Color.white, 26, FontStyle.Bold);
                UIBuilder.StretchRect(trophies.gameObject, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(-38, 0));
            }

            AddContinue(safe, "CONTINUE", mgr.ShowStartupLeagueResult);
            return root;
        }

        public static GameObject BuildLeagueResult(Transform parent, ScreenManager mgr)
        {
            GameObject root;
            Transform board = FlowScreens.Shell(parent, "StartupLeagueResult", "LEAGUE REWARD", () => mgr.NavigateTab("HOME"), out root);
            Image medal = UIReferenceArt.Draw(board, "season_medal");
            PSKit.BoardDisc(medal.gameObject, 238, 145);
            FlowScreens.Label(board, MetaGameState.LeagueTierName(MetaGameState.Trophies), 366, 48, true);
            FlowScreens.Label(board, MetaGameState.Trophies.ToString("N0") + " TROPHIES", 413);
            FlowScreens.Label(board, "YOUR WEEKLY LEAGUE CHEST", 502, 52, true);
            bool available = MetaGameState.ShouldShowStartupResults;
            FlowScreens.Label(board, available ? "A new week. A new upgrade.\nClaim your chest, then unlock it in Your Bags." : "This week's chest has been claimed.\nCome back next week for your next reward.", 562, 76);
            FlowScreens.ActionButton(board, available ? "CLAIM CHEST" : "YOUR BAGS", 681,
                available ? (System.Action)mgr.CompleteStartupResults : () => mgr.Show(ScreenId.BagInventory), true);
            FlowScreens.ActionButton(board, "BACK TO HOME", 766, () => mgr.NavigateTab("HOME"));
            return root;
        }

        private static GameObject Base(string rootName, Transform parent, string title)
        {
            GameObject root = UIBuilder.Child(rootName, parent);
            UIBuilder.Fill(root);
            PSKit.Backdrop(root.transform);
            Transform safe = UIBuilder.SafeArea(root.transform);
            GameObject ribbon = UIBuilder.Ribbon(safe, title, 850f, 124f, 40);
            UIBuilder.Rect(ribbon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -UITheme.HeaderTopOffset), new Vector2(850, 124));
            return root;
        }

        private static void AddContinue(Transform safe, string label, System.Action onClick)
        {
            UIBuilder.ButtonRefs button = UIBuilder.GreenButton(safe, label, new Vector2(0, UITheme.ButtonHeight), UITheme.TypeButton, onClick);
            UIBuilder.StretchRect(button.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceLg, 36), new Vector2(-UITheme.SpaceLg, 36 + UITheme.ButtonHeight));
        }
    }
}
#endif
