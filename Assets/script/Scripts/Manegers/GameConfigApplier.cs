using OrcaTetris.Adventure;
using UnityEngine;

/// <summary>
/// Pushes <see cref="GameSettings"/> values into the places that actually read them.
///
/// Two different kinds of target, for two different reasons:
///
/// The Adventure curve and tray-selection values live in <c>OrcaTetris.Adventure.Core</c>, an
/// assembly that deliberately does not reference this one — that isolation is what lets the EditMode
/// solvability tests drive the real selection policy. So configuration cannot be pulled from there;
/// it has to be pushed in from here.
///
/// The rest are <c>[SerializeField]</c> fields on scene components, which Unity has already
/// overwritten from the scene file by the time anything runs. Those are found and written per
/// scene, after Awake.
///
/// Runs on every settings change and once at startup, so the editor and a build behave the same
/// whether or not a fetch ever succeeds.
/// </summary>
public static class GameConfigApplier
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        ApplyGlobals();
        GameSettings.OnSettingsApplied -= HandleSettingsApplied;
        GameSettings.OnSettingsApplied += HandleSettingsApplied;
    }

    private static void HandleSettingsApplied()
    {
        ApplyGlobals();
        ApplySceneComponents();
    }

    /// <summary>
    /// Values held as statics — no scene lookup needed, and safe to apply before any scene loads.
    /// </summary>
    public static void ApplyGlobals()
    {
        AdventureLevelCurves.FirstGeneratedLevel = GameSettings.GetInt(RemoteConfigKeys.AdvFirstGeneratedLevel);
        AdventureLevelCurves.SaturationLevel = GameSettings.GetInt(RemoteConfigKeys.AdvSizeSaturationLevel);
        AdventureLevelCurves.DifficultySaturationLevel = GameSettings.GetInt(RemoteConfigKeys.AdvDifficultySaturationLevel);
        AdventureLevelCurves.DifficultyCeiling = GameSettings.GetInt(RemoteConfigKeys.AdvDifficultyCeiling);
        AdventureLevelCurves.InLevelDifficultyPeak = GameSettings.GetInt(RemoteConfigKeys.AdvInLevelDifficultyPeak);
        AdventureLevelCurves.TargetFloor = GameSettings.GetInt(RemoteConfigKeys.AdvTargetFloor);
        AdventureLevelCurves.TargetCeiling = GameSettings.GetInt(RemoteConfigKeys.AdvTargetCeiling);
        AdventureLevelCurves.FillFloor = GameSettings.GetInt(RemoteConfigKeys.AdvFillFloor);
        AdventureLevelCurves.FillCeiling = GameSettings.GetInt(RemoteConfigKeys.AdvFillCeiling);
        AdventureLevelCurves.MinPerType = GameSettings.GetInt(RemoteConfigKeys.AdvMinPerType);

        TraySelectionCore.JackpotCapReliefFill = GameSettings.GetFloat(RemoteConfigKeys.TrayJackpotReliefFill);
        TraySelectionCore.RobustAnchorCap = GameSettings.GetInt(RemoteConfigKeys.TrayRobustAnchorCap);
        TraySelectionCore.MinSafeAnchors = GameSettings.GetInt(RemoteConfigKeys.TrayMinSafeAnchors);
    }

    /// <summary>
    /// Writes the serialized-field values onto whatever components exist in the current scene.
    ///
    /// Called from <see cref="GameConfigSceneBinder"/> after Awake, because Unity applies scene
    /// overrides during load — anything written earlier is overwritten by the scene file. Scene
    /// values are also frequently NOT the code defaults (scorePerPlacedCell is 1 in code and 10 in
    /// the scene), which is why the defaults in RemoteConfigKeys were read from the scene.
    /// </summary>
    public static void ApplySceneComponents()
    {
        var tray = Object.FindFirstObjectByType<ShapeTrayManager>();
        if (tray != null)
            tray.ApplyRemoteSettings();

        var placer = Object.FindFirstObjectByType<GridPlacer>();
        if (placer != null)
            placer.ApplyRemoteSettings();

        var board = Object.FindFirstObjectByType<GridBoard>();
        if (board != null)
            board.ApplyRemoteSettings();

        var revive = Object.FindFirstObjectByType<ReviveManager>();
        if (revive != null)
            revive.ApplyRemoteSettings();

        // Lives in the menu scene rather than the game scene, hence the separate lookup.
        var player = Object.FindFirstObjectByType<PlayerManeger>();
        if (player != null)
            player.ApplyRemoteSettings();

        // Lobby scene.
        var gift = Object.FindFirstObjectByType<GiftPopupController>();
        if (gift != null)
            gift.ApplyRemoteSettings();
    }
}

/// <summary>
/// Applies scene-component settings once the scene's own Awake pass has finished.
/// Self-instantiating so no scene needs a prefab added to it.
/// </summary>
public class GameConfigSceneBinder : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bind()
    {
        GameConfigApplier.ApplySceneComponents();

        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        GameConfigApplier.ApplySceneComponents();
    }
}
