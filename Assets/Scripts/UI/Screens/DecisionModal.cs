using System;
using UnityEngine;
using UnityEngine.UI;

namespace Pickleball.UI
{
    /// <summary>
    /// Reusable blocking decision layer for daily rewards, pre-match validation and economy
    /// warnings. Restyled onto the Pickle Smash stack: a near-black card over a hard scrim, one
    /// volt confirm and one neutral dismiss. The dismiss used to be a gold button, which read as
    /// the more rewarding of the two choices and put the emphasis on "LATER".
    /// </summary>
    public static class DecisionModal
    {
        public static GameObject Build(Transform parent, string title, string body,
            string primaryLabel, string secondaryLabel, Action primary, Action secondary, IconId icon)
        {
            GameObject root = UIBuilder.Child("DecisionModal", parent);
            UIBuilder.Fill(root);
            UIBuilder.ModalScrim(root.transform);
            Transform safe = UIBuilder.SafeArea(root.transform);

            Vector2 size = new Vector2(UITheme.ModalWidth, 700);
            PSKit.Stack card = PSKit.BuildStack(safe, "Card", size, 56, false,
                UITheme.Ink0, UITheme.Panel, UITheme.SheenDark, 12, 18, 9);
            UIBuilder.Centered(card.root, size);
            UIFadeIn.Attach(card.root, UITheme.ModalEnterTime, 0f, 40f, 0.94f);

            // The reward disc straddles the card's top edge rather than sitting inside it — the
            // same rule the badges follow, and it stops the icon from stealing a row of body space.
            PSKit.Stack disc = PSKit.BuildStack(card.root.transform, "IconDisc", new Vector2(150, 150),
                75, true, UITheme.GoldShade, UITheme.Gold, UITheme.SheenBlaze, 9, 8, 6);
            UIBuilder.Rect(disc.root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 14), new Vector2(150, 150));
            UIBuilder.IconAt(disc.face.transform, icon, 84f, UITheme.Ink1, new Color(0, 0, 0, 0),
                new Vector2(0.5f, 0.5f), Vector2.zero);

            Text heading = PSKit.Display(card.face.transform, "Title", TextAnchor.MiddleCenter,
                title, UITheme.Volt, UITheme.TypeScreenTitle);
            UIBuilder.Rect(heading.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -128), new Vector2(-70, 66));
            UIBuilder.ClampLine(heading, 26);

            Text copy = PSKit.Body(card.face.transform, "Body", TextAnchor.UpperCenter, body, UITheme.Cream, 29);
            UIBuilder.Rect(copy.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -206), new Vector2(-90, 140));
            UIBuilder.Clamp(copy, 20);

            PSKit.ButtonStack primaryBtn = PSKit.PrimaryCta(card.face.transform, primaryLabel,
                new Vector2(0, UITheme.ButtonHeight), UITheme.TypeButton, primary);
            UIBuilder.StretchRect(primaryBtn.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(46, 156), new Vector2(-46, 156 + UITheme.ButtonHeight));

            PSKit.ButtonStack secondaryBtn = PSKit.SecondaryCta(card.face.transform, secondaryLabel,
                new Vector2(0, UITheme.ButtonHeightGhost), UITheme.TypeButtonSm, secondary);
            UIBuilder.StretchRect(secondaryBtn.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(46, 34), new Vector2(-46, 34 + UITheme.ButtonHeightGhost));

            return root;
        }
    }
}
