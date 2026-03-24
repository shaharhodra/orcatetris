using UnityEngine;
using System;
using Random = UnityEngine.Random;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Linq;

public class ShapeTrayManager : MonoBehaviour
{
    public event Action OnNoMovesDetected; // notify higher-level managers (PlaceManager) when no moves detected

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
    [SerializeField] private int minPlaceableToConsiderMovable = 1; // set to 2 or 3 to be stricter

    [Header("Difficulty Settings")]
    [SerializeField] private int difficulty = 1;
    [SerializeField] private int pointsPerDifficultyLevel = 10000;
    [SerializeField] private bool useSpaceAwareDifficulty = true;
    [SerializeField] private float minSpaceRatioForComplexShapes = 0.4f;

    private readonly List<Shape> activeShapes = new List<Shape>();
    private bool noMovesReviveTriggered;

    private readonly List<GameObject> loadedPrefabs = new List<GameObject>();
    private AsyncOperationHandle<IList<GameObject>> loadHandle;
    private bool addressablesLoaded;

   // [SerializeField] private List<Shape> availableShapes;

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

        // Subscribe to score updates to calculate difficulty
        if (ScoreManager.instance != null)
        {
            ScoreManager.instance.OnScoreUpdatedEvent += HandleScoreUpdated;
        }

        if (useAddressables)
        {
            LoadAddressablesAndRefill();
            return;
        }
        RefillIfNeeded();
    }

    private void HandleScoreUpdated(int newScore)
    {
        // Calculate difficulty based on score (1-100)
        int newDifficulty = (newScore / pointsPerDifficultyLevel) + 1;
        newDifficulty = Mathf.Min(newDifficulty, 100); // Cap at 100
        
        if (newDifficulty != difficulty)
        {
            difficulty = newDifficulty;
            Debug.Log($"[ShapeTrayManager] Difficulty increased to {difficulty} at score {newScore}");
        }
    }

    private void UpdateDifficultyBasedOnGridSpace()
    {
        if (board == null)
            return;

        int totalCells = board.width * board.height;
        int occupiedCells = 0;
        
        // Count occupied cells
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
        
        float occupiedPercentage = (float)occupiedCells / totalCells;
        int newDifficulty;
        
        // Higher difficulty when grid is more full
        if (occupiedPercentage < 0.3f)
            newDifficulty = 1; // Lots of space - easy
        else if (occupiedPercentage < 0.5f)
            newDifficulty = 2; // Some space - medium
        else if (occupiedPercentage < 0.7f)
            newDifficulty = 3; // Limited space - hard
        else
            newDifficulty = 4; // Very little space - very hard
            
        if (newDifficulty != difficulty)
        {
            difficulty = newDifficulty;
            Debug.Log($"[ShapeTrayManager] Difficulty set to {newDifficulty} based on {occupiedPercentage:P1} grid occupation");
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

        var cells = shape.GetCells(1f); // Assuming cell size of 1 for complexity calculation
        if (cells == null || cells.Length == 0)
            return 1;

        // Complexity based on number of cells
        if (cells.Length <= 2)
            return 1; // Very simple (1-2 blocks)
        else if (cells.Length <= 4)
            return 2; // Simple (3-4 blocks)
        else if (cells.Length <= 6)
            return 3; // Medium (5-6 blocks)
        else
            return 4; // Complex (7+ blocks)
    }

    private int GetShapeComplexity(Shape shapePrefab)
    {
        if (shapePrefab == null)
            return 1;

        var cells = shapePrefab.GetCells(1f); // Assuming cell size of 1 for complexity calculation
        if (cells == null || cells.Length == 0)
            return 1;

        // Complexity based on number of cells
        if (cells.Length <= 2)
            return 1; // Very simple (1-2 blocks)
        else if (cells.Length <= 4)
            return 2; // Simple (3-4 blocks)
        else if (cells.Length <= 6)
            return 3; // Medium (5-6 blocks)
        else
            return 4; // Complex (7+ blocks)
    }

    private void OnDestroy()
    {
        GameManager.instance.OnLevelRestartedEvent -= HandleOnLevelRestartedEvent;

        // Unsubscribe from score updates
        if (ScoreManager.instance != null)
        {
            ScoreManager.instance.OnScoreUpdatedEvent -= HandleScoreUpdated;
        }

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
            //   Debug.LogError("[ShapeTrayManager] useAddressables is enabled but shapesLabel is empty.");
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
                //  Debug.LogError($"[ShapeTrayManager] Failed to load addressables by label '{shapesLabel}'. Falling back to inspector prefabs.");
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

        if ((shapePrefabs == null || shapePrefabs.Length == 0)
            && (!useAddressables || !addressablesLoaded || loadedPrefabs.Count == 0))
            return;

        // Update difficulty based on current grid space if space-aware difficulty is enabled
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

        // Notify higher-level manager instead of calling ReviveManager directly.
        OnNoMovesDetected?.Invoke();
        // leave noMovesReviveTriggered as true until higher-level flow resolves it
    }

    private bool HasAnyMove()
    {
        // If core refs are missing, be conservative and treat as "moves available"
        if (board == null || placer == null)
        {
            //  Debug.LogWarning("[HasAnyMove] board or placer is null -> returning true to avoid premature revive");
            return true;
        }

        if (activeShapes.Count == 0)
        {
            // מצב שבו המגש ריק לרגע (למשל בזמן Refill) – עדיף להיות שמרני ולא להפעיל Revive.
            // נחזיר true כדי לציין שיש "מהלכים" פוטנציאליים, עד שהצורות החדשות יווצרו.
            // Debug.Log("[HasAnyMove] activeShapes is empty -> returning true (avoid premature revive)");
            return true;
        }

        // לוגיקה מפושטת: אם יש אפילו צורה אחת עם מיקום חוקי אחד – יש מהלכים.
        for (int i = 0; i < activeShapes.Count; i++)
        {
            var s = activeShapes[i];
            if (s == null)
                continue;

            if (HasAnyMoveForShape(s))
            {
                // Debug.Log($"[HasAnyMove] shape {i} has at least one valid move -> returning true");
                return true;
            }
        }

        // אף צורה לא יכולה להיכנס לשום מקום -> אין מהלכים
        // Debug.Log("[HasAnyMove] no shapes have any valid placement -> returning false");
        return false;
    }

    // Public wrapper so other systems (e.g. ReviveManager) can safely query if there are any valid moves.
    public bool HasAnyMoveAvailable()
    {
        return HasAnyMove();
    }

    private bool HasAnyMoveForShape(Shape s)
    {
        bool found = false;

        for (int x = 0; x < board.width; x++)
        {
            for (int y = 0; y < board.height; y++)
            {
                var cell = new Vector2Int(x, y);
                if (placer.CanPlaceShape(s, cell))
                {
                    // Debug.Log($"[HasAnyMoveForShape] shape '{s.name}' CAN be placed at {cell}");
                    found = true;
                    // לא שוברים את הלולאה, כדי לראות כל התאים החוקיים ללוג
                }
            }
        }

        if (!found)
        {
            // Debug.Log($"[HasAnyMoveForShape] shape '{s.name}' has NO valid placement");
        }

        return found;
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
                        // Debug.Log($"[EnsureSpaceForOneShape] Cleared space for shape at {targetCell}");
                        return true;
                    }
                }
            }
        }

        return false; // No space could be cleared for any shape
    }

    public void HandleOnLevelRestartedEvent (LevelData levelData)
    {
        Restart();
    }

    public void Restart ()
    {
        if (activeShapes != null)
        {
            for (int i = 0; i < activeShapes.Count; i++)
            {
                Destroy(activeShapes[i].gameObject);
            }

            activeShapes.Clear();
        }

        RefillIfNeeded();
    }

    private GameObject GetShapePrefabBySpaceAwareDifficulty(List<GameObject> prefabs)
    {
        if (prefabs == null || prefabs.Count == 0)
            return null;

        float availableSpaceRatio = GetAvailableSpaceRatio();
        
        // Filter shapes by complexity based on available space
        var suitableShapes = new List<GameObject>();
        int maxComplexity = GetMaxComplexityForSpace(availableSpaceRatio);
        
        foreach (var prefab in prefabs)
        {
            if (prefab == null) continue;
            
            int complexity = GetShapeComplexity(prefab);
            if (complexity <= maxComplexity)
            {
                suitableShapes.Add(prefab);
            }
        }
        
        // If no suitable shapes found, fall back to all shapes
        if (suitableShapes.Count == 0)
        {
            suitableShapes = prefabs.Where(p => p != null).ToList();
        }
        
        // Add some randomness but bias towards simpler shapes when space is limited
        if (availableSpaceRatio < 0.3f) // Very limited space
        {
            // Strong bias towards simple shapes
            var simpleShapes = suitableShapes.Where(p => GetShapeComplexity(p) <= 2).ToList();
            if (simpleShapes.Count > 0 && Random.value < 0.8f)
                return simpleShapes[Random.Range(0, simpleShapes.Count)];
        }
        else if (availableSpaceRatio < 0.5f) // Limited space
        {
            // Moderate bias towards simpler shapes
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
        
        // Filter shapes by complexity based on available space
        var suitableShapes = new List<Shape>();
        int maxComplexity = GetMaxComplexityForSpace(availableSpaceRatio);
        
        foreach (var prefab in prefabs)
        {
            if (prefab == null) continue;
            
            int complexity = GetShapeComplexity(prefab);
            if (complexity <= maxComplexity)
            {
                suitableShapes.Add(prefab);
            }
        }
        
        // If no suitable shapes found, fall back to all shapes
        if (suitableShapes.Count == 0)
        {
            suitableShapes = prefabs.Where(p => p != null).ToList();
        }
        
        // Add some randomness but bias towards simpler shapes when space is limited
        if (availableSpaceRatio < 0.3f) // Very limited space
        {
            // Strong bias towards simple shapes
            var simpleShapes = suitableShapes.Where(p => GetShapeComplexity(p) <= 2).ToList();
            if (simpleShapes.Count > 0 && Random.value < 0.8f)
                return simpleShapes[Random.Range(0, simpleShapes.Count)];
        }
        else if (availableSpaceRatio < 0.5f) // Limited space
        {
            // Moderate bias towards simpler shapes
            var simpleShapes = suitableShapes.Where(p => GetShapeComplexity(p) <= 3).ToList();
            if (simpleShapes.Count > 0 && Random.value < 0.6f)
                return simpleShapes[Random.Range(0, simpleShapes.Count)];
        }
        
        return suitableShapes[Random.Range(0, suitableShapes.Count)];
    }

    private int GetMaxComplexityForSpace(float spaceRatio)
    {
        if (spaceRatio >= 0.7f)
            return 4; // Lots of space - allow complex shapes
        else if (spaceRatio >= 0.5f)
            return 3; // Some space - allow medium complexity
        else if (spaceRatio >= 0.3f)
            return 2; // Limited space - allow simple shapes
        else
            return 1; // Very little space - only very simple shapes
    }
}
