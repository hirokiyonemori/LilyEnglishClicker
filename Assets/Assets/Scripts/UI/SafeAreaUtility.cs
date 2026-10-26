using UnityEngine;

/// <summary>
/// Screen.safeArea を RectTransform のアンカー（0〜1）へ安全に変換する共通処理。
///
/// Editor では Device Simulator 使用後などに、Screen.safeArea が現在の Game View とは
/// 別解像度の値を返し続けることがある（実測: 画面 1080x1920 / 720x1280 / 480x800 に対して
/// safeArea が常に x=0, y=102, w=1170, h=2289）。これをそのまま Screen.width/height で割ると
/// アンカーが 1 を超え、SafeArea 配下の UI 全体が画面外へ押し出される。
/// 画面内に収まらない safeArea は信用せず全画面として扱う。
/// </summary>
public static class SafeAreaUtility
{
    private const float Tolerance = 1f;
    private static Rect lastRejected;
    private static Vector2 lastRejectedScreen;

    /// <summary>画面サイズと矛盾しない safeArea を返す。矛盾する場合は全画面を返す。</summary>
    public static Rect Validate(Rect safeArea, float screenWidth, float screenHeight, bool logWarning = true)
    {
        Rect full = new Rect(0f, 0f, screenWidth, screenHeight);
        if (screenWidth <= 0f || screenHeight <= 0f) return full;

        bool invalid = float.IsNaN(safeArea.x) || float.IsNaN(safeArea.y)
                       || float.IsNaN(safeArea.width) || float.IsNaN(safeArea.height)
                       || safeArea.width <= 0f || safeArea.height <= 0f
                       || safeArea.xMin < -Tolerance || safeArea.yMin < -Tolerance
                       || safeArea.xMax > screenWidth + Tolerance || safeArea.yMax > screenHeight + Tolerance;
        if (!invalid) return safeArea;

        Vector2 screen = new Vector2(screenWidth, screenHeight);
        if (logWarning && (safeArea != lastRejected || screen != lastRejectedScreen))
        {
            lastRejected = safeArea;
            lastRejectedScreen = screen;
            Debug.LogWarning($"SafeAreaUtility: safeArea {safeArea} が画面 {screenWidth}x{screenHeight} に収まらないため全画面として扱います。");
        }
        return full;
    }

    /// <summary>safeArea（ピクセル）を 0〜1 のアンカーへ変換する。</summary>
    public static void ToAnchors(Rect safeArea, float screenWidth, float screenHeight, out Vector2 anchorMin, out Vector2 anchorMax)
    {
        Rect r = Validate(safeArea, screenWidth, screenHeight);
        if (screenWidth <= 0f || screenHeight <= 0f)
        {
            anchorMin = Vector2.zero;
            anchorMax = Vector2.one;
            return;
        }
        anchorMin = new Vector2(Mathf.Clamp01(r.xMin / screenWidth), Mathf.Clamp01(r.yMin / screenHeight));
        anchorMax = new Vector2(Mathf.Clamp01(r.xMax / screenWidth), Mathf.Clamp01(r.yMax / screenHeight));
    }
}
