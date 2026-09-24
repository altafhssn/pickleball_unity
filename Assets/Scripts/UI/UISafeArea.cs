using UnityEngine;

namespace Pickleball.UI
{
    /// <summary>
    /// Insets a RectTransform to Screen.safeArea so nothing lands under a notch, a punch-hole
    /// camera or the home indicator.
    ///
    /// UITheme.HeaderTopOffset always carried a comment saying the safe-area inset should be added
    /// on top of it, but nothing ever applied one — every screen's header sat at a fixed 96px from
    /// the physical top edge. This component is what makes that comment true: every screen is
    /// parented under one of these, so the header offset is measured from the safe area rather
    /// than the panel edge.
    ///
    /// Anchors are recomputed only when the safe area or orientation actually changes, so this
    /// costs a couple of float compares per frame.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UISafeArea : MonoBehaviour
    {
        /// <summary>Set false on full-bleed backdrops that should paint edge to edge.</summary>
        public bool applyTop = true;
        public bool applyBottom = true;

        private RectTransform rt;
        private Rect lastSafeArea = new Rect(0, 0, 0, 0);
        private ScreenOrientation lastOrientation;
        private Vector2Int lastResolution;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != lastSafeArea ||
                Screen.orientation != lastOrientation ||
                Screen.width != lastResolution.x ||
                Screen.height != lastResolution.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (rt == null) rt = GetComponent<RectTransform>();

            lastSafeArea = Screen.safeArea;
            lastOrientation = Screen.orientation;
            lastResolution = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0) return;

            Rect area = lastSafeArea;
            Vector2 min = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            Vector2 max = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);

            // Guard against a degenerate safe area (some editor/device combos report zero).
            if (float.IsNaN(min.x) || float.IsNaN(min.y) || float.IsNaN(max.x) || float.IsNaN(max.y)) return;
            if (max.x - min.x <= 0f || max.y - min.y <= 0f) return;

            if (!applyBottom) min.y = 0f;
            if (!applyTop) max.y = 1f;

            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
