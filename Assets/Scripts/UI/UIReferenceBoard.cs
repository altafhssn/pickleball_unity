using UnityEngine;

namespace Pickleball.UI
{
    /// <summary>Fits the 390 by 844 composition inside the available safe area.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIReferenceBoard : MonoBehaviour
    {
        private Vector2 lastSize;

        private void OnEnable() { Fit(); }
        private void LateUpdate() { Fit(); }

        private void Fit()
        {
            RectTransform rect = (RectTransform)transform;
            RectTransform parent = rect.parent as RectTransform;
            if (parent == null) return;
            Vector2 available = parent.rect.size;
            if (available.x <= 0 || available.y <= 0 || available == lastSize) return;
            lastSize = available;
            Vector2 design = new Vector2(UITheme.F(390f), UITheme.F(844f));
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = design;
            float scale = Mathf.Min(available.x / design.x, available.y / design.y);
            rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
