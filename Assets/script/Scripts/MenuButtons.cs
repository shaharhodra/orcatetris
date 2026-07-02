using UnityEngine;

public class MenuButtons : MonoBehaviour
{
    public void OnClassicClicked()
    {
        if (SoundManager.instance != null)
            SoundManager.instance.PlayButtonClick();
        if (AppManager.instance != null)
            AppManager.instance.LoadClassicGame();
    }

    public void OnAdventureClicked()
    {
        if (SoundManager.instance != null)
            SoundManager.instance.PlayButtonClick();
        if (AppManager.instance != null)
            AppManager.instance.StartAdventureGameFromLobby();
    }

    public void OnAdventureStartFromLobby()
    {
        if (SoundManager.instance != null)
            SoundManager.instance.PlayButtonClick();
        if (AppManager.instance != null)
            AppManager.instance.StartAdventureGameFromLobby();
    }
}