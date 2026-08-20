using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using UnityEngine;

/// <summary>
/// Fetches Remote Config and hands the values to <see cref="GameSettings"/>.
///
/// Rewritten from a version that could not survive the situation it was actually shipped into: it
/// registered an empty defaults dictionary and then dereferenced the parsed result of a
/// <c>GameConfig</c> key without checking it existed. With an empty console — which is where this
/// project started — that path throws on the first launch of every session.
///
/// The shape now is: register every key from <see cref="RemoteConfigKeys"/> with the value the
/// build already uses, fetch, then stage whatever came back. Every failure mode (no network,
/// throttled, unavailable Firebase, key missing from the console) lands on the same outcome —
/// the game runs on its compiled-in defaults — instead of on an exception.
/// </summary>
public class RemoteConfigManager : Singleton<RemoteConfigManager>
{
    /// <summary>Raised on the main thread once a fetch attempt finishes, successfully or not.</summary>
    public event Action OnRemoteFetchCompleted;

    /// <summary>Outcome of the last fetch, for the <c>remote_config_fetched</c> analytics event.</summary>
    public string LastFetchResult { get; private set; } = "not_started";

    public bool HasFetched { get; private set; }

    public void StartRemoteConfigFetch()
    {
        // Deferred rather than called directly: Firebase products are not safe to touch until the
        // dependency check completes, and this runs from AppManager.Start on the first frame.
        FirebaseBootstrap.RunWhenReady(BeginFetch);

        // Firebase never coming up must not leave callers waiting on an event that will not fire.
        if (FirebaseBootstrap.IsUnavailable)
            CompleteWith("firebase_unavailable");
    }

    private void BeginFetch()
    {
        var remoteConfig = FirebaseRemoteConfig.DefaultInstance;

        remoteConfig.SetDefaultsAsync(RemoteConfigKeys.BuildDefaults())
            .ContinueWithOnMainThread(_ => FetchDataAsync());
    }

    public Task FetchDataAsync()
    {
        // TimeSpan.Zero bypasses the local cache so a console change shows up on the next launch
        // instead of up to 12 hours later. Firebase still throttles server-side; that surfaces as
        // LastFetchStatus.Failure with FetchFailureReason.Throttled, handled below.
        return FirebaseRemoteConfig.DefaultInstance
            .FetchAsync(TimeSpan.Zero)
            .ContinueWithOnMainThread(OnFetchComplete);
    }

    private void OnFetchComplete(Task fetchTask)
    {
        if (fetchTask.IsCanceled)
        {
            CompleteWith("canceled");
            return;
        }

        if (fetchTask.IsFaulted)
        {
            Debug.LogWarning($"[RemoteConfig] Fetch faulted: {fetchTask.Exception}");
            CompleteWith("faulted");
            return;
        }

        var info = FirebaseRemoteConfig.DefaultInstance.Info;

        switch (info.LastFetchStatus)
        {
            case LastFetchStatus.Success:
                FirebaseRemoteConfig.DefaultInstance.ActivateAsync()
                    .ContinueWithOnMainThread(_ =>
                    {
                        StageFetchedValues();
                        CompleteWith("success");
                    });
                return;

            case LastFetchStatus.Failure:
                CompleteWith(info.LastFetchFailureReason == FetchFailureReason.Throttled
                    ? "throttled"
                    : "failure");
                return;

            case LastFetchStatus.Pending:
                CompleteWith("pending");
                return;

            default:
                CompleteWith("unknown");
                return;
        }
    }

    /// <summary>
    /// Reads every declared key back out of Firebase and stages it. Only values Firebase reports as
    /// <see cref="ValueSource.RemoteValue"/> are taken — anything still on a static or default
    /// source is left alone so <see cref="GameSettings"/> keeps its own default rather than
    /// round-tripping the same number through a type conversion.
    /// </summary>
    private void StageFetchedValues()
    {
        var remoteConfig = FirebaseRemoteConfig.DefaultInstance;
        var staged = new Dictionary<string, object>();

        foreach (var kvp in RemoteConfigKeys.BuildDefaults())
        {
            ConfigValue configValue;
            try
            {
                configValue = remoteConfig.GetValue(kvp.Key);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RemoteConfig] Could not read '{kvp.Key}': {e.Message}");
                continue;
            }

            if (configValue.Source != ValueSource.RemoteValue)
                continue;

            // The declared default's type decides how the value is read — Firebase hands back an
            // untyped ConfigValue, and asking for the wrong accessor silently yields zero.
            try
            {
                switch (kvp.Value)
                {
                    case bool _:
                        staged[kvp.Key] = configValue.BooleanValue;
                        break;
                    case long _:
                    case int _:
                        staged[kvp.Key] = configValue.LongValue;
                        break;
                    case double _:
                    case float _:
                        staged[kvp.Key] = configValue.DoubleValue;
                        break;
                    default:
                        staged[kvp.Key] = configValue.StringValue;
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RemoteConfig] '{kvp.Key}' has an unusable value, keeping default: {e.Message}");
            }
        }

        Debug.Log($"[RemoteConfig] Staged {staged.Count} remote value(s); they take effect at the next level boundary.");
        GameSettings.StageRemoteValues(staged);
    }

    private void CompleteWith(string result)
    {
        LastFetchResult = result;
        HasFetched = true;

        Debug.Log($"[RemoteConfig] Fetch finished: {result}");

        AnalyticsManager.instance?.SendEvent(
            AnalyticsManager.AnalyticsEvent.RemoteConfigFetched.ToString(),
            new List<AnalyticsManager.AnalyticsParam>
            {
                AnalyticsManager.AnalyticsParam.Of("Result", result),
            });

        OnRemoteFetchCompleted?.Invoke();
    }
}
