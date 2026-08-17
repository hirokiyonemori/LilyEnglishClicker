using UnityEngine;

namespace Idle
{
    /// <summary>
    /// 【一時的に無効化中】広告表示マネージャー
    ///
    /// このクラスは AdMob v3世代のAPI（RewardBasedVideoAd / AdRequest.Builder /
    /// InterstitialAd.IsLoaded など）で書かれていたが、プロジェクトが参照している
    /// SDKは v10.7.0 で、これらは全て削除済みのためコンパイルが通らなくなっていた。
    ///
    /// ゲーム本体の開発を止めないよう、いったん広告呼び出しを空実装にしてある。
    /// シーンに保存された設定を失わないよう、SerializeFieldと公開メソッドの
    /// 名前・シグネチャは元のまま維持している。
    ///
    /// TODO: v10 API へ移行する
    ///   - MobileAds.Initialize(appId)      → MobileAds.Initialize(Action&lt;InitializationStatus&gt;)
    ///   - RewardBasedVideoAd.Instance      → RewardedAd.Load(adUnitId, request, callback)
    ///   - new AdRequest.Builder().Build()  → new AdRequest()
    ///   - new InterstitialAd(id) + IsLoaded() → InterstitialAd.Load(id, request, callback)
    /// </summary>
    public class AdmobManager : MonoBehaviour
    {
        public enum BannerPosition { Top, Bottom } //Numerator for position of banner

        public static AdmobManager instance; //Singletop

        [Header("Your Admob config")]
        public AdmobConfig admobConfig;

        [Header("Banner settings")]
        [SerializeField] private bool showBannerOnAwake; //Flag to check the need to display a banner at startup
        [SerializeField] private BannerPosition bannerPosition; //Position of banner

        [Header("Reward settings")]
        public long rewardCount; //You can edit this from any scripts

        private void Awake()
        {
            instance = this;
        }

        public void Start()
        {
            Init();
        }

        private void Init()
        {
            Debug.LogWarning("AdmobManager: 広告機能は一時的に無効化されています（SDK v10へのAPI移行が未対応）。");

            if (showBannerOnAwake)
            {
                ShowBanner();
            }
        }

        #region // Show methods

        public void ShowBanner()
        {
            Debug.Log($"AdmobManager: [無効化中] バナー表示をスキップしました（位置: {bannerPosition}）");
        }

        public void HideBanner()
        {
            Debug.Log("AdmobManager: [無効化中] バナー非表示をスキップしました");
        }

        public void ShowInterstitial()
        {
            Debug.Log("AdmobManager: [無効化中] インタースティシャル表示をスキップしました");
        }

        public void ShowReward()
        {
            // 広告を再生していないため、報酬は付与しない。
            // 移行後は視聴完了コールバックの中で rewardCount を加算すること。
            Debug.Log("AdmobManager: [無効化中] リワード動画表示をスキップしました（報酬は付与されません）");
        }

        #endregion
    }
}
