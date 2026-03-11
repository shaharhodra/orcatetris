using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyPlayButton : MonoBehaviour
{
    // אם ה-BuildIndex של כל סצנת לבל תואם ל-LevelIndex, אפשר פשוט לטעון לפי אינדקס.
    // אם אתה משתמש בשמות סצנות אחרים, אפשר להחליף את ה-LoadScene בהתאם.

    public void OnPlayClicked()
    {
        SceneManager.LoadScene((int)AppManager.SceneType.Game );
    }
}
