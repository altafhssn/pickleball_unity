using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;

namespace Pickleball.UI
{
    /// <summary>
    /// Screen 16 — a short first-match primer. The live HUD follows it with contextual prompts, so
    /// this page establishes the control model without asking the player to memorise every gesture.
    /// </summary>
    public static class TutorialScreen
    {
        private const float CtaTop = 40f + UITheme.ButtonHeight;

        private struct Lesson
        {
            public IconId icon;
            public string title;
            public string body;
            public Lesson(IconId i, string t, string b) { icon = i; title = t; body = b; }
        }

        private static readonly Lesson[] Lessons =
        {
            new Lesson(IconId.Play, "YOU CONTROL THE SHOT",
                "Your player runs to the ball automatically.\nWatch the bounce, then swipe to return it."),
            new Lesson(IconId.Bolt, "AIM AND POWER",
                "Your swipe's direction aims the ball.\nA faster swipe hits it harder and deeper."),
            new Lesson(IconId.Refresh, "LOB WITH A CURVE",
                "Swipe upward in a curve for a high, looping lob.\nThere's no lob button -- the gesture is the lob."),
            new Lesson(IconId.Trophy, "FIRST TO SEVEN",
                "Singles. Only the server scores: win a rally on\nthe other serve to win the serve back. 7 wins.\nReplay this guide any time from Settings."),
        };

        public static GameObject Build(Transform parent, ScreenManager mgr)
        {
            GameObject root = UIBuilder.Child("TutorialScreen", parent);
            UIBuilder.Fill(root);
            PSKit.Backdrop(root.transform);
            Transform safe = UIBuilder.SafeArea(root.transform);

            GameObject ribbon = UIBuilder.Ribbon(safe, "HOW TO PLAY", 780f, 130f, 40);
            UIBuilder.Rect(ribbon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -UITheme.HeaderTopOffset), new Vector2(780, 130));

            RectTransform content;
            GameObject scroll = UIBuilder.ScrollView(safe, out content);
            UIBuilder.StretchRect(scroll, Vector2.zero, Vector2.one,
                new Vector2(UITheme.SpaceLg, CtaTop + 16f), new Vector2(-UITheme.SpaceLg, -(UITheme.HeaderTopOffset + 130f + 20f)));

            float y = 0f;
            foreach (Lesson lesson in Lessons) y += BuildLesson(content, y, lesson);
            content.sizeDelta = new Vector2(0, y + 8f);

            UIBuilder.ButtonRefs go = UIBuilder.GreenButton(safe, "CONTINUE", new Vector2(0, UITheme.ButtonHeight),
                UITheme.TypeButton, delegate { mgr.ContinueFromTutorial(); });
            UIBuilder.StretchRect(go.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceLg, 40), new Vector2(-UITheme.SpaceLg, CtaTop));

            return root;
        }

        private static float BuildLesson(Transform content, float y, Lesson lesson)
        {
            const float h = 210f, gap = 16f;

            GameObject card = UIBuilder.Child("Lesson", content);
            UIBuilder.StretchRect(card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -(y + h)), new Vector2(0, -y));
            card.AddComponent<Image>();
            UIBuilder.OutlinedGradientFill(card, UIBuilder.PanelSprite, UITheme.PanelDarkTop, UITheme.PanelDarkDeep, UITheme.OutlineNavy, UITheme.StrokeOutlineThin);

            GameObject iconBg = UIBuilder.Child("IconBg", card.transform);
            UIBuilder.Rect(iconBg, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26, 0), new Vector2(96, 96));
            iconBg.AddComponent<Image>();
            UIBuilder.OutlinedGradientFill(iconBg, UIBuilder.RoundedSprite(18), UITheme.SkyMid, UITheme.CourtDeep, UITheme.GlowCyan, 4f);
            UIBuilder.IconAt(iconBg.transform, lesson.icon, 52f, Color.white, UITheme.OutlineNavy, new Vector2(0.5f, 0.5f), Vector2.zero);

            Text title = UIBuilder.Text(card.transform, "Title", TextAnchor.LowerLeft, lesson.title, UITheme.TextPlayerBlue, 30, FontStyle.Bold);
            UIBuilder.StretchRect(title.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(140, -46), new Vector2(-24, -14));
            UIBuilder.ClampLine(title);

            Text body = UIBuilder.Text(card.transform, "Body", TextAnchor.UpperLeft, lesson.body, new Color(1f, 1f, 1f, 0.92f), 28, FontStyle.Normal);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIBuilder.StretchRect(body.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(140, -190), new Vector2(-24, -58));

            return h + gap;
        }
    }
}
