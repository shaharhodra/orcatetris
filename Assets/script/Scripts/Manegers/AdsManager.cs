using GoogleMobileAds.Api;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class AdsManager : Singleton<AdsManager>
{
	public const string TestInterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712"; // Test ad unit ID
	public const string TestRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917"; // Test ad unit ID

	public const string AndroidInterstitialAdUnitId = "ca-app-pub-6721702472765626/7955870421";
	public const string AndroidRewardedAdUnitId = "ca-app-pub-6721702472765626/5241957037";

	private InterstitialAd _interstitialAd;
	private RewardedAd _rewardedAd;
	public void Init()
	{
		Debug.Log("AdsManager Init");
		MobileAds.Initialize((InitializationStatus initstatus) =>
		  {
			  if (initstatus == null)
			  {
				  Debug.LogError("Google Mobile Ads initialization failed.");
				  return;
			  }

			  Debug.Log("Google Mobile Ads initialization complete.");
			  InitInterstitialAds();
			  InitRewardedAds();

			  // Google Mobile Ads events are raised off the Unity Main thread. If you need to
			  // access UnityEngine objects after initialization,
			  // use MobileAdsEventExecutor.ExecuteInUpdate(). For more information, see:
			  // https://developers.google.com/admob/unity/global-settings#raise_ad_events_on_the_unity_main_thread
		  });
	}

	public void InitInterstitialAds()
	{
		// Create our request used to load the ad.
		var adRequest = new AdRequest();

		// Send the request to load the ad.
		InterstitialAd.Load(TestInterstitialAdUnitId, adRequest, (InterstitialAd ad, LoadAdError error) =>
		{
			if (error != null)
			{
				// The ad failed to load.
				Debug.LogError("Interstitial ad failed to load: " + error);
				return;
			}
			_interstitialAd = ad;
			Debug.Log("Interstitial ad loaded successfully.");
			// The ad loaded successfully.

			_interstitialAd.OnAdPaid += (AdValue adValue) =>
			{
				// Raised when the ad is estimated to have earned money.
			};
			_interstitialAd.OnAdImpressionRecorded += () =>
			{
				// Raised when an impression is recorded for an ad.
			};
			_interstitialAd.OnAdClicked += () =>
			{
				// Raised when a click is recorded for an ad.
			};
			_interstitialAd.OnAdFullScreenContentOpened += () =>
			{
				// Raised when the ad opened full screen content.
			};
			_interstitialAd.OnAdFullScreenContentClosed += () =>
			{
				// Raised when the ad closed full screen content.
				Debug.Log("Interstitial ad closed full screen content.");

				// Reload the ad so that we can show another as soon as possible.
				var adRequest = new AdRequest();
				InterstitialAd.Load("AD_UNIT_ID", adRequest, (InterstitialAd ad, LoadAdError error) =>
				{
					// Handle ad loading here.
					_interstitialAd = ad;

				});
			};
			_interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
			{
				// Raised when the ad failed to open full screen content.
				Debug.LogError("Interstitial ad failed to open full screen content: " + error);
				DestroyInterstitialAd();
			};
		});
	}

	public void ShowInterstitialAd()
	{
		if (_interstitialAd != null && _interstitialAd.CanShowAd())
		{
			_interstitialAd.Show();
			Debug.Log("Interstitial ad is shown.");
		}
		else
		{
			Debug.Log("Interstitial ad is not ready yet.");
		}
	}

	public void DestroyInterstitialAd()
	{
		if (_interstitialAd != null)
		{
			_interstitialAd.Destroy();
			_interstitialAd = null;
			Debug.Log("Interstitial ad destroyed.");
		}
	}

	public void InitRewardedAds()
	{
		// Create our request used to load the ad.
		var adRequest = new AdRequest();

		// Send the request to load the ad.
		RewardedAd.Load(TestInterstitialAdUnitId, adRequest, (RewardedAd ad, LoadAdError error) =>
		{
			if (error != null)
			{
				// The ad failed to load.
				Debug.LogError("Rewarded ad failed to load: " + error);
				return;
			}
			_rewardedAd = ad;
			Debug.Log("Rewarded ad loaded successfully.");
			// The ad loaded successfully.
			_rewardedAd.OnAdPaid += (AdValue adValue) =>
			{
				// Raised when the ad is estimated to have earned money.
			};
			_rewardedAd.OnAdImpressionRecorded += () =>
			{
				// Raised when an impression is recorded for an ad.
			};
			_rewardedAd.OnAdClicked += () =>
			{
				// Raised when a click is recorded for an ad.
			};
			_rewardedAd.OnAdFullScreenContentOpened += () =>
			{
				// Raised when the ad opened full screen content.
			};
			_rewardedAd.OnAdFullScreenContentClosed += () =>
			{
				// Raised when the ad closed full screen content.
				Debug.Log("Rewarded ad closed full screen content.");

				// Reload the ad so that we can show another as soon as possible.
				var adRequest = new AdRequest();
				RewardedAd.Load(TestRewardedAdUnitId, adRequest, (RewardedAd ad, LoadAdError error) =>
				{
					// Handle ad loading here.
					_rewardedAd = ad;

				});
			};
			_rewardedAd.OnAdFullScreenContentFailed += (AdError error) =>
			{
				// Raised when the ad failed to open full screen content.
				Debug.LogError("Rewarded ad failed to open full screen content: " + error);
				DestroyRewardedAd();
			};
		});
	}

	public void DestroyRewardedAd()
	{
		if (_rewardedAd != null)
		{
			_rewardedAd.Destroy();
			_rewardedAd = null;
			Debug.Log("Rewarded ad destroyed.");
		}
	}
	 
	public void ShowRewardedAd()
	{
		if (_rewardedAd != null && _rewardedAd.CanShowAd())
		{
			_rewardedAd.Show((Reward reward) =>
			{
				// Handle the reward here.
				Debug.Log("User earned reward: " + reward.Amount);
			});
			Debug.Log("Rewarded ad is shown.");
		}
		else
		{
			Debug.Log("Rewarded ad is not ready yet.");
		}
	}


}

