using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 中心から外周へ透明になる楕円。キャラクターの足元の影など、画像を用意せずに柔らかい楕円を描くための UI 部品。
/// </summary>
[AddComponentMenu("UI/Soft Ellipse"), RequireComponent(typeof(CanvasRenderer))]
public sealed class SoftEllipseGraphic : MaskableGraphic
{
    [Range(0f, 1f), Tooltip("中心の不透明度（color.a に掛ける）")]
    public float centerAlpha = 1f;
    [Range(0f, 1f), Tooltip("この半径（0〜1）までは中心と同じ濃さで、そこから外周へ薄くなる")]
    public float solidRadius = 0.35f;
    [Range(8, 64)] public int segments = 40;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        if (r.width <= 0f || r.height <= 0f) return;

        Vector2 c = r.center;
        Vector2 radius = r.size * 0.5f;
        Color inner = color; inner.a *= centerAlpha;
        Color edge = color; edge.a = 0f;

        vh.AddVert(c, inner, Vector2.zero);
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            vh.AddVert(c + Vector2.Scale(dir, radius) * solidRadius, inner, Vector2.zero);
            vh.AddVert(c + Vector2.Scale(dir, radius), edge, Vector2.zero);
        }
        for (int i = 0; i < segments; i++)
        {
            int inA = 1 + i * 2, outA = inA + 1;
            int inB = 1 + ((i + 1) % segments) * 2, outB = inB + 1;
            vh.AddTriangle(0, inA, inB);
            vh.AddTriangle(inA, outA, outB);
            vh.AddTriangle(inA, outB, inB);
        }
    }
}
