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
    [SerializeField] private TextAsset _classicLevelJsonFile;
    [SerializeField] private int startLevelIndex;

    [Header("Scene Navigation")]
    [SerializeField] private int adventureLobbySceneBuildIndex = 0;
    [SerializeField] private int classicGameSceneBuildIndex = 0;

    public enum GameMode
    {
        Adventure = 0,
        Classic = 1
    }

    private const string SelectedGameModeKey = "selected_game_mode";

    public GameMode CurrentGameMode
    {
        get
        {
            return (GameMode)PlayerPrefs.GetInt(SelectedGameModeKey, (int)GameMode.Adventure);
        }
    }

    [Serializable]
    private class PlayerProgressData
    {
        public int highestUnlockedLevel;
    }

    private PlayerProgressData _playerProgress;

    public LevelData CurrentLevelData { get; private set; }

    public int HighestUnlockedLevel => _playerProgress != null ? _playerProgress.highestUnlockedLevel : startLevelIndex;

    private int lastLoadedSceneHandle = -1;

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

        var path = GetProgressFilePath();

        if (File.Exists(path))
        {
            try
            {
                var existingJson = File.ReadAllText(path);
                if (!string.IsNullOrEmpty(existingJson))
                {
                    var existing = JsonUtility.FromJson<PlayerProgressData>(existingJson);
                    if (existing != null && existing.highestUnlockedLevel > _playerProgress.highestUnlockedLevel)
                        _playerProgress.highestUnlockedLevel = existing.highestUnlockedLevel;
                }
            }
            catch
            {
                // ignore read/parse errors and keep current progress
            }
        }

        try
        {
            var json = JsonUtility.ToJson(_playerProgress);
            Debug.Log($"Saving player progress to: {path} | json: {json}");
            File.WriteAllText(path, json);
        }
        catch
        {
            Debug.LogError("Failed to save player progress");
        }
    }

    public void DebugResetProgressToLevel1()
    {
        if (_playerProgress == null)
            _playerProgress = new PlayerProgressData();

        _playerProgress.highestUnlockedLevel = 1;
        SavePlayerProgress();
    }

    public void DebugResetProgressToLevel1AndReloadScene()
    {
        DebugResetProgressToLevel1();

        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
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

    public void SetGameModeAdventure()
    {
        PlayerPrefs.SetInt(SelectedGameModeKey, (int)GameMode.Adventure);
        PlayerPrefs.Save();
    }

    public void SetGameModeClassic()
    {
        PlayerPrefs.SetInt(SelectedGameModeKey, (int)GameMode.Classic);
        PlayerPrefs.Save();
    }

    public void LoadAdventureLobby()
    {
        SetGameModeAdventure();
        SceneManager.LoadScene(adventureLobbySceneBuildIndex);
    }

    public void LoadClassicGame()
    {
        SetGameModeClassic();

        if (_classicLevelJsonFile == null && string.IsNullOrEmpty(_levelJson))
        {
            Debug.LogError("GameManager -> LoadClassicGame called but no classic json is assigned. Assign _classicLevelJsonFile on the persistent GameManager (DontDestroyOnLoad).");
            return;
        }

        SceneManager.LoadScene(classicGameSceneBuildIndex);
    }

    public void ReloadCurrentScene()
    {
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.handle == lastLoadedSceneHandle)
            return;

        lastLoadedSceneHandle = scene.handle;

        if (CurrentGameMode == GameMode.Classic)
        {
            if (_classicLevelJsonFile != null)
            {
                Debug.Log($"GameManager -> Classic mode: loading classic json '{_classicLevelJsonFile.name}' for scene '{scene.name}'.");
                LoadLevelFromJson(_classicLevelJsonFile.text);
                return;
            }

            if (!string.IsNullOrEmpty(_levelJson))
            {
                Debug.Log($"GameManager -> Classic mode: loading classic json from _levelJson for scene '{scene.name}'.");
                LoadLevelFromJson(_levelJson);
                return;
            }

            Debug.LogError($"GameManager -> Classic mode selected but no classic json is assigned. Assign _classicLevelJsonFile in the persistent GameManager (DontDestroyOnLoad).");
            return;
        }

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

        if (sceneLevel == null)
        {
            Debug.LogWarning($"GameManager -> scene '{scene.name}' has no SceneLevelIndex. LevelData will not be loaded and grid will rely on buildOnStart.");
            return;
        }

        int levelNumber = sceneLevel.LevelIndex;
        if (levelNumber <= 0)
        {
            Debug.LogWarning($"GameManager -> scene '{scene.name}' has invalid levelIndex={levelNumber}. Expected 1-based index (1..N).");
            return;
        }

        int index = levelNumber - 1;

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
            LoadLevel(Mathf.Max(0, startLevelIndex - 1));
        else
            LoadLevelFromJson(_levelJson);
    }

    [ContextMenu("Load Selected Level Index")]
    public void LoadSelectedLevelIndex()
    {
        LoadLevel(Mathf.Max(0, startLevelIndex - 1));
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