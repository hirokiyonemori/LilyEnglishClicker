using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Idle
{
    /// <summary>
    /// AdMob の ID 一式（プラットフォームごとに 1 つ：Configs/Android.asset、Configs/iOS.asset）。
    /// 開発中は useTestIds = true のまま使う（Google のテスト用 ID。自分の広告を押しても安全）。
    /// 本番 ID に切り替えるのはリリース直前だけにする。開発中に本番広告を自分で表示・クリックすると、
    /// AdMob アカウントが停止されるおそれがある。
    /// </summary>
    [CreateAssetMenu(fileName = "Admob Config", menuName = "Admob/Admob Config")]
    public class AdmobConfig : ScriptableObject
    {
        [Header("どちらのIDを使うか")]
        [Tooltip("true = 下の「テスト用ID」（開発中はこのまま）。false = 「本番ID」（リリース直前に切り替える）。本番IDが空のときは、安全のためテスト用IDを使う")]
        public bool useTestIds = true;

        [Header("テスト用ID（Google の公式サンプル。Android 用の値。iOS.asset は iOS 用の値）")]
        [Tooltip("You can find your app's App ID in the AdMob UI.")]
        public string appId = "ca-app-pub-3940256099942544~3347511713";
        public string bannerViewID = "ca-app-pub-3940256099942544/6300978111";
        public string interstitialID = "ca-app-pub-3940256099942544/1033173712";
        public string rewardBasedVideoID = "ca-app-pub-3940256099942544/5224354917";
        public string appOpenID = "ca-app-pub-3940256099942544/9257395921";

        [Header("本番ID（AdMob の管理画面で作成したもの）")]
        public string prodAppId = "";
        public string prodBannerViewID = "";
        public string prodInterstitialID = "";
        public string prodRewardID = "";
        public string prodAppOpenID = "";

        /// <summary>いま使う ID。useTestIds が false でも、本番IDが空ならテスト用IDにする（空の ID で広告を要求しない）</summary>
        public string ActiveAppId => Pick(appId, prodAppId);
        public string ActiveBannerId => Pick(bannerViewID, prodBannerViewID);
        public string ActiveInterstitialId => Pick(interstitialID, prodInterstitialID);
        public string ActiveRewardId => Pick(rewardBasedVideoID, prodRewardID);
        public string ActiveAppOpenId => Pick(appOpenID, prodAppOpenID);

        private string Pick(string test, string production)
        {
            return useTestIds || string.IsNullOrEmpty(production) ? test : production;
        }
    }
}
