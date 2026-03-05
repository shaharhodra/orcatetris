using UnityEngine;
using System.IO;
using System;


public class PlayerManeger : Singleton<PlayerManeger>
{
    public PlayerProgressData PlayerProgress { get; private set; }
    
    [Serializable]
    public class PlayerProgressData
    {
        public int highestUnlockedLevel;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
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
                highestUnlockedLevel = 1
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
                highestUnlockedLevel = 1
            };
        }
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
                    if (existing != null && existing.highestUnlockedLevel > PlayerProgress.highestUnlockedLevel)
                        PlayerProgress.highestUnlockedLevel = existing.highestUnlockedLevel;
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

}
