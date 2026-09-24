using System;
using UnityEngine;
using UnityEngine.UI;

namespace Pickleball.UI
{
    /// <summary>
    /// PICKLE SMASH component kit — the Figma boards in Docs/Figma expressed as runtime builders.
    ///
    /// Every solid shape in the direction is the same three-part stack, and this class exists so no
    /// screen ever hand-rolls it again:
    ///
    ///     OUTLINE   hard black, full size, the silhouette
    ///     BASE      the colour's dark shade, inset by the outline weight, full height
    ///     FACE      the colour itself, inset from the base on the sides and top, and stopping
    ///               <see cref="UITheme.FaceLift"/> short of the bottom so a sliver of BASE shows
    ///     SHEEN     a white band hugging the top of the face, at the intent's own opacity
    ///
    /// That visible sliver of base along the bottom edge is the whole trick: it is what makes a
    /// control read as a physical key with a side wall, and pressing simply slides the face down
    /// onto it. Flat rectangles and soft drop shadows both break the look instantly.
    ///
    /// <see cref="UIBuilder"/> stays the generic UGUI plumbing (rects, text, icons, scroll views);
    /// this is the styled vocabulary built on top of it.
    /// </summary>
    public static class PSKit
    {
        // ============================================================
        // STACK — the one primitive everything else is made of
        // ============================================================

        public class Stack
        {
            /// <summary>Outermost object. Size and anchor THIS.</summary>
            public GameObject root;
            /// <summary>The dark shade layer. Full size inside the outline.</summary>
            public Image baseImage;
            /// <summary>The colour layer. Parent anything that sits "on" the control here.</summary>
            public GameObject face;
            public Image faceImage;
            public GameObject sheen;
            /// <summary>How far the face sits above the base's bottom edge. A press travels exactly
            /// this far, so the control seats flush rather than overshooting.</summary>
            public int faceLift;
        }

        /// <summary>
        /// Builds the OUTLINE + BASE + FACE + SHEEN stack into a new child of <paramref name="parent"/>.
        /// The returned object is centred at (0,0) with the given size — position it afterwards.
        /// </summary>
        /// <param name="capsule">True for fully-rounded pills and CTAs; false uses <paramref name="radius"/>.</param>
        public static Stack BuildStack(Transform parent, string name, Vector2 size, int radius, bool capsule,
            Color baseColor, Color faceColor, float sheenAlpha, int outlineWeight = -1, int faceLift = -1, int bevel = -1)
        {
            // The boards keep the same proportions at every size rather than a fixed wall: on the
            // 70px CTA the keyline is 3px and the base shows 12px under the face; on a 29px pill it
            // is 2px and the face is flush. A fixed 14/16 collapsed anything short into a sliver, so
            // the defaults are derived from the control's own height.
            float h = size.y > 1f ? size.y : 120f;
            if (outlineWeight < 0) outlineWeight = Mathf.Clamp(Mathf.RoundToInt(h * 0.055f), 4, 12);
            if (bevel < 0) bevel = Mathf.Clamp(Mathf.RoundToInt(h * 0.045f), 3, 10);
            if (faceLift < 0) faceLift = Mathf.Clamp(Mathf.RoundToInt(h * 0.17f), 5, 36);

            Sprite shape = capsule ? UIBuilder.CapsuleSprite : UIBuilder.RoundedSprite(radius);
            Sprite innerShape = capsule ? UIBuilder.CapsuleSprite : UIBuilder.RoundedSprite(Mathf.Max(2, radius - outlineWeight / 2));

            GameObject root = UIBuilder.Child(name, parent);
            UIBuilder.Centered(root, size);

            Image outline = root.AddComponent<Image>();
            outline.sprite = shape;
            if (capsule) outline.gameObject.AddComponent<UICapsuleFit>();
            outline.type = Image.Type.Sliced;
            outline.color = UITheme.Outline;

            GameObject baseGO = UIBuilder.Child("Base", root.transform);
            UIBuilder.StretchRect(baseGO, Vector2.zero, Vector2.one,
                new Vector2(outlineWeight, outlineWeight), new Vector2(-outlineWeight, -outlineWeight));
            Image baseImg = baseGO.AddComponent<Image>();
            baseImg.sprite = innerShape;
            if (capsule) baseImg.gameObject.AddComponent<UICapsuleFit>();
            baseImg.type = Image.Type.Sliced;
            baseImg.color = baseColor;

            // Face: inset on the sides and top, stopping short of the bottom. Anchored top so it
            // keeps its height when the stack is stretched to fill a row.
            GameObject face = UIBuilder.Child("Face", baseGO.transform);
            UIBuilder.StretchRect(face, Vector2.zero, Vector2.one,
                new Vector2(bevel, faceLift), new Vector2(-bevel, -bevel));
            Image faceImg = face.AddComponent<Image>();
            faceImg.sprite = innerShape;
            if (capsule) faceImg.gameObject.AddComponent<UICapsuleFit>();
            faceImg.type = Image.Type.Sliced;
            faceImg.color = faceColor;

            GameObject sheen = null;
            if (sheenAlpha > 0f)
            {
                sheen = UIBuilder.Child("Sheen", face.transform);
                Image sImg = sheen.AddComponent<Image>();
                sImg.sprite = innerShape;
                if (capsule) sImg.gameObject.AddComponent<UICapsuleFit>();
                sImg.type = Image.Type.Sliced;
                sImg.raycastTarget = false;

                if (capsule)
                {
                    // Pills and CTAs get a brighter INNER pill inset on every side, which is what
                    // the boards actually draw. A white band across the top turned the kit's most
                    // prominent button into a glossy glass capsule — the one look this direction
                    // is explicitly not.
                    UIBuilder.StretchRect(sheen, Vector2.zero, Vector2.one,
                        new Vector2(bevel * 2f, bevel * 1.4f), new Vector2(-bevel * 2f, -bevel * 1.4f));
                    sImg.color = new Color(1f, 1f, 1f, sheenAlpha * 0.5f);
                }
                else
                {
                    // Cards keep a top-lit band, faded out so it does not leave a seam across the
                    // middle of the panel.
                    UIBuilder.StretchRect(sheen, new Vector2(0f, 1f - UITheme.SheenHeight), Vector2.one,
                        new Vector2(1, 0), new Vector2(-1, -1));
                    sImg.color = Color.white;
                    UIGradient sGrad = sheen.AddComponent<UIGradient>();
                    sGrad.topColor = new Color(1f, 1f, 1f, sheenAlpha * 0.7f);
                    sGrad.bottomColor = new Color(1f, 1f, 1f, 0f);
                    sGrad.direction = GradientDirection.Vertical;
                }
            }

            Stack s = new Stack();
            s.root = root;
            s.baseImage = baseImg;
            s.face = face;
            s.faceImage = faceImg;
            s.sheen = sheen;
            s.faceLift = faceLift;
            return s;
        }

        /// <summary>The neutral dark stack — panels, cards, pills, the nav bar, utility buttons.</summary>
        public static Stack DarkCard(Transform parent, string name, Vector2 size, int radius, bool capsule = false)
        {
            return BuildStack(parent, name, size, radius, capsule, UITheme.Ink0, UITheme.Panel, UITheme.SheenDark);
        }

        // ============================================================
        // BACKDROP
        // ============================================================

        /// <summary>
        /// The orchid-to-blush wash every meta screen sits on. Full-bleed: it paints under the notch
        /// and the home indicator, so it is parented outside the safe area.
        /// </summary>
        public static GameObject Backdrop(Transform parent, Color top, Color bottom)
        {
            GameObject go = UIBuilder.Child("Backdrop", parent);
            UIBuilder.Fill(go);
            Image img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;
            UIGradient grad = go.AddComponent<UIGradient>();
            grad.topColor = top;
            grad.bottomColor = bottom;
            grad.direction = GradientDirection.Vertical;
            return go;
        }

        public static GameObject Backdrop(Transform parent) { return Backdrop(parent, UITheme.BgOrchid, UITheme.BgBlush); }

        // ============================================================
        // TEXT
        // ============================================================

        /// <summary>
        /// A display-face label: cream (or volt), always carrying the black stroke. Anything set in
        /// the game's voice — titles, CTA labels, score digits — goes through here so the stroke
        /// weight tracks the type scale.
        /// </summary>
        public static Text Display(Transform parent, string name, TextAnchor anchor, string content, Color color, int size)
        {
            Text t = UIBuilder.StrokedText(parent, name, anchor, content, color, size);
            if (name == "Title" && size >= 60)
            {
                Font headline = Resources.Load<Font>("Fonts/Coiny-Regular");
                if (headline != null) t.font = headline;
            }
            Outline o = t.GetComponent<Outline>();
            if (o != null)
            {
                float d = UITheme.OutlineDistanceFor(size);
                o.effectColor = UITheme.OutlineText;
                o.effectDistance = new Vector2(d, -d);
                if (name == "Title" || name == "League" || name == "Name" || name == "Rank" ||
                    name == "Starting" || name == "Status") o.enabled = false;
            }
            return t;
        }

        /// <summary>Body copy — no stroke, cream on dark, ink on the backdrop.</summary>
        public static Text Body(Transform parent, string name, TextAnchor anchor, string content, Color color, int size)
        {
            Text text = UIBuilder.Text(parent, name, anchor, content, color, size, FontStyle.Normal);
            text.font = UIBuilder.DisplayFont;
            return text;
        }

        // ============================================================
        // BUTTONS
        // ============================================================

        public class ButtonStack
        {
            public GameObject root;
            public Button button;
            public Text label;
            public Stack stack;
        }

        /// <summary>
        /// A pressable stack. The BASE stays put and the FACE travels down onto it, so the control
        /// visibly loses its side wall on press — the same motion a real key makes.
        /// </summary>
        public static ButtonStack PressStack(Transform parent, string name, Vector2 size, int radius, bool capsule,
            Color baseColor, Color faceColor, float sheenAlpha, Action onClick)
        {
            Stack s = BuildStack(parent, name, size, radius, capsule, baseColor, faceColor, sheenAlpha);
            UIBuilder.ExpandHitArea(s.root, size);

            Button btn = s.root.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = s.root.GetComponent<Image>();
            if (onClick != null) btn.onClick.AddListener(delegate { onClick(); });

            UIButtonPress press = s.root.AddComponent<UIButtonPress>();
            press.content = s.face.GetComponent<RectTransform>();
            press.travel = s.faceLift;

            ButtonStack b = new ButtonStack();
            b.root = s.root;
            b.button = btn;
            b.stack = s;
            return b;
        }

        /// <summary>The screen's one headline action. Volt face, black label, full capsule.</summary>
        public static ButtonStack PrimaryCta(Transform parent, string label, Vector2 size, int fontSize, Action onClick)
        {
            ButtonStack b = PressStack(parent, "Cta_" + label, size, UITheme.RadiusButton, true,
                UITheme.VoltDeep, UITheme.Volt, UITheme.SheenVolt, onClick);
            b.label = Display(b.stack.face.transform, "Label", TextAnchor.MiddleCenter, label, UITheme.Ink1, fontSize);
            UIBuilder.Fill(b.label.gameObject);
            Outline o = b.label.GetComponent<Outline>();
            // The label is ink on volt; a black stroke on black type would only fatten it.
            if (o != null) UnityEngine.Object.Destroy(o);
            UIButtonTypography.Attach(b.label, size.y > 0 && size.y < 110);
            b.stack.baseImage.enabled = false;
            b.stack.faceImage.enabled = false;
            if (b.stack.sheen != null) b.stack.sheen.SetActive(false);
            b.root.GetComponent<Image>().color = Color.clear;
            Image skin = UIReferenceArt.Draw(b.root.transform, "cta", false);
            skin.transform.SetAsFirstSibling();
            b.label.transform.SetParent(skin.transform, false);
            b.root.GetComponent<UIButtonPress>().content = skin.rectTransform;
            b.root.GetComponent<UIButtonPress>().travel = UITheme.F(3f);
            UIBuilder.StretchRect(b.label.gameObject, Vector2.zero, Vector2.one,
                new Vector2(16, UITheme.F(12f)), new Vector2(-16, 0));
            return b;
        }

        /// <summary>Neutral secondary action — SETTINGS under RESUME, CANCEL, and so on.</summary>
        public static ButtonStack SecondaryCta(Transform parent, string label, Vector2 size, int fontSize, Action onClick)
        {
            ButtonStack b = PressStack(parent, "Btn_" + label, size, UITheme.RadiusButton, true,
                UITheme.Ink0, UITheme.PanelSoft, UITheme.SheenDark, onClick);
            b.label = Display(b.stack.face.transform, "Label", TextAnchor.MiddleCenter, label, UITheme.Cream, fontSize);
            UIBuilder.Fill(b.label.gameObject);
            UIButtonTypography.Attach(b.label, size.y > 0 && size.y < 110);
            return b;
        }

        /// <summary>Destructive action — CLAIM, QUIT MATCH.</summary>
        public static ButtonStack BlazeCta(Transform parent, string label, Vector2 size, int fontSize, Action onClick)
        {
            ButtonStack b = PressStack(parent, "Btn_" + label, size, UITheme.RadiusButton, true,
                UITheme.BlazeDeep, UITheme.Blaze, UITheme.SheenBlaze, onClick);
            b.label = Display(b.stack.face.transform, "Label", TextAnchor.MiddleCenter, label, UITheme.Cream, fontSize);
            UIBuilder.Fill(b.label.gameObject);
            UIButtonTypography.Attach(b.label, size.y > 0 && size.y < 110);
            return b;
        }

        /// <summary>
        /// Circular icon button. Dark by default (back, settings, menu); pass a colour pair for the
        /// volt "next" and blaze "close" variants.
        /// </summary>
        public static ButtonStack IconButton(Transform parent, IconId icon, float diameter, Action onClick,
            Color? baseColor = null, Color? faceColor = null, float sheen = UITheme.SheenDark, Color? glyph = null)
        {
            Color bc = baseColor ?? UITheme.Ink0;
            Color fc = faceColor ?? UITheme.Panel;
            ButtonStack b = PressStack(parent, "Ico_" + icon, new Vector2(diameter, diameter),
                Mathf.RoundToInt(diameter * 0.5f), true, bc, fc, sheen, onClick);
            if (icon == IconId.ChevronLeft)
            {
                b.stack.baseImage.gameObject.SetActive(false);
                b.root.GetComponent<Image>().color = Color.clear;
                Image art = UIReferenceArt.Draw(b.root.transform, "back");
                b.root.GetComponent<UIButtonPress>().content = art.rectTransform;
                b.root.GetComponent<UIButtonPress>().travel = UITheme.F(1f);
            }
            else
            {
                UIBuilder.IconAt(b.stack.face.transform, icon, diameter * 0.46f, glyph ?? UITheme.Cream,
                    new Color(0f, 0f, 0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero);
            }
            return b;
        }

        // ============================================================
        // BADGE
        // ============================================================

        /// <summary>
        /// A count or rank marker. Always straddles the OUTER edge of whatever it belongs to —
        /// never sits inside the face — so it reads as attached rather than as content.
        /// </summary>
        public static GameObject Badge(Transform parent, string text, float diameter, Vector2 anchor, Vector2 offset,
            Color baseColor, Color faceColor, Color inkColor)
        {
            Stack s = BuildStack(parent, "Badge", new Vector2(diameter, diameter),
                Mathf.RoundToInt(diameter * 0.5f), true, baseColor, faceColor, UITheme.SheenBlaze,
                Mathf.RoundToInt(diameter * 0.13f), Mathf.RoundToInt(diameter * 0.10f));
            UIBuilder.Rect(s.root, anchor, anchor, new Vector2(0.5f, 0.5f), offset, new Vector2(diameter, diameter));
            Text t = Display(s.face.transform, "Count", TextAnchor.MiddleCenter, text, inkColor, Mathf.RoundToInt(diameter * 0.5f));
            UIBuilder.Fill(t.gameObject);
            UIBuilder.ClampLine(t);
            return s.root;
        }

        /// <summary>Unread / count badge in blaze.</summary>
        public static GameObject CountBadge(Transform parent, int count, float diameter, Vector2 anchor, Vector2 offset)
        {
            return Badge(parent, count.ToString(), diameter, anchor, offset, UITheme.BlazeDeep, UITheme.Blaze, UITheme.Cream);
        }

        // ============================================================
        // CURRENCY PILL
        // ============================================================

        /// <summary>
        /// Dark capsule, currency disc on the left, count in cream. Optionally carries a grass "+"
        /// straddling the right edge, which is the route to the shop.
        /// </summary>
        public static GameObject CurrencyPill(Transform parent, IconId icon, string amount, Vector2 size, Action onAdd)
        {
            // Flat: the board draws the pills as one dark capsule with a keyline, no side wall.
            // A lifted face on a 29px-tall chip reads as a mistake, not as depth.
            Stack s = BuildStack(parent, "Pill_" + icon, size, UITheme.RadiusChip, true,
                UITheme.Panel, UITheme.Panel, 0f, 7, 0, 0);

            float disc = size.y * 0.62f;
            Color discTop, discDeep, discInk;
            UIBuilder.CurrencyColors(icon, out discTop, out discDeep, out discInk);

            GameObject discGO = UIBuilder.Child("Disc", s.face.transform);
            UIBuilder.Rect(discGO, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(size.y * 0.16f, 0), new Vector2(disc, disc));
            if (icon == IconId.Coin || icon == IconId.Trophy)
            {
                UIReferenceArt.Draw(discGO.transform, icon == IconId.Coin ? "coin" : "nav_league");
            }
            else
            {
                discGO.AddComponent<Image>();
                UIBuilder.OutlinedGradientFill(discGO, UIBuilder.CircleSprite, discTop, discDeep, UITheme.Outline, 4f);
                UIBuilder.IconAt(discGO.transform, icon, disc * 0.62f, UITheme.Cream, new Color(0, 0, 0, 0),
                    new Vector2(0.5f, 0.5f), Vector2.zero);
            }

            float rightInset = onAdd != null ? size.y * 0.72f : size.y * 0.30f;
            Text amountText = Body(s.face.transform, "Amount", TextAnchor.MiddleCenter, amount, UITheme.Cream, UITheme.TypePill);
            UIBuilder.StretchRect(amountText.gameObject, Vector2.zero, Vector2.one,
                new Vector2(disc + size.y * 0.30f, 0), new Vector2(-rightInset, 0));
            UIBuilder.ClampLine(amountText, 18);

            if (onAdd != null)
            {
                float plus = size.y * 0.80f;
                ButtonStack add = PressStack(s.root.transform, "Add", new Vector2(plus, plus),
                    Mathf.RoundToInt(plus * 0.5f), true, UITheme.GrassDeep, UITheme.Grass, UITheme.SheenVolt, onAdd);
                UIBuilder.Rect(add.root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-plus * 0.36f, 0), new Vector2(plus, plus));
                UIBuilder.IconAt(add.stack.face.transform, IconId.Plus, plus * 0.48f, UITheme.Ink1,
                    new Color(0, 0, 0, 0), new Vector2(0.5f, 0.5f), Vector2.zero);
            }
            return s.root;
        }

        // ============================================================
        // IDENTITY CHIP
        // ============================================================

        /// <summary>
        /// Avatar disc overlapping a dark name capsule, with an optional unread badge straddling the
        /// disc's top-right. The capsule is built first and the disc laid over its left cap, so the
        /// two read as one object rather than as an icon next to a pill.
        /// </summary>
        public static GameObject IdentityChip(Transform parent, string playerName, string tierName,
            float height, float width, int unread, Action onClick)
        {
            GameObject root = UIBuilder.Child("IdentityChip", parent);
            UIBuilder.Centered(root, new Vector2(width, height));

            float disc = height * 1.36f;

            Stack pill = BuildStack(root.transform, "NamePill", new Vector2(width - disc * 0.5f, height),
                UITheme.RadiusChip, true, UITheme.Panel, UITheme.Panel, 0f, 7, 0, 0);
            UIBuilder.Rect(pill.root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                Vector2.zero, new Vector2(width - disc * 0.5f, height));

            Text nameText = Body(pill.face.transform, "Name", TextAnchor.LowerLeft, playerName, UITheme.Cream, 30);
            UIBuilder.StretchRect(nameText.gameObject, new Vector2(0f, 0.5f), Vector2.one,
                new Vector2(disc * 0.62f, 0), new Vector2(-14, -4));
            UIBuilder.ClampLine(nameText, 20);

            Text tierText = Body(pill.face.transform, "Tier", TextAnchor.UpperLeft, tierName, UITheme.Cream, 30);
            UIBuilder.StretchRect(tierText.gameObject, Vector2.zero, new Vector2(1f, 0.5f),
                new Vector2(disc * 0.62f, 4), new Vector2(-14, 0));
            UIBuilder.ClampLine(tierText, 20);

            // Avatar well: court blue, so it stays distinct from the panel it sits on.
            Stack avatar = BuildStack(root.transform, "Avatar", new Vector2(disc, disc),
                Mathf.RoundToInt(disc * 0.5f), true, UITheme.CourtWell, UITheme.CourtWell, 0f,
                8, 0, 0);
            UIBuilder.Rect(avatar.root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(disc, disc));
            avatar.baseImage.gameObject.SetActive(false);
            avatar.root.GetComponent<Image>().color = Color.clear;
            UIReferenceArt.Draw(avatar.root.transform, "avatar");

            if (unread > 0)
            {
                CountBadge(avatar.root.transform, unread, disc * 0.62f, new Vector2(0.5f, 1f),
                    new Vector2(disc * 0.24f, disc * 0.06f));
            }

            if (onClick != null)
            {
                UIBuilder.ExpandHitArea(root, new Vector2(width, height));
                Button btn = root.AddComponent<Button>();
                Image raycast = root.AddComponent<Image>();
                raycast.color = new Color(0, 0, 0, 0);
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = raycast;
                btn.onClick.AddListener(delegate { onClick(); });
            }
            return root;
        }

        // ============================================================
        // METER
        // ============================================================

        /// <summary>Volt fill in a black-walled capsule track. Used for season tier and any XP bar.</summary>
        public static GameObject Meter(Transform parent, Vector2 size, float fill01, Color fillColor)
        {
            GameObject track = UIBuilder.Child("Meter", parent);
            UIBuilder.Centered(track, size);
            Image trackImg = track.AddComponent<Image>();
            trackImg.sprite = UIBuilder.CapsuleSprite;
            trackImg.gameObject.AddComponent<UICapsuleFit>();
            trackImg.type = Image.Type.Sliced;
            trackImg.color = UITheme.Ink0;

            float pad = Mathf.Max(4f, size.y * 0.16f);
            GameObject fill = UIBuilder.Child("Fill", track.transform);
            RectTransform rt = UIBuilder.StretchRect(fill, Vector2.zero, new Vector2(0f, 1f),
                new Vector2(pad, pad), new Vector2(pad, -pad));
            rt.sizeDelta = new Vector2((size.x - pad * 2f) * Mathf.Clamp01(fill01), rt.sizeDelta.y);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.sprite = UIBuilder.CapsuleSprite;
            fillImg.gameObject.AddComponent<UICapsuleFit>();
            fillImg.type = Image.Type.Sliced;
            fillImg.color = fillColor;
            return track;
        }

        // ============================================================
        // BRACKETED CARD
        // ============================================================

        /// <summary>
        /// The season card: a dark stack framed by two volt corner brackets rather than a full
        /// border. Returns the FACE so callers parent their content into the lit area.
        /// </summary>
        public static Stack BracketedCard(Transform parent, string name, Vector2 size, int radius, Color bracketColor)
        {
            GameObject host = UIBuilder.Child(name, parent);
            UIBuilder.Centered(host, size);

            // The card fills the host; the brackets are drawn on top of its own corners, tracing
            // part of the outline rather than framing a smaller card inside a larger box.
            Stack card = BuildStack(host.transform, "Card", size, radius, false,
                UITheme.Ink0, UITheme.Panel, UITheme.SheenDark, 12, 14, 8);
            UIBuilder.StretchRect(card.root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // The stroke is centred on the card's outline the way the board draws it, so the rect is
            // pulled in by half the thickness rather than hung off the corner.
            int thickness = 16;
            int armX = 118, armY = 54;
            float nudge = thickness * 0.5f;
            Sprite bracket = UIBuilder.CornerBracketSprite(radius, thickness, armX, armY);
            float spanX = radius + armX, spanY = radius + armY;
            Vector2 span = new Vector2(spanX, spanY);

            GameObject tl = UIBuilder.Child("BracketTL", host.transform);
            UIBuilder.Rect(tl, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(spanX * 0.5f + nudge, -spanY * 0.5f - nudge), span);
            Image tlImg = tl.AddComponent<Image>();
            tlImg.sprite = bracket;
            tlImg.color = bracketColor;
            tlImg.raycastTarget = false;

            // Pivoting from the centre and spinning 180 degrees lands the elbow on the opposite
            // corner; rotating a corner-pivoted rect would swing it clean off the card.
            GameObject br = UIBuilder.Child("BracketBR", host.transform);
            UIBuilder.Rect(br, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(-spanX * 0.5f - nudge, spanY * 0.5f + nudge), span);
            Image brImg = br.AddComponent<Image>();
            brImg.sprite = bracket;
            brImg.color = bracketColor;
            brImg.raycastTarget = false;
            br.transform.localRotation = Quaternion.Euler(0, 0, 180f);

            Stack result = new Stack();
            result.root = host;
            result.baseImage = card.baseImage;
            result.face = card.face;
            result.faceImage = card.faceImage;
            result.sheen = card.sheen;
            return result;
        }

        // ============================================================
        // BOARD-COORDINATE LAYOUT
        // ============================================================

        /// <summary>
        /// A full-width host exactly as tall as a Figma board (844 * scale), centred vertically in
        /// its parent. Children placed with <see cref="BoardRow"/> then use the board's own y
        /// coordinates directly.
        ///
        /// This is for the screens that are one centred composition — matchmaking, match found,
        /// season complete. They have no top-anchored header and no bottom-anchored nav to hang
        /// off, so counting from either edge puts the whole stack in the wrong place the moment the
        /// aspect ratio moves. Centring the board and letting it overhang instead keeps the
        /// composition intact and only ever trims empty margin.
        /// </summary>
        public static GameObject BoardHost(Transform parent)
        {
            GameObject host = UIBuilder.Child("BoardHost", parent);
            UIBuilder.Rect(host, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0, UITheme.F(844f)));
            host.AddComponent<UIReferenceBoard>();
            return host;
        }

        /// <summary>Full-width row inside a <see cref="BoardHost"/>, centred on the board's own
        /// <paramref name="boardY"/>. Both arguments are in Figma board pixels.</summary>
        public static void BoardRow(GameObject go, float boardY, float boardHeight, float sidePad = 0f)
        {
            UIBuilder.Rect(go, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -UITheme.F(boardY)),
                new Vector2(-UITheme.F(sidePad) * 2f, UITheme.F(boardHeight)));
        }

        /// <summary>Centred square inside a <see cref="BoardHost"/> — a disc, a ring, a badge.</summary>
        public static void BoardDisc(GameObject go, float boardY, float boardDiameter)
        {
            float d = UITheme.F(boardDiameter);
            UIBuilder.Rect(go, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -UITheme.F(boardY)), new Vector2(d, d));
        }

        // ============================================================
        // TOP BAR
        // ============================================================

        /// <summary>
        /// Back disc on the left, screen title beside it. The boards do not put titles in a ribbon
        /// or a bar — the words sit straight on the backdrop, and only the back button carries any
        /// chrome. Returns the row so callers can hang extra controls on the right.
        /// </summary>
        public static GameObject TopBar(Transform parent, string title, Action onBack)
        {
            float disc = UITheme.F(44f);
            GameObject row = UIBuilder.Child("TopBar", parent);
            UIBuilder.StretchRect(row, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(UITheme.F(21f), -(UITheme.HeaderTopOffset + disc)),
                new Vector2(-UITheme.F(21f), -UITheme.HeaderTopOffset));

            if (onBack != null)
            {
                ButtonStack back = IconButton(row.transform, IconId.ChevronLeft, disc, onBack);
                UIBuilder.Rect(back.root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    Vector2.zero, new Vector2(disc, disc));
            }

            Text t = Display(row.transform, "Title", TextAnchor.MiddleLeft, title, UITheme.InkOnLight,
                UITheme.TypeScreenTitle);
            UIBuilder.StretchRect(t.gameObject, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(disc + UITheme.F(19f), 0), Vector2.zero);
            UIBuilder.ClampLine(t, 26);
            return row;
        }

        /// <summary>Hairline rule between list rows. Court blue at 35%, per the settings board.</summary>
        public static GameObject Rule(Transform parent, float y)
        {
            GameObject rule = UIBuilder.Child("Rule", parent);
            UIBuilder.StretchRect(rule, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0, -y - 3f), new Vector2(0, -y));
            Image img = rule.AddComponent<Image>();
            img.color = new Color(UITheme.Court.r, UITheme.Court.g, UITheme.Court.b, 0.35f);
            img.raycastTarget = false;
            return rule;
        }

        /// <summary>
        /// Plain type as a control — LOG OUT, QUIT MATCH, CANCEL. The boards give a screen exactly
        /// one filled button; the way out is always a word, never a second slab competing with it.
        /// </summary>
        public static Text TextButton(Transform parent, string label, Color color, int fontSize, Action onClick)
        {
            GameObject host = UIBuilder.Child("TextBtn", parent);
            UIBuilder.Centered(host, new Vector2(420, UITheme.TouchMin));
            UIBuilder.ExpandHitArea(host, new Vector2(420, UITheme.TouchMin));
            Image raycast = host.AddComponent<Image>();
            raycast.color = new Color(0, 0, 0, 0);
            Button btn = host.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = raycast;
            if (onClick != null) btn.onClick.AddListener(delegate { onClick(); });

            Text t = Body(host.transform, "Label", TextAnchor.MiddleCenter, label, color, fontSize);
            UIBuilder.Fill(t.gameObject);
            UIButtonTypography.Attach(t);
            return t;
        }

        // ============================================================
        // TOGGLE
        // ============================================================

        /// <summary>
        /// Settings toggle. ON is a volt track with a dark knob; OFF is a panel track with a muted
        /// knob. Colour alone would not carry it for a colour-blind player, so the knob also
        /// travels — position is the primary signal and colour only reinforces it.
        /// </summary>
        public static GameObject Toggle(Transform parent, Vector2 size, bool on, Action<bool> onChanged)
        {
            GameObject host = UIBuilder.Child("Toggle", parent);
            UIBuilder.Centered(host, size);
            UIBuilder.ExpandHitArea(host, size);
            Image track = host.AddComponent<Image>();
            track.sprite = UIBuilder.CapsuleSprite;
            track.gameObject.AddComponent<UICapsuleFit>();
            track.type = Image.Type.Sliced;
            track.color = UITheme.Outline;

            GameObject fill = UIBuilder.Child("Track", host.transform);
            UIBuilder.StretchRect(fill, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
            Image fillImg = fill.AddComponent<Image>();
            fillImg.sprite = UIBuilder.CapsuleSprite;
            fillImg.gameObject.AddComponent<UICapsuleFit>();
            fillImg.type = Image.Type.Sliced;

            float knob = size.y * 0.74f;
            GameObject knobGO = UIBuilder.Child("Knob", host.transform);
            RectTransform knobRt = UIBuilder.Rect(knobGO, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(knob, knob));
            Image knobOutline = knobGO.AddComponent<Image>();
            knobOutline.sprite = UIBuilder.CircleSprite;
            knobOutline.color = UITheme.Outline;

            GameObject knobFace = UIBuilder.Child("KnobFace", knobGO.transform);
            UIBuilder.StretchRect(knobFace, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
            Image knobImg = knobFace.AddComponent<Image>();
            knobImg.sprite = UIBuilder.CircleSprite;

            PSToggle t = host.AddComponent<PSToggle>();
            t.Bind(knobRt, fillImg, knobImg, on, onChanged, size.x, knob);
            return host;
        }

        // ============================================================
        // AVATAR DISC
        // ============================================================

        /// <summary>
        /// The big matchmaking portrait: a coloured ring around a court-blue well. The ring colour
        /// is the identity — volt for you, bubblegum for the opponent — and on the match-found
        /// board it is the only thing separating the two cards.
        /// </summary>
        public static GameObject AvatarDisc(Transform parent, float diameter, Color ringColor)
        {
            Image portrait = UIReferenceArt.Draw(parent,
                ringColor == UITheme.Bubblegum ? "portrait_rival" : "portrait_you");
            UIBuilder.Centered(portrait.gameObject, new Vector2(diameter, diameter));
            return portrait.gameObject;
        }

        // ============================================================
        // PICKLEBALL MARK
        // ============================================================

        /// <summary>
        /// The ball itself: a volt disc with five ink holes. It is the game's logo on the splash,
        /// the pulse at the centre of matchmaking, and the VS badge between two players, so it is
        /// one builder rather than three near-identical hand-drawn versions.
        /// </summary>
        public static GameObject BallMark(Transform parent, float diameter)
        {
            GameObject host = UIBuilder.Child("BallMark", parent);
            UIBuilder.Centered(host, new Vector2(diameter, diameter));

            Image outline = host.AddComponent<Image>();
            outline.sprite = UIBuilder.CircleSprite;
            outline.color = UITheme.Outline;

            GameObject face = UIBuilder.Child("Face", host.transform);
            float wall = Mathf.Max(4f, diameter * 0.055f);
            UIBuilder.StretchRect(face, Vector2.zero, Vector2.one, new Vector2(wall, wall), new Vector2(-wall, -wall));
            Image faceImg = face.AddComponent<Image>();
            faceImg.sprite = UIBuilder.CircleSprite;
            faceImg.color = UITheme.Volt;

            // Board layout: four holes on the diagonals plus one dead centre.
            float r = diameter * 0.044f;
            float off = diameter * 0.162f;
            Vector2[] holes =
            {
                new Vector2(-off,  off), new Vector2(off,  off),
                new Vector2(0f, 0f),
                new Vector2(-off, -off), new Vector2(off, -off),
            };
            for (int i = 0; i < holes.Length; i++)
            {
                GameObject hole = UIBuilder.Child("Hole" + i, face.transform);
                UIBuilder.Rect(hole, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    holes[i], new Vector2(r * 2f, r * 2f));
                Image hImg = hole.AddComponent<Image>();
                hImg.sprite = UIBuilder.CircleSprite;
                hImg.color = UITheme.Ink1;
                hImg.raycastTarget = false;
            }
            return host;
        }

        // ============================================================
        // LAUREL WREATH
        // ============================================================

        /// <summary>
        /// Two mirrored arcs of leaves tied at the bottom, for framing the season badge.
        ///
        /// Built from <see cref="IconId.Leaf"/> rather than drawn as one illustration: the leaves
        /// arrive already outlined by the icon baker, so the wreath carries the same black stroke as
        /// everything else on the screen and tints with a single colour argument. Leaves taper and
        /// fan outward toward the top so the wreath opens rather than closing into a ring — a closed
        /// ring around a circular medal just reads as a second, thicker ring.
        ///
        /// Returns the host, sized <paramref name="size"/> square. Add it BEFORE the medal so it
        /// draws behind.
        /// </summary>
        public static GameObject LaurelWreath(Transform parent, float size, Color fill, Color outline)
        {
            const int LeavesPerSide = 7;
            // The arc starts above the bottom, not at it. Swept all the way down, the lowest leaves
            // lie horizontally across the medal's bottom edge and cover the tie they are supposed to
            // be gathered by.
            const float StartAngle = -62f;
            const float SweepAngle = 126f;   // stops short of the top, leaving the crown open

            GameObject host = UIBuilder.Child("Wreath", parent);
            UIBuilder.Centered(host, new Vector2(size, size));

            float radius = size * 0.435f;

            for (int side = 0; side < 2; side++)
            {
                float mirror = side == 0 ? 1f : -1f;
                for (int i = 0; i < LeavesPerSide; i++)
                {
                    float t = i / (float)(LeavesPerSide - 1);
                    float ang = StartAngle + SweepAngle * t;
                    float rad = ang * Mathf.Deg2Rad;
                    float leaf = size * (0.30f - t * 0.11f);

                    // A leaf sprite points straight up, so rotating it by the polar angle lays it
                    // along the arc; the extra 12 degrees tips each one outward off the tangent,
                    // which is what makes a laurel look grown rather than stamped.
                    GameObject go = UIIcon.Build(host.transform, IconId.Leaf, leaf, fill, outline);
                    UIBuilder.Rect(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(Mathf.Cos(rad) * radius * mirror, Mathf.Sin(rad) * radius),
                        new Vector2(leaf, leaf));
                    go.transform.localRotation = Quaternion.Euler(0, 0, (ang - 12f) * mirror);
                }
            }

            // The tie: a short capsule where the two stems meet, so the arcs read as one wreath.
            GameObject tie = UIBuilder.Child("Tie", host.transform);
            UIBuilder.Rect(tie, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -radius * 1.0f), new Vector2(size * 0.17f, size * 0.058f));
            Image tieOutline = tie.AddComponent<Image>();
            tieOutline.sprite = UIBuilder.CapsuleSprite;
            tieOutline.gameObject.AddComponent<UICapsuleFit>();
            tieOutline.type = Image.Type.Sliced;
            tieOutline.color = outline;
            tieOutline.raycastTarget = false;

            GameObject tieFace = UIBuilder.Child("Face", tie.transform);
            UIBuilder.StretchRect(tieFace, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
            Image tieFaceImg = tieFace.AddComponent<Image>();
            tieFaceImg.sprite = UIBuilder.CapsuleSprite;
            tieFaceImg.gameObject.AddComponent<UICapsuleFit>();
            tieFaceImg.type = Image.Type.Sliced;
            tieFaceImg.color = fill;
            tieFaceImg.raycastTarget = false;

            return host;
        }

        // ============================================================
        // SCATTER PATTERN
        // ============================================================

        /// <summary>
        /// The faint tumble of pickle slices behind the splash wordmark. Deterministic from
        /// <paramref name="seed"/> so the boot screen looks the same every launch rather than
        /// reshuffling itself while the player watches it load.
        /// </summary>
        public static GameObject ScatterPattern(Transform parent, int count, int seed, float alpha)
        {
            GameObject host = UIBuilder.Child("Scatter", parent);
            UIBuilder.Fill(host);

            System.Random rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float size = 46f + (float)rng.NextDouble() * 70f;
                float ax = (float)rng.NextDouble();
                float ay = (float)rng.NextDouble();

                // Square, not the 0.74 squash the placeholder ellipses used — the slice sprite is
                // a round shape with its own seed ring, and flattening it turns the seeds oval.
                GameObject dot = UIBuilder.Child("Slice" + i, host.transform);
                UIBuilder.Rect(dot, new Vector2(ax, ay), new Vector2(ax, ay), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(size, size));
                Image img = dot.AddComponent<Image>();
                img.sprite = UIIcon.Fill(IconId.PickleSlice);
                img.color = new Color(1f, 1f, 1f, alpha);
                img.raycastTarget = false;
                dot.transform.localRotation = Quaternion.Euler(0, 0, (float)rng.NextDouble() * 360f);
            }
            return host;
        }

        // ============================================================
        // BOTTOM NAV
        // ============================================================

        public struct NavTab
        {
            public string id;
            public string label;
            public IconId icon;
            public Color tint;
            public NavTab(string id, string label, IconId icon, Color tint)
            {
                this.id = id; this.label = label; this.icon = icon; this.tint = tint;
            }
        }

        public static readonly NavTab[] Tabs =
        {
            new NavTab("HOME",   "HOME",   IconId.Home,   default(Color)),   // active tile carries the colour
            new NavTab("GEAR",   "GEAR",   IconId.Shield, default(Color)),
            new NavTab("LEAGUE", "LEAGUE", IconId.Trophy, default(Color)),
            new NavTab("SHOP",   "SHOP",   IconId.Bag,    default(Color)),
        };

        private static Color TabTint(string id)
        {
            switch (id)
            {
                case "GEAR":   return UITheme.Blaze;
                case "LEAGUE": return UITheme.Gold;
                case "SHOP":   return UITheme.Bubblegum;
                default:       return UITheme.Volt;
            }
        }

        /// <summary>
        /// The four-tab bar. Inactive tabs are a bare tinted glyph over the bar's own dark face;
        /// the active tab gets a full volt stack, so "where I am" is the only lit thing down there.
        /// </summary>
        public static GameObject BottomNav(Transform parent, string activeTab, Action<string> onSelect)
        {
            // The bar itself is flat — it is the ground the tabs stand on, not a control.
            Stack bar = BuildStack(parent, "BottomNav", new Vector2(0, UITheme.NavBarHeight),
                UITheme.RadiusPanel + 26, false, UITheme.Panel, UITheme.Panel, 0f, 10, 0, 0);
            UIBuilder.StretchRect(bar.root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceXl, UITheme.NavBarBottomGap),
                new Vector2(-UITheme.SpaceXl, UITheme.NavBarBottomGap + UITheme.NavBarHeight));

            float slot = 1f / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
            {
                NavTab tab = Tabs[i];
                bool active = tab.id == activeTab;
                string captured = tab.id;

                GameObject cell = UIBuilder.Child("Tab_" + tab.id, bar.face.transform);
                UIBuilder.StretchRect(cell, new Vector2(i * slot, 0f), new Vector2((i + 1) * slot, 1f),
                    new Vector2(8, 6), new Vector2(-8, -6));

                Button btn = cell.AddComponent<Button>();
                Image raycast = cell.AddComponent<Image>();
                raycast.color = new Color(0, 0, 0, 0);
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = raycast;
                btn.onClick.AddListener(delegate { onSelect(captured); });

                // Inactive: a bare tinted glyph straight on the bar. The tints are the same
                // intent colours the rest of the kit uses (gear blaze, league gold, shop
                // bubblegum), so the row reads as four destinations rather than four grey icons.
                Transform iconHost = cell.transform;
                Transform labelHost = cell.transform;
                Color glyphColor = TabTint(tab.id);
                Color labelColor = UITheme.Cream;

                if (active)
                {
                    Stack tile = BuildStack(cell.transform, "ActiveTile", new Vector2(0, UITheme.NavBarHeight - 12),
                        34, false, UITheme.VoltDeep, UITheme.Volt, UITheme.SheenVolt, 8, 52, 8);
                    UIBuilder.StretchRect(tile.root, Vector2.zero, Vector2.one,
                        new Vector2(18, 2), new Vector2(-18, 0));
                    tile.root.transform.SetAsFirstSibling();
                    // Glyph on the lit face, label on the base sliver below it — the board splits
                    // them across the tile's two levels, which is what gives the active tab its
                    // raised-key look instead of a flat highlight.
                    iconHost = tile.face.transform;
                    labelHost = tile.root.transform;
                    glyphColor = UITheme.Ink1;
                    labelColor = UITheme.Ink1;
                }

                GameObject glyph = tab.id == "HOME"
                    ? UIIcon.BuildPlain(iconHost, IconId.Home, UITheme.F(37f), glyphColor)
                    : UIReferenceArt.Draw(iconHost, "nav_" + tab.id.ToLowerInvariant()).gameObject;
                UIBuilder.Rect(glyph, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 0.5f), new Vector2(0, active ? -62f : -78f),
                    new Vector2(UITheme.F(37f), UITheme.F(39f)));

                Text label = Body(labelHost, "Label", TextAnchor.LowerCenter, tab.label, labelColor, 23);
                UIBuilder.Rect(label.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0, active ? 11 : 19), new Vector2(-8, 32));
                UIBuilder.ClampLine(label, 16);
            }
            return bar.root;
        }
    }
}
