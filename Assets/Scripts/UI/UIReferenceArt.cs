using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Pickleball.UI
{
    /// <summary>Individual illustrations exported from the supplied Figma SVGs.</summary>
    public static class UIReferenceArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Load(string name)
        {
            Sprite sprite;
            if (Cache.TryGetValue(name, out sprite)) return sprite;
            Texture2D texture = Resources.Load<Texture2D>("UIReference/" + name);
            if (texture == null) return null;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            Cache[name] = sprite;
            return sprite;
        }

        public static Image Draw(Transform parent, string name, bool preserveAspect = true)
        {
            GameObject go = UIBuilder.Child(name, parent);
            Image image = go.AddComponent<Image>();
            image.sprite = Load(name);
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            UIBuilder.Fill(go);
            return image;
        }

        public static void Backdrop(Transform parent, string name)
        {
            Image image = Draw(parent, name, false);
            AspectRatioFitter fit = image.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 390f / 844f;
        }

        public static void Title(Text text, string name, float width, float height)
        {
            if (Load(name) == null) return;
            // Retain the semantic label; only its visual is replaced by the original outlined art.
            text.enabled = false;
            Image image = Draw(text.transform, name);
            // The exported title is a white alpha mask. Honour the semantic Text colour so the
            // same art remains legible on both dark match scrims and the light meta backdrop.
            image.color = text.color;
            UIBuilder.Rect(image.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(UITheme.F(width), UITheme.F(height)));
        }
    }
}
