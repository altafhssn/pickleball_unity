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
        private const float PulseY = 368f;
        private const float StatusY = 466f;
        private const float SubStatusY = 494f;
        private const float CancelY = 575f;
        private static readonly float[] RingDiameters = { 304f, 217f, 130f };
        private static readonly float[] RingAlphas = { 0.15f, 0.30f, 0.55f };

        public static GameObject Build(Transform parent, ScreenManager mgr, bool ranked = false)
        {
            GameObject root = UIBuilder.Child("MatchmakingScreen", parent);
            UIBuilder.Fill(root);
            PSKit.Backdrop(root.transform);
            Transform safe = UIBuilder.SafeArea(root.transform);
            GameObject board = PSKit.BoardHost(safe);

            Text title = PSKit.Display(board.transform, "Title", TextAnchor.MiddleCenter,
                ranked ? "RANKED MATCH" : "QUICK MATCH", UITheme.Volt, UITheme.TypeHeroTitle);
            PSKit.BoardRow(title.gameObject, TitleY, 70f, 20f);
            UIBuilder.ClampLine(title, 40);
            if (ranked) UIReferenceArt.Title(title, "title_ranked", 232.3f, 21.3f);

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
                "This usually takes a few seconds", UITheme.InkOnLight, 34);
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

#if PHOTON_UNITY_NETWORKING
            if (ranked)
            {
                BeginRealMatchmaking(root, mgr, status, subStatus);
            }
            else
#endif
            {
                UIBuilder.RunOnScreen(root, ResolveSearch(mgr, status, subStatus));
            }
            return root;
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
                if (subStatus != null) elapsedTimer = UIBuilder.RunOnScreen(root, TickElapsedSearchTime(subStatus));
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

        private static IEnumerator TickElapsedSearchTime(Text label)
        {
            float start = Time.unscaledTime;
            while (true)
            {
                int seconds = Mathf.FloorToInt(Time.unscaledTime - start);
                label.text = string.Format("Searching... {0}:{1:00}", seconds / 60, seconds % 60);
                yield return null;
            }
        }

        private static IEnumerator EnterAfterDelay(ScreenManager mgr, INetworkTransport transport, MatchStartMessage matchStart)
        {
            yield return new WaitForSeconds(0.7f);
            if (mgr != null) mgr.BeginPvPMatchIntro(transport, matchStart);
        }
#endif

        /// <summary>Tour matches are always against the AI, so the "search" is a beat of anticipation
        /// rather than a real wait. Keeping it short and honest matters more than dressing it up.</summary>
        private static IEnumerator ResolveSearch(ScreenManager mgr, Text status, Text subStatus)
        {
            yield return new WaitForSeconds(1.4f);
            if (status != null) status.text = "OPPONENT FOUND";
            if (subStatus != null) subStatus.text = MetaGameState.CurrentTour.name;
            yield return new WaitForSeconds(0.7f);
            if (mgr != null) mgr.BeginMatchIntro();
        }
    }
}
