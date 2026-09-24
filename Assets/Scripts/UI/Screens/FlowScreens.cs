using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;
using Pickleball.Backend;

namespace Pickleball.UI
{
    // Shared board geometry keeps the new flow screens aligned with the supplied 390 x 844 art.
    public static class FlowScreens
    {
        public static Transform Shell(Transform parent, string name, string title, Action back, out GameObject root)
        {
            root = UIBuilder.Child(name, parent);
            UIBuilder.Fill(root);
            PSKit.Backdrop(root.transform);
            Transform board = PSKit.BoardHost(UIBuilder.SafeArea(root.transform)).transform;
            PSKit.TopBar(board, title, back);
            return board;
        }

        public static Text Label(Transform parent, string text, float y, float height = 36, bool heading = false)
        {
            Text label = heading ? PSKit.Display(parent, "Heading", TextAnchor.MiddleCenter, text, UITheme.Volt, 56)
                : PSKit.Body(parent, "Body", TextAnchor.MiddleCenter, text, UITheme.InkOnLight, 36);
            PSKit.BoardRow(label.gameObject, y, height, 30);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            return label;
        }

        public static void ActionButton(Transform parent, string label, float y, Action action, bool primary = false)
        {
            if (primary)
            {
                var button = PSKit.PrimaryCta(parent, label, new Vector2(UITheme.F(330), UITheme.F(64)), 54, action);
                PSKit.BoardRow(button.root, y, 64, 30);
            }
            else
            {
                Text button = PSKit.TextButton(parent, label, UITheme.InkOnLight, 42, action);
                PSKit.BoardRow(button.transform.parent.gameObject, y, 44, 24);
            }
        }

        private static void Art(Transform board, string asset, float y, float diameter)
        {
            Image image = UIReferenceArt.Draw(board, asset);
            PSKit.BoardDisc(image.gameObject, y, diameter);
        }

        public static GameObject Play(Transform parent, ScreenManager mgr)
        {
            GameObject root;
            Transform board = Shell(parent, "PlayModeScreen", "LET'S PLAY", () => mgr.NavigateTab("HOME"), out root);
            Art(board, "season_medal", 202, 108);
            Label(board, "YOUR NEXT MATCH", 294, 38, true);
            Label(board, "Choose your court. Make it count.", 334);
            var tour = MetaGameState.CurrentTour;
            var card = PSKit.DarkCard(board, "TourSummary", new Vector2(UITheme.F(330), UITheme.F(128)), 32);
            PSKit.BoardRow(card.root, 439, 128, 30);
            Text info = PSKit.Body(card.face.transform, "Summary", TextAnchor.MiddleCenter,
                "TOUR  /  " + tour.name + "\nPlay against the AI\nEntry " + tour.entryCoins + " coins  ·  Win reward " + tour.rewardCoins, UITheme.Cream, 35);
            UIBuilder.Fill(info.gameObject);
            ActionButton(board, "CHOOSE TOUR", 551, () => mgr.Show(ScreenId.TourSelect), true);
            ActionButton(board, "PLAY RANKED", 648, mgr.StartRankedMatchmaking);
            Label(board, "Live opponent · Free entry · Trophy stakes", 689, 32);
            ActionButton(board, "HOW TO PLAY", 761, mgr.ShowHelp);
            return root;
        }

        public static GameObject Bags(Transform parent, ScreenManager mgr)
        {
            GameObject root;
            Transform board = Shell(parent, "BagInventoryScreen", "YOUR BAGS", () => mgr.NavigateTab("HOME"), out root);
            Label(board, "Win bags. Unlock your next upgrade.", 145);
            RectTransform content;
            GameObject scroll = UIBuilder.ScrollView(board, out content);
            UIBuilder.StretchRect(scroll, Vector2.zero, Vector2.one,
                new Vector2(UITheme.F(24), UITheme.F(166)), new Vector2(-UITheme.F(24), -UITheme.F(184)));
            for (int i = 0; i < MetaGameState.BagSlots.Count; i++)
            {
                int index = i;
                var card = PSKit.DarkCard(content, "Slot" + i, new Vector2(UITheme.F(342), UITheme.F(105)), 30);
                UIBuilder.StretchRect(card.root, new Vector2(0, 1), Vector2.one,
                    new Vector2(0, -UITheme.F(i * 117 + 105)), new Vector2(0, -UITheme.F(i * 117)));
                Image gift = UIReferenceArt.Draw(card.face.transform, "gift");
                UIBuilder.Rect(gift.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f),
                    new Vector2(UITheme.F(10), 0), new Vector2(UITheme.F(59), UITheme.F(65)));
                Text text = PSKit.Body(card.face.transform, "Status", TextAnchor.MiddleLeft, "", UITheme.Cream, 34);
                UIBuilder.StretchRect(text.gameObject, Vector2.zero, Vector2.one, new Vector2(UITheme.F(82), UITheme.F(12)), new Vector2(-UITheme.F(10), -UITheme.F(8)));
                Button button = card.root.AddComponent<Button>();
                button.targetGraphic = card.root.GetComponent<Image>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => mgr.SelectBag(index));
                UIBuilder.RunOnScreen(root, UpdateBag(index, text, button, gift));
            }
            content.sizeDelta = new Vector2(0, UITheme.F(MetaGameState.BagSlots.Count * 117));
            Label(board, "One bag unlocks at a time.\nTap an unlocking bag to finish with gems.", 713, 52);
            ActionButton(board, "WEEKLY REWARD", 773, () => mgr.ShowStartupLeagueResult());
            ActionButton(board, "BACK TO HOME", 817, () => mgr.NavigateTab("HOME"));
            return root;
        }

        private static IEnumerator UpdateBag(int index, Text text, Button button, Image gift)
        {
            while (text != null)
            {
                MetaGameState.RefreshBagTimers();
                var slot = MetaGameState.BagSlots[index];
                bool busy = false;
                foreach (var other in MetaGameState.BagSlots) if (other.state == BagSlotState.Unlocking) busy = true;
                string name = slot.state == BagSlotState.Empty ? "EMPTY SLOT" : (ChestCatalog.Find(slot.bagId) ?? ChestCatalog.Fallback).name.ToUpperInvariant();
                string action = slot.state == BagSlotState.Empty ? "Win a match to earn a bag" : slot.state == BagSlotState.Ready ? "READY  ·  TAP TO OPEN"
                    : slot.state == BagSlotState.Unlocking ? MetaGameState.BagTimerLabel(slot) + "  ·  FINISH NOW"
                    : busy ? "SEALED  ·  Another bag is unlocking" : "SEALED  ·  TAP TO UNLOCK";
                text.text = name + "\n" + action;
                button.interactable = slot.state != BagSlotState.Empty && !(slot.state == BagSlotState.Sealed && busy);
                gift.color = new Color(1, 1, 1, slot.state == BagSlotState.Empty ? .25f : 1);
                yield return new WaitForSecondsRealtime(.5f);
            }
        }

        public static GameObject Account(Transform parent, ScreenManager mgr)
        {
            GameObject root;
            Transform board = Shell(parent, "AccountScreen", "PLAYER PROFILE", mgr.CloseAccount, out root);
            Art(board, "portrait_you", 245, 160);
            Label(board, MetaGameState.PlayerName, 371, 45, true);
            Label(board, "GUEST PLAYER", 415);
            var service = ProfileService.Instance;
            Label(board, service != null && service.UsesCloudProfile ? "Progress syncs with this guest identity." : "Progress is saved on this device.", 464, 56);
            Label(board, "Account linking is not available yet.\nKeep your player ID for support.", 537, 65);
            ActionButton(board, "COPY PLAYER ID", 645, () => {
                GUIUtility.systemCopyBuffer = service != null ? service.GuestId : "";
                mgr.ShowNotice("PLAYER ID COPIED", "Your player ID is ready to paste.");
            }, true);
            ActionButton(board, "BACK", 747, mgr.CloseAccount);
            return root;
        }

        public static GameObject Purchase(Transform parent, ScreenManager mgr, string product)
        {
            GameObject root;
            Transform board = Shell(parent, "PurchaseScreen", "STORE", mgr.GoBack, out root);
            Art(board, "gift", 256, 136);
            Label(board, product.ToUpperInvariant(), 397, 66, true);
            Label(board, "COMING SOON", 461, 36, true);
            Label(board, "This purchase is not available yet.\nNo payment has been taken.", 526, 76);
            ActionButton(board, "BACK", 676, mgr.GoBack, true);
            return root;
        }

        public static GameObject ProfileRecovery(Transform parent, ScreenManager mgr)
        {
            GameObject root;
            Transform board = Shell(parent, "ProfileRecoveryScreen", "YOUR PROGRESS", null, out root);
            Art(board, "season_medal", 269, 130);
            Label(board, "LET'S GET YOU BACK", 402, 66, true);
            Label(board, "We couldn't finish loading your profile.\nCheck your connection and try again.", 500, 90);
            ActionButton(board, "RETRY", 646, mgr.RetryProfileLoad, true);
            Label(board, "Your existing progress will not be replaced.", 730, 50);
            return root;
        }
    }
}
