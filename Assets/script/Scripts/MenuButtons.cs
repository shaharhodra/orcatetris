using UnityEngine;

public class MenuButtons : MonoBehaviour
{
    public void OnClassicClicked()
    {
        if (AppManager.instance != null)
            AppManager.instance.LoadClassicGame();
    }

    public void OnAdventureClicked()
    {
        if (AppManager.instance != null)
            AppManager.instance.LoadAdventureLobby();
    }

    public void OnAdventureStartFromLobby()
    {
        if (AppManager.instance != null)
            AppManager.instance.StartAdventureGameFromLobby();
    }
}