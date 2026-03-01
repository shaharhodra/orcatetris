using UnityEngine;
using System;

public class ScoreManager : Singleton<ScoreManager>
{
    public event Action<int> OnScoreUpdatedEvent;
    public event Action<int> OnMaxScoreUpdatedEvent;

    public int Score { get; private set; }
    public int MaxScore { get; private set; }

    public const string HIGH_SCORE = "high_score";
    public const string PLAYER_DATA = "player_data";

    void Start()
    {
        MaxScore = GetUserHighScore();
        OnMaxScoreUpdatedEvent?.Invoke(MaxScore);
    }

    public void InvokeOnScoreUpdatedEvent (int score)
    {
        Score = score;
        OnScoreUpdatedEvent?.Invoke(score);
    }

    public void InvokeOnMaxScoreUpdatedEvent (int maxScore)
    {
        MaxScore = maxScore;
        OnMaxScoreUpdatedEvent?.Invoke(maxScore);
    }

    public void UpdateScore (int addedScore)
    {
        Score += addedScore;
        Debug.Log($"[ScoreManager] Score updated to {Score}, MaxScore={MaxScore}");
        OnScoreUpdatedEvent?.Invoke(Score);

        if (Score > MaxScore)
        {
            MaxScore = Score;
            Debug.Log($"[ScoreManager] New HIGH SCORE! MaxScore updated to {MaxScore}");
            OnMaxScoreUpdatedEvent?.Invoke(MaxScore);
            SaveUserScore(MaxScore);
        }
    }

    public void AddScore(int amount)
    {
        UpdateScore(amount);
    }

    public void ResetScore()
    {
        Score = 0;
        OnScoreUpdatedEvent?.Invoke(Score);
    }

    public void UpdateMaxScore(int addedScore)
    {
        MaxScore += addedScore;
        OnMaxScoreUpdatedEvent?.Invoke(MaxScore);
    }

    // Needs to be in dedicated manager - not in score manager
    public void SavePlayerData (PlayerData playerData)
    {
        var json = JsonUtility.ToJson(playerData);
        PlayerPrefs.SetString(PLAYER_DATA, json);
    }

    // Needs to be in dedicated manager - not in score manager
    public PlayerData GetPlayerData ()
    {
        if (PlayerPrefs.HasKey(PLAYER_DATA))
        {
            var json = PlayerPrefs.GetString(PLAYER_DATA);
            return JsonUtility.FromJson<PlayerData>(json);
        }
        else
            return null;
    }

    public void SaveUserScore (int score)
    {
        PlayerPrefs.SetInt(HIGH_SCORE, score);
        PlayerPrefs.Save();
    }

    public int GetUserHighScore ()
    {
        if (PlayerPrefs.HasKey(HIGH_SCORE))
            return PlayerPrefs.GetInt(HIGH_SCORE);
        else
            return 0;
    }
}
