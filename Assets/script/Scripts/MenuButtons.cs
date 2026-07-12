using UnityEngine;

public class MenuButtons : MonoBehaviour
{
    public void OnClassicClicked()
    {
        Debug.Log($"[MenuButtons] OnClassicClicked called! AppManager.instance={(AppManager.instance != null ? "EXISTS" : "NULL")}");
        if (SoundManager.instance != null)
            SoundManager.instance.PlayButtonClick();
        if (AppManager.instance != null)
            AppManager.instance.LoadClassicGame();
        else
            Debug.LogError("[MenuButtons] AppManager.instance is NULL!");
    }

    public void OnAdventureClicked()
    {
        Debug.Log($"[MenuButtons] OnAdventureClicked called! AppManager.instance={(AppManager.instance != null ? "EXISTS" : "NULL")}");
        if (SoundManager.instance != null)
            SoundManager.instance.PlayButtonClick();
        if (AppManager.instance != null)
            AppManager.instance.StartAdventureGameFromLobby();
        else
            Debug.LogError("[MenuButtons] AppManager.instance is NULL!");
    }

    public void OnAdventureStartFromLobby()
    {
        if (SoundManager.instance != null)
            SoundManager.instance.PlayButtonClick();
        if (AppManager.instance != null)
            AppManager.instance.StartAdventureGameFromLobby();
    }
}