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
                if (GameManager.instance != null)
                    GameManager.instance.LoadClassicGame();
            });
        }

        if (adventureButton != null)
        {
            adventureButton.onClick.RemoveAllListeners();
            adventureButton.onClick.AddListener(() =>
            {
                if (GameManager.instance != null)
                    GameManager.instance.LoadAdventureLobby();
            });
        }
    }
}
