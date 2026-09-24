using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Pickleball.UI
{
    /// <summary>
    /// A drag-and-tap slider drawn in the kit's style: dark inset track, gold fill, outlined knob.
    ///
    /// Settings previously cycled a value 25% per tap and then rebuilt the entire screen to show
    /// the new number — no drag, no knob, no affordance that the row was even interactive, and a
    /// full teardown of every widget on the screen per tap. This updates its own fill and knob in
    /// place and only calls back with the value.
    /// </summary>
    public class UISliderWidget : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private RectTransform trackRect;
        private RectTransform fillRect;
        private RectTransform knobRect;
        private Text valueLabel;

        private float value;
        private Action<float> onChanged;
        private Action<float> onReleased;

        /// <summary>Inset from each end of the track that the knob centre can reach, so the knob
        /// never hangs off the rounded cap at 0 or 1.</summary>
        private float knobInset;

        public float Value { get { return value; } }

        /// <summary>Builds a slider filling <paramref name="host"/>'s rect. The host must already
        /// be positioned and sized by the caller.</summary>
        public static UISliderWidget Build(GameObject host, float initial, Color fillColor, Action<float> onChanged, Action<float> onReleased = null)
        {
            float knobSize = 52f;

            // Track
            host.AddComponent<Image>();
            UIBuilder.ExpandHitArea(host, new Vector2(0f, 0f));
            Image trackFill = UIBuilder.OutlinedFill(host, UIBuilder.ChipSprite, UITheme.PanelDarkDeep, UITheme.OutlineNavy, 5f);

            GameObject fillGO = UIBuilder.Child("Fill", trackFill.transform);
            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite = UIBuilder.ChipSprite;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = fillColor;
            fillImg.raycastTarget = false;
            RectTransform fillRt = UIBuilder.StretchRect(fillGO, Vector2.zero, new Vector2(Mathf.Clamp01(initial), 1f), Vector2.zero, Vector2.zero);

            // Knob rides on top of the track, vertically centred, x driven in Apply().
            GameObject knobGO = UIBuilder.Child("Knob", host.transform);
            RectTransform knobRt = UIBuilder.Rect(knobGO, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(knobSize, knobSize));
            knobGO.AddComponent<Image>();
            UIBuilder.OutlinedGradientFill(knobGO, UIBuilder.CircleSprite, Color.white, new Color(0.82f, 0.88f, 0.95f), UITheme.OutlineNavy, 4f);

            UISliderWidget s = host.AddComponent<UISliderWidget>();
            s.trackRect = host.GetComponent<RectTransform>();
            s.fillRect = fillRt;
            s.knobRect = knobRt;
            s.value = Mathf.Clamp01(initial);
            s.onChanged = onChanged;
            s.onReleased = onReleased;
            s.knobInset = knobSize * 0.5f;
            s.Apply(s.value, false);
            return s;
        }

        /// <summary>Optional live read-out, updated on every value change.</summary>
        public void BindLabel(Text label)
        {
            valueLabel = label;
            UpdateLabel();
        }

        public void OnPointerDown(PointerEventData eventData) { SetFromPointer(eventData, false); }
        public void OnDrag(PointerEventData eventData) { SetFromPointer(eventData, false); }
        public void OnPointerUp(PointerEventData eventData) { SetFromPointer(eventData, true); }

        private void SetFromPointer(PointerEventData eventData, bool released)
        {
            if (trackRect == null) return;

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(trackRect, eventData.position, eventData.pressEventCamera, out local)) return;

            float width = trackRect.rect.width;
            if (width <= 0f) return;

            // local is pivot-relative; convert to 0..1 across the track, then discount the knob
            // inset at each end so dragging to the visual extremes reaches a true 0 and 1.
            float x = local.x - trackRect.rect.xMin;
            float usable = Mathf.Max(1f, width - knobInset * 2f);
            float v = Mathf.Clamp01((x - knobInset) / usable);

            Apply(v, true);
            if (released && onReleased != null) onReleased(v);
        }

        private void Apply(float v, bool notify)
        {
            value = Mathf.Clamp01(v);

            if (fillRect != null) fillRect.anchorMax = new Vector2(value, 1f);

            if (knobRect != null && trackRect != null)
            {
                float width = trackRect.rect.width;
                float usable = Mathf.Max(1f, width - knobInset * 2f);
                knobRect.anchoredPosition = new Vector2(knobInset + usable * value, 0f);
            }

            UpdateLabel();
            if (notify && onChanged != null) onChanged(value);
        }

        private void UpdateLabel()
        {
            if (valueLabel != null) valueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
        }

        /// <summary>Re-seats the knob after a layout pass — rect.width is 0 until the first one.</summary>
        private void Start() { Apply(value, false); }
    }

    /// <summary>
    /// An on/off pill that animates its knob across instead of being rebuilt in the new state.
    /// </summary>
    public class UIToggleWidget : MonoBehaviour, IPointerClickHandler
    {
        private RectTransform knobRect;
        private Image trackFill;
        private Image knobFill;
        private bool on;
        private Action<bool> onChanged;
        private float travel;
        private float anim = 1f;

        public bool IsOn { get { return on; } }

        public static UIToggleWidget Build(GameObject host, bool initial, Action<bool> onChanged)
        {
            float w = 126f, h = 66f, knob = 46f;

            host.AddComponent<Image>();
            Image track = UIBuilder.OutlinedFill(host, UIBuilder.ChipSprite, initial ? UITheme.FillActionGreenDeep : UITheme.PanelDarkDeep, UITheme.OutlineNavy, 5f);

            GameObject knobGO = UIBuilder.Child("Knob", host.transform);
            RectTransform knobRt = UIBuilder.Rect(knobGO, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(knob, knob));
            knobGO.AddComponent<Image>();
            Image knobImg = UIBuilder.OutlinedFill(knobGO, UIBuilder.CircleSprite, Color.white, UITheme.OutlineNavy, 3f);

            UIToggleWidget t = host.AddComponent<UIToggleWidget>();
            t.knobRect = knobRt;
            t.trackFill = track;
            t.knobFill = knobImg;
            t.on = initial;
            t.onChanged = onChanged;
            t.travel = w - knob - 16f;
            t.knobRect.anchoredPosition = new Vector2(initial ? 8f + t.travel : 8f, 0f);
            return t;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            on = !on;
            anim = 0f;
            if (onChanged != null) onChanged(on);
        }

        private void Update()
        {
            if (anim >= 1f) return;

            anim = Mathf.Clamp01(anim + Time.unscaledDeltaTime * 8f);
            float e = 1f - Mathf.Pow(1f - anim, 3f);

            float from = on ? 8f : 8f + travel;
            float to = on ? 8f + travel : 8f;
            if (knobRect != null) knobRect.anchoredPosition = new Vector2(Mathf.Lerp(from, to, e), 0f);
            if (trackFill != null) trackFill.color = Color.Lerp(on ? UITheme.PanelDarkDeep : UITheme.FillActionGreenDeep,
                                                               on ? UITheme.FillActionGreenDeep : UITheme.PanelDarkDeep, e);
            if (knobFill != null) knobFill.color = Color.Lerp(on ? new Color(0.45f, 0.57f, 0.69f) : Color.white,
                                                             on ? Color.white : new Color(0.45f, 0.57f, 0.69f), e);
        }
    }
}
