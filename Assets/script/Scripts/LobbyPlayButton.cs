using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class LobbyPlayButton : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI levelLabel;

    private void Start()
    {
        GiftPopupController.OnGiftClosed += RefreshLabel;
        RefreshLabel();
    }

    private void OnDestroy()
    {
        GiftPopupController.OnGiftClosed -= RefreshLabel;
    }

    private void RefreshLabel()
    {
        if (levelLabel == null || PlayerManeger.instance == null)
            return;

        int levelIndex = PlayerManeger.instance.PlayerProgress.DisplayLevel;
        int displayLevel = Mathf.Max(0, levelIndex) + 1;
        levelLabel.text = $"Level {displayLevel}";
    }

    public void OnPlayClicked()
    {
        if (AppManager.instance != null)
            AppManager.instance.StartAdventureGameFromLobby();
        else
            SceneManager.LoadScene((int)AppManager.SceneType.Game);
    }
}
