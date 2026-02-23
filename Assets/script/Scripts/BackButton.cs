using UnityEngine;
using UnityEngine.SceneManagement;

public class BackButton : MonoBehaviour
{
    public void OnBackClicked()
    {
        if (GameManager.instance.CurrentGameMode == GameManager.GameMode.Adventure)
        {
            // חזרה ללובי של האדוונצ'ר
            SceneManager.LoadScene("adventureMOdeLoby");   // או לפי BuildIndex אם אתה מעדיף
        }
        else
        {
            // חזרה ללובי הקלאסי (menu)
            SceneManager.LoadScene("menu");                // או BuildIndex של menu
        }
    }
}