using System;
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
