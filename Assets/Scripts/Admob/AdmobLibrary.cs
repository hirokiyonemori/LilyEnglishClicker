
using GoogleMobileAds.Api;
using System;
using UnityEngine;

/// <summary>
/// adMobを使用するためのクラス
/// </summary>
public class AdmobLibrary
{
	private static BannerView _bannerView;
	private static InterstitialAd _interstitialAd;
	private static RewardedAd _rewardedAd;
	private static AppOpenAd _appOpenAd;
	private static bool _isInitialized = false;
	private static bool _isBannerLoaded = false;

	public static Action<double> OnReward;
	public static Action OnRewardedClosed;

	public static Action OnLoadedInterstitial;
	public static Action OnLoadedReward;

	/// <summary>
	/// ゲーム起動　初回に一度だけ呼ぶ
	/// </summary>
	public static void FirstSetting()
	{
		// 既に初期化されている場合は何もしない
		if (_isInitialized)
		{
			Debug.Log("AdmobLibraryは既に初期化されています");
			return;
		}

		//13歳以下を対象と「する」場合はtrue
		// RequestConfiguration request
		// 	= new RequestConfiguration.Builder()
		// 		.SetTagForChildDirectedTreatment(TagForChildDirectedTreatment.False).build();
		//
		RequestConfiguration request = new RequestConfiguration
		{
			TagForChildDirectedTreatment = TagForChildDirectedTreatment.False
		};


		MobileAds.SetRequestConfiguration(request);
		LoadReward();
		// This callback is called once the MobileAds SDK is initialized.
		InitInterstitial();
		// App Open をロード
		LoadAppOpenAd();

		_isInitialized = true;
		Debug.Log("AdmobLibrary初期化完了");
	}




	/// <summary>
	/// バナー広告を生成
	/// </summary>
	/// <param name="size"></param>
	/// <param name="position"></param>
	public static void RequestBanner(AdSize size, AdPosition position)
	{
		// 広告削除済みの場合はバナー広告を表示しない
		if (IAPManager.IsAdsRemoved)
		{
			Debug.Log("広告削除済みのため、バナー広告を表示しません");
			return;
		}

		// 既にバナーが読み込まれている場合は何もしない
		if (_isBannerLoaded && _bannerView != null)
		{
			Debug.Log("バナー広告は既に読み込まれています");
			return;
		}

		// 既存のバナーがある場合は破棄
		if (_bannerView != null)
		{
			Debug.Log("既存のバナーを破棄して新しいバナーを作成します");
			_bannerView.Destroy();
			_bannerView = null;
			_isBannerLoaded = false;
		}

#if TEST_CODE || UNITY_EDITOR
		string adUnitId = "ca-app-pub-3940256099942544/6300978111"; // テスト用バナー広告ID
		Debug.Log("テストバナー広告を使用しています: " + adUnitId);
#elif UNITY_ANDROID
		string adUnitId = "ca-app-pub-8148356110096114/8257410685";
#elif UNITY_IPHONE
		string adUnitId = "ca-app-pub-8148356110096114/4685583912";
#else
		string adUnitId = "unexpected_platform";
#endif
		// Create a 320x50 banner at the top of the screen.

		_bannerView = new BannerView(adUnitId, size, position);

		//セーフエリアを考慮
		// var area = Screen.safearea;
		// _bannerView = new BannerView(adUnitId, size, Screen.width/4 ,Screen.height /10);

		// Create an empty ad request.
		var adRequest = new AdRequest();
		// Load the banner with the request.
		_bannerView.LoadAd(adRequest);
		_isBannerLoaded = true;
		Debug.Log($"バナー広告読み込み開始 - サイズ: {_bannerView.GetHeightInPixels()} x {_bannerView.GetWidthInPixels()}");
	}

	/// <summary>
	/// App Open Ad をロード
	/// </summary>
	public static void LoadAppOpenAd()
	{
		string adUnitId;
#if TEST_CODE || UNITY_EDITOR
		adUnitId = "ca-app-pub-3940256099942544/5662855259"; // テスト用App Open ID
#elif UNITY_ANDROID
		adUnitId = "ca-app-pub-8148356110096114/9296081996";
#elif UNITY_IPHONE
		adUnitId = "ca-app-pub-8148356110096114/2942712044";
#else
		adUnitId = "unexpected_platform";
#endif

		var request = new AdRequest();
		Debug.Log($"LoadAppOpenAd: {adUnitId}");
		// SDKバージョン差異に対応するため、引数3のオーバーロードを使用
		AppOpenAd.Load(adUnitId, request, (ad, error) =>
		{
			if (error != null || ad == null)
			{
				Debug.LogError($"AppOpen failed to load: {error}");
				_appOpenAd = null;
				return;
			}

			_appOpenAd = ad;
			Debug.Log("AppOpen loaded");

			_appOpenAd.OnAdFullScreenContentClosed += () =>
			{
				Debug.Log("AppOpen closed");
				_appOpenAd = null;
				LoadAppOpenAd();
			};
			_appOpenAd.OnAdFullScreenContentFailed += (AdError adError) =>
			{
				Debug.LogError($"AppOpen failed to show: {adError}");
				_appOpenAd = null;
			};
		});
	}

	/// <summary>
	/// App Open Ad があれば表示、なければロード
	/// </summary>
	public static void ShowAppOpenIfAvailable()
	{
		// 広告削除済みの場合はApp Open広告を表示しない
		if (IAPManager.IsAdsRemoved)
		{
			Debug.Log("広告削除済みのため、App Open広告を表示しません");
			return;
		}

		if (_appOpenAd != null && _appOpenAd.CanShowAd())
		{
			Debug.Log("Show AppOpen");
			_appOpenAd.Show();
		}
		else
		{
			Debug.Log("AppOpen not ready. Loading...");
			LoadAppOpenAd();
		}
	}

	public static float GetBannerHeight()
	{
		//Editrでのみ使用
		if (_bannerView == null)
		{
			return 0;
		}
		return _bannerView.GetHeightInPixels();
	}

	/// <summary>
	/// バナーが読み込まれているかどうかを確認
	/// </summary>
	public static bool IsBannerLoaded()
	{
		return _isBannerLoaded && _bannerView != null;
	}

	/// <summary>
	/// バナー広告削除
	/// </summary>
	public static void DestroyBanner()
	{
		if (_bannerView != null)
		{
			Debug.Log("バナー広告を破棄します");
			_bannerView.Destroy();
			_bannerView = null;
			_isBannerLoaded = false;
		}
	}

	/// <summary>
	/// インタースティシャル読み込み
	/// </summary>
	private static void InitInterstitial()
	{
#if TEST_CODE || UNITY_EDITOR
		string adUnitId = "ca-app-pub-3940256099942544/1033173712"; // テスト用インタースティシャル広告ID
#elif UNITY_ANDROID
		string adUnitId = "ca-app-pub-8148356110096114/9294322817";
#elif UNITY_IPHONE
        string adUnitId = "ca-app-pub-8148356110096114/5243682578";
#else
		string adUnitId = "unexpected_platform";
#endif
		// Initialize an InterstitialAd.


		// var adRequest = new AdRequest.Builder()
		// 	.AddKeyword("unity-admob-sample")
		// 	.Build();
		//		var adRequest = new AdRequest();

		var adRequest = new AdRequest
		{
			Keywords = { "unity-admob-sample" }
		};


		if (_interstitialAd != null)
		{
			_interstitialAd.Destroy();
			_interstitialAd = null;
		}

		Debug.Log("InitInterstitial");
		// send the request to load the ad.
		InterstitialAd.Load(adUnitId, adRequest,
			(InterstitialAd ad, LoadAdError error) =>
			{
				// if error is not null, the load request failed.
				if (error != null || ad == null)
				{
					Debug.LogError("interstitial ad failed to load an ad " +
								   "with error : " + error);
					return;
				}

				Debug.Log("Interstitial ad loaded with response : "
						  + ad.GetResponseInfo());

				// Raised when the ad is estimated to have earned money.
				ad.OnAdPaid += (AdValue adValue) =>
				{
					Debug.Log(String.Format("Interstitial ad paid {0} {1}.",
						adValue.Value,
						adValue.CurrencyCode));
				};
				// Raised when an impression is recorded for an ad.
				ad.OnAdImpressionRecorded += () => { Debug.Log("Interstitial ad recorded an impression."); };
				// Raised when a click is recorded for an ad.
				ad.OnAdClicked += () => { Debug.Log("Interstitial ad was clicked."); };
				// Raised when an ad opened full screen content.
				ad.OnAdFullScreenContentOpened += () => { Debug.Log("Interstitial ad full screen content opened."); };
				// Raised when the ad closed full screen content.
				ad.OnAdFullScreenContentClosed += () =>
				{
					DestroyInterstitial();
					InitInterstitial();
					Debug.Log("Interstitial ad full screen content closed.");
				};
				// Raised when the ad failed to open full screen content.
				ad.OnAdFullScreenContentFailed += (AdError error) =>
				{
					Debug.LogError("Interstitial ad failed to open full screen content " +
								   "with error : " + error);
				};
				_interstitialAd = ad;
				OnLoadedInterstitial?.Invoke();
			});

	}

	/// <summary>
	/// インタースティシャルを出す
	/// </summary>
	public static void PlayInterstitial()
	{
		// 広告削除済みの場合はインタースティシャル広告を表示しない
		if (IAPManager.IsAdsRemoved)
		{
			Debug.Log("広告削除済みのため、インタースティシャル広告を表示しません");
			return;
		}

		Debug.Log("PlayInterstitial");
		if (_interstitialAd != null && _interstitialAd.CanShowAd())
		{
			Debug.Log("Showing interstitial ad.");
			_interstitialAd.Show();
		}
		else
		{
			Debug.LogError("Interstitial ad is not ready yet.");
		}
	}

	/// <summary>
	/// インタースティシャル削除
	/// </summary>
	public static void DestroyInterstitial()
	{
		if (_interstitialAd != null)
		{
			Debug.Log("DestroyInterstitial");
			_interstitialAd.Destroy();
		}
	}

	/// <summary>
	/// リワード広告
	/// </summary>
	public static void LoadReward()
	{
		string adUnitId;
#if TEST_CODE || UNITY_EDITOR
		adUnitId = "ca-app-pub-3940256099942544/5224354917"; // テスト用リワード広告ID
#elif UNITY_ANDROID
		adUnitId = "ca-app-pub-8148356110096114/9200593749";
#elif UNITY_IPHONE
		adUnitId = "ca-app-pub-8148356110096114/1144109947";
#else
		adUnitId = "unexpected_platform";
		
#endif
		var adRequest = new AdRequest();
		_rewardedAd = null;
		RewardedAd.Load(adUnitId, adRequest,
			(RewardedAd ad, LoadAdError error) =>
			{
				// if error is not null, the load request failed.
				if (error != null || ad == null)
				{
					Debug.LogError("rewarded ad failed to load an ad " +
								   "with error : " + error);
					return;
				}

				Debug.Log("Rewarded ad loaded with response : "
						  + ad.GetResponseInfo());
				// Raised when the ad is estimated to have earned money.
				ad.OnAdPaid += (AdValue adValue) =>
				{
					Debug.Log(String.Format("Rewarded ad paid {0} {1}.",
						adValue.Value,
						adValue.CurrencyCode));
				};
				// Raised when an impression is recorded for an ad.
				ad.OnAdImpressionRecorded += () => { Debug.Log("Rewarded ad recorded an impression."); };
				// Raised when a click is recorded for an ad.
				ad.OnAdClicked += () => { Debug.Log("Rewarded ad was clicked."); };
				// Raised when an ad opened full screen content.
				ad.OnAdFullScreenContentOpened += () => { Debug.Log("Rewarded ad full screen content opened."); };
				// Raised when the ad closed full screen content.
				ad.OnAdFullScreenContentClosed += () =>
				{
					LoadReward();
					Debug.Log("Rewarded ad full screen content closed.");
					OnRewardedClosed?.Invoke();
				};
				// Raised when the ad failed to open full screen content.
				ad.OnAdFullScreenContentFailed += (AdError error) =>
				{
					Debug.LogError("Rewarded ad failed to open full screen content " +
								   "with error : " + error);
				};
				_rewardedAd = ad;
				OnLoadedReward?.Invoke();
			});
	}

	/// <summary>
	/// リワード広告を作成
	/// </summary>
	public static void ShowReward()
	{
		// 広告削除済みの場合はリワード広告を表示しない
		if (IAPManager.IsAdsRemoved)
		{
			Debug.Log("広告削除済みのため、リワード広告を表示しません");
			// 広告削除済みでも報酬は与える（毎日コイン特典として）
			OnReward?.Invoke(100); // 毎日ボーナス相当の報酬
			OnRewardedClosed?.Invoke();
			return;
		}

		if (_rewardedAd != null && _rewardedAd.CanShowAd())
		{
			_rewardedAd.Show((Reward reward) =>
			{
				// TODO: Reward the user.
				Debug.Log(String.Format("Reward ", reward.Type, reward.Amount));
				OnReward?.Invoke(reward.Amount);
				_rewardedAd.Destroy();
				LoadReward();
			});
		}
		else
		{
			Debug.LogError("Rewarded ad is not ready yet.");
			// 広告が表示できない場合でも待機状態を解除するため通知
			OnRewardedClosed?.Invoke();
		}
	}

	public static bool IsActiveInterstitial()
	{
		return _interstitialAd != null;
	}
	/// <summary>
	/// リワード削除
	/// </summary>
	public static void DestroyReward()
	{
		if (_rewardedAd != null)
		{
			_rewardedAd.Destroy();
		}
	}

	/// <summary>
	/// リワード
	/// </summary>
	/// <returns></returns>
	public static bool IsActiveReward()
	{
		return _rewardedAd != null && _rewardedAd.CanShowAd();
	}
}
