using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyPlayButton : MonoBehaviour
{
    // אם ה-BuildIndex של כל סצנת לבל תואם ל-LevelIndex, אפשר פשוט לטעון לפי אינדקס.
    // אם אתה משתמש בשמות סצנות אחרים, אפשר להחליף את ה-LoadScene בהתאם.

    public void OnPlayClicked()
    {
        if (GameManager.instance == null)
            return;

        int levelIndex = GameManager.instance.HighestUnlockedLevel;
        // כאן אני מניח שה-Build Index של הסצנה = levelIndex
        int sceneIndex = Mathf.Max(1, levelIndex);
        Debug.Log($"LobbyPlayButton -> OnPlayClicked loading scene buildIndex={sceneIndex} (HighestUnlockedLevel={levelIndex})");
        SceneManager.LoadScene(sceneIndex);
    }
}
