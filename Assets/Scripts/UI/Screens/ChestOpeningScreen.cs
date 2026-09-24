using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;

namespace Pickleball.UI
{
    /// <summary>
    /// Sequential bag opening: consume one ready bag, commit its reward bundle exactly once, reveal
    /// each reward as a full-screen beat, then finish on a compact summary grid.
    /// </summary>
    public static class ChestOpeningScreen
    {
        public static GameObject Build(Transform parent, ScreenManager mgr, int bagIndex = -1)
        {
            RewardBundle bundle = MetaGameState.OpenReadyBag(bagIndex);

            GameObject root = UIBuilder.Child("ChestOpeningScreen", parent);
            UIBuilder.Fill(root);
            PSKit.Backdrop(root.transform);
            Transform safe = UIBuilder.SafeArea(root.transform);

            // bundle.chestName reflects the real chest that was opened -- was a hardcoded "FREE MATCH
            // BAG" from when AddMatchBag was the only thing that ever granted a bag.
            Text title = UIBuilder.StrokedText(safe, "Title", TextAnchor.MiddleCenter,
                bundle == null ? "NO BAG READY" : bundle.chestName, Color.white, 50);
            UIBuilder.Rect(title.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -116), new Vector2(-80, 72));

            GameObject stage = UIBuilder.Child("Stage", safe);
            UIBuilder.StretchRect(stage, Vector2.zero, Vector2.one, new Vector2(40, 230), new Vector2(-40, -210));

            UIBuilder.ButtonRefs cta = UIBuilder.GreenButton(safe, bundle == null ? "BACK TO HOME" : "TAP TO OPEN",
                new Vector2(0, UITheme.ButtonHeight), UITheme.TypeButton, null);
            UIBuilder.StretchRect(cta.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceXl, 36), new Vector2(-UITheme.SpaceXl, 36 + UITheme.ButtonHeight));

            Text counter = UIBuilder.Text(safe, "Counter", TextAnchor.MiddleCenter, "",
                new Color(1f, 1f, 1f, 0.8f), 24, FontStyle.Bold);
            UIBuilder.Rect(counter.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 156), new Vector2(-60, 40));

            if (bundle == null)
            {
                BuildEmpty(stage.transform);
                cta.button.onClick.AddListener(delegate { mgr.NavigateTab("HOME"); });
                return root;
            }

            BuildBag(stage.transform);
            ChestRevealController controller = root.AddComponent<ChestRevealController>();
            controller.Configure(bundle, mgr, stage.transform, title, counter, cta.label);
            cta.button.onClick.AddListener(controller.Advance);
            return root;
        }

        private static void BuildBag(Transform stage)
        {
            GameObject glow = UIBuilder.Child("Glow", stage);
            UIBuilder.Rect(glow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 80), new Vector2(520, 520));
            Image glowImage = glow.AddComponent<Image>();
            glowImage.sprite = UIBuilder.CircleSprite;
            glowImage.color = new Color(0.25f, 1f, 0.5f, 0.22f);
            glow.AddComponent<UIPulse>().scaleAmount = 0.1f;

            UIBuilder.IconAt(stage, IconId.Chest, 330f, UITheme.FillActionGreen, UITheme.OutlineNavy,
                new Vector2(0.5f, 0.5f), new Vector2(0, 80));
            Text hint = UIBuilder.StrokedText(stage, "Hint", TextAnchor.MiddleCenter, "REWARDS INSIDE", UITheme.GoldTop, 38);
            UIBuilder.Rect(hint.gameObject, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -190), new Vector2(-80, 60));
        }

        private static void BuildEmpty(Transform stage)
        {
            UIBuilder.IconAt(stage, IconId.Chest, 260f, new Color(1f, 1f, 1f, 0.45f), UITheme.OutlineNavy,
                new Vector2(0.5f, 0.5f), new Vector2(0, 80));
            Text hint = UIBuilder.Text(stage, "Hint", TextAnchor.MiddleCenter,
                "Start an unlock from the home screen and come back when its timer finishes.",
                Color.white, 29, FontStyle.Bold);
            UIBuilder.Rect(hint.gameObject, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -160), new Vector2(-100, 150));
            UIBuilder.Clamp(hint, 20);
        }
    }

    public class ChestRevealController : MonoBehaviour
    {
        private RewardBundle bundle;
        private ScreenManager manager;
        private Transform stage;
        private Text title;
        private Text counter;
        private Text ctaLabel;
        private int index = -1;
        private bool summaryShown;

        public void Configure(RewardBundle rewards, ScreenManager mgr, Transform stageRoot, Text titleText,
            Text counterText, Text buttonLabel)
        {
            bundle = rewards;
            manager = mgr;
            stage = stageRoot;
            title = titleText;
            counter = counterText;
            ctaLabel = buttonLabel;
            counter.text = bundle.entries.Count + " REWARDS";
        }

        public void Advance()
        {
            if (summaryShown)
            {
                manager.NavigateTab("HOME");
                return;
            }

            index++;
            if (index >= bundle.entries.Count)
            {
                ShowSummary();
                return;
            }
            ShowReward(bundle.entries[index]);
        }

        private void ShowReward(RewardEntry reward)
        {
            ClearStage();
            title.text = reward.name;
            counter.text = (bundle.entries.Count - index - 1) + " LEFT";
            ctaLabel.text = index == bundle.entries.Count - 1 ? "SHOW ALL" : "NEXT REWARD";

            GameObject card = UIBuilder.Panel(stage, "Reward", new Vector2(580, 680),
                RarityTop(reward.rarity), RarityBottom(reward.rarity));
            UIBuilder.Centered(card, new Vector2(580, 680));
            UIBuilder.ShineLayer(card.transform, 0.35f);
            UIBuilder.IconAt(card.transform, reward.icon, 230f, Color.white, UITheme.OutlineNavy,
                new Vector2(0.5f, 0.5f), new Vector2(0, 90));
            Text amount = UIBuilder.StrokedText(card.transform, "Amount", TextAnchor.MiddleCenter,
                "+" + reward.amount, Color.white, 76);
            UIBuilder.Rect(amount.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 110), new Vector2(-60, 100));
            UIFadeIn.Attach(card, 0.42f, 0f, 80f, 0.72f);
        }

        private void ShowSummary()
        {
            ClearStage();
            summaryShown = true;
            title.text = "YOU GOT";
            counter.text = "ALL REWARDS COLLECTED";
            ctaLabel.text = "COLLECT";

            int count = bundle.entries.Count;
            int columns = 2;
            float cardWidth = 360f;
            float cardHeight = 310f;
            float gap = 24f;
            float totalHeight = Mathf.Ceil(count / (float)columns) * (cardHeight + gap) - gap;
            for (int i = 0; i < count; i++)
            {
                RewardEntry reward = bundle.entries[i];
                int col = i % columns;
                int row = i / columns;
                GameObject card = UIBuilder.Panel(stage, "Summary" + i, new Vector2(cardWidth, cardHeight),
                    RarityTop(reward.rarity), RarityBottom(reward.rarity));
                float x = (col - 0.5f) * (cardWidth + gap);
                float y = totalHeight * 0.5f - cardHeight * 0.5f - row * (cardHeight + gap);
                UIBuilder.Rect(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, y), new Vector2(cardWidth, cardHeight));
                UIBuilder.IconAt(card.transform, reward.icon, 112f, Color.white, UITheme.OutlineNavy,
                    new Vector2(0.5f, 0.5f), new Vector2(0, 38));
                Text label = UIBuilder.Text(card.transform, "Name", TextAnchor.MiddleCenter,
                    reward.name + "  +" + reward.amount, Color.white, 24, FontStyle.Bold);
                UIBuilder.Rect(label.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0, 28), new Vector2(-30, 52));
                UIBuilder.ClampLine(label, 16);
                UIFadeIn.Attach(card, 0.28f, i * 0.08f, 36f, 0.88f);
            }
        }

        private void ClearStage()
        {
            for (int i = stage.childCount - 1; i >= 0; i--) Destroy(stage.GetChild(i).gameObject);
        }

        private static Color RarityTop(Rarity rarity)
        {
            Color top, bottom;
            UIBuilder.RarityGradient(rarity, out top, out bottom);
            return top;
        }

        private static Color RarityBottom(Rarity rarity)
        {
            Color top, bottom;
            UIBuilder.RarityGradient(rarity, out top, out bottom);
            return bottom;
        }
    }
}
