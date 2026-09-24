using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Pickleball.UI
{
    public enum IconId
    {
        None,
        Coin, Gem, Trophy, Star, Crown,
        Lock, Check, Cross, Plus, Dot, Copy,
        ChevronLeft, ChevronRight, Play, Pause, Refresh,
        Paddle, Shoe, Tape, Ball, Chest, Shield, Player, Bag, Home,
        Bolt, Fire, Warn, ThumbUp,
        Sun, Wave, Leaf, Palm, Dune, Tent,
        Settings,
        // Appended rather than inserted in place -- every earlier value's underlying int must stay
        // put. Wristband is its own id specifically so its raster art (see RasterArt below) doesn't
        // also repaint IconId.Bolt's three unrelated callers (GameplayHUD's "IN RALLY" badge, the
        // bottom nav's PLAY tab, BootScreen's tip) -- Bolt was only ever a placeholder for this slot
        // (GearCatalog.cs), never a real "wristband" id, so those callers must keep the lightning-bolt
        // glyph they actually mean.
        Wristband,
        // Also appended. Gift is deliberately NOT a re-skin of Chest: Chest carries raster art of a
        // brown treasure chest and is used by the shop, the season pass and the chest-opening
        // sequence, all of which mean that object. The home board's gift is a different thing --
        // a ribboned box -- and only the lobby wants it.
        Gift,
        // Background decoration rather than an icon: the splash board scatters pickle slices behind
        // the wordmark. It lives here so it inherits the same SDF baking as everything else.
        PickleSlice
    }

    /// <summary>
    /// Procedural vector icons, rasterised from signed-distance fields at load time and cached.
    ///
    /// The kit originally drew every icon as an emoji codepoint in LegacyRuntime.ttf. Those glyphs
    /// do not exist in the font and Unity's dynamic-font fallback silently rasterises nothing, so
    /// every icon in the game rendered as blank space. Font.HasCharacter returns true for them
    /// regardless, which is why it read as working. These sprites have no font dependency at all,
    /// so they render identically in the editor and on device.
    ///
    /// Each icon is baked twice: a Fill sprite (the shape) and an Outline sprite (the same shape
    /// dilated by <see cref="StrokeUnits"/>), so <see cref="Build"/> can stack a navy silhouette
    /// behind a tintable fill and match the chunky outlined look of the rest of the kit.
    /// </summary>
    public static class UIIcon
    {
        private const int TexSize = 96;
        /// <summary>Outline dilation in normalised icon units. Every shape is authored inside the
        /// 0.06–0.94 box so the dilated silhouette still has room before the texture edge.</summary>
        private const float StrokeUnits = 0.055f;

        private static readonly Dictionary<int, Sprite> fillCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> outlineCache = new Dictionary<int, Sprite>();

        /// <summary>Flat-color, pre-outlined game-icon art (Canva-generated, see
        /// Docs/GearProgression.md) for the handful of ids where real art now exists, keyed to
        /// Resources.Load paths under Assets/Resources/Icons/. Every other id still renders through
        /// the procedural SDF path below -- this is additive, not a replacement for it.</summary>
        private static readonly Dictionary<IconId, string> RasterResourcePaths = new Dictionary<IconId, string>
        {
            { IconId.Paddle, "Icons/icon_paddle" },
            { IconId.Shoe, "Icons/icon_shoe" },
            { IconId.Tape, "Icons/icon_grip" },
            { IconId.Wristband, "Icons/icon_wristband" },
            { IconId.Chest, "Icons/icon_chest" },
        };
        private static readonly Dictionary<int, Sprite> rasterCache = new Dictionary<int, Sprite>();

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>The art already carries its own outline and color, unlike the tintable procedural
        /// icons -- callers that stack a separate navy outline layer behind an id with raster art
        /// would double up on the border, so Build/BuildPlain check this first and skip that layer.</summary>
        public static bool HasRasterArt(IconId id)
        {
            return RasterResourcePaths.ContainsKey(id);
        }

        /// <summary>Null if the id has no raster entry, or if the entry's Resources.Load comes back
        /// empty (asset missing/not yet imported) -- callers fall back to the procedural icon rather
        /// than rendering nothing.</summary>
        public static Sprite Raster(IconId id)
        {
            Sprite s;
            if (rasterCache.TryGetValue((int)id, out s)) return s;
            string path;
            s = RasterResourcePaths.TryGetValue(id, out path) ? Resources.Load<Sprite>(path) : null;
            rasterCache[(int)id] = s;
            return s;
        }

        public static Sprite Fill(IconId id)
        {
            Sprite s;
            if (fillCache.TryGetValue((int)id, out s)) return s;
            s = Bake(id, 0f);
            fillCache[(int)id] = s;
            return s;
        }

        public static Sprite Outline(IconId id)
        {
            Sprite s;
            if (outlineCache.TryGetValue((int)id, out s)) return s;
            s = Bake(id, StrokeUnits);
            outlineCache[(int)id] = s;
            return s;
        }

        /// <summary>Outlined icon: a navy dilated silhouette with the tintable fill stacked on top.
        /// Returns a host GameObject centred in its parent — reposition it with UIBuilder.Rect.</summary>
        public static GameObject Build(Transform parent, IconId id, float size, Color fill, Color outline)
        {
            GameObject host = UIBuilder.Child("Icon_" + id, parent);
            UIBuilder.Centered(host, new Vector2(size, size));
            if (id == IconId.None) return host;

            Sprite raster = Raster(id);
            if (raster != null)
            {
                // Pre-colored, pre-outlined art -- rendered at full white tint (its own colors, not
                // the caller's fill/outline) and with no separate outline layer (the art already has
                // one baked in; stacking the procedural navy outline sprite behind it would just
                // double the border).
                GameObject rasterGO = UIBuilder.Child("Raster", host.transform);
                UIBuilder.Fill(rasterGO);
                Image rasterImg = rasterGO.AddComponent<Image>();
                rasterImg.sprite = raster;
                rasterImg.preserveAspect = true;
                rasterImg.color = Color.white;
                rasterImg.raycastTarget = false;
                return host;
            }

            GameObject outlineGO = UIBuilder.Child("Outline", host.transform);
            UIBuilder.Fill(outlineGO);
            Image outlineImg = outlineGO.AddComponent<Image>();
            outlineImg.sprite = Outline(id);
            outlineImg.color = outline;
            outlineImg.raycastTarget = false;

            GameObject fillGO = UIBuilder.Child("Fill", host.transform);
            UIBuilder.Fill(fillGO);
            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite = Fill(id);
            fillImg.color = fill;
            fillImg.raycastTarget = false;

            return host;
        }

        /// <summary>Icon with no outline — for small inline glyphs where the stroke would muddy it.</summary>
        public static GameObject BuildPlain(Transform parent, IconId id, float size, Color fill)
        {
            GameObject host = UIBuilder.Child("Icon_" + id, parent);
            UIBuilder.Centered(host, new Vector2(size, size));
            if (id == IconId.None) return host;

            Sprite raster = Raster(id);
            Image img = host.AddComponent<Image>();
            img.sprite = raster != null ? raster : Fill(id);
            img.preserveAspect = true;
            img.color = raster != null ? Color.white : fill;
            img.raycastTarget = false;
            return host;
        }

        // ============================================================
        // BAKING
        // ============================================================

        private static Sprite Bake(IconId id, float dilate)
        {
            Texture2D tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[TexSize * TexSize];
            // One texel in normalised units — the anti-alias ramp width.
            float texel = 1f / TexSize;

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) * texel, (y + 0.5f) * texel);
                    float d = Sdf(id, p) - dilate;
                    float a = Mathf.Clamp01(0.5f - d / texel);
                    px[y * TexSize + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), 100f);
        }

        // ============================================================
        // SDF PRIMITIVES (normalised 0..1 space, y up)
        // ============================================================

        private static float Disc(Vector2 p, float cx, float cy, float r)
        {
            return (p - new Vector2(cx, cy)).magnitude - r;
        }

        private static float Ring(Vector2 p, float cx, float cy, float r, float thick)
        {
            return Mathf.Abs((p - new Vector2(cx, cy)).magnitude - r) - thick * 0.5f;
        }

        private static float RBox(Vector2 p, float cx, float cy, float halfW, float halfH, float r)
        {
            float qx = Mathf.Abs(p.x - cx) - halfW + r;
            float qy = Mathf.Abs(p.y - cy) - halfH + r;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        /// <summary>Capsule: a segment from a to b with radius r. The workhorse for strokes.</summary>
        private static float Seg(Vector2 p, float ax, float ay, float bx, float by, float r)
        {
            Vector2 a = new Vector2(ax, ay);
            Vector2 ba = new Vector2(bx, by) - a;
            Vector2 pa = p - a;
            float denom = Vector2.Dot(ba, ba);
            float h = denom <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(pa, ba) / denom);
            return (pa - ba * h).magnitude - r;
        }

        private static float Poly(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i];
                Vector2 w = p - v[i];
                float denom = Vector2.Dot(e, e);
                float t = denom <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(w, e) / denom);
                Vector2 b = w - e * t;
                d = Mathf.Min(d, Vector2.Dot(b, b));

                bool c1 = p.y >= v[i].y;
                bool c2 = p.y < v[j].y;
                bool c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        /// <summary>Union.</summary>
        private static float U(float a, float b) { return Mathf.Min(a, b); }
        /// <summary>Subtract b from a.</summary>
        private static float Sub(float a, float b) { return Mathf.Max(a, -b); }
        /// <summary>Intersect.</summary>
        private static float I(float a, float b) { return Mathf.Max(a, b); }

        // ============================================================
        // POLYGON TABLES
        // ============================================================

        private static readonly Vector2[] StarPts = BuildStar(5, 0.5f, 0.54f, 0.46f, 0.21f);
        private static readonly Vector2[] GemPts =
        {
            new Vector2(0.28f, 0.84f), new Vector2(0.72f, 0.84f), new Vector2(0.94f, 0.60f),
            new Vector2(0.50f, 0.10f), new Vector2(0.06f, 0.60f)
        };
        private static readonly Vector2[] CupPts =
        {
            new Vector2(0.30f, 0.90f), new Vector2(0.70f, 0.90f),
            new Vector2(0.65f, 0.56f), new Vector2(0.35f, 0.56f)
        };
        private static readonly Vector2[] ShieldPts =
        {
            new Vector2(0.50f, 0.94f), new Vector2(0.88f, 0.76f), new Vector2(0.88f, 0.44f),
            new Vector2(0.50f, 0.08f), new Vector2(0.12f, 0.44f), new Vector2(0.12f, 0.76f)
        };
        private static readonly Vector2[] BoltPts =
        {
            new Vector2(0.60f, 0.94f), new Vector2(0.24f, 0.46f), new Vector2(0.45f, 0.46f),
            new Vector2(0.40f, 0.06f), new Vector2(0.76f, 0.54f), new Vector2(0.55f, 0.54f)
        };
        private static readonly Vector2[] FirePts =
        {
            new Vector2(0.50f, 0.94f), new Vector2(0.74f, 0.66f), new Vector2(0.80f, 0.40f),
            new Vector2(0.68f, 0.16f), new Vector2(0.50f, 0.08f), new Vector2(0.32f, 0.16f),
            new Vector2(0.20f, 0.40f), new Vector2(0.28f, 0.62f), new Vector2(0.36f, 0.50f),
            new Vector2(0.40f, 0.70f)
        };
        private static readonly Vector2[] CrownPts =
        {
            new Vector2(0.10f, 0.30f), new Vector2(0.90f, 0.30f), new Vector2(0.90f, 0.80f),
            new Vector2(0.72f, 0.58f), new Vector2(0.50f, 0.86f), new Vector2(0.28f, 0.58f),
            new Vector2(0.10f, 0.80f)
        };
        private static readonly Vector2[] PlayPts =
        {
            new Vector2(0.24f, 0.90f), new Vector2(0.86f, 0.50f), new Vector2(0.24f, 0.10f)
        };
        private static readonly Vector2[] WarnPts =
        {
            new Vector2(0.50f, 0.92f), new Vector2(0.94f, 0.12f), new Vector2(0.06f, 0.12f)
        };
        private static readonly Vector2[] ShoePts =
        {
            new Vector2(0.08f, 0.26f), new Vector2(0.08f, 0.50f), new Vector2(0.30f, 0.56f),
            new Vector2(0.50f, 0.46f), new Vector2(0.74f, 0.42f), new Vector2(0.92f, 0.34f),
            new Vector2(0.92f, 0.26f)
        };
        // Pointed-top almond. The previous six-point outline was near-symmetric top and bottom and
        // read as a shield, which is exactly what the Shield icon already looks like.
        private static readonly Vector2[] LeafPts =
        {
            new Vector2(0.50f, 0.95f), new Vector2(0.68f, 0.72f), new Vector2(0.75f, 0.46f),
            new Vector2(0.63f, 0.25f), new Vector2(0.50f, 0.19f), new Vector2(0.37f, 0.25f),
            new Vector2(0.25f, 0.46f), new Vector2(0.32f, 0.72f)
        };
        private static readonly Vector2[] DunePts =
        {
            new Vector2(0.06f, 0.22f), new Vector2(0.94f, 0.22f), new Vector2(0.94f, 0.46f),
            new Vector2(0.74f, 0.66f), new Vector2(0.54f, 0.44f), new Vector2(0.34f, 0.74f),
            new Vector2(0.06f, 0.44f)
        };
        private static readonly Vector2[] TentPts =
        {
            new Vector2(0.50f, 0.92f), new Vector2(0.92f, 0.14f), new Vector2(0.08f, 0.14f)
        };
        private static readonly Vector2[] HomePts =
        {
            new Vector2(0.50f, 0.94f), new Vector2(0.94f, 0.54f), new Vector2(0.78f, 0.54f),
            new Vector2(0.78f, 0.10f), new Vector2(0.22f, 0.10f), new Vector2(0.22f, 0.54f),
            new Vector2(0.06f, 0.54f)
        };

        /// <summary>8-tooth cog outline, reusing BuildStar's alternating-radius polygon with shallow
        /// teeth (0.40/0.32) rather than a star's sharp points -- Sdf(Settings) cuts a center hole
        /// out of it to read as a gear rather than a spiky badge.</summary>
        private static readonly Vector2[] CogPts = BuildStar(8, 0.5f, 0.5f, 0.40f, 0.32f);

        private static Vector2[] BuildStar(int points, float cx, float cy, float outer, float inner)
        {
            Vector2[] pts = new Vector2[points * 2];
            for (int i = 0; i < points * 2; i++)
            {
                float r = (i % 2 == 0) ? outer : inner;
                float ang = Mathf.PI * 0.5f + i * Mathf.PI / points;
                pts[i] = new Vector2(cx + Mathf.Cos(ang) * r, cy + Mathf.Sin(ang) * r);
            }
            return pts;
        }

        // ============================================================
        // ICON DEFINITIONS
        // ============================================================

        private static float Sdf(IconId id, Vector2 p)
        {
            switch (id)
            {
                case IconId.Coin:
                    // Disc with an engraved inner ring so it reads as a coin, not a dot.
                    return Sub(Disc(p, 0.5f, 0.5f, 0.42f), Ring(p, 0.5f, 0.5f, 0.28f, 0.05f));

                case IconId.Gem:
                    return Sub(Poly(p, GemPts), Seg(p, 0.28f, 0.84f, 0.50f, 0.55f, 0.018f));

                case IconId.Trophy:
                {
                    float cup = Poly(p, CupPts);
                    float lHandle = Sub(Ring(p, 0.28f, 0.78f, 0.11f, 0.05f), RBox(p, 0.40f, 0.78f, 0.14f, 0.20f, 0f));
                    float rHandle = Sub(Ring(p, 0.72f, 0.78f, 0.11f, 0.05f), RBox(p, 0.60f, 0.78f, 0.14f, 0.20f, 0f));
                    float stem = RBox(p, 0.5f, 0.44f, 0.055f, 0.10f, 0.01f);
                    float footBar = RBox(p, 0.5f, 0.30f, 0.13f, 0.045f, 0.02f);
                    float plinth = RBox(p, 0.5f, 0.19f, 0.22f, 0.07f, 0.025f);
                    return U(U(U(cup, U(lHandle, rHandle)), stem), U(footBar, plinth));
                }

                case IconId.Star:
                    return Poly(p, StarPts);

                case IconId.Crown:
                    return U(Poly(p, CrownPts), RBox(p, 0.5f, 0.22f, 0.40f, 0.07f, 0.025f));

                case IconId.Lock:
                {
                    float body = RBox(p, 0.5f, 0.32f, 0.30f, 0.24f, 0.07f);
                    float shackle = Sub(Ring(p, 0.5f, 0.62f, 0.19f, 0.075f), RBox(p, 0.5f, 0.40f, 0.30f, 0.25f, 0f));
                    float keyhole = Disc(p, 0.5f, 0.34f, 0.065f);
                    return Sub(U(body, shackle), keyhole);
                }

                case IconId.Check:
                    return U(Seg(p, 0.16f, 0.52f, 0.40f, 0.24f, 0.085f),
                             Seg(p, 0.40f, 0.24f, 0.86f, 0.76f, 0.085f));

                case IconId.Cross:
                    return U(Seg(p, 0.22f, 0.78f, 0.78f, 0.22f, 0.085f),
                             Seg(p, 0.22f, 0.22f, 0.78f, 0.78f, 0.085f));

                case IconId.Plus:
                    return U(Seg(p, 0.5f, 0.20f, 0.5f, 0.80f, 0.085f),
                             Seg(p, 0.20f, 0.5f, 0.80f, 0.5f, 0.085f));

                case IconId.Dot:
                    return Disc(p, 0.5f, 0.5f, 0.24f);

                case IconId.Copy:
                {
                    float back = Sub(RBox(p, 0.40f, 0.60f, 0.26f, 0.28f, 0.05f), RBox(p, 0.40f, 0.60f, 0.20f, 0.22f, 0.03f));
                    float front = Sub(RBox(p, 0.60f, 0.40f, 0.26f, 0.28f, 0.05f), RBox(p, 0.60f, 0.40f, 0.20f, 0.22f, 0.03f));
                    return U(back, front);
                }

                case IconId.ChevronLeft:
                    return U(Seg(p, 0.64f, 0.86f, 0.34f, 0.50f, 0.095f),
                             Seg(p, 0.34f, 0.50f, 0.64f, 0.14f, 0.095f));

                case IconId.ChevronRight:
                    return U(Seg(p, 0.36f, 0.86f, 0.66f, 0.50f, 0.095f),
                             Seg(p, 0.66f, 0.50f, 0.36f, 0.14f, 0.095f));

                case IconId.Play:
                    return Poly(p, PlayPts);

                case IconId.Pause:
                    return U(RBox(p, 0.34f, 0.5f, 0.095f, 0.36f, 0.04f),
                             RBox(p, 0.66f, 0.5f, 0.095f, 0.36f, 0.04f));

                case IconId.Refresh:
                {
                    // Ring with a bite taken out of the top-right, plus an arrowhead on the open end.
                    float ring = Ring(p, 0.5f, 0.5f, 0.32f, 0.11f);
                    float bite = Poly(p, new[] { new Vector2(0.5f, 0.5f), new Vector2(1.1f, 0.5f), new Vector2(1.1f, 1.1f), new Vector2(0.5f, 1.1f) });
                    float head = Poly(p, new[] { new Vector2(0.52f, 0.62f), new Vector2(0.96f, 0.62f), new Vector2(0.74f, 0.96f) });
                    return U(Sub(ring, bite), head);
                }

                case IconId.Paddle:
                {
                    // A pickleball paddle is a rounded rectangle, not an oval. The previous blade
                    // used a corner radius almost equal to its half-extent, which rounded it into a
                    // circle — on screen it read as a lightbulb or a balloon on a stick.
                    float blade = RBox(p, 0.5f, 0.63f, 0.26f, 0.29f, 0.11f);
                    float neck = RBox(p, 0.5f, 0.33f, 0.10f, 0.09f, 0.02f);
                    float grip = RBox(p, 0.5f, 0.17f, 0.085f, 0.13f, 0.05f);
                    float cap = RBox(p, 0.5f, 0.07f, 0.11f, 0.035f, 0.02f);
                    return U(U(blade, neck), U(grip, cap));
                }

                case IconId.Shoe:
                {
                    float upper = Poly(p, ShoePts);
                    float sole = RBox(p, 0.5f, 0.20f, 0.42f, 0.055f, 0.03f);
                    return U(upper, sole);
                }

                case IconId.Tape:
                {
                    // A roll of grip tape: rounded body with a spool hole and two wrap lines. The
                    // previous version was a box with two diagonal cuts through it, which read as a
                    // "no entry" slash rather than as an object.
                    float body = RBox(p, 0.5f, 0.5f, 0.33f, 0.27f, 0.10f);
                    float hole = Disc(p, 0.5f, 0.5f, 0.10f);
                    float w1 = Seg(p, 0.26f, 0.24f, 0.40f, 0.76f, 0.026f);
                    float w2 = Seg(p, 0.60f, 0.24f, 0.74f, 0.76f, 0.026f);
                    return Sub(body, U(hole, U(w1, w2)));
                }

                case IconId.Ball:
                {
                    // Pickleball: a disc perforated with holes.
                    float ball = Disc(p, 0.5f, 0.5f, 0.42f);
                    float holes = Disc(p, 0.5f, 0.5f, 0.085f);
                    holes = U(holes, Disc(p, 0.5f, 0.75f, 0.075f));
                    holes = U(holes, Disc(p, 0.5f, 0.25f, 0.075f));
                    holes = U(holes, Disc(p, 0.25f, 0.5f, 0.075f));
                    holes = U(holes, Disc(p, 0.75f, 0.5f, 0.075f));
                    return Sub(ball, holes);
                }

                case IconId.Chest:
                {
                    // Domed lid over a square body. Every icon here is a single-colour silhouette,
                    // so interior detail has to be CUT OUT — the strap and latch were previously
                    // unioned into the shape, where they simply merged with the fill and left a
                    // featureless arch. (An earlier version also assigned `lid` twice and threw the
                    // first shape away.)
                    float lid = I(Disc(p, 0.5f, 0.44f, 0.36f), RBox(p, 0.5f, 0.60f, 0.36f, 0.16f, 0f));
                    float body = RBox(p, 0.5f, 0.28f, 0.36f, 0.16f, 0.03f);
                    float shell = U(lid, body);

                    float seam = Seg(p, 0.16f, 0.44f, 0.84f, 0.44f, 0.020f);
                    float strapL = Seg(p, 0.42f, 0.12f, 0.42f, 0.74f, 0.016f);
                    float strapR = Seg(p, 0.58f, 0.12f, 0.58f, 0.74f, 0.016f);
                    float keyhole = Disc(p, 0.5f, 0.38f, 0.052f);

                    return Sub(shell, U(U(seam, keyhole), U(strapL, strapR)));
                }

                case IconId.Shield:
                    return Poly(p, ShieldPts);

                case IconId.Player:
                {
                    float head = Disc(p, 0.5f, 0.76f, 0.17f);
                    float torso = Sub(RBox(p, 0.5f, 0.30f, 0.28f, 0.22f, 0.16f), RBox(p, 0.5f, 0.02f, 0.40f, 0.06f, 0f));
                    return U(head, torso);
                }

                case IconId.Bag:
                {
                    float body = RBox(p, 0.5f, 0.34f, 0.34f, 0.28f, 0.07f);
                    float handle = Sub(Ring(p, 0.5f, 0.62f, 0.17f, 0.06f), RBox(p, 0.5f, 0.44f, 0.30f, 0.20f, 0f));
                    return U(body, handle);
                }

                case IconId.Home:
                    return Poly(p, HomePts);

                case IconId.Settings:
                    return Sub(Poly(p, CogPts), Disc(p, 0.5f, 0.5f, 0.15f));

                case IconId.Bolt:
                    return Poly(p, BoltPts);

                case IconId.Fire:
                    return Poly(p, FirePts);

                case IconId.Warn:
                {
                    float tri = Poly(p, WarnPts);
                    float bar = RBox(p, 0.5f, 0.50f, 0.055f, 0.16f, 0.02f);
                    float dot = Disc(p, 0.5f, 0.27f, 0.065f);
                    return Sub(tri, U(bar, dot));
                }

                case IconId.ThumbUp:
                {
                    float fist = RBox(p, 0.56f, 0.34f, 0.30f, 0.22f, 0.07f);
                    float thumb = Seg(p, 0.34f, 0.52f, 0.52f, 0.86f, 0.11f);
                    float cuff = RBox(p, 0.18f, 0.30f, 0.11f, 0.20f, 0.045f);
                    return U(U(fist, thumb), cuff);
                }

                case IconId.Sun:
                {
                    float core = Disc(p, 0.5f, 0.5f, 0.24f);
                    float rays = 1f;
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI * 0.25f;
                        float c = Mathf.Cos(a), s = Mathf.Sin(a);
                        rays = U(rays, Seg(p, 0.5f + c * 0.34f, 0.5f + s * 0.34f, 0.5f + c * 0.46f, 0.5f + s * 0.46f, 0.05f));
                    }
                    return U(core, rays);
                }

                case IconId.Wave:
                {
                    float w = 1f;
                    for (int row = 0; row < 3; row++)
                    {
                        float y = 0.26f + row * 0.22f;
                        w = U(w, Seg(p, 0.10f, y, 0.30f, y + 0.09f, 0.048f));
                        w = U(w, Seg(p, 0.30f, y + 0.09f, 0.50f, y, 0.048f));
                        w = U(w, Seg(p, 0.50f, y, 0.70f, y + 0.09f, 0.048f));
                        w = U(w, Seg(p, 0.70f, y + 0.09f, 0.90f, y, 0.048f));
                    }
                    return w;
                }

                case IconId.Leaf:
                {
                    float blade = Poly(p, LeafPts);
                    float stem = Seg(p, 0.50f, 0.22f, 0.50f, 0.06f, 0.032f);
                    float vein = Seg(p, 0.50f, 0.86f, 0.50f, 0.24f, 0.016f);
                    return Sub(U(blade, stem), vein);
                }

                case IconId.Palm:
                {
                    float trunk = Seg(p, 0.50f, 0.10f, 0.56f, 0.58f, 0.055f);
                    float f1 = Seg(p, 0.56f, 0.60f, 0.16f, 0.72f, 0.05f);
                    float f2 = Seg(p, 0.56f, 0.60f, 0.90f, 0.74f, 0.05f);
                    float f3 = Seg(p, 0.56f, 0.60f, 0.32f, 0.92f, 0.05f);
                    float f4 = Seg(p, 0.56f, 0.60f, 0.80f, 0.92f, 0.05f);
                    return U(U(U(trunk, f1), U(f2, f3)), f4);
                }

                case IconId.Dune:
                    return Poly(p, DunePts);

                case IconId.Tent:
                {
                    float body = Poly(p, TentPts);
                    float door = Poly(p, new[] { new Vector2(0.50f, 0.60f), new Vector2(0.66f, 0.14f), new Vector2(0.34f, 0.14f) });
                    return Sub(body, door);
                }

                case IconId.Gift:
                {
                    // Box, lid, and a bow sitting on top of it. Like every icon here this is one
                    // silhouette, so the ribbon has to be CUT OUT rather than unioned in — over the
                    // lobby's dark plinth the gaps read as the board's black ribbon crossing gold.
                    float box = RBox(p, 0.5f, 0.28f, 0.34f, 0.21f, 0.045f);
                    float lid = RBox(p, 0.5f, 0.575f, 0.39f, 0.095f, 0.035f);
                    // Loops tucked close to the knot and low onto the lid. Set wider and higher they
                    // separate into two circles with holes in them, which on a box reads as a face.
                    float knot = RBox(p, 0.5f, 0.695f, 0.055f, 0.06f, 0.025f);
                    float loopL = Disc(p, 0.355f, 0.745f, 0.115f);
                    float loopR = Disc(p, 0.645f, 0.745f, 0.115f);
                    float shell = U(U(box, lid), U(knot, U(loopL, loopR)));

                    // The lid overhangs the box by a hair and the seam is cut along the overlap, so
                    // the lid reads as a separate piece rather than a stripe painted on one slab.
                    float seam = Seg(p, 0.08f, 0.482f, 0.92f, 0.482f, 0.018f);
                    float ribbonL = Seg(p, 0.415f, 0.06f, 0.415f, 0.66f, 0.016f);
                    float ribbonR = Seg(p, 0.585f, 0.06f, 0.585f, 0.66f, 0.016f);
                    float holeL = Disc(p, 0.335f, 0.752f, 0.036f);
                    float holeR = Disc(p, 0.665f, 0.752f, 0.036f);

                    return Sub(shell, U(U(seam, U(ribbonL, ribbonR)), U(holeL, holeR)));
                }

                case IconId.PickleSlice:
                {
                    // Rind, flesh and seeds cut from one disc.
                    //
                    // The seeds are a tight cluster of seven rather than the four-around-one that
                    // would fall out naturally, because IconId.Ball is a disc with exactly that
                    // arrangement of holes and the splash puts the two within a few hundred pixels
                    // of each other. A dense centre cluster inside a wide rind is the thing that
                    // stops the background reading as a field of loose pickleballs.
                    float slice = Disc(p, 0.5f, 0.5f, 0.46f);
                    float rindGap = Ring(p, 0.5f, 0.5f, 0.355f, 0.045f);
                    float seeds = 1f;
                    for (int i = 0; i < 7; i++)
                    {
                        float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 7f;
                        seeds = U(seeds, Disc(p, 0.5f + Mathf.Cos(a) * 0.155f, 0.5f + Mathf.Sin(a) * 0.155f, 0.052f));
                    }
                    return Sub(slice, U(rindGap, seeds));
                }

                default:
                    return 1f;
            }
        }
    }
}
