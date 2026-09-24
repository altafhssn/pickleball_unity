using System;
using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;

namespace Pickleball.UI
{
    public static class SeasonPassScreen
    {
        public static GameObject Build(Transform parent, ScreenManager mgr)
        {
            GameObject root;
            Transform board = FlowScreens.Shell(parent, "SeasonPassScreen", "SEASON " + MetaGameState.CurrentSeasonNumber,
                () => mgr.NavigateTab("HOME"), out root);
            DateTime now = DateTime.UtcNow;
            int days = (int)Math.Ceiling((new DateTime(now.Year, now.Month, 1).AddMonths(1) - now).TotalDays);
            FlowScreens.Label(board, "TIER " + MetaGameState.SeasonTier, 152, 42, true);
            FlowScreens.Label(board, MetaGameState.SeasonXP + " / " + MetaGameState.SeasonXPPerTier + " XP  ·  " + days + " days remaining", 198);
            FlowScreens.Label(board, "FREE REWARDS", 258, 40, true);
            RectTransform content;
            GameObject scroll = UIBuilder.ScrollView(board, out content);
            UIBuilder.StretchRect(scroll, Vector2.zero, Vector2.one,
                new Vector2(UITheme.F(24), UITheme.F(192)), new Vector2(-UITheme.F(24), -UITheme.F(293)));
            int count = MetaGameState.SeasonTier + 2;
            for (int i = 1; i <= count; i++)
            {
                int tier = i;
                bool claimed = MetaGameState.IsSeasonPassTierClaimed(tier);
                bool unlocked = tier <= MetaGameState.SeasonTier;
                var card = PSKit.DarkCard(content, "Tier" + tier, new Vector2(UITheme.F(342), UITheme.F(94)), 28);
                UIBuilder.StretchRect(card.root, new Vector2(0, 1), Vector2.one,
                    new Vector2(0, -UITheme.F((i - 1) * 106 + 94)), new Vector2(0, -UITheme.F((i - 1) * 106)));
                string reward = MetaGameState.SeasonRewardCoins(tier) + " COINS";
                if (MetaGameState.SeasonRewardGems(tier) > 0) reward += "  +  " + MetaGameState.SeasonRewardGems(tier) + " GEMS";
                Text label = PSKit.Body(card.face.transform, "Reward", TextAnchor.MiddleCenter,
                    "TIER " + tier + "  ·  " + reward + "\n" + (claimed ? "CLAIMED" : unlocked ? "TAP TO CLAIM" : "EARN XP TO UNLOCK"), UITheme.Cream, 35);
                UIBuilder.Fill(label.gameObject);
                Button button = card.root.AddComponent<Button>();
                button.targetGraphic = card.root.GetComponent<Image>();
                button.interactable = unlocked && !claimed;
                button.onClick.AddListener(() => {
                    if (MetaGameState.ClaimSeasonPassReward(tier) != null) mgr.Show(ScreenId.SeasonPass, false);
                });
            }
            content.sizeDelta = new Vector2(0, UITheme.F(count * 106));
            FlowScreens.ActionButton(board, "PREMIUM PASS", 706, () => mgr.ShowStoreUnavailable("Premium season pass"), true);
            FlowScreens.Label(board, "Premium rewards are coming soon", 760);
            return root;
        }
    }
}
