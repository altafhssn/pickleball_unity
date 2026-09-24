using UnityEngine;
using UnityEngine.UI;

namespace Pickleball.UI
{
    /// <summary>
    /// A matchmaking ring: swells outward and fades, then restarts. The three rings run the same
    /// loop at staggered phases so the group reads as a radar sweep rather than as three shapes
    /// breathing in unison.
    ///
    /// Unscaled time — matchmaking can be entered from a paused context, and the one thing this
    /// screen must never look is stopped.
    /// </summary>
    public class UIRingPulse : MonoBehaviour
    {
        public float period = 1.8f;
        public float phase;
        /// <summary>How far past its resting size the ring swells.</summary>
        public float swell = 0.14f;

        private Image image;
        private float baseAlpha;
        private float t;

        public static UIRingPulse Attach(GameObject go, float phase)
        {
            UIRingPulse p = go.AddComponent<UIRingPulse>();
            p.phase = phase;
            p.t = phase;
            return p;
        }

        private void Awake()
        {
            image = GetComponent<Image>();
            baseAlpha = image != null ? image.color.a : 1f;
            t = phase;
        }

        private void Update()
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Repeat(t, period) / period;

            // Ease-out on the swell, linear on the fade: the ring leaves quickly and dies slowly,
            // which is what makes it read as travelling outward rather than as a pulsing outline.
            float e = 1f - Mathf.Pow(1f - u, 2f);
            transform.localScale = Vector3.one * (1f + swell * e);

            if (image != null)
            {
                Color c = image.color;
                c.a = baseAlpha * (1f - u * 0.65f);
                image.color = c;
            }
        }
    }
}
