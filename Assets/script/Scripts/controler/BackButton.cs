using UnityEngine;
using UnityEngine.SceneManagement;

public class BackButton : MonoBehaviour
{
    public void OnBackClicked()
    {
        // Abandoning a level mid-play. This is the one exit that was never measured, and it is the
        // one that matters most for difficulty: a level people quit halfway through looks identical
        // in the numbers to a level nobody reached, unless it is reported here.
        if (AnalyticsManager.instance != null)
        {
            AnalyticsManager.instance.SendEvent(
                AnalyticsManager.AnalyticsEvent.LevelAbandon.ToString(),
                new System.Collections.Generic.List<AnalyticsManager.AnalyticsParam>
                {
                    AnalyticsManager.AnalyticsParam.Of("LevelTime", AnalyticsManager.instance.GetElapsedLevelTime()),
                });
        }

        if (GameManager.instance.CurrentGameMode == GameManager.GameMode.Adventure)
        {
            // שמירת מצב המשחק הנוכחי כדי לחזור אליו כשהשחקן ייכנס שוב לאותו שלב
            AdventureSessionCache.CaptureCurrentScene();

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