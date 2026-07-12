using UnityEngine;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    [SerializeField] private Button classicButton;
    [SerializeField] private Button adventureButton;

    private void Start()
    {
        Debug.Log($"[MenuController] Start() called. classicButton={(classicButton != null ? classicButton.name : "NULL")}, adventureButton={(adventureButton != null ? adventureButton.name : "NULL")}");

        SetupButton(classicButton, () =>
        {
            Debug.Log("[MenuController] Classic button clicked!");
            if (AppManager.instance != null)
                AppManager.instance.LoadClassicGame();
        });

        SetupButton(adventureButton, () =>
        {
            Debug.Log("[MenuController] Adventure button clicked!");
            if (AppManager.instance != null)
                AppManager.instance.StartAdventureGameFromLobby();
        });
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
}
