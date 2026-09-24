using UnityEngine;
using UnityEngine.UI;

namespace Pickleball.UI
{
    /// <summary>Keep sliced pill end caps circular at every rendered height.</summary>
    [RequireComponent(typeof(Image))]
    public class UICapsuleFit : MonoBehaviour
    {
        private Image image;
        private float lastHeight = -1f;

        private void LateUpdate()
        {
            if (image == null) image = GetComponent<Image>();
            if (image.sprite == null || image.type != Image.Type.Sliced) return;
            float height = image.rectTransform.rect.height;
            if (height <= 0f || Mathf.Approximately(height, lastHeight)) return;
            lastHeight = height;
            float canvasPixels = image.canvas != null ? image.canvas.referencePixelsPerUnit : 100f;
            float units = image.sprite.pixelsPerUnit / canvasPixels;
            image.pixelsPerUnitMultiplier = image.sprite.rect.height / (height * units);
        }
    }
}
