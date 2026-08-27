using System;
using UnityEngine;

// SettingsManager – Singleton for persisted player preferences (music, vibration).
// Self-bootstrapping (no scene wiring needed), same pattern as VibrationManager, so the
// saved preference is available from the very first frame regardless of which scene loads first.
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager instance;

    private const string MusicEnabledKey = "settings_music_enabled";
    private const string VibrationEnabledKey = "settings_vibration_enabled";

    public event Action<bool> OnMusicEnabledChanged;
    public event Action<bool> OnVibrationEnabledChanged;

    public bool MusicEnabled { get; private set; } = true;
    public bool VibrationEnabled { get; private set; } = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        var go = new GameObject("SettingsManager");
        go.AddComponent<SettingsManager>();
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadPrefs();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadPrefs()
    {
        MusicEnabled = PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;
        VibrationEnabled = PlayerPrefs.GetInt(VibrationEnabledKey, 1) == 1;
    }

    public void SetMusicEnabled(bool isEnabled)
    {
        if (MusicEnabled == isEnabled)
            return;

        MusicEnabled = isEnabled;
        PlayerPrefs.SetInt(MusicEnabledKey, isEnabled ? 1 : 0);
        PlayerPrefs.Save();
        OnMusicEnabledChanged?.Invoke(isEnabled);
    }

    public void SetVibrationEnabled(bool isEnabled)
    {
        if (VibrationEnabled == isEnabled)
            return;

        VibrationEnabled = isEnabled;
        PlayerPrefs.SetInt(VibrationEnabledKey, isEnabled ? 1 : 0);
        PlayerPrefs.Save();
        OnVibrationEnabledChanged?.Invoke(isEnabled);
    }

    public void ToggleMusic() => SetMusicEnabled(!MusicEnabled);
    public void ToggleVibration() => SetVibrationEnabled(!VibrationEnabled);
}
