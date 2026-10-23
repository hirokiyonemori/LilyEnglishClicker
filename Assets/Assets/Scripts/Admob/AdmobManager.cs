using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Idle
{
    /// <summary>
    /// 広告表示マネージャー（Google Mobile Ads SDK v10）。バナー・インタースティシャル・リワードを扱う。
    ///
    /// ・使う広告ユニット ID は AdmobConfig（Configs/Android.asset・iOS.asset）の Active*Id。
    ///   開発中は useTestIds = true（Google のテスト用 ID）のまま。本番 ID への切り替えはリリース直前だけ。
    /// ・起動すると UMP（同意フォーム。EEA・英国などでだけ出る）→ SDK 初期化 → リワード/インタースティシャルを先読み。
    /// ・エディタでは SDK を動かさない（enableInEditor を入れたときだけ）。リワードは GrowthService のダミーが代わりを務める。
    /// ・リワード広告は「最後まで見た（報酬が確定した）」ときだけ true を返す。読み込み失敗時は間隔を空けて再読み込みする。
    /// ・以前の公開メソッド（ShowBanner / HideBanner / ShowInterstitial / ShowReward）と保存済みの項目名は元のまま。
    /// </summary>
    public class AdmobManager : MonoBehaviour
    {
        public enum BannerPosition { Top, Bottom } //Numerator for position of banner

        public static AdmobManager instance; //Singletop

        [Header("Your Admob config")]
        [Tooltip("Android（とエディタ）で使う ID 一式（Configs/Android.asset）")]
        public AdmobConfig admobConfig;
        [Tooltip("iOS で使う ID 一式（Configs/iOS.asset）。未設定の iOS 端末では広告を出さない")]
        public AdmobConfig admobConfigIOS;

        [Header("Banner settings")]
        [SerializeField] private bool showBannerOnAwake; //Flag to check the need to display a banner at startup
        [SerializeField] private BannerPosition bannerPosition; //Position of banner

        [Header("Reward settings")]
        public long rewardCount; //You can edit this from any scripts

        [Header("スイッチ")]
        [Tooltip("false にすると、どの環境でも広告を一切出さない")]
        [SerializeField] private bool adsEnabled = true;
        [Tooltip("エディタでも SDK を動かす（テスト用 ID）。普段は false。エディタのリワードはダミーが代わりを務める")]
        [SerializeField] private bool enableInEditor;

        private AdmobConfig config;
        private BannerView bannerView;
        private InterstitialAd interstitialAd;
        private RewardedAd rewardedAd;
        private const float ConsentWaitSeconds = 8f;
        private bool consentPending;
        private float consentDeadline;
        private bool initializing;
        private bool bannerRequested;
        private bool loadingRewarded;
        private bool loadingInterstitial;
        private int rewardedFailures;
        private int interstitialFailures;
        private float nextRewardedLoad;
        private float nextInterstitialLoad;

        /// <summary>この環境で広告 SDK を動かすか（エディタは enableInEditor が必要。Android・iOS 以外は動かさない）</summary>
        public bool SdkEnabled
        {
            get
            {
                if (!adsEnabled) return false;
                if (Application.isEditor) return enableInEditor;
                return Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer;
            }
        }

        public bool IsInitialized { get; private set; }

        public bool IsRewardedReady => SdkEnabled && IsInitialized && rewardedAd != null && rewardedAd.CanShowAd();

        public bool IsInterstitialReady => SdkEnabled && IsInitialized && interstitialAd != null && interstitialAd.CanShowAd();

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
            if (!SdkEnabled)
            {
                Debug.Log("AdmobManager: 広告は動かしません（エディタ、または adsEnabled が off）。");
                return;
            }

            config = ResolveConfig();
            if (config == null)
            {
                Debug.LogWarning("AdmobManager: この端末用の AdmobConfig が未設定のため、広告を出しません。");
                return;
            }

            // 同意（UMP）。フォームが要る地域（EEA・英国など）でだけ表示され、他では何も出ない。
            // 通知が返ってこない環境（エディタの代用クライアントなど）でも止まらないよう、一定時間で先へ進む保険を持つ
            consentPending = true;
            consentDeadline = Time.unscaledTime + ConsentWaitSeconds;
            var parameters = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
            ConsentInformation.Update(parameters, updateError =>
            {
                if (updateError != null) Debug.LogWarning("AdmobManager: 同意情報の更新に失敗: " + updateError.Message);
                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null) Debug.LogWarning("AdmobManager: 同意フォームのエラー: " + formError.Message);
                    consentPending = false;
                    StartSdk();
                });
            });
        }

        private AdmobConfig ResolveConfig()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return admobConfigIOS;
#else
            return admobConfig;
#endif
        }

        private void StartSdk()
        {
            if (IsInitialized || initializing) return;
            if (!ConsentInformation.CanRequestAds())
            {
                Debug.Log("AdmobManager: 同意が得られていないため、広告を要求しません。");
                return;
            }

            initializing = true;
            MobileAds.Initialize(status =>
            {
                initializing = false;
                IsInitialized = true;
                Debug.Log("AdmobManager: 広告 SDK を初期化しました。");
                LoadRewarded();
                LoadInterstitial();
                if (showBannerOnAwake || bannerRequested) ShowBanner();
            });
        }

        private void Update()
        {
            // 同意の確認が長引いている間も、すでに同意済み（広告を要求してよい状態）なら先へ進める。
            // フォームの表示中で未同意なら CanRequestAds が false なので、ここでは何も起きず、確認が済んだあとの通知で進む
            if (consentPending && Time.unscaledTime >= consentDeadline)
            {
                consentPending = false;
                StartSdk();
            }

            // 読み込みに失敗したときは、間隔を空けて（5秒→10秒→…最大60秒）もう一度読み込む
            if (!IsInitialized) return;
            if (rewardedAd == null && !loadingRewarded && Time.unscaledTime >= nextRewardedLoad) LoadRewarded();
            if (interstitialAd == null && !loadingInterstitial && Time.unscaledTime >= nextInterstitialLoad) LoadInterstitial();
        }

        private static float Backoff(int failures)
        {
            return Mathf.Min(60f, 5f * Mathf.Pow(2f, Mathf.Max(0, failures - 1)));
        }

        #region // Show methods

        public void ShowBanner()
        {
            if (!SdkEnabled || config == null || IAPManager.IsAdsRemoved) return;
            if (!IsInitialized) { bannerRequested = true; return; }
            if (bannerView != null) { bannerView.Show(); return; }

            string id = config.ActiveBannerId;
            if (string.IsNullOrEmpty(id)) return;
            bannerView = new BannerView(id, AdSize.Banner, bannerPosition == BannerPosition.Top ? AdPosition.Top : AdPosition.Bottom);
            bannerView.LoadAd(new AdRequest());
        }

        public void HideBanner()
        {
            bannerRequested = false;
            if (bannerView != null) bannerView.Hide();
        }

        public void ShowInterstitial()
        {
            if (IAPManager.IsAdsRemoved) return;
            if (!IsInterstitialReady)
            {
                Debug.Log("AdmobManager: インタースティシャルの準備ができていません。");
                return;
            }

            InterstitialAd ad = interstitialAd;
            interstitialAd = null; // 1 回使い切り
            ad.OnAdFullScreenContentClosed += () => { ad.Destroy(); LoadInterstitial(); };
            ad.OnAdFullScreenContentFailed += error =>
            {
                Debug.LogWarning("AdmobManager: インタースティシャルを表示できませんでした: " + error.GetMessage());
                ad.Destroy();
                LoadInterstitial();
            };
            ad.Show();
        }

        /// <summary>
        /// リワード広告を再生する。最後まで見たとき（報酬が確定したとき）だけ onFinished(true)。
        /// 準備ができていない・表示に失敗・途中で閉じたときは onFinished(false)。onFinished は必ず 1 回だけ呼ばれる。
        /// </summary>
        public void ShowRewarded(Action<bool> onFinished)
        {
            if (!IsRewardedReady)
            {
                if (SdkEnabled && IsInitialized) LoadRewarded();
                if (onFinished != null) onFinished(false);
                return;
            }

            RewardedAd ad = rewardedAd;
            rewardedAd = null; // 1 回使い切り
            bool earned = false;
            bool finished = false;
            Action<bool> finish = ok =>
            {
                if (finished) return;
                finished = true;
                if (onFinished != null) onFinished(ok);
            };
            ad.OnAdFullScreenContentClosed += () => { ad.Destroy(); LoadRewarded(); finish(earned); };
            ad.OnAdFullScreenContentFailed += error =>
            {
                Debug.LogWarning("AdmobManager: リワード広告を表示できませんでした: " + error.GetMessage());
                ad.Destroy();
                LoadRewarded();
                finish(false);
            };
            ad.Show(reward => { earned = true; });
        }

        /// <summary>従来の呼び出し口。リワードを見終えたら rewardCount ぶんを所持金へ加算する。</summary>
        public void ShowReward()
        {
            long amount = rewardCount;
            ShowRewarded(ok =>
            {
                if (!ok || amount <= 0 || DataManager.data == null) return;
                DataManager.data.Money += amount;
                if (Managers.Instance != null && Managers.Instance.uIManager != null) Managers.Instance.uIManager.UpdateUI();
            });
        }

        #endregion

        private void LoadRewarded()
        {
            if (loadingRewarded || rewardedAd != null || config == null || !IsInitialized) return;
            string id = config.ActiveRewardId;
            if (string.IsNullOrEmpty(id)) return;

            loadingRewarded = true;
            RewardedAd.Load(id, new AdRequest(), (ad, error) =>
            {
                loadingRewarded = false;
                if (error != null || ad == null)
                {
                    rewardedFailures++;
                    nextRewardedLoad = Time.unscaledTime + Backoff(rewardedFailures);
                    Debug.LogWarning("AdmobManager: リワード広告の読み込みに失敗: " + (error != null ? error.GetMessage() : "ad is null"));
                    return;
                }
                rewardedFailures = 0;
                rewardedAd = ad;
                Debug.Log("AdmobManager: リワード広告を読み込みました。");
            });
        }

        private void LoadInterstitial()
        {
            if (loadingInterstitial || interstitialAd != null || config == null || !IsInitialized) return;
            string id = config.ActiveInterstitialId;
            if (string.IsNullOrEmpty(id)) return;

            loadingInterstitial = true;
            InterstitialAd.Load(id, new AdRequest(), (ad, error) =>
            {
                loadingInterstitial = false;
                if (error != null || ad == null)
                {
                    interstitialFailures++;
                    nextInterstitialLoad = Time.unscaledTime + Backoff(interstitialFailures);
                    Debug.LogWarning("AdmobManager: インタースティシャルの読み込みに失敗: " + (error != null ? error.GetMessage() : "ad is null"));
                    return;
                }
                interstitialFailures = 0;
                interstitialAd = ad;
                Debug.Log("AdmobManager: インタースティシャルを読み込みました。");
            });
        }

        private void OnDestroy()
        {
            if (bannerView != null) { bannerView.Destroy(); bannerView = null; }
            if (interstitialAd != null) { interstitialAd.Destroy(); interstitialAd = null; }
            if (rewardedAd != null) { rewardedAd.Destroy(); rewardedAd = null; }
            if (instance == this) instance = null;
        }
    }
}
