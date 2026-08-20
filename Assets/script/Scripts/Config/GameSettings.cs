using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one place runtime code reads a tunable value from.
///
/// Everything goes through here rather than touching <c>FirebaseRemoteConfig.DefaultInstance</c>
/// directly, for three reasons. It works before — and without — a successful fetch, falling back to
/// <see cref="RemoteConfigKeys.BuildDefaults"/>, so the editor, offline players and a throttled
/// fetch all behave identically to a shipped build. It gives the const-to-static refactor in
/// <c>AdventureLevelCurves</c> and <c>TraySelectionCore</c> a single well-defined source. And if
/// value clamping is ever wanted, this is the only file that would need it — see the note on
/// <see cref="ApplyPending"/>.
///
/// === Why config is snapshotted rather than read live ===
///
/// Adventure difficulty values are read while a level is being generated and while its tray is
/// being refilled. If a fetch activated mid-level, the board would get harder under the player's
/// hands halfway through a level they were already losing — indistinguishable from a bug, and
/// impossible to reproduce. So a fetched config is held in <see cref="pendingValues"/> and only
/// swapped in at a level boundary via <see cref="ApplyPending"/>. Within a level, values are
/// frozen.
/// </summary>
public static class GameSettings
{
    private static Dictionary<string, object> activeValues = RemoteConfigKeys.BuildDefaults();
    private static Dictionary<string, object> pendingValues;

    /// <summary>Raised after <see cref="ApplyPending"/> swaps in new values, so cached consumers can re-read.</summary>
    public static event Action OnSettingsApplied;

    /// <summary>True once a fetched config has been staged or applied — useful as an analytics dimension.</summary>
    public static bool HasRemoteValues { get; private set; }

    /// <summary>
    /// Stages a freshly fetched config. Not visible to readers until <see cref="ApplyPending"/>.
    /// </summary>
    public static void StageRemoteValues(Dictionary<string, object> values)
    {
        if (values == null || values.Count == 0)
            return;

        pendingValues = values;
    }

    /// <summary>
    /// Promotes staged values to active. Call at a level boundary only — never mid-level.
    ///
    /// This is also the single choke point where value clamping would go, if the "anything goes"
    /// stance on remote values is ever revisited. Nothing validates these numbers today, and the
    /// solvability suite only ever exercises the compiled-in defaults, so a bad value published to
    /// the console reaches every player without any test having had a chance to object.
    /// </summary>
    public static void ApplyPending()
    {
        if (pendingValues == null)
            return;

        // Start from defaults so a key deleted in the console reverts to shipped behaviour rather
        // than keeping whatever it happened to be last session.
        var merged = RemoteConfigKeys.BuildDefaults();
        foreach (var kvp in pendingValues)
            merged[kvp.Key] = kvp.Value;

        activeValues = merged;
        pendingValues = null;
        HasRemoteValues = true;

        OnSettingsApplied?.Invoke();
    }

    public static bool GetBool(string key)
    {
        return TryGet(key, out var value) && Convert.ToBoolean(value);
    }

    public static int GetInt(string key)
    {
        return TryGet(key, out var value) ? Convert.ToInt32(value) : 0;
    }

    public static long GetLong(string key)
    {
        return TryGet(key, out var value) ? Convert.ToInt64(value) : 0L;
    }

    public static float GetFloat(string key)
    {
        return TryGet(key, out var value) ? Convert.ToSingle(value) : 0f;
    }

    public static double GetDouble(string key)
    {
        return TryGet(key, out var value) ? Convert.ToDouble(value) : 0d;
    }

    public static string GetString(string key)
    {
        return TryGet(key, out var value) ? Convert.ToString(value) : string.Empty;
    }

    private static bool TryGet(string key, out object value)
    {
        if (activeValues.TryGetValue(key, out value))
            return true;

        // A key read but never registered is a code bug, not a config problem — the console can
        // only serve keys that BuildDefaults declares.
        Debug.LogError($"[GameSettings] Unknown key '{key}'. Add it to RemoteConfigKeys.BuildDefaults.");
        return false;
    }

    /// <summary>Test hook: restore compiled-in defaults and drop anything staged.</summary>
    public static void ResetToDefaultsForTests()
    {
        activeValues = RemoteConfigKeys.BuildDefaults();
        pendingValues = null;
        HasRemoteValues = false;
    }
}
