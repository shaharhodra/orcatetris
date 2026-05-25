using UnityEngine;
using System;
using System.Collections.Generic;

public class AdventureManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridPlacer gridPlacer;
    [SerializeField] private PopUpService winPopUpService;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    // Current level targets: remaining count per ColectionTypes
    private Dictionary<ColectionTypes, int> remainingTargets = new Dictionary<ColectionTypes, int>();

    // Events
    public event Action<Dictionary<ColectionTypes, int>> OnTargetsUpdated;
    public event Action OnAllTargetsCompleted;

    public bool IsAdventureMode => AppManager.instance != null
        && AppManager.instance.CurrentGameMode == AppManager.GameMode.Adventure;

    public Dictionary<ColectionTypes, int> RemainingTargets => remainingTargets;

    private bool levelCompleted;

    private void Start()
    {
        if (gridPlacer != null)
            gridPlacer.OnLinesCleared += HandleLinesCleared;

        if (AppManager.instance != null)
            AppManager.instance.OnDataLoaded += HandleDataLoaded;

        if (GameManager.instance != null)
            GameManager.instance.OnLevelRestartedEvent += HandleLevelRestarted;

        // If data is already loaded, init immediately
        if (IsAdventureMode && AppManager.instance.CurrentLevelData != null)
            InitTargets(AppManager.instance.CurrentLevelData);
    }

    private void OnDestroy()
    {
        if (gridPlacer != null)
            gridPlacer.OnLinesCleared -= HandleLinesCleared;

        if (AppManager.instance != null)
            AppManager.instance.OnDataLoaded -= HandleDataLoaded;

        if (GameManager.instance != null)
            GameManager.instance.OnLevelRestartedEvent -= HandleLevelRestarted;
    }

    private void HandleLevelRestarted(LevelData levelData)
    {
        if (!IsAdventureMode)
            return;

        if (levelData == null && AppManager.instance != null)
            levelData = AppManager.instance.CurrentLevelData;

        if (levelData != null)
            InitTargets(levelData);
    }

    private void HandleDataLoaded(LevelData levelData)
    {
        if (!IsAdventureMode)
            return;

        InitTargets(levelData);
    }

    public void InitTargets(LevelData levelData)
    {
        remainingTargets.Clear();
        levelCompleted = false;

        if (levelData.LevelTargets == null || levelData.LevelTargets.Count == 0)
            return;

        foreach (var target in levelData.LevelTargets)
        {
            if (target.Target <= 0)
                continue;

            if (remainingTargets.ContainsKey(target.TargetItem))
                remainingTargets[target.TargetItem] += target.Target;
            else
                remainingTargets[target.TargetItem] = target.Target;
        }

        if (debugLogs)
        {
            foreach (var kvp in remainingTargets)
                Debug.Log($"[AdventureManager] Target: {kvp.Key} = {kvp.Value}");
        }

        OnTargetsUpdated?.Invoke(remainingTargets);
    }

    // Called when lines are cleared - decrement targets for cleared symbols
    private void HandleLinesCleared(LineClearResult result)
    {
        if (!IsAdventureMode || result.ClearedSymbols == null || result.ClearedSymbols.Count == 0)
            return;

        bool changed = false;

        foreach (var kvp in result.ClearedSymbols)
        {
            if (!remainingTargets.ContainsKey(kvp.Key))
                continue;

            remainingTargets[kvp.Key] = Mathf.Max(0, remainingTargets[kvp.Key] - kvp.Value);
            changed = true;

            if (debugLogs)
                Debug.Log($"[AdventureManager] Cleared {kvp.Value}x {kvp.Key}, remaining: {remainingTargets[kvp.Key]}");
        }

        if (changed)
        {
            OnTargetsUpdated?.Invoke(remainingTargets);
            CheckWinCondition();
        }
    }

    private void CheckWinCondition()
    {
        if (levelCompleted)
            return;

        foreach (var kvp in remainingTargets)
        {
            if (kvp.Value > 0)
                return;
        }

        levelCompleted = true;

        if (debugLogs)
            Debug.Log("[AdventureManager] All targets completed!");

        OnAllTargetsCompleted?.Invoke();

        // Advance to next level
        var levelData = AppManager.instance != null ? AppManager.instance.CurrentLevelData : null;
        int currentLevel = levelData != null ? levelData.Level : 0;

        if (currentLevel > 0)
        {
            GameManager.instance.SetLevelCompleted(currentLevel);

            if (debugLogs)
                Debug.Log($"[AdventureManager] Level {currentLevel} completed, advancing...");
        }

        // Show win popup, then reload scene to load next level
        if (winPopUpService != null)
        {
            winPopUpService.OnPopupDismissed += OnWinPopupDismissed;
            winPopUpService.RunIfConditionMetAndStay(PopUpCondition.OnWin);
        }
        else
        {
            GameManager.instance.ReloadCurrentScene();
        }
    }

    private void OnWinPopupDismissed()
    {
        if (winPopUpService != null)
            winPopUpService.OnPopupDismissed -= OnWinPopupDismissed;

        GameManager.instance.ReloadCurrentScene();
    }
}
