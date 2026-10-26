using UnityEngine;

/// <summary>Canvas直下のUIだけを端末のノッチ・ホームバーから避ける。背景は全画面のまま。</summary>
/// <remarks>
/// Edit モードでは全画面のまま保つ。本編 Canvas の SafeArea（Crystal）は Play 中しか動かないため、
/// Edit 中だけ Quest 側に inset がかかると Editor 上の見た目と Play 時の配置が食い違う。
/// また Edit 中に書き換えた端末依存のアンカーが Scene / Prefab に保存されるのも防ぐ。
/// </remarks>
[ExecuteAlways, RequireComponent(typeof(RectTransform))]
public sealed class QuestSafeArea : MonoBehaviour
{
    private Rect lastSafeArea;
    private Vector2 lastScreen;
    private bool lastPlaying;

    private void OnEnable() { Refresh(true); }
    private void Update() { Refresh(false); }

    private void Refresh(bool force)
    {
        Vector2 size = new Vector2(Screen.width, Screen.height);
        bool playing = Application.isPlaying;
        Rect safe = playing ? Screen.safeArea : new Rect(0f, 0f, size.x, size.y);
        if (force || size != lastScreen || safe != lastSafeArea || playing != lastPlaying)
        {
            lastPlaying = playing;
            ApplySafeArea(safe, size);
        }
    }

    public void ApplySafeArea(Rect pixels, Vector2 screenSize)
    {
        if (screenSize.x <= 0f || screenSize.y <= 0f) return;
        SafeAreaUtility.ToAnchors(pixels, screenSize.x, screenSize.y, out Vector2 anchorMin, out Vector2 anchorMax);
        RectTransform rt = (RectTransform)transform;
        if (rt.anchorMin != anchorMin) rt.anchorMin = anchorMin;
        if (rt.anchorMax != anchorMax) rt.anchorMax = anchorMax;
        if (rt.offsetMin != Vector2.zero) rt.offsetMin = Vector2.zero;
        if (rt.offsetMax != Vector2.zero) rt.offsetMax = Vector2.zero;
        lastSafeArea = pixels;
        lastScreen = screenSize;
    }
}
