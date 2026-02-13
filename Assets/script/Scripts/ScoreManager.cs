using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    private const string MaxScoreKey = "MaxScore";

    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text maxScoreText;

    public int Score { get; private set; }
    public int MaxScore { get; private set; }

    private void Awake()
    {
        LoadMaxScore();
        UpdateUI();
    }

    public void ResetScoreForLevel()
    {
        Score = 0;
        UpdateUI();
    }

    public void AddScore(int amount)
    {
        if (amount <= 0)
            return;

        Score += amount;
        TryUpdateMaxScore();
        UpdateUI();
    }

    public void SetScore(int value)
    {
        Score = Mathf.Max(0, value);
        TryUpdateMaxScore();
        UpdateUI();
    }

    private void TryUpdateMaxScore()
    {
        if (Score <= MaxScore)
            return;

        MaxScore = Score;
        PlayerPrefs.SetInt(MaxScoreKey, MaxScore);
        PlayerPrefs.Save();
    }

    private void LoadMaxScore()
    {
        MaxScore = PlayerPrefs.GetInt(MaxScoreKey, 0);
    }

    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = Score.ToString();

        if (maxScoreText != null)
            maxScoreText.text = MaxScore.ToString();
    }
}
