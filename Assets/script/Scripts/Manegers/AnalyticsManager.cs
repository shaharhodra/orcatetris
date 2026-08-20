using Firebase.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AnalyticsManager : Singleton<AnalyticsManager>
{
    /// <summary>
    /// Wall-clock stamp for the level currently being played, used to derive LevelTime.
    /// Set by <see cref="MarkLevelStarted"/> at the moment play actually begins.
    /// </summary>
    public float LevelStartTime { get; private set; }

    public enum AnalyticsEvent
    {
        GameStart,
        GameEnd,
        LevelStart,
        LevelComplete,
        LevelFail,
        LevelAbandon,
        LevelResumed,
        NoMoves,
        BoardCleared,
        ComboStep,
        DailyBonusClaimed,
        CoinsSpent,
        CoinsEarned,
        ReviveOffered,
        ReviveAccepted,
        ReviveDeclined,
        ReviveTimedOut,
        ReviveUnavailable,
        ReviveAdNotCompleted,
        AdRevenue,
        AdImpression,
        AdClicked,
        AdFailed,
        AdUnavailable,
        TutorialStep,
        RemoteConfigFetched
    }

    /// <summary>
    /// One analytics parameter, carrying its own type.
    ///
    /// This replaces a string-only version, and the distinction matters more than it looks: GA4
    /// registers a string parameter as a custom *dimension* and a numeric one as a custom *metric*,
    /// and only metrics can be summed or averaged. Sending a score or a duration as text produces
    /// a report where every distinct value is its own row and no aggregate is possible — data that
    /// looks fine going in and is worthless coming out. Since analytics data cannot be
    /// retroactively re-typed, this had to be fixed before any real collection started.
    ///
    /// Both numeric parameters still need registering as custom metrics in the GA4 console
    /// (Admin > Custom definitions) before they appear in reports. The property caps out at 50
    /// custom dimensions and 50 custom metrics.
    /// </summary>
    public readonly struct AnalyticsParam
    {
        private enum Kind { String, Long, Double }

        private readonly Kind kind;
        private readonly string name;
        private readonly string stringValue;
        private readonly long longValue;
        private readonly double doubleValue;

        private AnalyticsParam(Kind kind, string name, string s, long l, double d)
        {
            this.kind = kind;
            this.name = name;
            stringValue = s;
            longValue = l;
            doubleValue = d;
        }

        public static AnalyticsParam Of(string name, string value) =>
            new AnalyticsParam(Kind.String, name, value ?? string.Empty, 0, 0);

        public static AnalyticsParam Of(string name, long value) =>
            new AnalyticsParam(Kind.Long, name, null, value, 0);

        public static AnalyticsParam Of(string name, int value) =>
            new AnalyticsParam(Kind.Long, name, null, value, 0);

        public static AnalyticsParam Of(string name, double value) =>
            new AnalyticsParam(Kind.Double, name, null, 0, value);

        public static AnalyticsParam Of(string name, float value) =>
            new AnalyticsParam(Kind.Double, name, null, 0, value);

        public static AnalyticsParam Of(string name, bool value) =>
            new AnalyticsParam(Kind.Long, name, null, value ? 1 : 0, 0);

        public string Name => name;

        public Parameter ToFirebase()
        {
            switch (kind)
            {
                case Kind.Long: return new Parameter(name, longValue);
                case Kind.Double: return new Parameter(name, doubleValue);
                default: return new Parameter(name, stringValue);
            }
        }

        public override string ToString()
        {
            switch (kind)
            {
                case Kind.Long: return $"{name}: {longValue}";
                case Kind.Double: return $"{name}: {doubleValue}";
                default: return $"{name}: {stringValue}";
            }
        }
    }

    // Events fired before Firebase finishes initializing. GameStart is sent from AppManager.Start
    // on the very first frame, which is always before the dependency check can have completed, so
    // without this every session would lose its opening event — and with it the denominator for
    // every funnel.
    private readonly List<(string eventName, List<AnalyticsParam> parameters)> pendingEvents =
        new List<(string, List<AnalyticsParam>)>();

    private const int MaxPendingEvents = 64;

    public void SendEvent(string eventName, List<AnalyticsParam> eventData = null)
    {
        if (string.IsNullOrEmpty(eventName))
            return;

        var parameters = BuildParameters(eventData);

        Debug.Log($"[Analytics] {eventName} — {string.Join(", ", parameters.Select(p => p.ToString()))}");

        if (!FirebaseBootstrap.IsReady)
        {
            if (FirebaseBootstrap.IsUnavailable)
                return;

            if (pendingEvents.Count < MaxPendingEvents)
            {
                pendingEvents.Add((eventName, parameters));
                FirebaseBootstrap.RunWhenReady(FlushPendingEvents);
            }

            return;
        }

        Dispatch(eventName, parameters);
    }

    /// <summary>Convenience overload for the common single-parameter case.</summary>
    public void SendEvent(string eventName, AnalyticsParam parameter)
    {
        SendEvent(eventName, new List<AnalyticsParam> { parameter });
    }

    /// <summary>
    /// Stamps the start of play for LevelTime.
    ///
    /// Called explicitly rather than inferred from the event name, which is what the previous
    /// version did — it compared the event string inside SendEvent and stamped on LevelStart, and
    /// LevelStart is emitted while the level JSON is still being parsed. The resulting LevelTime
    /// included the three-second intro popup and any interstitial that happened to play, so every
    /// level looked several seconds harder than it was.
    /// </summary>
    public void MarkLevelStarted()
    {
        LevelStartTime = Time.realtimeSinceStartup;
    }

    public float GetElapsedLevelTime()
    {
        return LevelStartTime <= 0f ? 0f : Time.realtimeSinceStartup - LevelStartTime;
    }

    private void FlushPendingEvents()
    {
        if (pendingEvents.Count == 0)
            return;

        foreach (var (eventName, parameters) in pendingEvents)
            Dispatch(eventName, parameters);

        pendingEvents.Clear();
    }

    private static void Dispatch(string eventName, List<AnalyticsParam> parameters)
    {
        FirebaseAnalytics.LogEvent(eventName, parameters.Select(p => p.ToFirebase()).ToArray());
    }

    /// <summary>
    /// Appends the parameters every event carries.
    ///
    /// Level reports the level actually being played, read from the loaded LevelData. The previous
    /// version derived it as HighestUnlockedLevel + 1, which is the *next* level — so every
    /// LevelComplete and LevelFail was attributed to the wrong level, and precisely the reports
    /// used to find a difficulty spike pointed one level past it.
    /// </summary>
    private static List<AnalyticsParam> BuildParameters(List<AnalyticsParam> supplied)
    {
        var parameters = supplied != null
            ? new List<AnalyticsParam>(supplied)
            : new List<AnalyticsParam>();

        bool hasLevel = parameters.Any(p => p.Name == "Level");
        if (!hasLevel)
        {
            int level = ResolveCurrentLevel();
            if (level > 0)
                parameters.Add(AnalyticsParam.Of("Level", level));
        }

        if (PlayerManeger.instance != null && PlayerManeger.instance.PlayerProgress != null)
        {
            var progress = PlayerManeger.instance.PlayerProgress;
            parameters.Add(AnalyticsParam.Of("Coins", progress.Coins));
            parameters.Add(AnalyticsParam.Of("HighestUnlockedLevel", progress.HighestUnlockedLevel));
        }

        if (AppManager.instance != null)
            parameters.Add(AnalyticsParam.Of("GameMode", AppManager.instance.CurrentGameMode.ToString()));

        return parameters;
    }

    private static int ResolveCurrentLevel()
    {
        var levelData = AppManager.instance != null ? AppManager.instance.CurrentLevelData : null;
        if (levelData != null && levelData.Level > 0)
            return levelData.Level;

        // Outside a level (menu, lobby) there is no level being played; fall back to progress so
        // session-level events still carry a position in the campaign.
        if (PlayerManeger.instance != null && PlayerManeger.instance.PlayerProgress != null)
            return PlayerManeger.instance.PlayerProgress.HighestUnlockedLevel + 1;

        return 0;
    }
}
