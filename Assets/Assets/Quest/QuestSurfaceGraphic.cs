using UnityEngine;
using UnityEngine.UI;

/// <summary>解像度に依存しない角丸カード。画像が無くても紺・金の外観を保つUI部品。</summary>
[AddComponentMenu("UI/Quest Surface"), RequireComponent(typeof(CanvasRenderer))]
public sealed class QuestSurfaceGraphic : MaskableGraphic
{
    public Color topColor = new Color(0.12f, 0.23f, 0.34f, 0.98f);
    public Color bottomColor = new Color(0.06f, 0.12f, 0.23f, 0.98f);
    public Color borderColor = new Color(0.77f, 0.64f, 0.38f);
    [Min(0f)] public float cornerRadius = 28f;
    [Min(0f)] public float borderWidth = 2f;
    private float lastPixelScale = -1f;

    // CanvasScalerで縮小されても、直線と角の線幅を実画面の1.5px以上に保つ。
    private float PixelScale()
    {
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 origin = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(Vector3.zero));
        Vector2 x = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(Vector3.right));
        Vector2 y = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(Vector3.up));
        return Mathf.Max(.0001f, Mathf.Min((x - origin).magnitude, (y - origin).magnitude));
    }

    private void LateUpdate()
    {
        float scale = PixelScale();
        if (Mathf.Approximately(lastPixelScale, scale)) return;
        lastPixelScale = scale;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect outer = GetPixelAdjustedRect();
        if (outer.width <= 0f || outer.height <= 0f) return;
        float width = borderWidth > 0f ? Mathf.Max(borderWidth, 1.5f / PixelScale()) : 0f;
        float radius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(outer.width, outer.height) * .5f);
        if (width > 0f) Fill(vh, outer, radius, borderColor, borderColor);
        float inset = Mathf.Min(width, Mathf.Min(outer.width, outer.height) * 0.5f);
        Rect inner = new Rect(outer.x + inset, outer.y + inset, outer.width - inset * 2f, outer.height - inset * 2f);
        if (inner.width > 0f && inner.height > 0f)
            Fill(vh, inner, Mathf.Max(0f, radius - inset), bottomColor, topColor);
    }

    private void Fill(VertexHelper vh, Rect rect, float radius, Color low, Color high)
    {
        radius = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);
        int start = vh.currentVertCount;
        vh.AddVert(rect.center, Color.Lerp(low, high, .5f) * color, Vector2.zero);
        const int steps = 8;
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 center = new Vector2(corner < 2 ? rect.xMax - radius : rect.xMin + radius,
                                         corner == 0 || corner == 3 ? rect.yMax - radius : rect.yMin + radius);
            for (int step = 0; step <= steps; step++)
            {
                float angle = (90f - corner * 90f - step * 90f / steps) * Mathf.Deg2Rad;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                float p = rect.height > 0f ? (point.y - rect.yMin) / rect.height : .5f;
                vh.AddVert(point, Color.Lerp(low, high, p) * color, Vector2.zero);
            }
        }
        int count = 4 * (steps + 1);
        for (int i = 0; i < count; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % count);
    }
}
