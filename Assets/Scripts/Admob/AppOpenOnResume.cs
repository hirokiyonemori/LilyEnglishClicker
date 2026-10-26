using UnityEngine;

/// <summary>
/// アプリがフォアグラウンド（Resume）に戻ったタイミングで App Open 広告を表示します。
/// シーンに配置不要。自動で生成・永続化されます。
/// </summary>
public class AdmobAppOpenOnResume : MonoBehaviour
{
    private static bool _created;
    private static float _lastAttemptTime;
    private const float MinIntervalSeconds = 2.0f; // 二重呼び出し抑制

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
#if LILY_ENABLE_APP_OPEN_ADS
        if (_created) return;
        var go = new GameObject(nameof(AdmobAppOpenOnResume));
        DontDestroyOnLoad(go);
        go.AddComponent<AdmobAppOpenOnResume>();
        _created = true;
#endif
    }

    private void TryShow()
    {
// App Open ads remain opt-in while the game's advertising integration is disabled.
#if LILY_ENABLE_APP_OPEN_ADS
        // 広告削除済みの場合は表示しない
        if (IAPManager.IsAdsRemoved)
        {
            Debug.Log("広告削除済みのため、App Open広告を表示しません");
            return;
        }

        // 直近表示から一定時間経過していなければスキップ
        if (Time.realtimeSinceStartup - _lastAttemptTime < MinIntervalSeconds)
            return;

        _lastAttemptTime = Time.realtimeSinceStartup;
        AdmobLibrary.ShowAppOpenIfAvailable();
#endif
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            TryShow();
        }
    }

    private void OnApplicationPause(bool pause)
    {
        if (!pause)
        {
            TryShow();
        }
    }
}


