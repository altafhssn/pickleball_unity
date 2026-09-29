using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Pickleball.Systems;

namespace Pickleball.UI
{
    /// <summary>
    /// A stand-in rewarded ad for the editor and development builds, so the watch-an-ad flow can be
    /// played end to end before an ad network is chosen: a full-screen card counts down and then
    /// reports a completed view, or reports a skip if CLOSE is tapped first. It says plainly that it
    /// is a test. RewardedAds never uses it in a release build.
    /// </summary>
    public class SimulatedAdProvider : IRewardedAdProvider
    {
        private const float AdSeconds = 5f;

        public bool IsReady => true;

        public void Show(string placementId, Action<bool> onFinished)
        {
            GameObject host = new GameObject("SimulatedAd");
            UnityEngine.Object.DontDestroyOnLoad(host);
            Canvas canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            CanvasScaler scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            host.AddComponent<GraphicRaycaster>();

            GameObject root = UIBuilder.Child("Ad", host.transform);
            UIBuilder.Fill(root);
            Image scrim = root.AddComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.94f);
            Transform safe = UIBuilder.SafeArea(root.transform);

            Text title = PSKit.Display(safe, "Title", TextAnchor.MiddleCenter, "TEST AD", UITheme.Volt, 96);
            UIBuilder.Rect(title.gameObject, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 260), new Vector2(-120, 140));
            Text note = PSKit.Body(safe, "Note", TextAnchor.MiddleCenter,
                "No ad network is connected yet.\nThis stand-in only runs in editor and development builds.",
                UITheme.Cream, 36);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIBuilder.Rect(note.gameObject, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 90), new Vector2(-160, 140));
            Text countdown = PSKit.Display(safe, "Countdown", TextAnchor.MiddleCenter, "", UITheme.Gold, 140);
            UIBuilder.Rect(countdown.gameObject, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -120), new Vector2(-120, 180));

            bool finished = false;
            Action<bool> finish = delegate (bool completed)
            {
                if (finished) return;
                finished = true;
                UnityEngine.Object.Destroy(host);
                onFinished?.Invoke(completed);
            };

            PSKit.ButtonStack close = PSKit.SecondaryCta(safe, "CLOSE (NO REWARD)",
                new Vector2(UITheme.F(300f), UITheme.ButtonHeightGhost), UITheme.TypeButtonSm,
                delegate { finish(false); });
            UIBuilder.Rect(close.root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 160), new Vector2(UITheme.F(300f), UITheme.ButtonHeightGhost));

            UIBuilder.RunOnScreen(root, Countdown(countdown, finish));
        }

        private static IEnumerator Countdown(Text label, Action<bool> finish)
        {
            float end = Time.unscaledTime + AdSeconds;
            while (Time.unscaledTime < end)
            {
                if (label != null) label.text = Mathf.CeilToInt(end - Time.unscaledTime).ToString();
                yield return null;
            }
            finish(true);
        }
    }
}
