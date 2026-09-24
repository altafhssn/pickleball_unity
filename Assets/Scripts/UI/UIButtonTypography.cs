using UnityEngine;
using UnityEngine.UI;

namespace Pickleball.UI
{
    /// <summary>Fixed type sizes for action labels. Layouts must fit the text, rather than quietly
    /// shrinking different buttons to different sizes through best-fit.</summary>
    [RequireComponent(typeof(Text))]
    public class UIButtonTypography : MonoBehaviour
    {
        private Text label;
        private int size;

        public static void Attach(Text text, bool compact = false)
        {
            var style = text.GetComponent<UIButtonTypography>();
            if (style == null) style = text.gameObject.AddComponent<UIButtonTypography>();
            style.label = text;
            style.size = compact ? UITheme.TypeButtonSm : UITheme.TypeButton;
            style.Apply();
        }

        private void LateUpdate() { Apply(); }
        private void Apply()
        {
            if (label == null || size == 0) return;
            label.fontSize = size;
            label.font = UIBuilder.DisplayFont;
            label.fontStyle = FontStyle.Normal;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }
}
