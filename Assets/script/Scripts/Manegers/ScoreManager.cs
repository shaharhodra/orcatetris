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

    [Header("Animated Score")]
    [Tooltip("Duration (seconds) to animate each score change from the current displayed value to the new target.")]
    [SerializeField] private float scoreAnimationDuration = 1f; // seconds per animation

    [Header("Coins")]
    [Tooltip("Every this many points awards coinsPerThreshold coins.")]
    [SerializeField] private int scorePerCoinThreshold = 1000;
    [SerializeField] private int coinsPerThreshold = 10;

    private int lastAwardedThreshold;

    private Coroutine scoreCoroutine;
    private int targetScore;
    private float animationStartTime;
    private int animationStartScore;

    void Start()
    {
        GameManager.instance.OnLevelRestartedEvent += HandleOnLevelRestartedEvent;

        MaxScore = GetUserHighScore();
        OnMaxScoreUpdatedEvent?.Invoke(MaxScore);
    }

    void OnDestroy()
    {
        GameManager.instance.OnLevelRestartedEvent -= HandleOnLevelRestartedEvent;
    }

    public void InvokeOnScoreUpdatedEvent(int score)
    {
        // Immediate set and cancel any running animation
        if (scoreCoroutine != null)
        {
            StopCoroutine(scoreCoroutine);
            scoreCoroutine = null;
        }

        Score = score;
        targetScore = Score;
        OnScoreUpdatedEvent?.Invoke(score);
        CheckAndAwardCoins(Score);

        if (Score > MaxScore)
        {
            MaxScore = Score;
            OnMaxScoreUpdatedEvent?.Invoke(MaxScore);
            SaveUserScore(MaxScore);
        }
    }

    public void InvokeOnMaxScoreUpdatedEvent(int maxScore)
    {
        MaxScore = maxScore;
        OnMaxScoreUpdatedEvent?.Invoke(maxScore);
    }

    // Public API: will animate the displayed score from current value to the new target over scoreAnimationDuration seconds.
    public void UpdateScore(int addedScore)
    {
        if (addedScore <= 0)
            return;

        // accumulate target
        targetScore = Mathf.Max(targetScore, Score) + addedScore;

        // restart animation from current displayed Score toward updated target
        if (scoreCoroutine != null)
        {
            StopCoroutine(scoreCoroutine);
            scoreCoroutine = null;
        }

        animationStartScore = Score;
        animationStartTime = Time.time;
        scoreCoroutine = StartCoroutine(AnimateScoreToTarget());
        // Debug.Log($"[ScoreManager] Animating score from {animationStartScore} to {targetScore} over {scoreAnimationDuration}s");
    }

    public void AddScore(int amount)
    {
        UpdateScore(amount);
    }

    public void ResetScore()
    {
        if (scoreCoroutine != null)
        {
            StopCoroutine(scoreCoroutine);
            scoreCoroutine = null;
        }

        Score = 0;
        targetScore = 0;
        lastAwardedThreshold = 0;
        OnScoreUpdatedEvent?.Invoke(Score);
    }

    private void CheckAndAwardCoins(int currentScore)
    {
        if (scorePerCoinThreshold <= 0)
            return;

        int currentThreshold = currentScore / scorePerCoinThreshold;
        int previousThreshold = lastAwardedThreshold;

        if (currentThreshold > previousThreshold)
        {
            int newThresholds = currentThreshold - previousThreshold;
            int coinsToAward = newThresholds * coinsPerThreshold;
            lastAwardedThreshold = currentThreshold;

            if (PlayerManeger.instance != null)
                PlayerManeger.instance.AddCoins(coinsToAward);
        }
    }

    public void UpdateMaxScore(int addedScore)
    {
        MaxScore += addedScore;
        OnMaxScoreUpdatedEvent?.Invoke(MaxScore);
    }

    // Needs to be in dedicated manager - not in score manager
    public void SavePlayerData(PlayerData playerData)
    {
        var json = JsonUtility.ToJson(playerData);
        PlayerPrefs.SetString(PLAYER_DATA, json);
    }

    // Needs to be in dedicated manager - not in score manager
    public PlayerData GetPlayerData()
    {
        if (PlayerPrefs.HasKey(PLAYER_DATA))
        {
            var json = PlayerPrefs.GetString(PLAYER_DATA);
            return JsonUtility.FromJson<PlayerData>(json);
        }
        else
            return null;
    }

    public void SaveUserScore(int score)
    {
        PlayerPrefs.SetInt(HIGH_SCORE, score);
        PlayerPrefs.Save();
    }

    public int GetUserHighScore()
    {
        if (PlayerPrefs.HasKey(HIGH_SCORE))
            return PlayerPrefs.GetInt(HIGH_SCORE);
        else
            return 0;
    }

    private System.Collections.IEnumerator AnimateScoreToTarget()
    {
        if (scoreAnimationDuration <= 0f)
        {
            Score = targetScore;
            OnScoreUpdatedEvent?.Invoke(Score);
            scoreCoroutine = null;
            if (Score > MaxScore)
            {
                MaxScore = Score;
                OnMaxScoreUpdatedEvent?.Invoke(MaxScore);
                SaveUserScore(MaxScore);
            }
            yield break;
        }

        while (Score != targetScore)
        {
            float t = (Time.time - animationStartTime) / scoreAnimationDuration;
            t = Mathf.Clamp01(t);

            // Interpolate and round to int for smooth counting
            float interpolated = Mathf.Lerp(animationStartScore, targetScore, t);
            int newScore = Mathf.Clamp(Mathf.RoundToInt(interpolated), animationStartScore, targetScore);

            if (newScore != Score)
            {
                Score = newScore;
                OnScoreUpdatedEvent?.Invoke(Score);

                if (Score > MaxScore)
                {
                    MaxScore = Score;
                    OnMaxScoreUpdatedEvent?.Invoke(MaxScore);
                    SaveUserScore(MaxScore);
                }
            }

            if (t >= 1f)
                break;

            yield return null;
        }

        // ensure final exact value
        Score = targetScore;
        OnScoreUpdatedEvent?.Invoke(Score);
        CheckAndAwardCoins(Score);
        if (Score > MaxScore)
        {
            MaxScore = Score;
            OnMaxScoreUpdatedEvent?.Invoke(MaxScore);
            SaveUserScore(MaxScore);
        }

        scoreCoroutine = null;
    }

    public void HandleOnLevelRestartedEvent (LevelData levelData)
    {
        Restart();
    }

    public void Restart ()
    {
        ResetScore();
        MaxScore = GetUserHighScore();
        OnMaxScoreUpdatedEvent?.Invoke(MaxScore);
        targetScore = Score;
    }
}
