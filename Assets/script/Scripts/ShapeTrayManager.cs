using UnityEngine;
using System;
using Random = UnityEngine.Random;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ShapeTrayManager : MonoBehaviour
{
    public event Action OnNoMovesDetected;

    [SerializeField] private GridBoard board;
    [SerializeField] private GridPlacer placer;
    [SerializeField] private Transform[] slots;
    [SerializeField] private Shape[] shapePrefabs;

    [Header("Addressables")]
    [SerializeField] private bool useAddressables;
    [SerializeField] private string shapesLabel;

    [Header("Revive Timing")]
    [SerializeField] private float noMovesReviveDelay = 0.7f;

    [Header("Move Threshold")]
    [SerializeField] private int minPlaceableToConsiderMovable = 1;

    [Header("Difficulty Settings")]
    [SerializeField] private bool useSpaceAwareDifficulty = true;
    [SerializeField] private float minSpaceRatioForComplexShapes = 0.4f;

    private readonly List<Shape> activeShapes = new List<Shape>();
    private bool noMovesReviveTriggered;

    private readonly List<GameObject> loadedPrefabs = new List<GameObject>();
    private AsyncOperationHandle<IList<GameObject>> loadHandle;
    private bool addressablesLoaded;

    // ===== Adventure predefined waves =====
    private List<ShapeWave> shapeWaves;
    private int currentWaveIndex;
    private bool useAdventureWaves;

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

    private void Start()
    {
        GameManager.instance.OnLevelRestartedEvent += HandleOnLevelRestartedEvent;

        InitAdventureWaves();

        if (useAddressables)
        {
            LoadAddressablesAndRefill();
            return;
        }
        RefillIfNeeded();
    }

    private void InitAdventureWaves()
    {
        useAdventureWaves = false;
        currentWaveIndex = 0;
        shapeWaves = null;

        if (AppManager.instance == null)
            return;

        // Only use waves in Adventure mode
        if (AppManager.instance.CurrentGameMode != AppManager.GameMode.Adventure)
            return;

        var levelData = AppManager.instance.CurrentLevelData;
        if (levelData == null || levelData.ShapeWaves == null || levelData.ShapeWaves.Count == 0)
            return;

        shapeWaves = levelData.ShapeWaves;
        useAdventureWaves = true;
        currentWaveIndex = 0;

        Debug.Log($"[ShapeTrayManager] Adventure mode: {shapeWaves.Count} predefined waves loaded.");
    }

    private void UpdateDifficultyBasedOnGridSpace()
    {
        if (board == null)
            return;

        int totalCells = board.width * board.height;
        int occupiedCells = 0;
        
        for (int x = 0; x < board.width; x++)
        {
            for (int y = 0; y < board.height; y++)
            {
                var cell = new Vector2Int(x, y);
                if (board.IsOccupied(cell))
                {
                    occupiedCells++;
                }
            }
        }
    }

    private float GetAvailableSpaceRatio()
    {
        if (board == null)
            return 1.0f;

        int totalCells = board.width * board.height;
        int occupiedCells = 0;
        
        for (int x = 0; x < board.width; x++)
        {
            for (int y = 0; y < board.height; y++)
            {
                var cell = new Vector2Int(x, y);
                if (board.IsOccupied(cell))
                {
                    occupiedCells++;
                }
            }
        }
        
        return 1.0f - ((float)occupiedCells / totalCells);
    }

    private int GetShapeComplexity(GameObject shapePrefab)
    {
        if (shapePrefab == null)
            return 1;

        var shape = shapePrefab.GetComponent<Shape>();
        if (shape == null)
            return 1;

        var cells = shape.GetCells(1f);
        if (cells == null || cells.Length == 0)
            return 1;

        if (cells.Length <= 2)
            return 1;
        else if (cells.Length <= 4)
            return 2;
        else if (cells.Length <= 6)
            return 3;
        else
            return 4;
    }

    private int GetShapeComplexity(Shape shapePrefab)
    {
        if (shapePrefab == null)
            return 1;

        var cells = shapePrefab.GetCells(1f);
        if (cells == null || cells.Length == 0)
            return 1;

        if (cells.Length <= 2)
            return 1;
        else if (cells.Length <= 4)
            return 2;
        else if (cells.Length <= 6)
            return 3;
        else
            return 4;
    }

    private void OnDestroy()
    {
        GameManager.instance.OnLevelRestartedEvent -= HandleOnLevelRestartedEvent;

        if (useAddressables && loadHandle.IsValid())
            Addressables.Release(loadHandle);
    }

    private void LoadAddressablesAndRefill()
    {
        if (addressablesLoaded)
        {
            RefillIfNeeded();
            return;
        }

        if (string.IsNullOrEmpty(shapesLabel))
        {
            RefillIfNeeded();
            return;
        }

        loadHandle = Addressables.LoadAssetsAsync<GameObject>(shapesLabel, null);
        loadHandle.Completed += handle =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                loadedPrefabs.Clear();
                foreach (var go in handle.Result)
                {
                    if (go != null)
                        loadedPrefabs.Add(go);
                }

                addressablesLoaded = true;
            }
            else
            {
                addressablesLoaded = false;
            }

            RefillIfNeeded();
        };
    }

    private void HandleShapePlaced(Shape placed)
    {
        if (placed != null)
            activeShapes.Remove(placed);

        if (activeShapes.Count == 0)
        {
            noMovesReviveTriggered = false;
            RefillIfNeeded();
            return;
        }

        CheckNoMovesAndMaybeRevive();
    }

    private void RefillIfNeeded()
    {
        if (activeShapes.Count > 0)
            return;

        if (slots == null || slots.Length < 3)
            return;

        // ===== Adventure predefined waves (only in Adventure mode) =====
        if (useAdventureWaves && shapeWaves != null)
        {
            if (currentWaveIndex < shapeWaves.Count)
            {
                RefillFromWave(shapeWaves[currentWaveIndex]);
                currentWaveIndex++;
            }
            else
            {
                Debug.Log("[ShapeTrayManager] All adventure waves used. No more shapes to spawn.");
            }

            CheckNoMovesAndMaybeRevive();
            return;
        }

        // ===== Classic / random mode (unchanged) =====
        if ((shapePrefabs == null || shapePrefabs.Length == 0)
            && (!useAddressables || !addressablesLoaded || loadedPrefabs.Count == 0))
            return;

        if (useSpaceAwareDifficulty)
        {
            UpdateDifficultyBasedOnGridSpace();
        }

        for (int i = 0; i < 3; i++)
        {
            var slot = slots[i];
            if (slot == null)
                continue;

            Shape shape = null;

            if (useAddressables && addressablesLoaded && loadedPrefabs.Count > 0)
            {
                var goPrefab = GetShapePrefabBySpaceAwareDifficulty(loadedPrefabs);
                var go = Instantiate(goPrefab, slot.position, slot.rotation, slot);
                shape = go != null ? go.GetComponent<Shape>() : null;
            }

            if (shape == null)
            {
                if (shapePrefabs == null || shapePrefabs.Length == 0)
                    continue;

                var prefab = GetShapePrefabBySpaceAwareDifficulty(shapePrefabs);
                if (prefab == null)
                    continue;

                shape = Instantiate(prefab, slot.position, slot.rotation, slot);
            }

            if (shape == null)
                continue;

            activeShapes.Add(shape);

            var handler = shape.GetComponent<ShapeDragHandler>();
            if (handler != null)
                handler.Init(board, placer, shape);
        }

        CheckNoMovesAndMaybeRevive();
    }

    /// <summary>
    /// Spawns shapes defined in a ShapeWave by matching names to loaded prefabs.
    /// Used only in Adventure mode.
    /// </summary>
    private void RefillFromWave(ShapeWave wave)
    {
        if (wave == null || wave.Shapes == null)
            return;

        int count = Mathf.Min(wave.Shapes.Count, slots.Length);

        for (int i = 0; i < count; i++)
        {
            var slot = slots[i];
            if (slot == null)
                continue;

            string shapeName = wave.Shapes[i].Name;
            if (string.IsNullOrEmpty(shapeName))
                continue;

            Shape shape = null;

            // Try Addressables first
            if (useAddressables && addressablesLoaded && loadedPrefabs.Count > 0)
            {
                var goPrefab = loadedPrefabs.Find(p => p != null && p.name == shapeName);
                if (goPrefab != null)
                {
                    var go = Instantiate(goPrefab, slot.position, slot.rotation, slot);
                    shape = go != null ? go.GetComponent<Shape>() : null;
                }
            }

            // Fallback to inspector prefabs
            if (shape == null && shapePrefabs != null)
            {
                var prefab = shapePrefabs.FirstOrDefault(p => p != null && p.name == shapeName);
                if (prefab != null)
                    shape = Instantiate(prefab, slot.position, slot.rotation, slot);
            }

            if (shape == null)
            {
                Debug.LogWarning($"[ShapeTrayManager] Wave shape '{shapeName}' not found in any prefab source!");
                continue;
            }

            activeShapes.Add(shape);

            var handler = shape.GetComponent<ShapeDragHandler>();
            if (handler != null)
                handler.Init(board, placer, shape);
        }

        Debug.Log($"[ShapeTrayManager] Adventure wave {currentWaveIndex + 1}/{shapeWaves.Count}: spawned {activeShapes.Count} shapes.");
    }

    private void CheckNoMovesAndMaybeRevive()
    {
        if (noMovesReviveTriggered)
            return;

        if (board == null || placer == null)
            return;

        if (HasAnyMove())
            return;

        if (ReviveManager.instance != null && ReviveManager.instance.CanRevive)
        {
            noMovesReviveTriggered = true;
            StartCoroutine(NoMovesReviveRoutine());
        }
    }

    private IEnumerator NoMovesReviveRoutine()
    {
        yield return new WaitForSeconds(noMovesReviveDelay);

        if (board == null || placer == null)
        {
            noMovesReviveTriggered = false;
            yield break;
        }

        if (HasAnyMove())
        {
            noMovesReviveTriggered = false;
            yield break;
        }

        OnNoMovesDetected?.Invoke();
    }

    private bool HasAnyMove()
    {
        if (board == null || placer == null)
            return true;

        if (activeShapes.Count == 0)
        {
            // Adventure mode: if all waves are exhausted and no shapes left, there are truly no moves.
            if (useAdventureWaves && shapeWaves != null && currentWaveIndex >= shapeWaves.Count)
                return false;

            return true;
        }

        for (int i = 0; i < activeShapes.Count; i++)
        {
            var s = activeShapes[i];
            if (s == null)
                continue;

            if (HasAnyMoveForShape(s))
                return true;
        }

        return false;
    }

    public bool HasAnyMoveAvailable()
    {
        return HasAnyMove();
    }

    private bool HasAnyMoveForShape(Shape s)
    {
        for (int x = 0; x < board.width; x++)
        {
            for (int y = 0; y < board.height; y++)
            {
                var cell = new Vector2Int(x, y);
                if (placer.CanPlaceShape(s, cell))
                    return true;
            }
        }

        return false;
    }

    public IEnumerable<Shape> GetAvailableShapes()
    {
        return activeShapes.Where(shape => shape != null);
    }

    public bool EnsureSpaceForOneShape()
    {
        foreach (var shape in GetAvailableShapes())
        {
            if (shape == null)
                continue;

            for (int row = 0; row < board.Rows; row++)
            {
                for (int col = 0; col < board.Columns; col++)
                {
                    Vector2Int targetCell = new Vector2Int(col, row);
                    if (board.CanPlaceShape(shape, targetCell))
                    {
                        board.ClearCellsForShape(shape, targetCell);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public void HandleOnLevelRestartedEvent(LevelData levelData)
    {
        Restart();
    }

    public void Restart()
    {
        if (activeShapes != null)
        {
            for (int i = 0; i < activeShapes.Count; i++)
            {
                Destroy(activeShapes[i].gameObject);
            }

            activeShapes.Clear();
        }

        currentWaveIndex = 0;
        InitAdventureWaves();

        RefillIfNeeded();
    }

    private GameObject GetShapePrefabBySpaceAwareDifficulty(List<GameObject> prefabs)
    {
        if (prefabs == null || prefabs.Count == 0)
            return null;

        float availableSpaceRatio = GetAvailableSpaceRatio();
        
        var suitableShapes = new List<GameObject>();
        int maxComplexity = GetMaxComplexityForSpace(availableSpaceRatio);
        
        foreach (var prefab in prefabs)
        {
            if (prefab == null) continue;
            
            int complexity = GetShapeComplexity(prefab);
            if (complexity <= maxComplexity)
                suitableShapes.Add(prefab);
        }
        
        if (suitableShapes.Count == 0)
            suitableShapes = prefabs.Where(p => p != null).ToList();
        
        if (availableSpaceRatio < 0.3f)
        {
            var simpleShapes = suitableShapes.Where(p => GetShapeComplexity(p) <= 2).ToList();
            if (simpleShapes.Count > 0 && Random.value < 0.8f)
                return simpleShapes[Random.Range(0, simpleShapes.Count)];
        }
        else if (availableSpaceRatio < 0.5f)
        {
            var simpleShapes = suitableShapes.Where(p => GetShapeComplexity(p) <= 3).ToList();
            if (simpleShapes.Count > 0 && Random.value < 0.6f)
                return simpleShapes[Random.Range(0, simpleShapes.Count)];
        }
        
        return suitableShapes[Random.Range(0, suitableShapes.Count)];
    }

    private Shape GetShapePrefabBySpaceAwareDifficulty(Shape[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
            return null;

        float availableSpaceRatio = GetAvailableSpaceRatio();
        
        var suitableShapes = new List<Shape>();
        int maxComplexity = GetMaxComplexityForSpace(availableSpaceRatio);
        
        foreach (var prefab in prefabs)
        {
            if (prefab == null) continue;
            
            int complexity = GetShapeComplexity(prefab);
            if (complexity <= maxComplexity)
                suitableShapes.Add(prefab);
        }
        
        if (suitableShapes.Count == 0)
            suitableShapes = prefabs.Where(p => p != null).ToList();
        
        if (availableSpaceRatio < 0.3f)
        {
            var simpleShapes = suitableShapes.Where(p => GetShapeComplexity(p) <= 2).ToList();
            if (simpleShapes.Count > 0 && Random.value < 0.8f)
                return simpleShapes[Random.Range(0, simpleShapes.Count)];
        }
        else if (availableSpaceRatio < 0.5f)
        {
            var simpleShapes = suitableShapes.Where(p => GetShapeComplexity(p) <= 3).ToList();
            if (simpleShapes.Count > 0 && Random.value < 0.6f)
                return simpleShapes[Random.Range(0, simpleShapes.Count)];
        }
        
        return suitableShapes[Random.Range(0, suitableShapes.Count)];
    }

    private int GetMaxComplexityForSpace(float spaceRatio)
    {
        if (spaceRatio >= 0.7f)
            return 4;
        else if (spaceRatio >= 0.5f)
            return 3;
        else if (spaceRatio >= 0.3f)
            return 2;
        else
            return 1;
    }
}
