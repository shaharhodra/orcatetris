using UnityEngine;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    [SerializeField] private Button classicButton;
    [SerializeField] private Button adventureButton;

    private void Start()
    {
        if (classicButton != null)
        {
            classicButton.onClick.RemoveAllListeners();
            classicButton.onClick.AddListener(() =>
            {
                if (AppManager.instance != null)
                    AppManager.instance.LoadClassicGame();
            });
        }

        if (adventureButton != null)
        {
            adventureButton.onClick.RemoveAllListeners();
            adventureButton.onClick.AddListener(() =>
            {
                if (AppManager.instance != null)
                    AppManager.instance.StartAdventureGameFromLobby();
            });
        }
    }
}
