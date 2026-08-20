using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Extensions;
using UnityEngine;

/// <summary>
/// Brings the Firebase SDK up before anything touches it.
///
/// Firebase requires <c>CheckAndFixDependenciesAsync</c> to complete before any product
/// (Analytics, Remote Config, Crashlytics) is safe to use — on Android it may need to prompt for a
/// Play Services update first. Nothing in this project was doing that: <c>AnalyticsManager</c> and
/// <c>RemoteConfigManager</c> both reached for <c>DefaultInstance</c> on the first frame. On a
/// healthy device that happens to work, which is exactly why it went unnoticed; on a device that
/// needs fixing it fails silently and the whole session goes unreported.
///
/// Deliberately a static class driven by <see cref="RuntimeInitializeOnLoadMethodAttribute"/>
/// rather than a <c>Singleton&lt;T&gt;</c> MonoBehaviour: those need a GameObject placed in every
/// scene that might run first, and analytics that silently stops because somebody opened a scene
/// without the prefab is worse than no analytics. This has nothing to wire up and cannot be
/// left out of a scene.
///
/// Consumers call <see cref="RunWhenReady"/> rather than checking a flag, so "already up" and
/// "not up yet" are the same call and no caller has to remember to handle both.
/// </summary>
public static class FirebaseBootstrap
{
    /// <summary>Dependencies resolved; Firebase products are safe to touch.</summary>
    public static bool IsReady { get; private set; }

    /// <summary>
    /// Dependencies could not be resolved on this device. Terminal — nothing queued will ever run,
    /// and further <see cref="RunWhenReady"/> calls are dropped rather than leaking a growing queue.
    /// </summary>
    public static bool IsUnavailable { get; private set; }

    public static DependencyStatus Status { get; private set; } = DependencyStatus.UnavailableOther;

    private static readonly List<Action> PendingActions = new List<Action>();
    private static bool initializationStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (initializationStarted)
            return;

        initializationStarted = true;

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirebaseBootstrap] Dependency check failed: {task.Exception}");
                MarkUnavailable();
                return;
            }

            Status = task.Result;

            if (Status != DependencyStatus.Available)
            {
                Debug.LogError($"[FirebaseBootstrap] Firebase unavailable: {Status}. " +
                    "Analytics and Remote Config are disabled for this session; the game runs on local defaults.");
                MarkUnavailable();
                return;
            }

            // Touching DefaultInstance is what actually creates the app object. Do it here, once,
            // where a failure is visible, instead of leaving it to whichever product happens to be
            // called first.
            _ = FirebaseApp.DefaultInstance;

            // Crashlytics ships in the project and was never switched on. It needs no API beyond
            // being initialized — from here on, native and managed crashes are reported.
            Firebase.Crashlytics.Crashlytics.ReportUncaughtExceptionsAsFatal = true;

            IsReady = true;
            Debug.Log("[FirebaseBootstrap] Firebase ready.");

            FlushPending();
        });
    }

    /// <summary>
    /// Runs <paramref name="action"/> once Firebase is usable — immediately if it already is.
    /// Silently drops the action if Firebase turned out to be unavailable on this device, so
    /// callers never need a fallback path for something they cannot do anything about.
    /// </summary>
    public static void RunWhenReady(Action action)
    {
        if (action == null)
            return;

        if (IsUnavailable)
            return;

        if (IsReady)
        {
            action();
            return;
        }

        PendingActions.Add(action);
    }

    private static void MarkUnavailable()
    {
        IsUnavailable = true;
        PendingActions.Clear();
    }

    private static void FlushPending()
    {
        // Copied before running: a queued action may itself queue more work, and mutating the list
        // mid-iteration would throw.
        var toRun = new List<Action>(PendingActions);
        PendingActions.Clear();

        foreach (var action in toRun)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError($"[FirebaseBootstrap] Queued action threw: {e}");
            }
        }
    }
}
