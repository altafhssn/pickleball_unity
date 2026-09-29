using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;
using Pickleball.Gameplay;

namespace Pickleball.UI
{
    /// <summary>
    /// Screen 05 — match found, rebuilt against the Pickle Smash board (Docs/Figma/Frame-5.svg).
    ///
    /// Two portraits stacked with a ball between them, each with a name and a rank, then the
    /// countdown. The ring colour is the whole identity system here: volt is you, bubblegum is
    /// them, and those are the same two colours the in-match score plates use, so the pairing a
    /// player learns on this screen is the one they read during the rally.
    ///
    /// The venue title and the live-court reveal are kept from the previous version — the camera
    /// fly-in is the only thing that connects this overlay to the match behind it.
    /// </summary>
    public static class MatchIntroScreen
    {
        private const float IntroDuration = 3.0f;

        // Board coordinates (390x844).
        private const float TitleY = 122f;
        private const float YouDiscY = 210f;
        private const float YouNameY = 275f;
        private const float YouRankY = 302f;
        private const float VsY = 349f;
        private const float RivalDiscY = 439f;
        private const float RivalNameY = 502f;
        private const float RivalRankY = 529f;
        private const float StartingY = 614f;
        private const float CountdownY = 681f;
        private const float DiscDiameter = 100f;

        /// <param name="opponentRank">Under the opponent's name: their league for a live opponent, a
        /// note that the AI is matched to the player's gear otherwise.</param>
        /// <param name="aiMatch">True for Play with AI, false for a multiplayer match.</param>
        public static GameObject Build(Transform parent, ScreenManager mgr, string opponentName,
            string opponentRank, bool aiMatch)
        {
            GameObject root = UIBuilder.Child("MatchIntroScreen", parent);
            UIBuilder.Fill(root);

            // Keep the live venue visible behind the match card. The camera already performs a court
            // fly-in here; an opaque meta-screen backdrop used to hide that transition completely.
            GameObject scrim = UIBuilder.Child("CourtRevealScrim", root.transform);
            UIBuilder.Fill(scrim);
            Image scrimImage = scrim.AddComponent<Image>();
            scrimImage.color = new Color(0.025f, 0.05f, 0.12f, 0.72f);
            scrimImage.raycastTarget = false;

            Transform safe = UIBuilder.SafeArea(root.transform);
            GameObject board = PSKit.BoardHost(safe);

            // An AI match has no search, so nothing was "found".
            Text title = PSKit.Display(board.transform, "Title", TextAnchor.MiddleCenter,
                aiMatch ? "PLAY WITH AI" : "MATCH FOUND", UITheme.Volt, UITheme.TypeHeroTitle);
            PSKit.BoardRow(title.gameObject, TitleY, 70f, 20f);
            UIBuilder.ClampLine(title, 40);
            if (!aiMatch) UIReferenceArt.Title(title, "title_found", 218f, 21.3f);

            BuildSide(board.transform, YouDiscY, YouNameY, YouRankY, UITheme.Volt,
                "YOU",
                MetaGameState.CurrentLeague.Name + " LEAGUE");

            // VS badge: the ball itself, the same mark the splash and the search pulse use.
            GameObject vs = PSKit.BallMark(board.transform, UITheme.F(48f));
            PSKit.BoardDisc(vs, VsY, 48f);
            Text vsText = PSKit.Display(vs.transform, "Vs", TextAnchor.MiddleCenter, "VS", UITheme.Ink1, 40);
            UIBuilder.Fill(vsText.gameObject);
            Outline vsOutline = vsText.GetComponent<Outline>();
            if (vsOutline != null) Object.Destroy(vsOutline);
            // The mark's holes would show through the VS, so this variant drops them.
            for (int i = 0; i < vs.transform.childCount; i++)
            {
                Transform face = vs.transform.GetChild(i);
                if (face.name != "Face") continue;
                for (int h = face.childCount - 1; h >= 0; h--)
                {
                    if (face.GetChild(h).name.StartsWith("Hole")) Object.Destroy(face.GetChild(h).gameObject);
                }
            }

            BuildSide(board.transform, RivalDiscY, RivalNameY, RivalRankY, UITheme.Bubblegum,
                opponentName, opponentRank);

            Text starting = PSKit.Display(board.transform, "Starting", TextAnchor.MiddleCenter,
                "MATCH STARTING IN", UITheme.Cream, 42);
            PSKit.BoardRow(starting.gameObject, StartingY, 40f, 16f);
            UIBuilder.ClampLine(starting, 24);

            Text countdown = PSKit.Display(board.transform, "Countdown", TextAnchor.MiddleCenter,
                "3", UITheme.Gold, 240);
            PSKit.BoardRow(countdown.gameObject, CountdownY, 112f);

            CameraController cameraController = Object.FindAnyObjectByType<CameraController>();
            if (cameraController != null) cameraController.BeginVenueIntro(IntroDuration);
            UIBuilder.RunOnScreen(root, RunCountdown(countdown, starting, mgr));
            return root;
        }

        /// <summary>One player: ringed portrait, name, rank line.</summary>
        private static void BuildSide(Transform board, float discY, float nameY, float rankY,
            Color ring, string name, string rank)
        {
            GameObject disc = PSKit.AvatarDisc(board, UITheme.F(DiscDiameter), ring);
            PSKit.BoardDisc(disc, discY, DiscDiameter);

            Text nameText = PSKit.Display(board, "Name", TextAnchor.MiddleCenter, name, UITheme.Cream, 54);
            PSKit.BoardRow(nameText.gameObject, nameY, 42f, 24f);
            UIBuilder.ClampLine(nameText, 28);

            Text rankText = PSKit.Display(board, "Rank", TextAnchor.MiddleCenter, rank, UITheme.Cream, 36);
            PSKit.BoardRow(rankText.gameObject, rankY, 32f, 24f);
            UIBuilder.ClampLine(rankText, 20);
        }

        private static IEnumerator RunCountdown(Text countdown, Text status, ScreenManager mgr)
        {
            if (mgr != null && mgr.HasPreparedPvPMatch)
            {
                while (mgr.GetPreparedMatchStartDelay() > 0f)
                {
                    float remaining = mgr.GetPreparedMatchStartDelay();
                    countdown.text = Mathf.Max(1, Mathf.CeilToInt(remaining)).ToString();
                    status.text = "SYNCHRONIZING COURT";
                    yield return null;
                }
                countdown.text = "GO";
                status.text = "THE MATCH IS STARTING";
                mgr.EnterPreparedMatch();
                yield break;
            }

            for (int value = 3; value >= 1; value--)
            {
                countdown.text = value.ToString();
                countdown.transform.localScale = Vector3.one * 1.18f;
                yield return new WaitForSecondsRealtime(0.18f);
                countdown.transform.localScale = Vector3.one;
                yield return new WaitForSecondsRealtime(0.82f);
            }
            countdown.text = "GO";
            status.text = "THE MATCH IS STARTING";
            yield return new WaitForSecondsRealtime(0.35f);
            if (mgr != null) mgr.EnterPreparedMatch();
        }
    }
}
