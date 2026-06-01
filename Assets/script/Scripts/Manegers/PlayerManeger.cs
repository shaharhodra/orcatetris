using UnityEngine;
using System.IO;
using System;


public class PlayerManeger : Singleton<PlayerManeger>
{
    public PlayerProgressData PlayerProgress { get; private set; }
    
    [Serializable]
    public class PlayerProgressData
    {
        public int HighestUnlockedLevel;
        public int Coins;
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

    public void LoadPlayerProgress()
    {
        var path = GetProgressFilePath();
        if (!File.Exists(path))
        {
            PlayerProgress = new PlayerProgressData
            {
                HighestUnlockedLevel = 1
            };
            return;
        }
        
        var json = File.ReadAllText(path);

        if (!string.IsNullOrEmpty(json))
        {
            var data = JsonUtility.FromJson<PlayerProgressData>(json);
            if (data != null)
                PlayerProgress = data;
        }

        if (PlayerProgress == null)
        {
            PlayerProgress = new PlayerProgressData
            {
                HighestUnlockedLevel = 1
            };
        }

        if (PlayerProgress.HighestUnlockedLevel <= 0)
        {
            PlayerProgress.HighestUnlockedLevel = 1;
        }
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
            HighestUnlockedLevel = 1,
            Coins = 0
        };

        SavePlayerProgress();
        Debug.Log("[PlayerManeger] Progress reset to level 1 with 0 coins.");
    }
}
