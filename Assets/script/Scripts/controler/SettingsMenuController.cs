using UnityEngine;
using UnityEngine.UI;

// Drives the in-game settings panel: gear button opens it, close button hides it,
// and its two toggles read/write SettingsManager (music + vibration).
public class SettingsMenuController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button closeButton;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("Toggles")]
    [SerializeField] private Toggle musicToggle;
    [SerializeField] private Toggle vibrationToggle;

    private void Start()
    {
        SetupButton(settingsButton, Open);
        SetupButton(closeButton, Close);

        if (panelRoot != null)
            panelRoot.SetActive(false);

        SetupToggle(musicToggle, SettingsManager.instance.MusicEnabled, OnMusicToggleChanged);
        SetupToggle(vibrationToggle, SettingsManager.instance.VibrationEnabled, OnVibrationToggleChanged);
    }

    private void Open()
    {
        SoundManager.instance?.PlayButtonClick();
        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    private void Close()
    {
        SoundManager.instance?.PlayButtonClick();
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnMusicToggleChanged(bool isOn)
    {
        SettingsManager.instance.SetMusicEnabled(isOn);
    }

    private void OnVibrationToggleChanged(bool isOn)
    {
        SettingsManager.instance.SetVibrationEnabled(isOn);
    }

    private void SetupButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;

        // Remove all persistent (Inspector) and runtime listeners to avoid stale references
        button.onClick.RemoveAllListeners();
        int persistentCount = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < persistentCount; i++)
            button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);

        button.onClick.AddListener(action);
    }

    private void SetupToggle(Toggle toggle, bool initialValue, UnityEngine.Events.UnityAction<bool> action)
    {
        if (toggle == null) return;

        toggle.onValueChanged.RemoveAllListeners();
        int persistentCount = toggle.onValueChanged.GetPersistentEventCount();
        for (int i = 0; i < persistentCount; i++)
            toggle.onValueChanged.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);

        toggle.SetIsOnWithoutNotify(initialValue);
        toggle.onValueChanged.AddListener(action);
    }
}
