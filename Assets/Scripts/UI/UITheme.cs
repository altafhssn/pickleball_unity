using UnityEngine;
using Pickleball.Gameplay;

namespace Pickleball.UI
{
    /// <summary>
    /// PICKLE SMASH — the single source of truth for UI color, type and game-feel values.
    ///
    /// Art direction (Figma boards in Docs/Figma): an orchid-to-blush gradient backdrop, near-black
    /// panels that recede into it, and one electric VOLT accent that means "this is the thing to
    /// tap". Every shape is built from the same three-part recipe — BASE (dark shade, full size)
    /// + FACE (inset, top-aligned, shorter) + SHEEN (hugs the face's top edge) — over a hard black
    /// outline. Colour carries intent, never decoration:
    ///
    ///   VOLT      forward / confirm / primary       (LETS PLAY, RESUME, active nav tab)
    ///   BLAZE     destructive + urgency             (quit, close, CLAIM, unread badges)
    ///   BUBBLEGUM social / shop
    ///   COURT     tertiary (equip, avatar wells)
    ///   GOLD      currency and rewards only
    ///   PANEL     neutral utility, everything else
    ///
    /// All sizes are reference pixels at the 1080x1920 CanvasScaler reference resolution. The
    /// Figma boards are 390x844, so Figma px * <see cref="FigmaScale"/> = reference px.
    /// </summary>
    public static class UITheme
    {
        private static Color Hex(int r, int g, int b, float a = 1f)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        /// <summary>Figma board (390 wide) to canvas reference (1080 wide).</summary>
        public const float FigmaScale = 1080f / 390f;

        /// <summary>Converts a Figma-board measurement into reference px.</summary>
        public static float F(float figmaPx) { return figmaPx * FigmaScale; }

        // ============================================================
        // CORE PALETTE
        // ============================================================

        // ---------- Volt: the brand. One accent, used for one meaning. ----------
        public static readonly Color Volt       = Hex(215, 249,   0);  // #D7F900 face
        public static readonly Color VoltBright = Hex(227, 249,  70);  // #E3F946 lit face / sheen
        public static readonly Color VoltDeep   = Hex(107, 122,   0);  // #6B7A00 base under volt
        public static readonly Color VoltShade  = Hex( 62,  92,   0);  // #3E5C00 deepest volt
        public static readonly Color VoltDim    = Hex(133, 146,  50);  // #859232 disabled volt

        // ---------- Ink and panel: the near-black the kit outlines and recedes into ----------
        public static readonly Color Ink0       = Hex(  5,   7,   9);  // #050709 base / deepest
        public static readonly Color Ink1       = Hex( 11,  15,  20);  // #0B0F14 outline, text on volt
        public static readonly Color Panel      = Hex( 18,  23,  28);  // #12171C panel face
        public static readonly Color PanelSoft  = Hex( 26,  32,  39);  // #1A2027 raised / locked face
        public static readonly Color PanelLift  = Hex( 21,  36,  49);  // #152431 in-match plate face
        public static readonly Color Outline    = Color.black;         // hard black, always

        // ---------- Cream: every piece of text that is not sitting on volt ----------
        public static readonly Color Cream      = Hex(255, 249, 234);  // #FFF9EA

        // ---------- Intent colours ----------
        public static readonly Color Blaze      = Hex(255,  75,  38);  // #FF4B26 destructive / claim
        public static readonly Color BlazeDeep  = Hex(122,  28,  10);  // #7A1C0A
        public static readonly Color Bubblegum  = Hex(255, 111, 168);  // #FF6FA8 social / shop
        public static readonly Color BubbleDeep = Hex(168,  53, 100);  // #A83564
        public static readonly Color Court      = Hex( 15,  76,  92);  // #0F4C5C tertiary
        public static readonly Color CourtWell  = Hex( 10,  53,  64);  // #0A3540 avatar well
        public static readonly Color Gold       = Hex(247, 201,  72);  // #F7C948 currency only
        public static readonly Color GoldShade  = Hex(138, 106,   0);  // #8A6A00
        public static readonly Color Grass      = Hex(111, 207,  90);  // #6FCF5A the "+" affordance
        public static readonly Color GrassDeep  = Hex( 62, 122,  46);  // #3E7A2E
        public static readonly Color Crimson    = Hex(212,  61,  79);  // #D43D4F quit-match text

        // ---------- Backdrop ----------
        /// <summary>Meta screens: orchid at the top, blush at the bottom.</summary>
        public static readonly Color BgOrchid   = Hex(207, 135, 219);  // #CF87DB
        public static readonly Color BgBlush    = Hex(226, 172, 168);  // #E2ACA8
        /// <summary>Screens with 3D behind them (lobby, match) sit on the deeper violet pair.</summary>
        public static readonly Color BgViolet   = Hex(157,  95, 223);  // #9D5FDF

        // ---------- In-match chrome ----------
        // Anything drawn over live 3D uses this cooler, bluer family rather than the meta screens'
        // flat near-black: over a bright green court the warm panel read as a grey sticker, and the
        // HUD plates have to sit on the play field without becoming the thing you look at.
        /// <summary>Score-plate fill. Semi-transparent so the court still shows through.</summary>
        public static readonly Color PlateFill  = Hex( 21,  36,  49, 0.74f);  // #152431 @ 74%
        /// <summary>Pause / result card over the match.</summary>
        public static readonly Color NightCard  = Hex( 10,  15,  38);  // #0A0F26
        /// <summary>Inset row inside a night card — the paused score line.</summary>
        public static readonly Color NightRow   = Hex( 20,  27,  61);  // #141B3D
        /// <summary>Hairline rule inside a night card.</summary>
        public static readonly Color NightRule  = Hex( 30,  41,  59);  // #1E293B
        /// <summary>Secondary copy on a night card — elapsed time, the score dash, the loser's name.</summary>
        public static readonly Color Slate      = Hex(100, 116, 139);  // #64748B
        /// <summary>Volt as TYPE rather than as a fill: a hair brighter so it holds up at small sizes.</summary>
        public static readonly Color VoltInk    = Hex(223, 255,  25);  // #DFFF19

        // ---------- Sheen opacities, per the component sheet ----------
        public const float SheenVolt  = 0.40f;   // volt / grass faces
        public const float SheenBlaze = 0.30f;   // blaze / bubblegum faces
        public const float SheenCourt = 0.20f;   // court blue faces
        public const float SheenDark  = 0.07f;   // dark utility faces still read as pressable
        /// <summary>Fraction of a face's height the sheen band occupies.</summary>
        public const float SheenHeight = 0.30f;

        // ============================================================
        // COMPATIBILITY ALIASES
        // The kit's 20 screens were authored against the previous stadium-blue direction. Rather
        // than rename every call site in one pass, each old token is repointed at its Pickle Smash
        // equivalent, so a screen that has not been reworked yet still lands inside the new palette
        // instead of rendering in the retired blues.
        // ============================================================
        public static readonly Color SkyTop     = BgOrchid;
        public static readonly Color SkyMid     = BgBlush;
        public static readonly Color ApronGreen = Volt;
        public static readonly Color ApronDeep  = VoltDeep;
        public static readonly Color CourtBlue  = Court;
        public static readonly Color CourtDeep  = CourtWell;

        public static readonly Color BgDeep     = BgOrchid;
        public static readonly Color BgMid      = BgBlush;
        public static readonly Color BgFloor    = BgViolet;
        public static readonly Color BgGlow     = Hex(215, 249, 0, 0.35f);

        public static readonly Color GlowGold   = Hex(247, 201,  72, 0.70f);
        public static readonly Color GlowCyan   = Hex(215, 249,   0, 0.65f);
        public static readonly Color GlowBlue   = Hex( 15,  76,  92, 0.55f);

        public static readonly Color ShimmerWhite = new Color(1f, 1f, 1f, 0.32f);
        public static readonly Color ShimmerTop   = new Color(1f, 1f, 1f, 0.28f);
        public static readonly Color ShimmerBot   = new Color(1f, 1f, 1f, 0.00f);

        /// <summary>Cards do not float on this direction — the black outline carries the elevation,
        /// so the "shadow" is a hard black offset copy rather than a soft blur.</summary>
        public static readonly Color ShadowColor  = new Color(0f, 0f, 0f, 0.55f);

        public static readonly Color AvatarRingGold = Volt;

        public static readonly Color FillPlayerBlue     = Court;
        public static readonly Color FillPlayerBlueDeep = CourtWell;
        public static readonly Color FillRivalRed       = Blaze;
        public static readonly Color FillRivalRedDeep   = BlazeDeep;
        /// <summary>Primary action for the whole kit. Volt, not green — see the class summary.</summary>
        public static readonly Color FillActionGreen     = Volt;
        public static readonly Color FillActionGreenDeep = VoltDeep;
        public static readonly Color FillActionGreenInk  = Ink1;
        public static readonly Color FillRivalRedBtnTop  = Blaze;
        public static readonly Color FillRivalRedBtnDeep = BlazeDeep;

        public static readonly Color GoldTop  = Hex(255, 226, 150);
        public static readonly Color GoldMid  = Gold;
        public static readonly Color GoldDeep = GoldShade;
        public static readonly Color GoldInk  = Hex(74, 56, 0);

        public static readonly Color PassTop  = Bubblegum;
        public static readonly Color PassDeep = BubbleDeep;
        public static readonly Color PassGlow = Hex(255, 111, 168, 0.65f);

        public static readonly Color PanelTop = Panel;
        public static readonly Color PanelDeep= Ink0;
        public static readonly Color PanelDarkTop  = Panel;
        public static readonly Color PanelDarkDeep = Ink0;

        /// <summary>Every shape in this kit is outlined in hard black, not navy.</summary>
        public static readonly Color OutlineNavy = Color.black;

        public static readonly Color RarityCommon = Hex(159, 182, 204);
        public static readonly Color RarityRare   = Hex( 79, 184, 201);   // court cyan
        public static readonly Color RarityEpic   = Bubblegum;
        public static readonly Color RarityLegend = Volt;

        public static readonly Color LeagueBronze   = Hex(203, 138, 46);
        public static readonly Color LeagueSilver   = Hex(199, 208, 220);
        public static readonly Color LeagueGold     = Gold;
        public static readonly Color LeaguePlatinum = Hex(111, 211, 216);
        public static readonly Color LeagueDiamond  = Hex(127, 182, 255);
        public static readonly Color LeagueChampion = Blaze;

        public static readonly Color TextPlayerBlue = Volt;
        public static readonly Color TextRivalRed   = Hex(255, 167, 158);

        // ---------- Neutrals ----------
        public static readonly Color Ink     = Cream;
        public static readonly Color InkMute = new Color(1f, 0.976f, 0.918f, 0.72f);
        public static readonly Color InkDim  = new Color(1f, 0.976f, 0.918f, 0.5f);

        /// <summary>Ink for copy sitting directly on the orchid/blush backdrop. Cream reads at
        /// roughly 2:1 on #CF87DB — fine for a display face carrying a black outline, not for body
        /// copy, which uses these instead.</summary>
        public static readonly Color InkOnLight     = Ink1;
        public static readonly Color InkOnLightMute = new Color(11 / 255f, 15 / 255f, 20 / 255f, 0.72f);
        public static readonly Color InkOnLightDim  = new Color(11 / 255f, 15 / 255f, 20 / 255f, 0.55f);
        public static readonly Color InkOnLightGold = Hex(122, 28, 10);

        public static readonly Color TickFilled = Volt;

        /// <summary>Full-screen scrim behind pause and result modals.</summary>
        public static readonly Color ModalScrim = new Color(0f, 0f, 0f, 0.62f);

        /// <summary>The black stroke on every display-face label. Distance scales with font size.</summary>
        public static readonly Color OutlineText = Color.black;

        /// <summary>Stroke distance in reference px for a given display font size.</summary>
        public static float OutlineDistanceFor(int fontSize)
        {
            if (fontSize >= 112) return 5f;
            if (fontSize >= 60) return 4f;
            if (fontSize >= 34) return 3f;
            return 2f;
        }

        // ============================================================
        // TYPE SCALE (reference px)
        // ============================================================
        public const int TypeResultTitle = 150;   // VICTORY / DEFEAT
        public const int TypeCallout     = 112;   // PERFECT! / GREAT!
        public const int TypeLogo        = 132;   // boot wordmark
        public const int TypeScoreDigit  = 66;    // HUD plate score
        public const int TypeStatValue   = 64;
        public const int TypeScreenTitle = 44;    // top-bar titles: SETTINGS
        /// <summary>The centred volt headline on a full-screen moment — RANKED MATCH, MATCH FOUND,
        /// MATCH PAUSED, SEASON COMPLETE. These screens have one thing to say and the type says it.</summary>
        public const int TypeHeroTitle   = 92;
        public const int TypeButton      = 48;    // primary CTA label
        public const int TypeButtonSm    = 32;
        public const int TypePill        = 30;    // currency pill counter
        public const int TypeLabel       = 24;    // uppercase micro-labels
        public const int TypeMicro       = 20;

        // ============================================================
        // SPACE, SHAPE, DEPTH (reference px)
        // ============================================================
        public const int SpaceXs = 8, SpaceSm = 16, SpaceMd = 24, SpaceLg = 32, SpaceXl = 48, Space2Xl = 64;

        /// <summary>Screen side margin. 23 Figma px at 390 wide.</summary>
        public const int ScreenPad = 64;

        public const int RadiusChip = 24, RadiusButton = 26, RadiusPanel = 28;

        /// <summary>Outline weight. The component sheet draws 5-6px at 390 wide; at 1080 that is
        /// ~14-17, which reads as the chunky black keyline the whole direction hangs on.</summary>
        public const int StrokeOutline = 14;
        public const int StrokeOutlineThin = 11;

        /// <summary>How far the FACE is inset from the BASE on the sides and top. The BASE stays
        /// visible along the bottom edge — that visible sliver is what makes a control read as a
        /// physical key rather than a flat rectangle.</summary>
        public const int BevelButton = 9;
        /// <summary>Extra BASE left showing under the FACE's bottom edge.</summary>
        public const int FaceLift = 16;
        /// <summary>Button press-down travel. Equal to FaceLift so a press seats the face onto
        /// the base exactly.</summary>
        public const int PressTravel = 16;
        public const int BevelPanel = 12;
        /// <summary>Minimum interactive dimension: 44 Figma px, the mobile touch-target floor.</summary>
        public const int TouchMin = 122;

        // ============================================================
        // LAYOUT ANCHORS (measured off the Figma boards)
        // ============================================================
        /// <summary>Gap from the top of the SAFE AREA to the header row.</summary>
        public const int HeaderTopOffset = 133;

        /// <summary>Bottom nav bar: Figma 80px tall at 390 wide.</summary>
        public const int NavBarHeight = 222;
        /// <summary>Gap from the very bottom edge to the nav bar. Figma y=835 of 844.</summary>
        public const int NavBarBottomGap = 26;
        /// <summary>Gap between the primary CTA and the nav bar. Figma 69px.</summary>
        public const int CtaBottomGap = 190;
        public const int ButtonHeight = 194;       // Figma 70px
        public const int ButtonHeightGhost = 150;
        public const int ButtonHeightSmall = 118;
        /// <summary>Distance from the bottom edge to the top of the primary CTA on a nav screen.</summary>
        public const int CtaStackHeight = NavBarBottomGap + NavBarHeight + CtaBottomGap + ButtonHeight;
        public const int ContentTopInset = HeaderTopOffset + TouchMin + 8 + SpaceMd;

        public const int ScorePlateWidth = 440, ScorePlateHeight = 132;
        public const int ChipHeight = 80, PillTopOffset = 250;
        public const int CalloutY = 880;
        public const int ModalWidth = 940;
        public const int ThumbZoneTop = 1350;

        // ============================================================
        // GAME FEEL (seconds / degrees)
        // ============================================================
        public const float CalloutPopTime = 0.15f;
        public const float CalloutSettleTime = 0.20f;
        public const float CalloutLifeTime = 1.00f;
        public const float HitStopDuration = 0.12f;
        public const float HitStopTimeScale = 0.2f;
        public const float PunchZoomDegrees = 5f;
        public const float PunchZoomTime = 0.20f;
        public const float ShakeMin = 0.10f, ShakeMax = 0.40f, ShakeTime = 0.15f;
        public const float BounceShake = 0.05f, BounceShakeTime = 0.10f;
        public const float PointZoomDegrees = 8f, PointZoomTime = 0.50f;
        public const float ButtonPressTime = 0.09f;
        public const float ModalEnterTime = 0.30f;
        public const float ScreenEnterTime = 0.20f;

        // ============================================================
        // SHOT QUALITY CONTRACT
        // ============================================================
        public static Color QualityColor(ShotQuality quality)
        {
            switch (quality)
            {
                case ShotQuality.Perfect: return Volt;
                case ShotQuality.Great:   return Hex(79, 184, 201);   // court cyan
                case ShotQuality.Good:    return Grass;
                case ShotQuality.Weak:    return Gold;
                default:                  return Blaze;
            }
        }

        /// <summary>
        /// Callout copy. Positive states end in an exclamation mark, failure states do not —
        /// the punctuation carries the reward.
        /// </summary>
        public static string QualityLabel(ShotQuality quality)
        {
            switch (quality)
            {
                case ShotQuality.Perfect: return "PERFECT!";
                case ShotQuality.Great:   return "GREAT!";
                case ShotQuality.Good:    return "GOOD!";
                case ShotQuality.Weak:    return "WEAK";
                default:                  return "MISS";
            }
        }

        /// <summary>Tilt punch applied to the callout, in degrees. Perfect leans the other way.</summary>
        public static float QualityTilt(ShotQuality quality)
        {
            return quality == ShotQuality.Perfect ? 4f : -3f;
        }
    }
}
