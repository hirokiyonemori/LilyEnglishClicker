using UnityEngine;
using UnityEngine.UI;

namespace Idle
{
    /// <summary>Small grounded shadow and trailing dust, drawn without new image assets.</summary>
    public sealed class TravelFootfallGraphic : MaskableGraphic
    {
        float steps, weight;
        public void SetStride(float stepCount, float opacity)
        {
            steps = stepCount; weight = opacity; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Ellipse(vh, new Vector2(2, 0), new Vector2(39, 6), new Color(.15f, .19f, .10f, .16f * weight));
            for (int i = 0; i < 3; i++)
            {
                float age = steps - i;
                if (age < 0) continue;
                age %= 3;
                if (age > 1.3f) continue;
                float fade = (1 - age / 1.3f) * weight;
                var center = new Vector2(-8 - age * 35, 3 + age * 8);
                var tint = new Color(.78f, .70f, .45f, fade * .22f);
                Ellipse(vh, center, new Vector2(8 + age * 6, 3 + age * 3), tint);
                Ellipse(vh, center + new Vector2(-10, 4), new Vector2(4 + age * 3, 3 + age * 2), tint);
            }
        }
        static void Ellipse(VertexHelper vh, Vector2 center, Vector2 radius, Color tint)
        {
            int first = vh.currentVertCount;
            vh.AddVert(center, tint, Vector2.zero);
            // Soft edge avoids hard circular particle cutouts.
            var edge = tint; edge.a = 0;
            const int segments = 16;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y), edge, Vector2.zero);
                if (i > 0) vh.AddTriangle(first, first + i, first + i + 1);
            }
        }
    }
}
