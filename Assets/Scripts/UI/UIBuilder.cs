using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Pickleball.UI
{
    public enum Rarity { Common, Rare, Epic, Legendary }

    /// <summary>
    /// Kitchen Line Arcade — shared runtime UI toolkit. Every meta screen (lobby, tour select,
    /// gear, league, pass, shop, settings, chest, matchmaking, pause) is built out of these same
    /// six pieces: Panel, Button, Ribbon, Capsule, Card, Meter. Keeping them here means every
    /// screen script only writes layout, not raw GameObject/Image/Text boilerplate.
    /// </summary>
    public static class UIBuilder
    {
        private static Font cachedFont;
        public static Font Font
        {
            get
            {
                if (cachedFont == null)
                {
                    cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    if (cachedFont == null)
                    {
                        Font[] allFonts = Resources.FindObjectsOfTypeAll<Font>();
                        if (allFonts != null && allFonts.Length > 0) cachedFont = allFonts[0];
                    }
                    if (cachedFont == null)
                    {
                        try
                        {
                            cachedFont = Font.CreateDynamicFontFromOSFont(new string[] { "Arial", "Segoe UI", "Helvetica", "Roboto", "Liberation Sans" }, 16);
                        }
                        catch {}
                    }
                }
                return cachedFont;
            }
        }

        private static Font cachedDisplayFont;
        private static bool displayFontProbed;

        /// <summary>
        /// The game's voice: Lilita One, the rounded display face the Figma boards are set in.
        /// Headlines, CTA labels, score digits and callouts use it; body copy and numbers in lists
        /// stay on <see cref="Font"/>, which is a text face and reads better at small sizes.
        ///
        /// Falls back to <see cref="Font"/> if the asset is missing, so a stripped build still
        /// renders words rather than nothing.
        /// </summary>
        public static Font DisplayFont
        {
            get
            {
                if (!displayFontProbed)
                {
                    displayFontProbed = true;
                    cachedDisplayFont = Resources.Load<Font>("Fonts/LilitaOne-Regular");
                }
                return cachedDisplayFont != null ? cachedDisplayFont : Font;
            }
        }

        private static readonly Dictionary<int, Sprite> spriteCache = new Dictionary<int, Sprite>();

        // ============================================================
        // SPRITES
        // ============================================================
        /// <summary>
        /// A white rounded rect, 9-sliced so the corner radius survives any stretch.
        ///
        /// The texture used to be a fixed 64px, which capped the radius at 32 reference px. The
        /// Pickle Smash boards run far rounder than that — the primary CTA is a full capsule at
        /// ~97px and the season card corners are ~55px — so the texture now sizes itself to the
        /// requested radius instead. Anything at or past <see cref="CapsuleRadius"/> is served by
        /// the capsule sprite, which stays a true half-circle cap at any height.
        /// </summary>
        public static Sprite RoundedSprite(int cornerRadius)
        {
            cornerRadius = Mathf.Clamp(cornerRadius, 1, 128);
            Sprite cached;
            if (spriteCache.TryGetValue(cornerRadius, out cached)) return cached;

            int size = Mathf.Max(64, cornerRadius * 2 + 8);
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isCorner = (x < cornerRadius && y < cornerRadius) ||
                                    (x >= size - cornerRadius && y < cornerRadius) ||
                                    (x < cornerRadius && y >= size - cornerRadius) ||
                                    (x >= size - cornerRadius && y >= size - cornerRadius);
                    if (isCorner)
                    {
                        int cx = x < cornerRadius ? cornerRadius : size - cornerRadius - 1;
                        int cy = y < cornerRadius ? cornerRadius : size - cornerRadius - 1;
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                        // One-pixel feather. A hard cut was invisible at radius 20 and obviously
                        // stair-stepped once the boards asked for 55-100px corners.
                        float a = Mathf.Clamp01(cornerRadius - dist);
                        colors[y * size + x] = new Color(1f, 1f, 1f, a);
                    }
                    else
                    {
                        colors[y * size + x] = Color.white;
                    }
                }
            }
            tex.SetPixels(colors);
            tex.Apply();

            Vector4 border = new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            spriteCache[cornerRadius] = sprite;
            return sprite;
        }

        private static Sprite cachedCapsule;
        /// <summary>
        /// A pill: a 9-sliced circle whose border is exactly half its width, so the caps stay true
        /// half-circles at any height. Every fully-rounded shape in the kit — the primary CTA, the
        /// currency pills, the identity chip, the season meter — uses this rather than
        /// <see cref="RoundedSprite"/>, which would have to guess a radius.
        /// </summary>
        public static Sprite CapsuleSprite
        {
            get
            {
                if (cachedCapsule != null) return cachedCapsule;
                const int size = 128;
                const float r = size * 0.5f;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                Color[] colors = new Color[size * size];
                Vector2 center = new Vector2(r - 0.5f, r - 0.5f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), center);
                        colors[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));
                    }
                }
                tex.SetPixels(colors);
                tex.Apply();
                // Border of exactly r on each side leaves a 0-px centre slice, which Unity stretches
                // to whatever width the rect needs — the two caps stay circular.
                cachedCapsule = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(r - 1f, r - 1f, r - 1f, r - 1f));
                return cachedCapsule;
            }
        }

        private static readonly Dictionary<int, Sprite> bracketCache = new Dictionary<int, Sprite>();

        /// <summary>
        /// The top-left corner of a rounded-rect stroke: a rounded elbow with an arm running along
        /// the top and down the left. Rotate 180 degrees for the bottom-right. The season card on
        /// the home board is framed by exactly this pair of volt brackets rather than a full border,
        /// which is what stops the card from reading as one more outlined box.
        /// </summary>
        public static Sprite CornerBracketSprite(int cornerRadius, int thickness, int armX, int armY)
        {
            cornerRadius = Mathf.Clamp(cornerRadius, 2, 96);
            thickness = Mathf.Clamp(thickness, 1, cornerRadius);
            // The board's two brackets have different arm lengths — the top one runs further along
            // the card's top edge than it does down the side — so the two are separate parameters
            // rather than one square span.
            armX = Mathf.Clamp(armX, 0, 220);
            armY = Mathf.Clamp(armY, 0, 220);

            int key = cornerRadius * 10000000 + thickness * 100000 + armX * 300 + armY;
            Sprite cached;
            if (bracketCache.TryGetValue(key, out cached)) return cached;

            int w = cornerRadius + armX;
            int h = cornerRadius + armY;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] colors = new Color[w * h];

            // Texture space is bottom-up; the elbow therefore sits at the TOP-left of the image,
            // which is (cornerRadius, h - cornerRadius) in pixel coords.
            Vector2 elbow = new Vector2(cornerRadius, h - cornerRadius);
            float outer = cornerRadius;
            float inner = cornerRadius - thickness;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float a;
                    if (x < elbow.x && y > elbow.y)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), elbow);
                        a = Mathf.Min(Mathf.Clamp01(outer - d), Mathf.Clamp01(d - inner));
                    }
                    else if (y > elbow.y)
                    {
                        a = (h - y) <= thickness ? 1f : 0f;   // arm along the top edge
                    }
                    else if (x < elbow.x)
                    {
                        a = x < thickness ? 1f : 0f;          // arm down the left edge
                    }
                    else
                    {
                        a = 0f;
                    }
                    colors[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            bracketCache[key] = sprite;
            return sprite;
        }

        private static readonly Dictionary<int, Sprite> rrRingCache = new Dictionary<int, Sprite>();

        /// <summary>
        /// A rounded-rect OUTLINE — the border only, hollow centre — 9-sliced so it stretches to any
        /// size at a constant stroke weight.
        ///
        /// A filled rect painted behind a translucent face does not work as a keyline: whatever is
        /// behind shows through the face and tints the whole panel. The in-match score plates are
        /// deliberately see-through, so their volt keyline has to be an actual ring drawn over the
        /// top.
        /// </summary>
        public static Sprite RoundedRingSprite(int cornerRadius, int thickness)
        {
            cornerRadius = Mathf.Clamp(cornerRadius, 2, 96);
            thickness = Mathf.Clamp(thickness, 1, cornerRadius);

            int key = cornerRadius * 1000 + thickness;
            Sprite cached;
            if (rrRingCache.TryGetValue(key, out cached)) return cached;

            int size = cornerRadius * 2 + 8;
            float half = size * 0.5f;
            float innerHalf = half - cornerRadius;   // half-size of the straight-edge core

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] colors = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Signed distance to a rounded rect: negative inside, 0 on the edge.
                    float qx = Mathf.Abs(x + 0.5f - half) - innerHalf;
                    float qy = Mathf.Abs(y + 0.5f - half) - innerHalf;
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float sdf = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - cornerRadius;

                    // Keep the band just inside the edge; feather both sides by a pixel.
                    float a = Mathf.Min(Mathf.Clamp01(-sdf), Mathf.Clamp01(sdf + thickness + 1f));
                    colors[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();

            Vector4 border = new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
            rrRingCache[key] = sprite;
            return sprite;
        }

        private static readonly Dictionary<int, Sprite> ringCache = new Dictionary<int, Sprite>();

        /// <summary>
        /// A hollow circle. <paramref name="thicknessPercent"/> is the stroke as a percentage of the
        /// radius, so one sprite scales to any ring size without the stroke thickening with it —
        /// which is what the matchmaking screen's three concentric rings need.
        /// </summary>
        public static Sprite RingSprite(int thicknessPercent)
        {
            thicknessPercent = Mathf.Clamp(thicknessPercent, 1, 50);
            Sprite cached;
            if (ringCache.TryGetValue(thicknessPercent, out cached)) return cached;

            const int size = 256;
            const float r = size * 0.5f;
            float inner = r * (1f - thicknessPercent / 100f);

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] colors = new Color[size * size];
            Vector2 centre = new Vector2(r - 0.5f, r - 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), centre);
                    float a = Mathf.Min(Mathf.Clamp01(r - d), Mathf.Clamp01(d - inner));
                    colors[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            ringCache[thicknessPercent] = sprite;
            return sprite;
        }

        private static Sprite cachedCircle;
        public static Sprite CircleSprite
        {
            get
            {
                if (cachedCircle != null) return cachedCircle;
                const int size = 128;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                Color[] colors = new Color[size * size];
                float radius = size * 0.5f;
                Vector2 center = new Vector2(radius - 0.5f, radius - 0.5f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), center);
                        if (dist <= radius - 1f) colors[y * size + x] = Color.white;
                        else if (dist <= radius) colors[y * size + x] = new Color(1f, 1f, 1f, radius - dist);
                        else colors[y * size + x] = Color.clear;
                    }
                }
                tex.SetPixels(colors);
                tex.Apply();
                cachedCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return cachedCircle;
            }
        }

        public static Sprite PanelSprite { get { return RoundedSprite(UITheme.RadiusPanel); } }
        public static Sprite ButtonSprite { get { return RoundedSprite(UITheme.RadiusButton); } }
        public static Sprite ChipSprite { get { return RoundedSprite(UITheme.RadiusChip); } }

        // ============================================================
        // RECT / LAYOUT HELPERS
        // ============================================================
        public static RectTransform Rect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return rt;
        }

        public static RectTransform Centered(GameObject go, Vector2 size)
        {
            return Rect(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        }

        public static RectTransform StretchRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Fill(GameObject go)
        {
            return StretchRect(go, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        public static GameObject Child(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>
        /// Adds an invisible raycast surface so a compact visual control still meets the shared
        /// mobile touch-target minimum. The visual rect is deliberately left alone: price pills,
        /// sliders and toggles keep their intended proportions while taps near their edges land.
        /// The helper follows later layout changes, including BoardRow resizing a control after it
        /// is built. EventSystem routes the child's pointer event to handlers on the host.
        /// </summary>
        public static void ExpandHitArea(GameObject host, Vector2 declaredSize)
        {
            if (host == null || host.transform.Find("HitArea") != null) return;

            GameObject hit = Child("HitArea", host.transform);
            StretchRect(hit, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image image = hit.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            UIExpandedHitArea target = hit.AddComponent<UIExpandedHitArea>();
            target.declaredSize = declaredSize;
            target.Refresh();
            hit.transform.SetAsFirstSibling();
        }

        /// <summary>
        /// A safe-area-inset container. Screens paint their background straight onto the full-bleed
        /// root so it runs edge to edge under the notch, then parent every readable element to this
        /// so nothing lands beneath system chrome.
        /// </summary>
        public static Transform SafeArea(Transform parent)
        {
            GameObject go = Child("SafeArea", parent);
            Fill(go);
            go.AddComponent<UISafeArea>();
            return go.transform;
        }

        // ============================================================
        // TEXT
        // ============================================================
        public static Text Text(Transform parent, string goName, TextAnchor anchor, string content, Color color, int fontSize, FontStyle style)
        {
            GameObject go = Child(goName, parent);
            Text t = go.AddComponent<Text>();
            t.font = Font;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = anchor;
            t.text = content;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>Display-face label with the mandatory navy stroke, sized by UITheme.OutlineDistanceFor.</summary>
        /// <summary>
        /// A display-face label with the kit's black stroke. Set in <see cref="DisplayFont"/> at
        /// Normal weight — Lilita One ships one weight, and asking Unity for Bold on a single-weight
        /// dynamic font gets a synthesised smear rather than a heavier cut.
        /// </summary>
        public static Text StrokedText(Transform parent, string goName, TextAnchor anchor, string content, Color color, int fontSize)
        {
            Text t = Text(parent, goName, anchor, content, color, fontSize, FontStyle.Normal);
            t.font = DisplayFont;
            Outline o = t.gameObject.AddComponent<Outline>();
            o.effectColor = UITheme.OutlineText;
            float d = UITheme.OutlineDistanceFor(fontSize);
            o.effectDistance = new Vector2(d, -d);
            return t;
        }

        /// <summary>
        /// Constrains a label to its rect by shrinking the type rather than letting it bleed.
        ///
        /// Text() defaults to Overflow on both axes, which is right for centred display numbers but
        /// wrong for anything data-driven: item names, player names and offer titles were spilling
        /// past their card edges and overlapping neighbours (a 300px deal card happily drew
        /// "PADDLE x10" 40px wider than itself). Call this on any label whose content comes from
        /// game state rather than from a literal.
        /// </summary>
        public static Text Clamp(Text t, int minSize = 0)
        {
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = t.fontSize;
            t.resizeTextMinSize = minSize > 0 ? minSize : Mathf.Max(10, Mathf.RoundToInt(t.fontSize * 0.55f));
            return t;
        }

        /// <summary>Single-line variant of <see cref="Clamp"/> — shrinks but never wraps, for names
        /// and numbers that must stay on one line.</summary>
        public static Text ClampLine(Text t, int minSize = 0)
        {
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = t.fontSize;
            t.resizeTextMinSize = minSize > 0 ? minSize : Mathf.Max(10, Mathf.RoundToInt(t.fontSize * 0.5f));
            return t;
        }

        // ============================================================
        // ICON + LABEL COMPOSITION
        // ============================================================
        /// <summary>
        /// Places an icon of <paramref name="size"/> with its CENTRE at <paramref name="offset"/>
        /// from <paramref name="anchor"/>.
        ///
        /// The pivot is always centred, never tied to the anchor. Matching the pivot to the anchor
        /// makes the offset mean "where the icon's corner goes" for corner anchors and "where its
        /// centre goes" for centred ones, so the same call reads differently depending on which
        /// corner you anchored to — the lobby's season-pass crown was positioned as a centre and
        /// landed as a top-left corner, overlapping the label beside it.
        /// </summary>
        public static GameObject IconAt(Transform parent, IconId id, float size, Color fill, Color outline, Vector2 anchor, Vector2 offset)
        {
            GameObject go = UIIcon.Build(parent, id, size, fill, outline);
            Rect(go, anchor, anchor, new Vector2(0.5f, 0.5f), offset, new Vector2(size, size));
            return go;
        }

        /// <summary>
        /// A chip carrying an icon and a label, laid out as one unit and sized to its content.
        /// Replaces the old pattern of embedding an emoji codepoint in the chip string — those
        /// glyphs never rasterised, so every such chip rendered with a blank gap where the icon
        /// should have been ("2,140" with a hole in front of it).
        /// </summary>
        public static GameObject IconChip(Transform parent, IconId icon, string text, Color fillColor, float height, int fontSize, Color iconColor)
        {
            float iconSize = height * 0.62f;
            float pad = height * 0.30f;
            float textWidth = text.Length * fontSize * 0.60f;
            float width = pad + iconSize + 8f + textWidth + pad;

            GameObject go = Child("IconChip", parent);
            Centered(go, new Vector2(width, height));
            go.AddComponent<Image>();
            OutlinedFill(go, ChipSprite, fillColor, UITheme.OutlineNavy, Mathf.Min(3f, height * 0.06f));

            IconAt(go.transform, icon, iconSize, iconColor, UITheme.OutlineNavy,
                new Vector2(0f, 0.5f), new Vector2(pad + iconSize * 0.5f, 0f));

            Text t = Text(go.transform, "Text", TextAnchor.MiddleLeft, text, Color.white, fontSize, FontStyle.Bold);
            StretchRect(t.gameObject, Vector2.zero, Vector2.one, new Vector2(pad + iconSize + 8f, 0), new Vector2(-pad * 0.6f, 0));
            return go;
        }

        // ============================================================
        // PANEL / OUTLINED FILL
        // ============================================================
        /// <summary>Flat-color outline + inset fill. <paramref name="container"/> must already have an Image.</summary>
        public static Image OutlinedFill(GameObject container, Sprite sprite, Color fillColor, Color outlineColor, float pad)
        {
            Image outlineImg = container.GetComponent<Image>();
            outlineImg.sprite = sprite;
            outlineImg.type = (sprite != null && sprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
            outlineImg.color = outlineColor;

            GameObject fillGO = Child("Fill", container.transform);
            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite = sprite;
            fillImg.type = (sprite != null && sprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
            fillImg.color = fillColor;
            StretchRect(fillGO, Vector2.zero, Vector2.one, new Vector2(pad, pad), new Vector2(-pad, -pad));
            return fillImg;
        }

        /// <summary>Vertical-gradient outline + inset fill — the standard "pnl" piece from the kit.</summary>
        public static Image OutlinedGradientFill(GameObject container, Sprite sprite, Color topColor, Color bottomColor, Color outlineColor, float pad)
        {
            Image fillImg = OutlinedFill(container, sprite, Color.white, outlineColor, pad);
            UIGradient grad = fillImg.gameObject.AddComponent<UIGradient>();
            grad.topColor = topColor;
            grad.bottomColor = bottomColor;
            return fillImg;
        }

        /// <summary>A standalone panel GameObject, centered at (0,0) in its parent — reposition after return.</summary>
        public static GameObject Panel(Transform parent, string name, Vector2 size, Color topColor, Color bottomColor)
        {
            GameObject go = Child(name, parent);
            Centered(go, size);
            go.AddComponent<Image>();
            OutlinedGradientFill(go, PanelSprite, topColor, bottomColor, UITheme.OutlineNavy, UITheme.StrokeOutline);
            return go;
        }

        public static GameObject FlatPanel(Transform parent, string name, Vector2 size, Color fillColor)
        {
            GameObject go = Child(name, parent);
            Centered(go, size);
            go.AddComponent<Image>();
            OutlinedFill(go, PanelSprite, fillColor, UITheme.OutlineNavy, UITheme.StrokeOutline);
            return go;
        }

        // ============================================================
        // BUTTON (press-travel game feel)
        // ============================================================
        public class ButtonRefs
        {
            public GameObject root;
            public Button button;
            public Text label;
            public Image fill;
        }

        /// <summary>
        /// Every filled button in the kit. Builds the Pickle Smash stack — black keyline, dark base,
        /// lit face, and a press that seats the face onto the base — so the screens that have not
        /// been reworked against a board get the new look from the call they already make.
        ///
        /// <paramref name="topColor"/> becomes the FACE and <paramref name="bottomColor"/> the BASE,
        /// which is what the old gradient pair already meant.
        ///
        /// The face is a direct child of the root named "Content", not a grandchild inside the base.
        /// Six screens reach into the button with root.Find("Content") to hang a price or a sub-label
        /// on it, and Find only searches direct children — nesting it one level deeper silently
        /// returned null and reparented their content to the scene root, where it vanished.
        /// </summary>
        public static ButtonRefs Button(Transform parent, string label, Vector2 size, Color topColor, Color bottomColor, Color outlineColor, int fontSize, Action onClick)
        {
            float h = size.y > 1f ? size.y : 120f;
            int wall = Mathf.Clamp(Mathf.RoundToInt(h * 0.055f), 4, 12);
            int bevel = Mathf.Clamp(Mathf.RoundToInt(h * 0.045f), 3, 10);
            int lift = Mathf.Clamp(Mathf.RoundToInt(h * 0.17f), 5, 36);

            GameObject root = Child("Btn_" + label, parent);
            Centered(root, size);
            ExpandHitArea(root, size);

            Image outline = root.AddComponent<Image>();
            outline.sprite = UIBuilder.CapsuleSprite;
            outline.gameObject.AddComponent<UICapsuleFit>();
            outline.type = Image.Type.Sliced;
            outline.color = UITheme.Outline;

            GameObject baseGO = Child("Base", root.transform);
            StretchRect(baseGO, Vector2.zero, Vector2.one, new Vector2(wall, wall), new Vector2(-wall, -wall));
            Image baseImg = baseGO.AddComponent<Image>();
            baseImg.sprite = UIBuilder.CapsuleSprite;
            baseImg.gameObject.AddComponent<UICapsuleFit>();
            baseImg.type = Image.Type.Sliced;
            baseImg.color = bottomColor;

            GameObject content = Child("Content", root.transform);
            StretchRect(content, Vector2.zero, Vector2.one,
                new Vector2(wall + bevel, wall + lift), new Vector2(-(wall + bevel), -(wall + bevel)));
            Image fillImg = content.AddComponent<Image>();
            fillImg.sprite = UIBuilder.CapsuleSprite;
            fillImg.gameObject.AddComponent<UICapsuleFit>();
            fillImg.type = Image.Type.Sliced;
            fillImg.color = topColor;

            // Sheen strength follows the face's brightness: a lit face carries the full highlight,
            // a near-black utility face would only look smeared.
            float luminance = topColor.r * 0.299f + topColor.g * 0.587f + topColor.b * 0.114f;
            float sheen = luminance > 0.6f ? UITheme.SheenVolt
                        : luminance > 0.3f ? UITheme.SheenBlaze
                        : UITheme.SheenDark;

            GameObject gloss = Child("Sheen", content.transform);
            StretchRect(gloss, Vector2.zero, Vector2.one,
                new Vector2(bevel * 2f, bevel * 1.4f), new Vector2(-bevel * 2f, -bevel * 1.4f));
            Image glossImg = gloss.AddComponent<Image>();
            glossImg.sprite = UIBuilder.CapsuleSprite;
            glossImg.gameObject.AddComponent<UICapsuleFit>();
            glossImg.type = Image.Type.Sliced;
            glossImg.color = new Color(1f, 1f, 1f, sheen * 0.5f);
            glossImg.raycastTarget = false;

            // Ink on a light face, cream on a dark one — contrast decides, not the intent colour.
            Color ink = luminance > 0.55f ? UITheme.Ink1 : UITheme.Cream;
            Text t = PSKit.Display(content.transform, "Label", TextAnchor.MiddleCenter, label, ink, fontSize);
            Fill(t.gameObject);
            UIButtonTypography.Attach(t, size.y > 0 && size.y < 110);
            if (luminance > 0.55f)
            {
                Outline o = t.GetComponent<Outline>();
                if (o != null) UnityEngine.Object.Destroy(o);
            }

            Button btn = root.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = outline;
            if (onClick != null) btn.onClick.AddListener(delegate { onClick(); });

            UIButtonPress press = root.AddComponent<UIButtonPress>();
            press.content = content.GetComponent<RectTransform>();
            press.travel = lift;

            ButtonRefs refs = new ButtonRefs();
            refs.root = root;
            refs.button = btn;
            refs.label = t;
            refs.fill = fillImg;
            return refs;
        }

        public static ButtonRefs GreenButton(Transform parent, string label, Vector2 size, int fontSize, Action onClick)
        {
            return Button(parent, label, size, UITheme.FillActionGreen, UITheme.FillActionGreenDeep, UITheme.FillActionGreenInk, fontSize, onClick);
        }

        public static ButtonRefs GoldButton(Transform parent, string label, Vector2 size, int fontSize, Action onClick)
        {
            ButtonRefs r = Button(parent, label, size, UITheme.GoldTop, UITheme.GoldDeep, UITheme.GoldInk, fontSize, onClick);
            r.label.color = UITheme.GoldInk;
            Outline o = r.label.GetComponent<Outline>();
            if (o != null) o.effectColor = new Color(1f, 1f, 1f, 0.5f);
            return r;
        }

        public static ButtonRefs RedButton(Transform parent, string label, Vector2 size, int fontSize, Action onClick)
        {
            return Button(parent, label, size, UITheme.FillRivalRedBtnTop, UITheme.FillRivalRedBtnDeep, Hex(92, 15, 15), fontSize, onClick);
        }

        public static ButtonRefs GhostButton(Transform parent, string label, Vector2 size, int fontSize, Action onClick)
        {
            return Button(parent, label, size, Hex(75, 125, 174), Hex(43, 85, 128), UITheme.OutlineNavy, fontSize, onClick);
        }

        public static ButtonRefs BlueButton(Transform parent, string label, Vector2 size, int fontSize, Action onClick)
        {
            return Button(parent, label, size, Hex(99, 196, 255), Hex(30, 124, 200), UITheme.OutlineNavy, fontSize, onClick);
        }

        /// <summary>Small round icon-only button (e.g. the '‹' back button).</summary>
        public static ButtonRefs IconButton(Transform parent, string icon, float diameter, Action onClick)
        {
            return GhostButton(parent, icon, new Vector2(diameter, diameter), Mathf.RoundToInt(diameter * 0.42f), onClick);
        }

        private static Color Hex(int r, int g, int b, float a = 1f)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        // ============================================================
        // RIBBON
        // ============================================================
        /// <summary>
        /// A section title plate. Was a gold slab, which put the single loudest colour in the kit
        /// behind the least important words on the screen; it is now a dark capsule with volt type,
        /// so a heading reads as a heading rather than as a reward.
        /// </summary>
        public static GameObject Ribbon(Transform parent, string text, float width, float height, int fontSize)
        {
            PSKit.Stack s = PSKit.BuildStack(parent, "Ribbon", new Vector2(width, height),
                UITheme.RadiusChip, true, UITheme.Ink0, UITheme.Panel, UITheme.SheenDark, 8, 0, 0);
            Text t = PSKit.Display(s.face.transform, "Text", TextAnchor.MiddleCenter, text, UITheme.Volt, fontSize);
            StretchRect(t.gameObject, Vector2.zero, Vector2.one, new Vector2(height * 0.3f, 0), new Vector2(-height * 0.3f, 0));
            ClampLine(t, 20);
            return s.root;
        }

        // ============================================================
        // CAPSULE / CHIP
        // ============================================================
        public static GameObject Chip(Transform parent, string text, Color fillColor, float height, int fontSize)
        {
            GameObject go = Child("Chip", parent);
            // 0.66 rather than 0.62 per character, with a tighter text inset. Best-fit shrinking
            // does not prevent a wrap — it only prevents vertical overflow — so a chip whose
            // estimate came up a few px short would break the label onto two lines inside the
            // capsule rather than shrinking it. The estimate has to be generous.
            float width = Mathf.Max(height + 20f, text.Length * fontSize * 0.66f + height * 0.7f);
            Centered(go, new Vector2(width, height));
            go.AddComponent<Image>();
            OutlinedFill(go, ChipSprite, fillColor, UITheme.OutlineNavy, Mathf.Min(3f, height * 0.06f));
            Text t = Text(go.transform, "Text", TextAnchor.MiddleCenter, text, Color.white, fontSize, FontStyle.Bold);
            StretchRect(t.gameObject, Vector2.zero, Vector2.one, new Vector2(height * 0.24f, 0), new Vector2(-height * 0.24f, 0));
            ClampLine(t);
            return go;
        }

        /// <summary>Per-currency disc colours, so a coin pill and a gem pill are distinguishable at a
        /// glance. Both used to render as an identical gold disc with an invisible emoji on it.</summary>
        public static void CurrencyColors(IconId icon, out Color discTop, out Color discDeep, out Color discInk)
        {
            switch (icon)
            {
                case IconId.Gem:
                    discTop = Hex(140, 226, 255); discDeep = Hex(28, 128, 208); discInk = Hex(8, 48, 92); return;
                case IconId.Trophy:
                    discTop = Hex(255, 226, 140); discDeep = Hex(206, 122, 20); discInk = UITheme.GoldInk; return;
                case IconId.Star:
                    // Deep enough that a white star on it still has contrast; the pale variant left
                    // the overall-rating glyph almost invisible.
                    discTop = Hex(64, 152, 224); discDeep = Hex(19, 74, 138); discInk = Hex(6, 34, 66); return;
                default:
                    discTop = UITheme.GoldTop; discDeep = UITheme.GoldDeep; discInk = UITheme.GoldInk; return;
            }
        }

        /// <summary>Currency capsule: icon disc + amount, matching the header "cap-pill" piece.</summary>
        public static GameObject CurrencyPill(Transform parent, IconId icon, string amount)
        {
            float height = 66f;
            float width = 190f;
            GameObject go = Child("CurrencyPill", parent);
            Centered(go, new Vector2(width, height));
            go.AddComponent<Image>();
            OutlinedGradientFill(go, ChipSprite, UITheme.PanelDarkTop, UITheme.PanelDarkDeep, UITheme.OutlineNavy, UITheme.StrokeOutlineThin);

            Color discTop, discDeep, discInk;
            CurrencyColors(icon, out discTop, out discDeep, out discInk);

            GameObject iconGO = Child("IconDisc", go.transform);
            Rect(iconGO, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8, 0), new Vector2(50, 50));
            iconGO.AddComponent<Image>();
            OutlinedGradientFill(iconGO, CircleSprite, discTop, discDeep, discInk, 3f);
            IconAt(iconGO.transform, icon, 30f, Color.white, discInk, new Vector2(0.5f, 0.5f), Vector2.zero);

            Text amountText = Text(go.transform, "Amount", TextAnchor.MiddleLeft, amount, Color.white, 26, FontStyle.Bold);
            StretchRect(amountText.gameObject, Vector2.zero, Vector2.one, new Vector2(66, 0), new Vector2(-14, 0));
            ClampLine(amountText);
            return go;
        }

        // ============================================================
        // METER / TICKS / PIPS
        // ============================================================
        public static Image Meter(Transform parent, Vector2 size, float fill01, Color fillColor)
        {
            GameObject bg = Child("Meter", parent);
            Centered(bg, size);
            return MeterOn(bg, fill01, fillColor, UITheme.RadiusChip, 5f);
        }

        /// <summary>Builds a meter that fills its parent rect exactly — for use inside a host GameObject
        /// whose RectTransform is already positioned/sized by the caller. `cornerRadius`/`pad` default
        /// to UITheme.RadiusChip (20px) / 5px, sized for the ~18-48px-tall meters every existing caller
        /// uses. Both need to shrink together for anything shorter than about 20px:
        /// - `pad` is applied on *every* edge including top+bottom (OutlinedFill), so a meter's actual
        ///   rendered height is `meterHeight - 2*pad` -- at the default 5px pad, anything under 10px
        ///   tall collapses to a zero-or-negative-height rect that Unity silently renders as nothing,
        ///   at *any* fill fraction. This is not a "too thin to see" problem, it's invisible outright.
        /// - `cornerRadius` is the sprite's 9-slice border width; a 20px radius on a bar shorter than
        ///   ~15-20px has nowhere to put the sprite's straight middle section and renders as one
        ///   indistinguishable rounded blob regardless of fill.
        /// (Both discovered building GearCard's stat rows -- an 8px-tall bar with the old hardcoded
        /// 5px pad rendered nothing at all, not even a sliver, at every fill value tested.)</summary>
        public static Image MeterStretch(Transform parent, float fill01, Color fillColor, int cornerRadius = UITheme.RadiusChip, float pad = 5f)
        {
            GameObject bg = Child("Meter", parent);
            Fill(bg);
            return MeterOn(bg, fill01, fillColor, cornerRadius, pad);
        }

        private static Image MeterOn(GameObject bg, float fill01, Color fillColor, int cornerRadius, float pad)
        {
            Sprite sprite = RoundedSprite(cornerRadius);
            bg.AddComponent<Image>();
            Image bgFill = OutlinedFill(bg, sprite, UITheme.PanelDarkDeep, UITheme.OutlineNavy, pad);

            GameObject fillGO = Child("Fill", bgFill.transform);
            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite = sprite;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = fillColor;
            StretchRect(fillGO, Vector2.zero, new Vector2(Mathf.Clamp01(fill01), 1f), Vector2.zero, Vector2.zero);
            return fillImg;
        }

        /// <summary>Ten fat attribute ticks: filled (blue), incoming (green), empty (dark outline).</summary>
        public static void Ticks(Transform parent, int total, int filled, int incoming, Color filledColor, Color incomingColor)
        {
            float gap = 5f;
            RectTransform parentRect = parent as RectTransform;
            float totalWidth = parentRect != null ? parentRect.rect.width : total * 24f;
            float w = (totalWidth - gap * (total - 1)) / total;
            for (int i = 0; i < total; i++)
            {
                GameObject tick = Child("Tick" + i, parent);
                Color c;
                if (i < filled) c = filledColor;
                else if (i < filled + incoming) c = incomingColor;
                else c = new Color(0.02f, 0.08f, 0.15f, 0.55f);
                Rect(tick, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(i * (w + gap), 0), new Vector2(w, 0));
                tick.AddComponent<Image>();
                OutlinedFill(tick, RoundedSprite(5), c, UITheme.OutlineNavy, 2f);
            }
        }

        /// <summary>Seven narrow race pips (match score), identical shape to the in-match HUD.</summary>
        public static void Pips(Transform parent, int total, int filled, Color filledColor, float pipW, float pipH, float gap)
        {
            float totalW = total * pipW + (total - 1) * gap;
            RectTransform rt = parent.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(totalW, pipH);
            for (int i = 0; i < total; i++)
            {
                GameObject pip = Child("Pip" + i, parent);
                Image img = pip.AddComponent<Image>();
                img.sprite = RoundedSprite(4);
                img.type = Image.Type.Sliced;
                img.color = i < filled ? filledColor : new Color(1f, 1f, 1f, 0.22f);
                Rect(pip, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(i * (pipW + gap), 0), new Vector2(pipW, pipH));
            }
        }

        // ============================================================
        // RARITY
        // ============================================================
        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return UITheme.RarityRare;
                case Rarity.Epic: return UITheme.RarityEpic;
                case Rarity.Legendary: return UITheme.RarityLegend;
                default: return UITheme.RarityCommon;
            }
        }

        public static string RarityLabel(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return "RARE";
                case Rarity.Epic: return "EPIC";
                case Rarity.Legendary: return "LEGENDARY";
                default: return "COMMON";
            }
        }

        /// <summary>
        /// The card fill for a rarity. Derived from <see cref="RarityColor"/> rather than a second
        /// hardcoded table: the two lists had drifted apart, so a Legendary chip and a Legendary card
        /// were different colours on the same screen. The darker stop is the same hue at 42%.
        /// </summary>
        public static void RarityGradient(Rarity r, out Color top, out Color bottom)
        {
            top = RarityColor(r);
            bottom = new Color(top.r * 0.42f, top.g * 0.42f, top.b * 0.42f, 1f);
        }

        // ============================================================
        // GEAR CARD
        // ============================================================
        /// <summary>statLabels/statValues (each of length 3, matching GearProgressionCurve.DisplayLabels/
        /// DisplayValues) draw the slot's 3 relevant stats as compact label+meter rows below the rarity
        /// label -- matches the original gear-card mockup. Optional and defaulted to null so the two
        /// other callers (GearUpgradeRevealScreen's reveal card, ShopScreen's bundle deal cards -- which
        /// have no per-item stats to show, only a bundle name) are completely unaffected.</summary>
        public static GameObject GearCard(Transform parent, Vector2 size, IconId icon, string name, Rarity rarity, float progress01,
            string[] statLabels = null, int[] statValues = null)
        {
            bool showStats = statLabels != null && statValues != null && statLabels.Length > 0 && statLabels.Length == statValues.Length;

            GameObject card = Child("Card_" + name, parent);
            Centered(card, size);
            card.AddComponent<Image>();
            OutlinedGradientFill(card, PanelSprite, UITheme.PanelTop, UITheme.PanelDeep, UITheme.OutlineNavy, UITheme.StrokeOutline);

            // Showing 3 stat rows needs more room below the art than the original layout had.
            // GearCatalogScreen also overlays a status chip ("LEVEL 7"/"EQUIPPED"/etc) on top of every
            // card at a fixed y-band of 34-82 from the card's bottom -- that chip is built by the
            // caller, not by this method, so it doesn't show up by reading this function alone; a first
            // pass at this missed it entirely and the stat rows collided with it. Shrinking the art to
            // 42% (from 58%) reclaims enough room for 3 rows to sit entirely above that chip, with
            // margin. Callers with no stats to show keep the original 58%, unchanged.
            float artHeight = size.y * (showStats ? 0.42f : 0.58f);
            GameObject art = Child("Art", card.transform);
            Rect(art, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -UITheme.StrokeOutline), new Vector2(0, artHeight));
            art.AddComponent<Image>();
            Color rTop, rBottom;
            RarityGradient(rarity, out rTop, out rBottom);
            OutlinedFill(art, RoundedSprite(6), rTop, UITheme.OutlineNavy, 0f);
            UIGradient artGrad = art.transform.Find("Fill").gameObject.AddComponent<UIGradient>();
            artGrad.topColor = rTop;
            artGrad.bottomColor = rBottom;

            float iconSize = Mathf.Min(artHeight * 0.68f, size.x * 0.52f);
            IconAt(art.transform, icon, iconSize, Color.white, UITheme.OutlineNavy, new Vector2(0.5f, 0.5f), Vector2.zero);

            Text nameText = StrokedText(card.transform, "Name", TextAnchor.MiddleCenter, name, Color.white, Mathf.Min(32, Mathf.RoundToInt(size.x * 0.1f)));
            Rect(nameText.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, size.y - artHeight - 44f), new Vector2(-20, 40));
            // Item names are data-driven, so shrink rather than bleed past the card edge.
            ClampLine(nameText);

            Text rarityText = Text(card.transform, "Rarity", TextAnchor.MiddleCenter, RarityLabel(rarity), rTop, Mathf.Min(24, Mathf.RoundToInt(size.x * 0.08f)), FontStyle.Bold);
            Rect(rarityText.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, size.y - artHeight - 78f), new Vector2(-16, 30));

            if (showStats)
            {
                // Point-anchored throughout (not stretch) so every position/size below is an absolute
                // pixel value computed from `size`, not anchor-derived -- easier to verify by hand.
                const float rowHeight = 24f;
                const float rowGap = 8f;
                const float leftMargin = 20f;
                const float labelGap = 10f;
                const float rightMargin = 20f;
                const float meterHeight = 12f;
                // GearCatalogScreen's status chip occupies y 34-82 (fixed, independent of card size) --
                // the last row's bottom edge must clear that with margin. See artHeight's own comment.
                const float chipTopY = 82f;
                const float chipClearance = 10f;
                float labelWidth = size.x * 0.30f;
                float meterWidth = Mathf.Max(20f, size.x - leftMargin - labelWidth - labelGap - rightMargin);
                // Starts just under rarityText's own bottom edge (size.y - artHeight - 78) and grows
                // downward toward the status chip.
                float blockTop = size.y - artHeight - 78f - 6f;
                float blockBottom = blockTop - statLabels.Length * rowHeight - (statLabels.Length - 1) * rowGap;
                if (blockBottom < chipTopY + chipClearance)
                {
                    Debug.LogWarning("GearCard: stat block (bottom " + blockBottom + ") does not clear the status chip (top " + chipTopY +
                        ") on a " + size + " card -- rows will visually overlap it. Shrink artHeight's showStats fraction further or reduce rowHeight/rowGap.");
                }

                for (int i = 0; i < statLabels.Length; i++)
                {
                    float rowBottom = blockTop - i * (rowHeight + rowGap) - rowHeight;

                    Text statLbl = Text(card.transform, "StatLbl" + i, TextAnchor.MiddleLeft, statLabels[i],
                        new Color(1f, 1f, 1f, 0.82f), Mathf.RoundToInt(size.x * 0.042f), FontStyle.Bold);
                    Rect(statLbl.gameObject, Vector2.zero, Vector2.zero, Vector2.zero,
                        new Vector2(leftMargin, rowBottom), new Vector2(labelWidth, rowHeight));

                    GameObject statMeterHost = Child("StatMeter" + i, card.transform);
                    Rect(statMeterHost, Vector2.zero, Vector2.zero, Vector2.zero,
                        new Vector2(leftMargin + labelWidth + labelGap, rowBottom + (rowHeight - meterHeight) * 0.5f), new Vector2(meterWidth, meterHeight));
                    // Same /100 normalization GearDetailScreen's ticks and GearLoadoutScreen's
                    // ATTRIBUTES panel already use for a raw stat value. cornerRadius 3, pad 2 (not
                    // the 20px/5px defaults) -- see MeterStretch's own doc comment; at the defaults
                    // this 12px-tall bar's fill would render completely invisible, not just thin.
                    statMeterHost.GetComponent<RectTransform>().sizeDelta = new Vector2(Mathf.Max(20, meterWidth - 48), meterHeight);
                    MeterStretch(statMeterHost.transform, Mathf.Clamp01(statValues[i] / 100f), rTop, cornerRadius: 3, pad: 2f);
                    Text number = Text(card.transform, "StatValue" + i, TextAnchor.MiddleRight,
                        statValues[i].ToString(), Color.white, 24, FontStyle.Bold);
                    Rect(number.gameObject, Vector2.zero, Vector2.zero, Vector2.zero,
                        new Vector2(size.x - rightMargin - 44, rowBottom - 4), new Vector2(44, 32));
                }
            }

            if (progress01 >= 0f)
            {
                GameObject meterHost = Child("MeterHost", card.transform);
                Rect(meterHost, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(-24, 18));
                MeterStretch(meterHost.transform, progress01, rTop);
            }
            return card;
        }

        // ============================================================
        // GEAR SLOT
        // ============================================================
        public static GameObject GearSlot(Transform parent, float size, IconId icon, int level, Rarity rarity, bool empty)
        {
            GameObject slot = Child("Slot", parent);
            Centered(slot, new Vector2(size, size));
            slot.AddComponent<Image>();
            if (empty)
            {
                OutlinedFill(slot, RoundedSprite(UITheme.RadiusPanel), new Color(0.03f, 0.13f, 0.24f, 0.5f), new Color(0.03f, 0.13f, 0.24f, 0.7f), 4f);
                IconAt(slot.transform, IconId.Plus, size * 0.36f, new Color(1f, 1f, 1f, 0.55f), new Color(0f, 0f, 0f, 0.35f), new Vector2(0.5f, 0.5f), Vector2.zero);
                return slot;
            }

            OutlinedGradientFill(slot, RoundedSprite(UITheme.RadiusPanel), Hex(27, 83, 145), Hex(12, 44, 80), RarityColor(rarity), UITheme.StrokeOutlineThin);
            IconAt(slot.transform, icon, size * 0.76f, Color.white, UITheme.OutlineNavy, new Vector2(0.5f, 0.5f), Vector2.zero);

            GameObject lvBadge = Child("LvBadge", slot.transform);
            Rect(lvBadge, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-8, 8), new Vector2(size * 0.34f, size * 0.26f));
            lvBadge.AddComponent<Image>();
            OutlinedGradientFill(lvBadge, RoundedSprite(8), UITheme.GoldTop, UITheme.GoldDeep, UITheme.GoldInk, 3f);
            Text lvText = Text(lvBadge.transform, "Lv", TextAnchor.MiddleCenter, level.ToString(), UITheme.GoldInk, Mathf.RoundToInt(size * 0.16f), FontStyle.Bold);
            Fill(lvText.gameObject);
            return slot;
        }

        // ============================================================
        // TOP BAR (back button + ribbon title + optional currency)
        // ============================================================
        /// <summary>
        /// Back disc, title, and an optional balance on the right. Delegates to
        /// <see cref="PSKit.TopBar"/> so every screen shares the settings board's header instead of
        /// the retired gold ribbon.
        /// </summary>
        public static GameObject TopBar(Transform parent, string title, Action onBack, IconId currencyIcon, string currencyAmount)
        {
            GameObject bar = PSKit.TopBar(parent, title, onBack);

            if (!string.IsNullOrEmpty(currencyAmount) && currencyIcon != IconId.None)
            {
                float h = UITheme.F(29f);
                GameObject pill = PSKit.CurrencyPill(bar.transform, currencyIcon, currencyAmount,
                    new Vector2(UITheme.F(88f), h), null);
                Rect(pill, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    Vector2.zero, new Vector2(UITheme.F(88f), h));

                // Stop the title running under the balance.
                Transform titleT = bar.transform.Find("Title");
                if (titleT != null)
                {
                    RectTransform rt = titleT.GetComponent<RectTransform>();
                    rt.offsetMax = new Vector2(-UITheme.F(100f), rt.offsetMax.y);
                }
            }
            return bar;
        }

        // ============================================================
        // BOTTOM NAV
        // ============================================================
        public static readonly string[] NavTabIds = new string[] { "HOME", "PLAY", "GEAR", "LEAGUE", "SHOP" };
        private static readonly IconId[] navIcons = new IconId[] { IconId.Home, IconId.Play, IconId.Bolt, IconId.Trophy, IconId.Bag };

        /// <summary>
        /// Bottom tab bar. Tabs divide the full width evenly rather than sitting on a fixed 200px
        /// pitch — the old fixed pitch assumed exactly 1080 reference px and drifted off-centre on
        /// any other aspect ratio.
        /// </summary>
        /// <summary>
        /// The four-tab bar, shared by every nav screen. Delegates to <see cref="PSKit.BottomNav"/>.
        ///
        /// The old bar carried a fifth PLAY tab. The board drops it: play is the lobby's own
        /// full-width CTA, and a nav tab that duplicates the biggest button on the home screen only
        /// splits the one action the whole screen exists to offer. A screen passing "PLAY" as the
        /// active tab simply lights nothing, which is honest — a tour list is not a destination in
        /// this bar.
        /// </summary>
        public static GameObject BottomNav(Transform parent, string activeTab, Action<string> onSelect)
        {
            return PSKit.BottomNav(parent, activeTab, onSelect);
        }

        // ============================================================
        // SCREEN-LIFETIME COROUTINES
        // ============================================================
        /// <summary>Starts a coroutine on the screen's own root GameObject rather than on the
        /// long-lived ScreenManager, so it is automatically killed when the screen is torn down
        /// (e.g. the player cancels matchmaking before the fake search resolves).</summary>
        public static Coroutine RunOnScreen(GameObject screenRoot, IEnumerator routine)
        {
            ScreenCoroutineHost host = screenRoot.GetComponent<ScreenCoroutineHost>();
            if (host == null) host = screenRoot.AddComponent<ScreenCoroutineHost>();
            return host.StartCoroutine(routine);
        }

        // ============================================================
        // SCROLL VIEW
        // ============================================================
        /// <summary>A vertical-only scroll view. Reposition the returned root with Rect/StretchRect,
        /// then build children under <paramref name="content"/> top-down and set its sizeDelta.y to
        /// the total stacked height once you know it.</summary>
        public static GameObject ScrollView(Transform parent, out RectTransform content)
        {
            GameObject root = Child("ScrollView", parent);
            ScrollRect scroll = root.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            // Elastic rather than Clamped: the rubber-band at the ends is the feedback that tells
            // the player they have reached the end of the list rather than hit a dead control.
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 40f;

            GameObject viewport = Child("Viewport", root.transform);
            Fill(viewport);

            // RectMask2D, NOT Mask. The previous implementation used a stencil Mask driven by an
            // Image tinted to alpha 0.001 — and Unity's UI mask shader clips at exactly
            // `alpha - 0.001`, so every pixel of every child failed the test and the entire
            // contents of the scroll view were discarded. Tour Select and League rendered blank.
            // RectMask2D clips to the rect directly: no stencil, no graphic, no draw call, and
            // nothing to mis-tune.
            viewport.AddComponent<RectMask2D>();

            GameObject contentGO = Child("Content", viewport.transform);
            RectTransform contentRt = Rect(contentGO, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0, 0));

            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRt;
            content = contentRt;
            return root;
        }

        // ============================================================
        // BACKGROUND
        // ============================================================
        /// <summary>The screen wash. Delegates to <see cref="PSKit.Backdrop"/>.</summary>
        public static GameObject SkyBackground(Transform parent, Color top, Color bottom)
        {
            return PSKit.Backdrop(parent, top, bottom);
        }

        // ============================================================
        // MODAL SCRIM
        // ============================================================
        public static GameObject ModalScrim(Transform parent)
        {
            GameObject go = Child("Scrim", parent);
            Fill(go);
            Image img = go.AddComponent<Image>();
            img.color = UITheme.ModalScrim;
            return go;
        }

        // ============================================================
        // AAA UI EXTENSIONS: DEPTH, SHINE, GLOW, POLISH
        // ============================================================

        /// <summary>Adds a dark drop-shadow duplicate behind the target element for visual elevation.</summary>
        public static GameObject DropShadow(GameObject target, float offsetY = -8f, float extraSize = 4f)
        {
            GameObject shadow = Child(target.name + "_Shadow", target.transform.parent);
            shadow.transform.SetSiblingIndex(target.transform.GetSiblingIndex());

            RectTransform targetRt = target.GetComponent<RectTransform>();
            RectTransform shadowRt = shadow.AddComponent<RectTransform>();
            if (targetRt != null)
            {
                shadowRt.anchorMin = targetRt.anchorMin;
                shadowRt.anchorMax = targetRt.anchorMax;
                shadowRt.pivot = targetRt.pivot;
                shadowRt.anchoredPosition = targetRt.anchoredPosition + new Vector2(0, offsetY);
                shadowRt.sizeDelta = targetRt.sizeDelta + new Vector2(extraSize, extraSize);
            }

            Image img = shadow.AddComponent<Image>();
            img.sprite = PanelSprite;
            img.type = Image.Type.Sliced;
            img.color = UITheme.ShadowColor;
            return shadow;
        }

        /// <summary>Creates a top-to-bottom glass shine highlight across the upper half of a container.</summary>
        public static GameObject ShineLayer(Transform parent, float heightRatio = 0.45f)
        {
            GameObject shine = Child("ShineOverlay", parent);
            RectTransform rt = StretchRect(shine, new Vector2(0f, 1f - heightRatio), Vector2.one, new Vector2(6, -6), new Vector2(-6, -6));
            Image img = shine.AddComponent<Image>();
            img.sprite = RoundedSprite(14);
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            UIGradient grad = shine.AddComponent<UIGradient>();
            // Was 0.28 -> 0.02: a strong glass highlight across every panel, chip and chest slot in
            // the kit, which is exactly the glossy "candy" look the flat-button pass moved away from.
            // Kept faint rather than removed outright — panels still want a hint of light direction.
            grad.topColor = new Color(1f, 1f, 1f, 0.12f);
            grad.bottomColor = new Color(1f, 1f, 1f, 0.00f);
            grad.direction = GradientDirection.Vertical;
            return shine;
        }

        /// <summary>Beveled, glowing AAA panel with gradient fill, outer glow border, and glass shine.</summary>
        public static GameObject GlowPanel(Transform parent, string name, Vector2 size, Color topColor, Color bottomColor, Color glowColor)
        {
            // Drop Shadow
            GameObject shadow = Child(name + "_Shadow", parent);
            Centered(shadow, size + new Vector2(10, 10));
            Image sImg = shadow.AddComponent<Image>();
            sImg.sprite = PanelSprite;
            sImg.type = Image.Type.Sliced;
            sImg.color = UITheme.ShadowColor;

            // Main Panel
            GameObject go = Child(name, parent);
            Centered(go, size);
            go.AddComponent<Image>();
            OutlinedGradientFill(go, PanelSprite, topColor, bottomColor, glowColor, UITheme.StrokeOutline);

            // Glass Shine
            ShineLayer(go.transform, 0.42f);

            return go;
        }

        /// <summary>High-contrast currency pill with a per-currency rim and bold counter. Returns the
        /// amount Text so callers can animate it (see UICountUp) when the balance changes.</summary>
        public static GameObject CurrencyPillAAA(Transform parent, IconId icon, string amount, Color accentGlow, Action onClick = null)
        {
            float height = 72f;
            float width = 230f;
            GameObject go = Child("CurrencyPillAAA", parent);
            Centered(go, new Vector2(width, height));
            go.AddComponent<Image>();
            OutlinedGradientFill(go, ChipSprite, UITheme.PanelDarkTop, UITheme.PanelDarkDeep, accentGlow, 4f);

            Color discTop, discDeep, discInk;
            CurrencyColors(icon, out discTop, out discDeep, out discInk);

            GameObject iconGO = Child("IconDisc", go.transform);
            Rect(iconGO, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8, 0), new Vector2(56, 56));
            iconGO.AddComponent<Image>();
            OutlinedGradientFill(iconGO, CircleSprite, discTop, discDeep, discInk, 3f);
            IconAt(iconGO.transform, icon, 34f, Color.white, discInk, new Vector2(0.5f, 0.5f), Vector2.zero);

            Text amountText = StrokedText(go.transform, "Amount", TextAnchor.MiddleLeft, amount, Color.white, 30);
            StretchRect(amountText.gameObject, Vector2.zero, Vector2.one, new Vector2(74, 0), new Vector2(-52, 0));
            ClampLine(amountText);

            // A "+" affordance turns the balance read-out into the shortcut to the shop, which is
            // where a player looks first when they cannot afford something.
            if (onClick != null)
            {
                GameObject plus = Child("Plus", go.transform);
                Rect(plus, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8, 0), new Vector2(44, 44));
                plus.AddComponent<Image>();
                OutlinedGradientFill(plus, CircleSprite, UITheme.FillActionGreen, UITheme.FillActionGreenDeep, Hex(11, 61, 22), 3f);
                IconAt(plus.transform, IconId.Plus, 24f, Color.white, Hex(11, 61, 22), new Vector2(0.5f, 0.5f), Vector2.zero);

                Button btn = go.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = go.GetComponent<Image>();
                btn.onClick.AddListener(delegate { onClick(); });
            }
            return go;
        }

        /// <summary>
        /// The screen's single headline CTA: flat color fill, a tight offset shadow for elevation,
        /// and an idle pulse. Used to be gold-only with a glass ShineLayer highlight across the top —
        /// that glossy-bevel treatment plus a gold fill made every screen's biggest button read as
        /// "gacha gold", and it was also the only button on the whole lobby that wasn't the kit's own
        /// green primary-action color. Color is now a parameter so the CTA can carry the same brand
        /// green as every other primary action, while gold stays reserved for currency/rewards.
        /// </summary>
        public static ButtonRefs PrimaryButtonAAA(Transform parent, string label, Vector2 size, int fontSize,
            Color fillTop, Color fillDeep, Color outlineColor, Color textColor, Action onClick)
        {
            GameObject root = Child("Btn_AAA_" + label, parent);
            Centered(root, size);

            // Drop shadow — tightened from -10 to -6 so the button reads as flat-with-elevation
            // rather than as a thick 3D slab standing off the background.
            GameObject shadow = Child("Shadow", root.transform);
            StretchRect(shadow, Vector2.zero, Vector2.one, new Vector2(0, -6), new Vector2(0, -6));
            Image sImg = shadow.AddComponent<Image>();
            sImg.sprite = ButtonSprite;
            sImg.type = Image.Type.Sliced;
            sImg.color = UITheme.ShadowColor;

            Image raycast = root.AddComponent<Image>();
            raycast.color = new Color(0f, 0f, 0f, 0f);

            Button btn = root.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = raycast;

            GameObject content = Child("Content", root.transform);
            Fill(content);
            content.AddComponent<Image>();
            Color flatTop = Color.Lerp(fillTop, fillDeep, 0.30f);
            Color flatBottom = Color.Lerp(fillTop, fillDeep, 0.62f);
            Image fillImg = OutlinedGradientFill(content, ButtonSprite, flatTop, flatBottom, outlineColor, UITheme.BevelButton);

            // No ShineLayer here — the glass highlight was the biggest single contributor to the
            // "candy button" look on the kit's most prominent control.
            Text t = StrokedText(content.transform, "Label", TextAnchor.MiddleCenter, label, textColor, fontSize);
            Fill(t.gameObject);

            UIButtonPress press = root.AddComponent<UIButtonPress>();
            press.content = content.GetComponent<RectTransform>();
            press.travel = UITheme.PressTravel;

            // Pulse on CTA
            root.AddComponent<UIPulse>();

            if (onClick != null) btn.onClick.AddListener(delegate { onClick(); });

            ButtonRefs refs = new ButtonRefs();
            refs.root = root;
            refs.button = btn;
            refs.label = t;
            refs.fill = fillImg;
            return refs;
        }

        /// <summary>Gold-flavored PrimaryButtonAAA — reserved for reward-collection moments (e.g. a
        /// chest's COLLECT ALL) where the gold fill is thematically the reward itself, not just chrome.</summary>
        public static ButtonRefs GoldButtonAAA(Transform parent, string label, Vector2 size, int fontSize, Action onClick)
        {
            ButtonRefs r = PrimaryButtonAAA(parent, label, size, fontSize, UITheme.GoldTop, UITheme.GoldDeep, UITheme.GoldInk, UITheme.GoldInk, onClick);
            // Dark ink text needs a light halo, not the default dark-navy stroke — the same override
            // GoldButton applies below.
            Outline o = r.label.GetComponent<Outline>();
            if (o != null) o.effectColor = new Color(1f, 1f, 1f, 0.75f);
            return r;
        }

        /// <summary>Brawl Stars style glowing circular avatar frame with pulsing gold rim and level badge.</summary>
        public static GameObject AvatarFrame(Transform parent, IconId icon, int level, Vector2 size)
        {
            GameObject root = Child("AvatarFrame", parent);
            Centered(root, size);

            // Outer Glow Rim
            GameObject glow = Child("OuterGlow", root.transform);
            Centered(glow, size + new Vector2(16, 16));
            Image glowImg = glow.AddComponent<Image>();
            glowImg.sprite = CircleSprite;
            glowImg.color = UITheme.GlowGold;

            // Main Avatar Base
            GameObject avatar = Child("Base", root.transform);
            Fill(avatar);
            avatar.AddComponent<Image>();
            OutlinedGradientFill(avatar, CircleSprite, UITheme.SkyMid, UITheme.CourtDeep, UITheme.AvatarRingGold, 7f);

            // Avatar Icon
            IconAt(avatar.transform, icon, size.x * 0.54f, Color.white, UITheme.OutlineNavy, new Vector2(0.5f, 0.5f), Vector2.zero);

            // Top Shine
            GameObject shine = Child("Shine", avatar.transform);
            Rect(shine, new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.95f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image sImg = shine.AddComponent<Image>();
            sImg.sprite = CircleSprite;
            sImg.color = new Color(1f, 1f, 1f, 0.25f);

            // Level Badge
            GameObject lvBadge = Child("LvBadge", root.transform);
            Rect(lvBadge, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(74, 38));
            lvBadge.AddComponent<Image>();
            OutlinedGradientFill(lvBadge, RoundedSprite(10), UITheme.GoldTop, UITheme.GoldDeep, UITheme.GoldInk, 3f);
            Text lvText = StrokedText(lvBadge.transform, "Lv", TextAnchor.MiddleCenter, "LV " + level, UITheme.GoldInk, 22);
            Outline o = lvText.GetComponent<Outline>();
            if (o != null) o.effectColor = new Color(1f, 1f, 1f, 0.6f);
            Fill(lvText.gameObject);

            return root;
        }

        /// <summary>Futuristic stadium stage platform disc with perspective ellipse look.</summary>
        public static GameObject HeroPlatformDisc(Transform parent, Vector2 size)
        {
            GameObject disc = Child("HeroStageDisc", parent);
            Centered(disc, size);

            // A black-walled court-blue well with a volt ring inside it -- the same construction as
            // the matchmaking portraits, so the character reads as standing in the same kind of
            // frame wherever they appear. It used to be a cyan-rimmed navy disc with a soft floor
            // shadow, which on the orchid backdrop looked like a hole cut in the screen.
            Image outline = disc.AddComponent<Image>();
            outline.sprite = CircleSprite;
            outline.color = UITheme.Outline;

            GameObject well = Child("Well", disc.transform);
            StretchRect(well, Vector2.zero, Vector2.one, new Vector2(9, 9), new Vector2(-9, -9));
            Image wellImg = well.AddComponent<Image>();
            wellImg.sprite = CircleSprite;
            wellImg.color = UITheme.CourtWell;

            GameObject innerRing = Child("InnerRing", disc.transform);
            Centered(innerRing, size * 0.78f);
            Image irImg = innerRing.AddComponent<Image>();
            irImg.sprite = RingSprite(4);
            irImg.color = new Color(UITheme.Volt.r, UITheme.Volt.g, UITheme.Volt.b, 0.5f);
            irImg.raycastTarget = false;

            return disc;
        }

        /// <summary>Season pass progress meter with notched tick divisions and shimmer sweep.</summary>
        public static GameObject SeasonMeterAAA(Transform parent, float fill01, string tierText)
        {
            return SeasonMeterAAA(parent, fill01, tierText, UITheme.GoldTop, UITheme.GoldDeep);
        }

        public static GameObject SeasonMeterAAA(Transform parent, float fill01, string tierText, Color fillTop, Color fillDeep)
        {
            GameObject bar = Child("SeasonMeterAAA", parent);
            Fill(bar);

            // Dark Inset Base
            bar.AddComponent<Image>();
            OutlinedFill(bar, ChipSprite, UITheme.PanelDarkDeep, UITheme.OutlineNavy, 5f);

            // Progress Fill
            GameObject fillGO = Child("ProgressFill", bar.transform);
            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite = ChipSprite;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = Color.white;
            StretchRect(fillGO, Vector2.zero, new Vector2(Mathf.Clamp01(fill01), 1f), Vector2.zero, Vector2.zero);

            UIGradient grad = fillGO.AddComponent<UIGradient>();
            grad.topColor = fillTop;
            grad.bottomColor = fillDeep;
            grad.direction = GradientDirection.Vertical;

            // Shimmer Sweep
            fillGO.AddComponent<UIShimmer>();

            return bar;
        }
    }

    /// <summary>
    /// Gentle looping scale pulse for badges and CTA buttons.
    ///
    /// Waits for any <see cref="UIFadeIn"/> on the same object to finish before capturing its rest
    /// scale — both components drive localScale, and capturing in Start() used to snapshot whatever
    /// mid-entrance scale happened to be set that frame and then pulse around it forever.
    /// </summary>
    public class UIPulse : MonoBehaviour
    {
        public float scaleAmount = 0.035f;
        public float speed = 2.4f;

        private UIFadeIn entrance;
        private bool captured;
        private float phase;

        private void Awake()
        {
            entrance = GetComponent<UIFadeIn>();
        }

        private void Update()
        {
            // Hold off while the entrance animation owns localScale.
            if (!captured)
            {
                if (entrance != null && entrance.enabled) return;
                captured = true;
            }

            phase += Time.unscaledDeltaTime * speed;
            float s = 1f + Mathf.Sin(phase) * scaleAmount;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>
    /// A specular highlight that actually travels across its parent, left to right, on a loop.
    ///
    /// The previous implementation only oscillated the host Image's overall brightness, which on a
    /// gradient-filled meter is invisible — it read as a very slight flicker rather than as a sweep.
    /// This adds a narrow angled band as a child and animates its position instead.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIShimmer : MonoBehaviour
    {
        public float period = 2.6f;
        /// <summary>Fraction of the host width the band occupies.</summary>
        public float bandWidth = 0.22f;

        private RectTransform band;
        private float t;

        private void Start()
        {
            GameObject clip = UIBuilder.Child("ShimmerClip", transform);
            UIBuilder.Fill(clip);
            clip.AddComponent<RectMask2D>();

            GameObject go = UIBuilder.Child("ShimmerBand", clip.transform);
            band = UIBuilder.Rect(go, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 0f));

            Image img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;

            UIGradient grad = go.AddComponent<UIGradient>();
            grad.topColor = new Color(1f, 1f, 1f, 0f);
            grad.bottomColor = new Color(1f, 1f, 1f, 0f);

            // Horizontal falloff isn't available from the vertical gradient, so the band is kept
            // narrow and semi-transparent instead, which reads cleanly at this size.
            img.color = new Color(1f, 1f, 1f, 0.30f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 14f);
        }

        private void Update()
        {
            if (band == null) return;

            RectTransform host = (RectTransform)transform;
            float w = host.rect.width;
            if (w <= 0f) return;

            band.sizeDelta = new Vector2(Mathf.Max(24f, w * bandWidth), 0f);

            t += Time.unscaledDeltaTime / Mathf.Max(0.1f, period);
            if (t > 1f) t -= 1f;

            // Travel from just off the left edge to just off the right.
            float x = Mathf.Lerp(-band.sizeDelta.x, w + band.sizeDelta.x, t);
            band.anchoredPosition = new Vector2(x, 0f);
        }
    }

    /// <summary>Empty behaviour whose sole purpose is to host screen-lifetime coroutines so they
    /// die with the screen GameObject instead of outliving it on the ScreenManager.</summary>
    public class ScreenCoroutineHost : MonoBehaviour { }

    /// <summary>Maintains an invisible minimum-size raycast rect after later layout code resizes
    /// its parent. Several board helpers deliberately restyle a control after construction, so a
    /// one-time expansion would be lost as soon as BoardRow applies the final dimensions.</summary>
    public class UIExpandedHitArea : MonoBehaviour
    {
        public Vector2 declaredSize;
        private RectTransform hit;
        private RectTransform host;
        private Vector2 lastSize = new Vector2(-1f, -1f);

        private void OnEnable() { Refresh(); }
        private void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            if (hit == null) hit = transform as RectTransform;
            if (host == null) host = transform.parent as RectTransform;
            if (hit == null || host == null) return;

            Vector2 size = host.rect.size;
            if (size.x <= 0f) size.x = declaredSize.x;
            if (size.y <= 0f) size.y = declaredSize.y;
            if (size == lastSize) return;
            lastSize = size;

            float expandX = size.x > 0f ? Mathf.Max(0f, (UITheme.TouchMin - size.x) * 0.5f) : 0f;
            float expandY = size.y > 0f ? Mathf.Max(0f, (UITheme.TouchMin - size.y) * 0.5f) : 0f;
            hit.anchorMin = Vector2.zero;
            hit.anchorMax = Vector2.one;
            hit.offsetMin = new Vector2(-expandX, -expandY);
            hit.offsetMax = new Vector2(expandX, expandY);
        }
    }

    /// <summary>Moves a button's Content rect down by <see cref="travel"/> px while pressed — the
    /// entire "game feel" of every button in the kit comes from this single interaction.</summary>
    public class UIButtonPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public RectTransform content;
        public float travel = 11f;
        private Vector2 originalPos;
        private bool captured;
        private bool pressed;

        private void EnsureCaptured()
        {
            if (!captured && content != null)
            {
                originalPos = content.anchoredPosition;
                captured = true;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Button button = GetComponent<Button>();
            if (button != null && !button.IsInteractable()) return;
            EnsureCaptured();
            if (content != null)
            {
                pressed = true;
                content.anchoredPosition = originalPos + new Vector2(0f, -travel);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        private void Release()
        {
            if (content != null && pressed)
            {
                content.anchoredPosition = originalPos;
            }
            pressed = false;
        }
    }
}
