using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Pickleball.UI
{
    /// <summary>
    /// Screen 01 — boot / loading, rebuilt against the Pickle Smash board (Docs/Figma/Frame-1.svg).
    ///
    /// The approved paddle-and-ball logo sits above a tagline, progress bar and loading status
    /// over the original patterned background.
    /// </summary>
    public static class BootScreen
    {
        private const float LoadDuration = 1.6f;

        private static readonly string[] Steps =
        {
            "Warming up the court", "Stringing paddles", "Chalking the lines", "Loading your profile"
        };

        // Board positions on the 390x844 frame.
        private static readonly float LogoCentre = UITheme.F(285f);
        private static readonly float LogoSize   = UITheme.F(300f);
        private static readonly float TaglineY   = UITheme.F(442f);
        private static readonly float BarY       = UITheme.F(481f);
        private static readonly float BarWidth   = UITheme.F(200f);
        private static readonly float BarHeight  = UITheme.F(12f);
        private static readonly float StatusY    = UITheme.F(510f);

        public static GameObject Build(Transform parent, ScreenManager mgr)
        {
            GameObject root = UIBuilder.Child("BootScreen", parent);
            UIBuilder.Fill(root);

            UIReferenceArt.Backdrop(root.transform, "boot_background");
            Transform safe = PSKit.BoardHost(UIBuilder.SafeArea(root.transform)).transform;

            Image logo = UIReferenceArt.Draw(safe, "pickle_smash_logo");
            UIBuilder.Rect(logo.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -LogoCentre), new Vector2(LogoSize, LogoSize));

            Text tagline = PSKit.Body(safe, "Tagline", TextAnchor.MiddleCenter, "SMASH. SCORE. REPEAT.",
                UITheme.Cream, 34);
            CentreRow(tagline.gameObject, TaglineY, 48f);
            UIBuilder.ClampLine(tagline, 22);

            // ---- Progress ----
            GameObject barHost = UIBuilder.Child("BarHost", safe);
            UIBuilder.Rect(barHost, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -BarY), new Vector2(BarWidth, BarHeight));

            Image track = barHost.AddComponent<Image>();
            track.sprite = UIBuilder.CapsuleSprite;
            track.gameObject.AddComponent<UICapsuleFit>();
            track.type = Image.Type.Sliced;
            track.color = UITheme.Outline;

            GameObject well = UIBuilder.Child("Well", barHost.transform);
            UIBuilder.StretchRect(well, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
            Image wellImg = well.AddComponent<Image>();
            wellImg.sprite = UIBuilder.CapsuleSprite;
            wellImg.gameObject.AddComponent<UICapsuleFit>();
            wellImg.type = Image.Type.Sliced;
            wellImg.color = UITheme.Panel;

            GameObject fill = UIBuilder.Child("Fill", well.transform);
            UIBuilder.StretchRect(fill, Vector2.zero, new Vector2(0f, 1f), new Vector2(3, 3), new Vector2(3, -3));
            Image fillImg = fill.AddComponent<Image>();
            fillImg.sprite = UIBuilder.CapsuleSprite;
            fillImg.gameObject.AddComponent<UICapsuleFit>();
            fillImg.type = Image.Type.Sliced;
            fillImg.color = UITheme.Volt;

            Text status = PSKit.Body(safe, "Status", TextAnchor.MiddleCenter, Steps[0],
                new Color(1f, 0.976f, 0.918f, 0.85f), 26);
            CentreRow(status.gameObject, StatusY, 40f);
            UIBuilder.ClampLine(status, 18);

            UIBuilder.RunOnScreen(root, FakeLoad(fillImg, status, mgr));
            return root;
        }

        /// <summary>Full-width row centred <paramref name="y"/> below the top of the safe area.</summary>
        private static void CentreRow(GameObject go, float y, float height)
        {
            UIBuilder.Rect(go, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -y), new Vector2(-UITheme.SpaceXl * 2f, height));
        }

        private static IEnumerator FakeLoad(Image fillImg, Text status, ScreenManager mgr)
        {
            float t = 0f;
            while (t < LoadDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / LoadDuration);

                if (fillImg != null) fillImg.rectTransform.anchorMax = new Vector2(p, 1f);
                if (status != null)
                {
                    status.text = Steps[Mathf.Clamp(Mathf.FloorToInt(p * Steps.Length), 0, Steps.Length - 1)];
                }
                yield return null;
            }
            mgr.CompleteBoot();
        }
    }
}
