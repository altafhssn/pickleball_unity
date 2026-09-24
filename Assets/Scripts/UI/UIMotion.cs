using UnityEngine;
using UnityEngine.UI;

namespace Pickleball.UI
{
    /// <summary>
    /// Screen and element entrance motion.
    ///
    /// Navigation used to be an instant Destroy + rebuild, which reads as a glitch rather than a
    /// transition. Every screen root now gets a <see cref="UIFadeIn"/> and key elements stagger in
    /// behind it, so moving between screens has a direction and a beat.
    ///
    /// All timing is unscaled: the pause overlay and the match-result modal both run while
    /// Time.timeScale is 0, and scaled time would freeze them mid-animation.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIFadeIn : MonoBehaviour
    {
        public float duration = 0.22f;
        public float delay = 0f;
        /// <summary>Pixels below the resting position to start from. Negative slides down instead.</summary>
        public float rise = 0f;
        /// <summary>Scale to start from. 1 disables the scale component.</summary>
        public float scaleFrom = 1f;

        private CanvasGroup group;
        private RectTransform rt;
        private Vector2 restPos;
        private Vector3 restScale;
        private float elapsed;
        private bool done;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();

            restPos = rt.anchoredPosition;
            restScale = rt.localScale;
            group.alpha = 0f;
            if (rise != 0f) rt.anchoredPosition = restPos - new Vector2(0f, rise);
            if (!Mathf.Approximately(scaleFrom, 1f)) rt.localScale = restScale * scaleFrom;
        }

        private void Update()
        {
            if (done) return;

            elapsed += Time.unscaledDeltaTime;
            float t = (elapsed - delay) / Mathf.Max(0.0001f, duration);

            if (t <= 0f) return;

            if (t >= 1f)
            {
                t = 1f;
                done = true;
            }

            // Ease-out cubic: fast off the mark, settles gently.
            float e = 1f - Mathf.Pow(1f - t, 3f);

            group.alpha = e;
            if (rise != 0f) rt.anchoredPosition = Vector2.LerpUnclamped(restPos - new Vector2(0f, rise), restPos, e);
            if (!Mathf.Approximately(scaleFrom, 1f)) rt.localScale = Vector3.LerpUnclamped(restScale * scaleFrom, restScale, e);

            if (done)
            {
                // Hand raycasts back to a plain state so the CanvasGroup isn't left intercepting.
                group.alpha = 1f;
                rt.anchoredPosition = restPos;
                rt.localScale = restScale;
            }
        }

        /// <summary>Attaches an entrance to <paramref name="go"/>, replacing any existing one.</summary>
        public static UIFadeIn Attach(GameObject go, float duration, float delay, float rise, float scaleFrom = 1f)
        {
            UIFadeIn existing = go.GetComponent<UIFadeIn>();
            if (existing != null) Destroy(existing);

            UIFadeIn f = go.AddComponent<UIFadeIn>();
            f.duration = duration;
            f.delay = delay;
            f.rise = rise;
            f.scaleFrom = scaleFrom;
            return f;
        }
    }

    /// <summary>
    /// Counts a Text up to a target value. Used for reward payouts so coins and trophies land as
    /// an event rather than as a number that was simply already there.
    /// </summary>
    public class UICountUp : MonoBehaviour
    {
        public Text target;
        public int from;
        public int to;
        public float duration = 0.6f;
        public string format = "N0";
        public string prefix = "";

        private float elapsed;

        private void Update()
        {
            if (target == null) { enabled = false; return; }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
            float e = 1f - Mathf.Pow(1f - t, 3f);

            int value = Mathf.RoundToInt(Mathf.Lerp(from, to, e));
            target.text = prefix + value.ToString(format);

            if (t >= 1f) enabled = false;
        }

        public static UICountUp Attach(Text text, int from, int to, float duration, string prefix = "")
        {
            UICountUp c = text.GetComponent<UICountUp>();
            if (c == null) c = text.gameObject.AddComponent<UICountUp>();
            c.elapsed = 0f;
            c.enabled = true;
            c.target = text;
            c.from = from;
            c.to = to;
            c.duration = duration;
            c.prefix = prefix;
            return c;
        }
    }
}
