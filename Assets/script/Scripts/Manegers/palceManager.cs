using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Central placement / decision manager:
/// - קורא נתונים מ־GridBoard, GridPlacer ו־ShapeTrayManager
/// - בודק אם יש מהלכים זמינים
/// - מחליט איזה prefab של Shape לתת לפי רמת קושי / מצב לוח
/// - מנהל מדיניות revive (איזה שורות/עמודות לנקות) ומייצר אירועים ל‑UI
/// </summary>
public class PlaceManager : Singleton<PlaceManager>
{
    public LevelData CurrentLevelData => AppManager.instance.CurrentLevelData;

    [Header("References")]
    [SerializeField] private GridBoard board;
    [SerializeField] private GridPlacer placer;
    [SerializeField] private ShapeTrayManager shapeTrayManager;
    [SerializeField] private ReviveManager reviveManager;

    private int _difficultyMin = 1;
	private int _difficultyMax = 5;


	[Header("Shape selection")]
    [Tooltip("Inspector prefabs used as candidates to give to the tray / player.")]
    [SerializeField] private Shape[] candidatePrefabs;

   
    [SerializeField] private int _difficulty = 1;

    [Header("Revive policy")]
    [Tooltip("If true, PlaceManager will choose the most-occupied row+column to clear on revive. Otherwise uses GridBoard.ReviveClearOneRowAndOneColumn().")]
    [SerializeField] private bool useSmartRevive = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    // Events that UI or other systems can listen to:
    public event Action OnNoMovesDetected; // fired when we detect no moves and will request revive
    public event Action<int, int> OnReviveWillClearRowCol; // (row, col) indices planned to clear (or -1 if random/default)
    public event Func<Shape> OnChooseShapePrefab; // optional override: if set, used instead of internal selection

	private void Start()
	{
        HandleLevelDataLoaded();

	}
	
	private void OnEnable()
    {
        if (placer != null)
            placer.OnShapePlaced += HandleShapePlaced;
    }

    private void OnDisable()
    {
        if (placer != null)
            placer.OnShapePlaced -= HandleShapePlaced;
    }

    private void HandleShapePlaced(Shape placed)
    {
        // when a shape is placed -> re-evaluate tray / moves
        EvaluateMovesAndMaybeRequestRevive();
    }

    /// <summary>
    /// Public query helper: asks shapeTrayManager if any move exists.
    /// </summary>
    public bool HasAnyMove()
    {
        if (shapeTrayManager == null)
        {
            if (debugLogs) Debug.LogWarning("[PlaceManager] shapeTrayManager == null in HasAnyMove()");
            return true; // be conservative
        }

        return shapeTrayManager.HasAnyMoveAvailable();
    }

    /// <summary>
    /// Evaluate current state: if no moves -> request revive flow.
    /// </summary>
    public void EvaluateMovesAndMaybeRequestRevive()
    {
        if (board == null || placer == null || shapeTrayManager == null)
            return;

        // Adventure mode handles game over via AdventureGameMode.CheckGameOver()
        // which runs after the tray has been refilled with the next wave.
        var app = AppManager.instance;
        if (app != null && app.CurrentGameMode == AppManager.GameMode.Adventure)
            return;

        bool anyMove = HasAnyMove();

        if (debugLogs) Debug.Log($"[PlaceManager] EvaluateMoves: anyMove={anyMove}");

        if (!anyMove)
        {
            // notify UI systems that revive is needed
            OnNoMovesDetected?.Invoke();

            // Request revive via reviveManager (ReviveManager will check CanRevive etc.)
            if (reviveManager != null)
            {
                // Before calling RequestRevive we can optionally compute a suggested clear (for UI)
                if (useSmartRevive)
                {
                    var planned = ComputeMostOccupiedRowAndColumn();
                    OnReviveWillClearRowCol?.Invoke(planned.row, planned.col);
                }
                else
                {
                    OnReviveWillClearRowCol?.Invoke(-1, -1);
                }

                reviveManager.RequestRevive();
            }
        }
    }

    /// <summary>
    /// Chooses a prefab to give to the tray based on difficulty and board state.
    /// Returns null if no candidates are available.
    /// - difficulty: higher -> prefer smaller shapes (harder)
    /// - algorithm: compute "size" = number of cells of each prefab and pick weighted-random biased by difficulty
    /// </summary>
    public Shape ChooseShapePrefab()
    {
        // Allow external override
        if (OnChooseShapePrefab != null)
        {
            try
            {
                var overrideShape = OnChooseShapePrefab.Invoke();
                if (overrideShape != null)
                    return overrideShape;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlaceManager] OnChooseShapePrefab threw: {ex.Message}");
            }
        }

        if (candidatePrefabs == null || candidatePrefabs.Length == 0)
            return null;

        // build list of (prefab, cellCount)
        var list = new List<(Shape prefab, int size)>();
        foreach (var p in candidatePrefabs)
        {
            if (p == null) continue;
            int size = SafeGetPrefabCellCount(p);
            list.Add((p, Mathf.Max(1, size)));
        }

        if (list.Count == 0)
            return null;

        // compute weights: smaller size -> higher weight when difficulty is high
        // base weight inverse to size, raised to power (difficulty)
        var weights = new float[list.Count];
        float total = 0f;
        for (int i = 0; i < list.Count; i++)
        {
            float inv = 1f / list[i].size; // smaller shapes -> larger inv
            float w = Mathf.Pow(inv, _difficulty); // difficulty controls skew
            weights[i] = w;
            total += w;
        }

        // random weighted pick
        float r = UnityEngine.Random.value * total;
        float acc = 0f;
        for (int i = 0; i < list.Count; i++)
        {
            acc += weights[i];
            if (r <= acc)
            {
                if (debugLogs) Debug.Log($"[PlaceManager] Chose prefab '{list[i].prefab.name}' (size={list[i].size})");
                return list[i].prefab;
            }
        }

        // fallback
        return list[list.Count - 1].prefab;
    }

    private int SafeGetPrefabCellCount(Shape prefab)
    {
        try
        {
            var arr = prefab.GetCells(board != null ? board.cellSize : 1f);
            return arr != null ? arr.Length : 1;
        }
        catch
        {
            // If prefab.GetCells relies on runtime children, fallback to counting children
            return Mathf.Max(1, prefab.transform.childCount);
        }
    }

    /// <summary>
    /// Perform a "smart revive" by clearing the most-occupied row and most-occupied column.
    /// Returns a tuple (rowsCleared, colsCleared, cellsCleared) similar to GridBoard functions.
    /// If useSmartRevive==false, falls back to GridBoard.ReviveClearOneRowAndOneColumn().
    /// </summary>
    public (int rowsCleared, int colsCleared, int cellsCleared) PerformSmartRevive()
    {
        if (board == null || shapeTrayManager == null)
        {
            if (debugLogs) Debug.LogWarning("[PlaceManager] PerformSmartRevive called but board/shapeTray == null");
            return (0, 0, 0);
        }

        // ✅ קבל את הצורות הזמינות
        var remainingShapes = shapeTrayManager.GetAvailableShapes().ToList();
        
        // ✅ השתמש בפונקציה החדשה!
        int cellsCleared = board.SmartReviveWithValidation(remainingShapes);
        
        // נניח שזה מחק לפחות שורה ועמודה אחת
        return (1, 1, cellsCleared);
    }

    /// <summary>
    /// Helper used by ReviveManager: compute indices of most occupied row/col (used to display preview).
    /// </summary>
    public (int row, int col) ComputeMostOccupiedRowAndColumn()
    {
        if (board == null)
            return (-1, -1);

        var rowCounts = new int[board.height];
        var colCounts = new int[board.width];

        for (int x = 0; x < board.width; x++)
        {
            for (int y = 0; y < board.height; y++)
            {
                if (board.IsOccupied(new Vector2Int(x, y)))
                {
                    colCounts[x]++;
                    rowCounts[y]++;
                }
            }
        }

        int bestRow = -1; int bestRowCount = 0;
        for (int y = 0; y < rowCounts.Length; y++)
        {
            if (rowCounts[y] > bestRowCount) { bestRow = y; bestRowCount = rowCounts[y]; }
        }

        int bestCol = -1; int bestColCount = 0;
        for (int x = 0; x < colCounts.Length; x++)
        {
            if (colCounts[x] > bestColCount) { bestCol = x; bestColCount = colCounts[x]; }
        }

        return (bestRow, bestCol);
    }

    // Optional API: set difficulty at runtime
    public void SetDifficulty(int newDifficulty)
    {
        _difficulty = Mathf.Clamp(newDifficulty,_difficultyMin, _difficultyMax);
    }

    public void HandleLevelDataLoaded()
    {
        // In Adventure mode or when there is no level data, do nothing.
        var app = AppManager.instance;
        if (app == null || app.CurrentLevelData == null)
            return;

        if (app.CurrentGameMode == AppManager.GameMode.Adventure)
            return;

        _difficultyMin = CurrentLevelData.DifficultyLevel;
        _difficultyMax = CurrentLevelData.DifficultyThreshold; 
        SetDifficulty(CurrentLevelData.DifficultyLevel);
	}
}
