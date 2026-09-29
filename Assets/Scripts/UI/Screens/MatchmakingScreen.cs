using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;
using Pickleball.Net;

namespace Pickleball.UI
{
    /// <summary>
    /// Screen 04 — matchmaking, rebuilt against the Pickle Smash board (Docs/Figma/Frame-4.svg).
    ///
    /// The board makes this a single idea: three concentric volt rings pulsing outward from the
    /// ball, a status line, and a way out. The previous screen showed two fighter cards side by side
    /// with a "?" standing in for the opponent — which meant the moment of finding someone was
    /// spent here, on a card that swapped in place, rather than on the match-found screen built for
    /// it. Finding an opponent now hands straight over to <see cref="MatchIntroScreen"/>, which is
    /// the board that actually introduces them.
    /// </summary>
    public static class MatchmakingScreen
    {
        // Board coordinates (390x844).
        private const float TitleY = 122f;
        private const float LeagueY = 172f;
        private const float PulseY = 368f;
        private const float StatusY = 466f;
        private const float SubStatusY = 494f;
        private const float CancelY = 575f;
        private static readonly float[] RingDiameters = { 304f, 217f, 130f };
        private static readonly float[] RingAlphas = { 0.15f, 0.30f, 0.55f };

        /// <summary>How long a search runs before the screen offers to play the AI instead. Nobody
        /// in the league may be online, and no bot is ever substituted for a ranked opponent -- the
        /// switch is the player's choice, and an AI match never counts for the league.</summary>
        private const float AiOfferSeconds = 10f;
        private const float AiOfferY = 650f;

        public static GameObject Build(Transform parent, ScreenManager mgr)
        {
            GameObject root = UIBuilder.Child("MatchmakingScreen", parent);
            UIBuilder.Fill(root);
            PSKit.Backdrop(root.transform);
            Transform safe = UIBuilder.SafeArea(root.transform);
            GameObject board = PSKit.BoardHost(safe);

            Text title = PSKit.Display(board.transform, "Title", TextAnchor.MiddleCenter,
                "RANKED MATCH", UITheme.Volt, UITheme.TypeHeroTitle);
            PSKit.BoardRow(title.gameObject, TitleY, 70f, 20f);
            UIBuilder.ClampLine(title, 40);

            Text league = PSKit.Body(board.transform, "League", TextAnchor.MiddleCenter,
                MetaGameState.CurrentLeague.Name + " LEAGUE  ·  SINGLES  ·  FIRST TO " + Pickleball.Gameplay.MatchConfig.PointsToWin,
                UITheme.InkOnLight, 34);
            PSKit.BoardRow(league.gameObject, LeagueY, 30f, 20f);
            UIBuilder.ClampLine(league, 22);

            // ---- Pulse ----
            GameObject pulse = UIBuilder.Child("Pulse", board.transform);
            PSKit.BoardDisc(pulse, PulseY, RingDiameters[0]);

            for (int i = 0; i < RingDiameters.Length; i++)
            {
                float d = UITheme.F(RingDiameters[i]);
                GameObject ring = UIBuilder.Child("Ring" + i, pulse.transform);
                UIBuilder.Rect(ring, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(d, d));
                Image img = ring.AddComponent<Image>();
                // Thickness as a percentage of radius, so the outer ring is not three times heavier
                // than the inner one just for being bigger.
                img.sprite = UIBuilder.RingSprite(Mathf.RoundToInt(600f / RingDiameters[i]));
                img.color = new Color(UITheme.Volt.r, UITheme.Volt.g, UITheme.Volt.b, RingAlphas[i]);
                img.raycastTarget = false;
                UIRingPulse.Attach(ring, i * 0.45f);
            }

            GameObject ball = PSKit.BallMark(pulse.transform, UITheme.F(43.4f));
            UIBuilder.Rect(ball, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(UITheme.F(43.4f), UITheme.F(43.4f)));

            // ---- Status ----
            Text status = PSKit.Display(board.transform, "Status", TextAnchor.MiddleCenter,
                "SEARCHING FOR OPPONENT...", UITheme.InkOnLight, 48);
            PSKit.BoardRow(status.gameObject, StatusY, 40f, 16f);
            UIBuilder.ClampLine(status, 26);

            Text subStatus = PSKit.Body(board.transform, "SubStatus", TextAnchor.MiddleCenter,
                "Looking for a player in your league", UITheme.InkOnLight, 34);
            PSKit.BoardRow(subStatus.gameObject, SubStatusY, 34f, 16f);
            UIBuilder.ClampLine(subStatus, 22);

            // ---- Way out ----
            // Plain type with a dashed rule under it, not a filled button: this screen's one filled
            // shape is the ball, and a slab labelled CANCEL would outweigh the thing it interrupts.
            Text cancel = PSKit.TextButton(board.transform, "CANCEL", UITheme.InkOnLight, 52,
                delegate { mgr.ExitMatchToLobby(); });
            PSKit.BoardRow(cancel.transform.parent.gameObject, CancelY, 50f);

            GameObject dash = UIBuilder.Child("CancelRule", board.transform);
            UIBuilder.Rect(dash, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -UITheme.F(590f)), new Vector2(UITheme.F(60f), 3f));
            Image dashImg = dash.AddComponent<Image>();
            dashImg.color = UITheme.InkOnLightDim;
            dashImg.raycastTarget = false;

            UIBuilder.RunOnScreen(root, OfferAiAfter(board.transform, mgr));

#if PHOTON_UNITY_NETWORKING
            BeginRealMatchmaking(root, mgr, status, subStatus);
#else
            // Multiplayer is online-only; a build without Photon has nobody to search for.
            mgr.MatchmakingFailed();
#endif
            return root;
        }

        /// <summary>After a while with nobody found, offer to leave the search and play the AI instead.</summary>
        private static IEnumerator OfferAiAfter(Transform board, ScreenManager mgr)
        {
            yield return new WaitForSecondsRealtime(AiOfferSeconds);
            if (board == null) yield break;
            Text ai = PSKit.TextButton(board, "PLAY WITH AI INSTEAD", UITheme.InkOnLight, 40, mgr.PlayAiInstead);
            PSKit.BoardRow(ai.transform.parent.gameObject, AiOfferY, 44f);
            UIFadeIn.Attach(ai.transform.parent.gameObject, 0.3f, 0f, 12f);
        }

#if PHOTON_UNITY_NETWORKING
        /// <summary>
        /// Real opponent path: PhotonQuickMatch replaces the scripted search. There is no bot
        /// backfill yet, so this can genuinely sit waiting with nobody else queued — the elapsed
        /// counter on the sub-status line exists so that reads as "still searching" rather than as
        /// "frozen".
        /// </summary>
        private static void BeginRealMatchmaking(GameObject root, ScreenManager mgr, Text status, Text subStatus)
        {
            PhotonQuickMatch quickMatch = root.AddComponent<PhotonQuickMatch>();
            Coroutine elapsedTimer = null;
            bool searchFailed = false;

            quickMatch.OnSearching += () =>
            {
                StopTimer(root, elapsedTimer);
                if (subStatus != null) elapsedTimer = UIBuilder.RunOnScreen(root, TickElapsedSearchTime(subStatus, quickMatch));
            };

            quickMatch.OnFailure += message =>
            {
                if (searchFailed || root == null) return;
                searchFailed = true;
                StopTimer(root, elapsedTimer);
                UnityEngine.Object.Destroy(quickMatch);
                Text retry = PSKit.TextButton(root.transform.Find("SafeArea/BoardHost"), "TRY AGAIN", UITheme.InkOnLight, 42, mgr.MatchmakingFailed);
                PSKit.BoardRow(retry.transform.parent.gameObject, 537, 38);
                if (status != null) { status.text = "CONNECTION ERROR"; status.color = UITheme.InkOnLightGold; }
                if (subStatus != null) subStatus.text = "Please check your connection.";
                mgr.MatchmakingFailed();
            };

            quickMatch.OnMatchReady += (transport, matchStart) =>
            {
                if (searchFailed || root == null) { transport?.Disconnect(); return; }
                // Stop the ticker before the screen is torn down, or it keeps writing to a dead Text.
                StopTimer(root, elapsedTimer);
                if (status != null) status.text = "OPPONENT FOUND";
                if (subStatus != null) subStatus.text = matchStart.opponentName;
                mgr.BeginPvPMatchIntro(transport, matchStart);
            };
            quickMatch.BeginQuickMatch();
        }

        private static void StopTimer(GameObject root, Coroutine timer)
        {
            if (timer == null || root == null) return;
            MonoBehaviour host = root.GetComponent<ScreenCoroutineHost>();
            if (host != null) host.StopCoroutine(timer);
        }

        private static IEnumerator TickElapsedSearchTime(Text label, PhotonQuickMatch quickMatch)
        {
            float start = Time.unscaledTime;
            while (true)
            {
                int seconds = Mathf.FloorToInt(Time.unscaledTime - start);
                label.text = string.Format("Searching... {0}:{1:00}\n{2}", seconds / 60, seconds % 60,
                    quickMatch != null ? quickMatch.Diagnostics : string.Empty);
                yield return null;
            }
        }

        private static IEnumerator EnterAfterDelay(ScreenManager mgr, INetworkTransport transport, MatchStartMessage matchStart)
        {
            yield return new WaitForSeconds(0.7f);
            if (mgr != null) mgr.BeginPvPMatchIntro(transport, matchStart);
        }
#endif
    }
}
