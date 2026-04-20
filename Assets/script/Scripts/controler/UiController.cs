using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class UiController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private TextMeshProUGUI _highScoreText;


    void Start()
    {
        var app = AppManager.instance;
        var scene = SceneManager.GetActiveScene();

        // Hide score UI only when we are in Adventure mode AND on the main gameplay scene.
        // In the menu scene, the top bar should remain visible even if game mode is Adventure.
        if (app != null &&
            app.CurrentGameMode == AppManager.GameMode.Adventure &&
            scene.buildIndex == app.ClassicGameSceneBuildIndex)
        {
            gameObject.SetActive(false);
            return;
        }

        ScoreManager.instance.OnScoreUpdatedEvent += HandleOnScroreUpdatedEvent;
        ScoreManager.instance.OnMaxScoreUpdatedEvent += HandleOnMaxScroreUpdatedEvent;

        // Initialize UI immediately with current values so player sees score and high score at scene start.
        if (ScoreManager.instance != null)
        {
            HandleOnScroreUpdatedEvent(ScoreManager.instance.Score);
            HandleOnMaxScroreUpdatedEvent(ScoreManager.instance.MaxScore);
        }
    }

    void OnDestroy()
    {
        ScoreManager.instance.OnScoreUpdatedEvent -= HandleOnScroreUpdatedEvent;
        ScoreManager.instance.OnMaxScoreUpdatedEvent -= HandleOnMaxScroreUpdatedEvent;
    }

    private void HandleOnScroreUpdatedEvent (int score)
    {
        _scoreText.text = score.ToString();
    }

    private void HandleOnMaxScroreUpdatedEvent (int maxScore)
    {
       // Debug.Log($"[UiController] HighScore UI updated to {maxScore}");
        _highScoreText.text = maxScore.ToString();
    }
}
