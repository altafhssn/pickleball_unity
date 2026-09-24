using UnityEngine;
using UnityEngine.UI;

namespace Pickleball.UI
{
    public enum GradientDirection
    {
        Vertical,
        Horizontal,
        DiagonalTL,
        DiagonalBL
    }

    /// <summary>
    /// Recolors a Graphic's mesh vertices into a gradient. Supports vertical, horizontal,
    /// and diagonal modes for glass shine and beveled AAA mobile card aesthetics.
    /// </summary>
    [AddComponentMenu("UI/Effects/Vertical Gradient")]
    public class UIGradient : BaseMeshEffect
    {
        public Color topColor = Color.white;
        public Color bottomColor = Color.black;
        public GradientDirection direction = GradientDirection.Vertical;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            UIVertex vertex = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                float x = vertex.position.x;
                float y = vertex.position.y;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            float rangeX = Mathf.Max(maxX - minX, 0.0001f);
            float rangeY = Mathf.Max(maxY - minY, 0.0001f);

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                float tx = (vertex.position.x - minX) / rangeX;
                float ty = (vertex.position.y - minY) / rangeY;

                float t;
                switch (direction)
                {
                    case GradientDirection.Horizontal:
                        t = tx;
                        break;
                    case GradientDirection.DiagonalTL:
                        t = (tx + (1f - ty)) * 0.5f;
                        break;
                    case GradientDirection.DiagonalBL:
                        t = (tx + ty) * 0.5f;
                        break;
                    case GradientDirection.Vertical:
                    default:
                        t = ty;
                        break;
                }

                Color32 tint = Color32.Lerp(bottomColor, topColor, Mathf.Clamp01(t));
                vertex.color = tint;
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
