using Firebase.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

public class RemoteConfigManager : Singleton<RemoteConfigManager>
{
	public event Action OnRemoteFetchCompleted;
	public RemoteConfigData GameConfig;
	public LevelData LevelData;
	public int InterstitialAdsInterval;

	[Serializable]
	public class RemoteConfigData
	{
		public int InterstitialAdsInterval;
	}

	public void StartRemoteConfigFetch()
	{

		Dictionary<string, object> defaults = new Dictionary<string, object>();

		Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(defaults);

		FetchDataAsync();
    }


	public Task FetchDataAsync()
	{
		Debug.Log("Fetching remote config data...");
		Task fetchTask = Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.FetchAsync(TimeSpan.Zero);
		return fetchTask.ContinueWithOnMainThread(OnFetchComplete);

	}

	public void OnFetchComplete(Task fetchTask)
	{
		if (fetchTask.IsCanceled)
		{
			Debug.Log("---------------------Fetch canceled.");
		}
		else if (fetchTask.IsFaulted)
		{
			Debug.Log("---------------------Fetch encountered an error." + fetchTask.Exception);
		}
		else if (fetchTask.IsCompleted)
		{
			Debug.Log("---------------------Fetch completed successfully!");
		}

		var info = Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.Info;

		switch (info.LastFetchStatus)
		{
			case Firebase.RemoteConfig.LastFetchStatus.Success:

				Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.ActivateAsync();

				var json = Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue("GameConfig").StringValue;
				GameConfig = JsonUtility.FromJson<RemoteConfigData>(json);
				InterstitialAdsInterval= GameConfig.InterstitialAdsInterval;


				var leveljson = Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue("Level1").StringValue;
				LevelData = JsonUtility.FromJson<LevelData>(leveljson);
				//CurrentMergeFieldDataJson = Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue("").StringValue;
				//InterstitialAdsInterval = (int)Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue("InterstitialAdsInterval").DoubleValue;
				//ItemHolderAdsActive = Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue("").BooleanValue;

				break;

			case Firebase.RemoteConfig.LastFetchStatus.Failure:

				switch (info.LastFetchFailureReason)
				{
					case Firebase.RemoteConfig.FetchFailureReason.Error:
						Debug.Log("------------------Fetch failed for unknown reason");
						break;
					case Firebase.RemoteConfig.FetchFailureReason.Throttled:
						Debug.Log("Fetch throttled until " + info.ThrottledEndTime);
						break;
				}
				break;

			case Firebase.RemoteConfig.LastFetchStatus.Pending:
				Debug.Log("---------------Latest Fetch call still pending.");
				break;
		}

		OnRemoteFetchCompleted?.Invoke();

	}
}
