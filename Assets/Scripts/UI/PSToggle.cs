using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Pickleball.UI
{
    /// <summary>
    /// The settings toggle's behaviour. Built by <see cref="PSKit.Toggle"/>.
    ///
    /// Runs on unscaled time: settings is reachable from the in-match pause menu, where
    /// Time.timeScale is 0 and a scaled animation would leave the knob frozen half way across.
    /// </summary>
    public class PSToggle : MonoBehaviour, IPointerClickHandler
    {
        private RectTransform knob;
        private Image trackFill;
        private Image knobFill;
        private Action<bool> onChanged;
        private bool on;
        private float travel;
        private float inset;
        private float anim = 1f;

        public bool IsOn { get { return on; } }

        public void Bind(RectTransform knobRect, Image track, Image knobImage, bool initial,
            Action<bool> changed, float width, float knobSize)
        {
            knob = knobRect;
            trackFill = track;
            knobFill = knobImage;
            on = initial;
            onChanged = changed;
            // The knob is centre-pivoted and left-anchored, so its resting x is half its own width
            // plus the wall it sits inside.
            inset = 6f + knobSize * 0.5f;
            travel = width - knobSize - 12f;

            knob.anchoredPosition = new Vector2(inset + (on ? travel : 0f), 0f);
            Paint(on ? 1f : 0f);
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

            anim = Mathf.Clamp01(anim + Time.unscaledDeltaTime * 9f);
            float e = 1f - Mathf.Pow(1f - anim, 3f);
            float t = on ? e : 1f - e;

            if (knob != null) knob.anchoredPosition = new Vector2(inset + travel * t, 0f);
            Paint(t);
        }

        /// <summary>
        /// <paramref name="t"/> is 0 at OFF, 1 at ON. Track and knob cross-fade together so the
        /// control never sits in a state that reads as neither.
        /// </summary>
        private void Paint(float t)
        {
            if (trackFill != null) trackFill.color = Color.Lerp(UITheme.Panel, UITheme.Volt, t);
            // Dark knob on the lit track, pale knob on the dark one — the knob always contrasts
            // with whatever it is sitting on.
            if (knobFill != null) knobFill.color = Color.Lerp(new Color(1f, 0.976f, 0.918f, 0.55f), UITheme.VoltDim, t);
        }
    }
}
