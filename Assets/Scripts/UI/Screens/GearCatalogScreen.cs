using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;

namespace Pickleball.UI
{
    /// <summary>Category inventory between the loadout slots and an individual item's detail view.</summary>
    public static class GearCatalogScreen
    {
        public static GameObject Build(Transform parent, ScreenManager mgr, GearType type)
        {
            GameObject root = UIBuilder.Child("GearCatalogScreen", parent);
            UIBuilder.Fill(root);
            PSKit.Backdrop(root.transform);
            Transform safe = UIBuilder.SafeArea(root.transform);
            UIBuilder.TopBar(safe, type.ToString().ToUpperInvariant(), delegate { mgr.NavigateTab("GEAR"); },
                IconId.Coin, MetaGameState.Coins.ToString("N0"));

            List<GearItem> items = MetaGameState.Gear.FindAll(g => g.type == type);
            if (items.Count == 0)
            {
                Text empty = UIBuilder.StrokedText(safe, "Empty", TextAnchor.MiddleCenter,
                    "NO " + type.ToString().ToUpperInvariant() + " ITEMS YET", Color.white, 42);
                UIBuilder.Rect(empty.gameObject, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0, 120), new Vector2(-100, 80));
                UIBuilder.ButtonRefs shop = UIBuilder.GoldButton(safe, "VISIT SHOP", new Vector2(0, UITheme.ButtonHeight), UITheme.TypeButton,
                    delegate { mgr.Show(ScreenId.Shop); });
                UIBuilder.StretchRect(shop.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(UITheme.SpaceXl, 44), new Vector2(-UITheme.SpaceXl, 44 + UITheme.ButtonHeight));
                return root;
            }

            RectTransform content;
            GameObject scroll = UIBuilder.ScrollView(safe, out content);
            UIBuilder.StretchRect(scroll, Vector2.zero, Vector2.one,
                new Vector2(UITheme.SpaceLg, 40), new Vector2(-UITheme.SpaceLg, -UITheme.ContentTopInset));

            const int columns = 2;
            const float gap = 24f;
            const float cardHeight = 470f;
            float cardWidth = (1020f - gap) * 0.5f;
            int rows = Mathf.CeilToInt(items.Count / (float)columns);
            content.sizeDelta = new Vector2(0, rows * (cardHeight + gap));

            for (int i = 0; i < items.Count; i++)
            {
                GearItem item = items[i];
                int col = i % columns;
                int row = i / columns;
                GameObject cell = UIBuilder.Child("Cell" + i, content);
                float left = col * 0.5f;
                float right = (col + 1) * 0.5f;
                UIBuilder.StretchRect(cell, new Vector2(left, 1f), new Vector2(right, 1f),
                    new Vector2(col == 0 ? 0 : gap * 0.5f, -(row + 1) * (cardHeight + gap)),
                    new Vector2(col == 0 ? -gap * 0.5f : 0, -row * (cardHeight + gap) - gap));

                // cardsNeeded is int.MaxValue once an item hits max level (GearCatalog.ApplyLevel) --
                // without this guard a maxed item's card would show an all-but-empty progress meter.
                float progress = MetaGameState.IsMaxLevel(item) ? 1f : (float)item.cardsCollected / Mathf.Max(1, item.cardsNeeded);
                // The 3 stats this item's slot actually carries (Docs/GearProgression.md#2-slots) --
                // same DisplayLabels/DisplayValues GearDetailScreen's upgrade preview already uses, so
                // an item shows identical stats whether you're looking at its card or its detail screen.
                string[] statLabels = GearProgressionCurve.DisplayLabels(item.type);
                int[] statValues = GearProgressionCurve.DisplayValues(item.type, GearProgressionCurve.StatsOf(item));
                GameObject card = UIBuilder.GearCard(cell.transform, new Vector2(cardWidth, cardHeight), item.icon,
                    item.name, item.rarity, progress, statLabels, statValues);
                UIBuilder.Fill(card);
                Button button = cell.AddComponent<Button>();
                Image raycast = cell.AddComponent<Image>();
                raycast.color = new Color(0, 0, 0, 0);
                button.targetGraphic = raycast;
                button.transition = Selectable.Transition.None;
                GearItem captured = item;
                button.onClick.AddListener(delegate { mgr.ShowGearDetail(captured); });

                string status = MetaGameState.IsEquipped(item) ? "EQUIPPED" : MetaGameState.CanUpgrade(item) ? "UPGRADE READY" : "LEVEL " + item.level;
                Color statusColor = MetaGameState.IsEquipped(item) ? UITheme.FillActionGreenDeep : UITheme.PanelDarkDeep;
                GameObject chip = UIBuilder.Chip(card.transform, status, statusColor, 48f, 19);
                UIBuilder.Rect(chip, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0, 34), chip.GetComponent<RectTransform>().sizeDelta);
            }

            return root;
        }
    }
}
