using UnityEngine;
using System;
using System.IO;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using NUnit.Framework;

// every manager will derive from the Singleton class - this makes sure there is only one single manager of this type in the whole app.
public class AppManager : Singleton<AppManager>
{
    // this is how to define an event - how the manager communicates with the rest of the app components
    public event Action<LevelData> OnDataLoaded;
    public LevelData CurrentLevelData { get; private set; }

    [SerializeField] private LevelData _levelData;
    [SerializeField] private string _levelJson;
    [SerializeField] private TextAsset[] _levelJsonFiles;
    [SerializeField] private TextAsset _classicLevelJsonFile;
    [Min(1)]
    [SerializeField] private int startLevelIndex = 1;

    [Header("Addressables Levels")]
    [SerializeField] private bool useAddressablesForLevels;
    [SerializeField] private string adventureLevelsLabel;
    [SerializeField] private string classicLevelsLabel;

    [Header("Scene Navigation")]
    [SerializeField] private int adventureLobbySceneBuildIndex = 0;
    [SerializeField] private int classicGameSceneBuildIndex = 0;

    public enum GameMode
    {
        Adventure = 0,
        Classic = 1
    }

    public enum SceneType
    {
        Game=3,
    }

    private const string SelectedGameModeKey = "selected_game_mode";

    public GameMode CurrentGameMode
    {
        get
        {
            return (GameMode)PlayerPrefs.GetInt(SelectedGameModeKey, (int)GameMode.Adventure);
        }
    }

    public int ClassicGameSceneBuildIndex => classicGameSceneBuildIndex;
   


    private int lastLoadedSceneHandle = -1;

    private readonly List<TextAsset> addressableAdventureLevels = new List<TextAsset>();
    private TextAsset addressableClassicLevel;
    private bool addressablesAdventureLoaded;
    private bool addressablesClassicLoaded;
    private bool addressablesAdventureLoading;
    private bool addressablesClassicLoading;
    private AsyncOperationHandle<IList<TextAsset>> adventureHandle;
    private AsyncOperationHandle<IList<TextAsset>> classicHandle;

    [ContextMenu("Debug/Force Reload Addressables Levels")]
    public void DebugForceReloadAddressablesLevels()
    {
        if (adventureHandle.IsValid())
            Addressables.Release(adventureHandle);
        if (classicHandle.IsValid())
            Addressables.Release(classicHandle);

        addressableAdventureLevels.Clear();
        addressableClassicLevel = null;
        addressablesAdventureLoaded = false;
        addressablesClassicLoaded = false;
        addressablesAdventureLoading = false;
        addressablesClassicLoading = false;

    }

    [ContextMenu("Debug/Force Reload Addressables Levels And Reload Scene")]
    public void DebugForceReloadAddressablesLevelsAndReloadScene()
    {
        DebugForceReloadAddressablesLevels();
        ReloadCurrentScene();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
     
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
       
    }

    private void OnDestroy()
    {
        if (adventureHandle.IsValid())
            Addressables.Release(adventureHandle);
        if (classicHandle.IsValid())
            Addressables.Release(classicHandle);
    }

    private void Start()
    {

        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        AnalyticsManager.instance.SendEvent(AnalyticsManager.AnalyticsEvent.GameStart.ToString());

        RemoteConfigManager.instance.OnRemoteFetchCompleted += () => {};
        RemoteConfigManager.instance.StartRemoteConfigFetch();
        AdsManager.instance.Init(); 

	}

    //private string GetProgressFilePath()
    //{
    //    return Path.Combine(Application.persistentDataPath, "player_progress.json");
    //}







    public void DebugResetProgressToLevel1AndReloadScene()
    {
       

        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }

  

    public void SetGameModeAdventure()
    {
        PlayerPrefs.SetInt(SelectedGameModeKey, (int)GameMode.Adventure);
        PlayerPrefs.Save();
        CurrentLevelData = null;
    }

    public void SetGameModeClassic()
    {
        PlayerPrefs.SetInt(SelectedGameModeKey, (int)GameMode.Classic);
        PlayerPrefs.Save();
        CurrentLevelData = null;
    }

    public void LoadAdventureLobby()
    {
        SetGameModeAdventure();

        SceneManager.LoadScene(adventureLobbySceneBuildIndex);
    }

    public void LoadClassicGame()
    {
        Debug.Log($"[AppManager] LoadClassicGame called. useAddressables={useAddressablesForLevels}, classicJsonFile={(_classicLevelJsonFile != null ? _classicLevelJsonFile.name : "NULL")}, levelJson empty={string.IsNullOrEmpty(_levelJson)}, targetScene={classicGameSceneBuildIndex}");
        SetGameModeClassic();

        if (!useAddressablesForLevels && _classicLevelJsonFile == null && string.IsNullOrEmpty(_levelJson))
        {
            Debug.LogError("[AppManager] LoadClassicGame BLOCKED: no classic json assigned!");
            return;
        }

        Debug.Log($"[AppManager] LoadClassicGame -> loading scene {classicGameSceneBuildIndex}");
        SceneManager.LoadScene(classicGameSceneBuildIndex);
    }

    public void StartAdventureGameFromLobby()
    {
        SetGameModeAdventure();
       // Debug.Log($"AppManager -> StartAdventureGameFromLobby called. CurrentGameMode={CurrentGameMode}, loading scene buildIndex={classicGameSceneBuildIndex}");
        SceneManager.LoadScene(classicGameSceneBuildIndex);
    }

    public void ReloadCurrentScene()
    {
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
       // Debug.Log($"AppManager -> HandleSceneLoaded: scene='{scene.name}' (buildIndex={scene.buildIndex}), CurrentGameMode={CurrentGameMode}");
        if (scene.handle == lastLoadedSceneHandle)
            return;

        lastLoadedSceneHandle = scene.handle;

        // We only want to load level JSON when the main gameplay scene is loaded.
        // This prevents preloading Adventure data while we are still on the main menu
        // and ensures the first click on Classic uses the correct Classic JSON.
        if (scene.buildIndex != classicGameSceneBuildIndex)
            return;

        // Reset current score whenever the main gameplay scene loads, while keeping MaxScore/high score.
        if (scene.buildIndex == classicGameSceneBuildIndex && ScoreManager.instance != null)
        {
            ScoreManager.instance.ResetScore();
        }


        if (CurrentGameMode == GameMode.Classic)
        {
            // Classic mode: always load the single classic JSON/config for the shared GameScene
            if (useAddressablesForLevels)
            {
                StartCoroutine(LoadClassicFromAddressablesThenInvoke(scene));
                return;
            }

            if (_classicLevelJsonFile != null)
            {
             //   Debug.Log($"AppManager -> Classic mode: loading classic json '{_classicLevelJsonFile.name}' for scene '{scene.name}'.");
                LoadLevelFromJson(_classicLevelJsonFile.text);
                return;
            }

            if (!string.IsNullOrEmpty(_levelJson))
            {
              //  Debug.Log($"AppManager -> Classic mode: loading classic json from _levelJson for scene '{scene.name}'.");
                LoadLevelFromJson(_levelJson);
                return;
            }

          //  Debug.LogError($"AppManager -> Classic mode selected but no classic json is assigned. Assign _classicLevelJsonFile in the persistent GameManager (DontDestroyOnLoad).");
            return;
        }

        // Adventure mode: single shared GameScene, level is chosen purely by player progress (0-based HighestUnlockedLevel)
        int index = Mathf.Max(0, PlayerManeger.instance.PlayerProgress.HighestUnlockedLevel);

        if (useAddressablesForLevels)
        {
            StartCoroutine(LoadAdventureFromAddressablesThenLoadIndex(index));
            return;
        }

        if (_levelJsonFiles != null && _levelJsonFiles.Length > 0)
            LoadLevel(index);
        else
            LoadLevelFromJson(_levelJson);
    }

    private IEnumerator LoadAdventureFromAddressablesThenLoadIndex(int index)
    {
        if (addressablesAdventureLoaded)
        {
            LoadAdventureLevelByIndex(index);
            yield break;
        }

        if (addressablesAdventureLoading)
        {
            while (addressablesAdventureLoading)
                yield return null;

            if (addressablesAdventureLoaded)
                LoadAdventureLevelByIndex(index);
            yield break;
        }

        addressablesAdventureLoading = true;

        if (string.IsNullOrEmpty(adventureLevelsLabel))
        {
          //  Debug.LogError("AppManager -> useAddressablesForLevels is enabled but adventureLevelsLabel is empty. Falling back to inspector levels.");
            addressablesAdventureLoading = false;
            if (_levelJsonFiles != null && _levelJsonFiles.Length > 0)
                LoadLevel(index);
            else
                LoadLevelFromJson(_levelJson);
            yield break;
        }

        adventureHandle = Addressables.LoadAssetsAsync<TextAsset>(adventureLevelsLabel, null);
        yield return adventureHandle;

        addressableAdventureLevels.Clear();
        if (adventureHandle.Status == AsyncOperationStatus.Succeeded && adventureHandle.Result != null)
        {
            foreach (var ta in adventureHandle.Result)
            {
                if (ta != null)
                    addressableAdventureLevels.Add(ta);
            }

            addressableAdventureLevels.Sort((a, b) => GetLevelNumberSafe(a).CompareTo(GetLevelNumberSafe(b)));
            addressablesAdventureLoaded = addressableAdventureLevels.Count > 0;
        }
        else
        {
          //  Debug.LogError($"AppaManager -> Failed to load adventure levels via Addressables label '{adventureLevelsLabel}'. Falling back to inspector levels.");
            addressablesAdventureLoaded = false;
        }

        addressablesAdventureLoading = false;

        if (addressablesAdventureLoaded)
            LoadAdventureLevelByIndex(index);
        else if (_levelJsonFiles != null && _levelJsonFiles.Length > 0)
            LoadLevel(index);
        else
            LoadLevelFromJson(_levelJson);
    }

    private void LoadAdventureLevelByIndex(int index)
    {
        if (index < 0 || index >= addressableAdventureLevels.Count)
        {
          //  Debug.LogError($"GameManager -> Adventure level index out of range: {index}. Loaded addressable levels count: {addressableAdventureLevels.Count}");
            return;
        }

        var ta = addressableAdventureLevels[index];
        if (ta == null)
            return;

        LoadLevelFromJson(ta.text);
    }

    private IEnumerator LoadClassicFromAddressablesThenInvoke(Scene scene)
    {
        if (addressablesClassicLoaded)
        {
            if (addressableClassicLevel != null)
            {
            //    Debug.Log($"GameManager -> Classic mode: loading classic json '{addressableClassicLevel.name}' via Addressables for scene '{scene.name}'.");
                LoadLevelFromJson(addressableClassicLevel.text);
            }
            yield break;
        }

        if (addressablesClassicLoading)
        {
            while (addressablesClassicLoading)
                yield return null;

            if (addressablesClassicLoaded && addressableClassicLevel != null)
            {
             //   Debug.Log($"GameManager -> Classic mode: loading classic json '{addressableClassicLevel.name}' via Addressables for scene '{scene.name}'.");
                LoadLevelFromJson(addressableClassicLevel.text);
            }
            yield break;
        }

        addressablesClassicLoading = true;

        if (string.IsNullOrEmpty(classicLevelsLabel))
        {
           // Debug.LogError("GameManager -> useAddressablesForLevels is enabled but classicLevelsLabel is empty. Falling back to inspector classic json.");
            addressablesClassicLoading = false;

            if (_classicLevelJsonFile != null)
                LoadLevelFromJson(_classicLevelJsonFile.text);
            else
                LoadLevelFromJson(_levelJson);
            yield break;
        }

        classicHandle = Addressables.LoadAssetsAsync<TextAsset>(classicLevelsLabel, null);
        yield return classicHandle;

        addressableClassicLevel = null;
        if (classicHandle.Status == AsyncOperationStatus.Succeeded && classicHandle.Result != null)
        {
            for (int i = 0; i < classicHandle.Result.Count; i++)
            {
                var ta = classicHandle.Result[i];
                if (ta == null)
                    continue;

                addressableClassicLevel = ta;
                break;
            }

            addressablesClassicLoaded = addressableClassicLevel != null;
        }
        else
        {
          //  Debug.LogError($"GameManager -> Failed to load classic levels via Addressables label '{classicLevelsLabel}'. Falling back to inspector classic json.");
            addressablesClassicLoaded = false;
        }

        addressablesClassicLoading = false;

        if (addressablesClassicLoaded && addressableClassicLevel != null)
        {
          //  Debug.Log($"GameManager -> Classic mode: loading classic json '{addressableClassicLevel.name}' via Addressables for scene '{scene.name}'.");
            LoadLevelFromJson(addressableClassicLevel.text);
        }
        else if (_classicLevelJsonFile != null)
        {
            LoadLevelFromJson(_classicLevelJsonFile.text);
        }
        else
        {
            LoadLevelFromJson(_levelJson);
        }
    }

    private int GetLevelNumberSafe(TextAsset ta)
    {
        if (ta == null)
            return int.MaxValue;

        try
        {
            var data = JsonUtility.FromJson<LevelData>(ta.text);
            if (data != null && data.Level > 0)
                return data.Level;
        }
        catch
        {
        }

        return int.MaxValue;
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
        AnalyticsManager.instance.SendEvent(AnalyticsManager.AnalyticsEvent.LevelStart.ToString(), new List<AnalyticsManager.AnalyticsEventData>() { 
            new AnalyticsManager.AnalyticsEventData("GameType", levelData.LevelType)});
        

        InvokeOnDataLoaded(levelData);
    }

    public void InvokeOnDataLoaded(LevelData levelData)
    {
        OnDataLoaded?.Invoke(levelData);
    }
}