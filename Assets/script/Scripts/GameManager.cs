using UnityEngine;
using System;
using System.IO;
using UnityEngine.SceneManagement;

// every manager will derive from the Singleton class - this makes sure there is only one single manager of this type in the whole app.
public class GameManager : Singleton<GameManager>
{
    // this is how to define an event - how the manager communicates with the rest of the app components
    public event Action<LevelData> OnDataLoaded;

    [SerializeField] private LevelData _levelData;
    [SerializeField] private string _levelJson;
    [SerializeField] private TextAsset[] _levelJsonFiles;
    [SerializeField] private int startLevelIndex;

    [Serializable]
    private class PlayerProgressData
    {
        public int highestUnlockedLevel;
    }

    private PlayerProgressData _playerProgress;

    public LevelData CurrentLevelData { get; private set; }

    public int HighestUnlockedLevel => _playerProgress != null ? _playerProgress.highestUnlockedLevel : startLevelIndex;

    private string lastLoadedSceneName;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        LoadPlayerProgress();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SavePlayerProgress();
    }

    private void Start()
    {
        if (CurrentLevelData == null)
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private string GetProgressFilePath()
    {
        return Path.Combine(Application.persistentDataPath, "player_progress.json");
    }

    private void LoadPlayerProgress()
    {
        var path = GetProgressFilePath();
        if (!File.Exists(path))
        {
            _playerProgress = new PlayerProgressData
            {
                highestUnlockedLevel = startLevelIndex
            };
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonUtility.FromJson<PlayerProgressData>(json);
                if (data != null)
                    _playerProgress = data;
            }
        }
        catch
        {
            _playerProgress = new PlayerProgressData
            {
                highestUnlockedLevel = startLevelIndex
            };
        }

        if (_playerProgress == null)
        {
            _playerProgress = new PlayerProgressData
            {
                highestUnlockedLevel = startLevelIndex
            };
        }
    }

    private void SavePlayerProgress()
    {
        if (_playerProgress == null)
            return;

        try
        {
            var json = JsonUtility.ToJson(_playerProgress);
            var path = GetProgressFilePath();
            Debug.Log($"Saving player progress to: {path} | json: {json}");
            File.WriteAllText(path, json);
        }
        catch
        {
            Debug.LogError("Failed to save player progress");
        }
    }

    public void SetLevelCompleted(int levelIndex)
    {
        if (_playerProgress == null)
            LoadPlayerProgress();

        if (levelIndex + 1 > _playerProgress.highestUnlockedLevel)
        {
            _playerProgress.highestUnlockedLevel = levelIndex + 1;
            SavePlayerProgress();
        }
        else
        {
            Debug.Log($"Level {levelIndex} completed but highestUnlockedLevel already {HighestUnlockedLevel}, not updating.");
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.IsNullOrEmpty(lastLoadedSceneName) && scene.name == lastLoadedSceneName)
            return;

        lastLoadedSceneName = scene.name;

        int index = startLevelIndex;

        SceneLevelIndex sceneLevel = null;
        var allSceneLevels = FindObjectsOfType<SceneLevelIndex>(true);
        if (allSceneLevels != null && allSceneLevels.Length > 0)
        {
            for (int i = 0; i < allSceneLevels.Length; i++)
            {
                var candidate = allSceneLevels[i];
                if (candidate != null && candidate.gameObject.scene == scene)
                {
                    sceneLevel = candidate;
                    break;
                }
            }
        }

        if (sceneLevel != null)
            index = sceneLevel.LevelIndex;
        else
            ;

        if (_levelJsonFiles != null && _levelJsonFiles.Length > 0)
            LoadLevel(index);
        else
            LoadLevelFromJson(_levelJson);
    }

    public void InitData()
    {
        // take LevelData and convert it to json string
        var json = JsonUtility.ToJson(_levelData);

        if (_levelJsonFiles != null && _levelJsonFiles.Length > 0)
            LoadLevel(startLevelIndex);
        else
            LoadLevelFromJson(_levelJson);
    }

    [ContextMenu("Load Selected Level Index")]
    public void LoadSelectedLevelIndex()
    {
        LoadLevel(startLevelIndex);
    }

    public void LoadLevel(int index)
    {
        if (_levelJsonFiles == null || _levelJsonFiles.Length == 0)
            return;

        if (index < 0 || index >= _levelJsonFiles.Length)
            return;

        var file = _levelJsonFiles[index];
        if (file == null)
            return;

        LoadLevelFromJson(file.text);
    }

    private void LoadLevelFromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return;

        var levelData = JsonUtility.FromJson<LevelData>(json);
        if (levelData == null)
            return;

        CurrentLevelData = levelData;

        InvokeOnDataLoaded(levelData);
    }

    public void InvokeOnDataLoaded(LevelData levelData)
    {
        OnDataLoaded?.Invoke(levelData);
    }
}