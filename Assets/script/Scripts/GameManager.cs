using UnityEngine;
using System;
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

    public LevelData CurrentLevelData { get; private set; }

    private string lastLoadedSceneName;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        if (CurrentLevelData == null)
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
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
            Debug.LogWarning($"GameManager -> scene loaded '{scene.name}' has no SceneLevelIndex, falling back to startLevelIndex={startLevelIndex}");

        Debug.Log($"GameManager -> scene loaded '{scene.name}', loading levelIndex={index}");

        if (_levelJsonFiles != null && _levelJsonFiles.Length > 0)
            LoadLevel(index);
        else
            LoadLevelFromJson(_levelJson);
    }

    public void InitData()
    {
        // take LevelData and convert it to json string
        var json = JsonUtility.ToJson(_levelData);
        Debug.Log("This is the level data : " + json);

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
        Debug.Log("Rows = " + levelData.GridRows);
        Debug.Log("Columns = " + levelData.GridColumns);

        InvokeOnDataLoaded(levelData);
    }

    public void InvokeOnDataLoaded(LevelData levelData)
    {
        OnDataLoaded?.Invoke(levelData);
    }
}