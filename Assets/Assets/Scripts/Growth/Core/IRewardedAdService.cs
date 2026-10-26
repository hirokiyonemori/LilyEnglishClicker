using System;
using UnityEngine;

namespace LilyEnglish.Growth
{
    /// <summary>
    /// リワード広告の差し替え口。AdMob / Unity Ads を入れたらこれを実装する。
    /// </summary>
    public interface IRewardedAdService
    {
        bool IsReady { get; }
        void Show(Action<bool> onComplete); // true = 最後まで視聴
    }

    /// <summary>開発用ダミー：即「視聴完了」扱い。</summary>
    public class DummyRewardedAdService : IRewardedAdService
    {
        public bool IsReady => true;

        public void Show(Action<bool> onComplete)
        {
            Debug.Log("[Ad] Dummy rewarded ad watched");
            onComplete?.Invoke(true);
        }
    }

    /// <summary>広告 SDK 未接続（実機の既定）。ボタンを押せない状態にする。</summary>
    public class UnavailableRewardedAdService : IRewardedAdService
    {
        public bool IsReady => false;
        public void Show(Action<bool> onComplete) => onComplete?.Invoke(false);
    }
}
