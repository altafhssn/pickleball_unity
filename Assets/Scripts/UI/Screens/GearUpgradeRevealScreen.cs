using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;

namespace Pickleball.UI
{
    /// <summary>Short, skippable reward beat between spending coins and returning to item detail.</summary>
    public static class GearUpgradeRevealScreen
    {
        public static GameObject Build(Transform parent, ScreenManager mgr, GearItem item)
        {
            GameObject root = UIBuilder.Child("GearUpgradeRevealScreen", parent);
            UIBuilder.Fill(root);
            PSKit.Backdrop(root.transform);
            Transform safe = UIBuilder.SafeArea(root.transform);

            Text title = UIBuilder.StrokedText(safe, "Title", TextAnchor.MiddleCenter, item != null ? item.name : "GEAR", Color.white, 56);
            UIBuilder.Rect(title.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(-80, 80));

            GameObject glow = UIBuilder.Child("Glow", safe);
            UIBuilder.Rect(glow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(560, 560));
            Image glowImage = glow.AddComponent<Image>();
            glowImage.sprite = UIBuilder.CircleSprite;
            glowImage.color = new Color(0.2f, 0.9f, 1f, 0.25f);
            glow.AddComponent<UIPulse>().scaleAmount = 0.14f;

            GameObject card = UIBuilder.GearCard(safe, new Vector2(410, 500), item.icon, item.name, item.rarity, -1f);
            UIBuilder.Rect(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(410, 500));
            UIFadeIn.Attach(card, 0.7f, 0.2f, 100f, 0.55f);

            Text level = UIBuilder.StrokedText(safe, "Level", TextAnchor.MiddleCenter, "LEVEL " + item.level, UITheme.FillActionGreen, 64);
            UIBuilder.Rect(level.gameObject, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -220), new Vector2(-100, 90));

            Text stats = UIBuilder.Text(safe, "Stats", TextAnchor.MiddleCenter,
                "+9 POWER   +6 SPIN   +4 SERVE", Color.white, 29, FontStyle.Bold);
            UIBuilder.Rect(stats.gameObject, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -310), new Vector2(-100, 60));

            UIBuilder.ButtonRefs done = UIBuilder.GreenButton(safe, "CONTINUE", new Vector2(0, UITheme.ButtonHeight), UITheme.TypeButton,
                delegate { mgr.RefreshGearDetail(item); });
            UIBuilder.StretchRect(done.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceXl, 40), new Vector2(-UITheme.SpaceXl, 40 + UITheme.ButtonHeight));

            UIBuilder.RunOnScreen(root, AutoContinue(done.button));
            return root;
        }

        private static IEnumerator AutoContinue(Button button)
        {
            button.interactable = false;
            yield return new WaitForSecondsRealtime(1.25f);
            if (button != null) button.interactable = true;
        }
    }
}
