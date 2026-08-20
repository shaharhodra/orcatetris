using UnityEngine;
using System.IO;
using System;


public class PlayerManeger : Singleton<PlayerManeger>
{
    // Lazy: PlayerManeger.instance becomes non-null in Awake() (via Singleton<T>), but the actual
    // progress data used to only load in Start() — and Unity doesn't guarantee Start() ordering
    // between different singletons on the same frame. Any code that reached
    // PlayerManeger.instance.PlayerProgress before this component's own Start() had run (very
    // possible on a cold app launch, when several singletons Awake/Start in the same frame) hit a
    // null PlayerProgress and threw, aborting whatever caller was mid-initialization. Making this
    // a lazy property closes that race entirely: PlayerProgress is now guaranteed non-null the
    // instant PlayerManeger.instance itself is non-null, regardless of Start() order.
    private PlayerProgressData _playerProgress;
    public PlayerProgressData PlayerProgress
    {
        get
        {
            if (_playerProgress == null)
                LoadPlayerProgress();
            return _playerProgress;
        }
        private set => _playerProgress = value;
    }

    [SerializeField] private bool enableDailyBonus = true;

    /// <summary>
    /// Overwrites the daily-bonus switch from Remote Config. The bonus grants free level skips, so
    /// being able to turn it off without a build matters if it ever turns out to be handing away
    /// more progress than intended.
    /// </summary>
    public void ApplyRemoteSettings()
    {
        enableDailyBonus = GameSettings.GetBool(RemoteConfigKeys.DailyBonusEnabled);
    }
    
    [Serializable]
    public class PlayerProgressData
    {
        public int HighestUnlockedLevel;
        public int Coins;
        public int DisplayLevel; // visual-only level shown to player, not tied to JSON index
        public string LastBonusDate; // yyyy-MM-dd of last daily bonus grant
    }

    public bool IsDailyBonusAvailable()
    {
        if (!enableDailyBonus)
            return false;
        if (PlayerProgress == null) return false;
        if (string.IsNullOrEmpty(PlayerProgress.LastBonusDate)) return true;
        return PlayerProgress.LastBonusDate != System.DateTime.Now.ToString("yyyy-MM-dd");
    }

    public void MarkDailyBonusUsed()
    {
        if (!enableDailyBonus)
            return;
        if (PlayerProgress == null) return;
        PlayerProgress.LastBonusDate = System.DateTime.Now.ToString("yyyy-MM-dd");
        SavePlayerProgress();

        // Also declared-but-never-fired. This is the retention hook — how many players come back
        // on a second day at all is not currently answerable from the data.
        AnalyticsManager.instance?.SendEvent(
            AnalyticsManager.AnalyticsEvent.DailyBonusClaimed.ToString());
    }

    public event Action<int> OnCoinsUpdatedEvent;


    void Start()
    {
        LoadPlayerProgress();
    }
     
   
    void OnDestroy()
    {
        SavePlayerProgress();
    }

    public string GetProgressFilePath()
    {
        return Path.Combine(Application.persistentDataPath, "player_progress.json");
    }

    // Reads/writes the backing field directly throughout (never the PlayerProgress property) —
    // going through the property here would re-enter this same method via its lazy getter.
    public void LoadPlayerProgress()
    {
        var path = GetProgressFilePath();
        if (!File.Exists(path))
        {
            _playerProgress = new PlayerProgressData
            {
                HighestUnlockedLevel = 0,
                DisplayLevel = 0
            };
            return;
        }

        var json = File.ReadAllText(path);

        if (!string.IsNullOrEmpty(json))
        {
            var data = JsonUtility.FromJson<PlayerProgressData>(json);
            if (data != null)
                _playerProgress = data;
        }

        if (_playerProgress == null)
        {
            _playerProgress = new PlayerProgressData
            {
                HighestUnlockedLevel = 0,
                DisplayLevel = 0
            };
        }

        if (_playerProgress.HighestUnlockedLevel < 0)
            _playerProgress.HighestUnlockedLevel = 0;

        if (_playerProgress.DisplayLevel < 0)
            _playerProgress.DisplayLevel = _playerProgress.HighestUnlockedLevel;

#if UNITY_EDITOR
        // DEBUG: Uncomment the line below to test high level progression (change 40 to any level number)
        //_playerProgress.HighestUnlockedLevel = 34;
#endif
    }

    public int GetCoins()
    {
        return PlayerProgress != null ? PlayerProgress.Coins : 0;
    }

    public void AddCoins(int amount)
    {
        if (PlayerProgress == null || amount <= 0)
            return;

        PlayerProgress.Coins += amount;
        SavePlayerProgress();
        OnCoinsUpdatedEvent?.Invoke(PlayerProgress.Coins);

        // CoinsEarned has been declared in the analytics enum since the beginning and never once
        // fired. Coins are Classic-only (ScoreManager short-circuits the award in Adventure), so
        // this doubles as a read on how much of the playerbase touches Classic at all.
        AnalyticsManager.instance?.SendEvent(
            AnalyticsManager.AnalyticsEvent.CoinsEarned.ToString(),
            AnalyticsManager.AnalyticsParam.Of("Amount", amount));
    }

    public void SavePlayerProgress()
    {
        if (PlayerProgress == null)
            return;

        var path = GetProgressFilePath();

        if (File.Exists(path))
        {
            try
            {
                var existingJson = File.ReadAllText(path);
                if (!string.IsNullOrEmpty(existingJson))
                {
                    var existing = JsonUtility.FromJson<PlayerProgressData>(existingJson);
                    if (existing != null && existing.HighestUnlockedLevel > PlayerProgress.HighestUnlockedLevel)
                        PlayerProgress.HighestUnlockedLevel = existing.HighestUnlockedLevel;
                }
            }
            catch
            {
                // ignore read/parse errors and keep current progress
            }
        }

        try
        {
            var json = JsonUtility.ToJson(PlayerProgress);
            Debug.Log($"Saving player progress to: {path} | json: {json}");
            File.WriteAllText(path, json);
        }
        catch
        {
            Debug.LogError("Failed to save player progress");
        }
    }
        [ContextMenu("Debug/Reset Player Progress To Level 1")]
       public void ResetProgress()
    {
        PlayerProgress = new PlayerProgressData
        {
            HighestUnlockedLevel = 0,
            DisplayLevel = 0,
            Coins = 0
        };

        PlayerPrefs.DeleteAll(); // Clear PlayerPrefs to ensure no old data remains
		try
        {
            var json = JsonUtility.ToJson(PlayerProgress);
            File.WriteAllText(GetProgressFilePath(), json);
        }
        catch
        {
            Debug.LogError("Failed to reset player progress");
        }

        OnCoinsUpdatedEvent?.Invoke(PlayerProgress.Coins);
        Debug.Log("[PlayerManeger] Progress reset to level 1 with 0 coins.");
    }
    [System.NonSerialized]
    public bool JustCompletedLevel;
}
