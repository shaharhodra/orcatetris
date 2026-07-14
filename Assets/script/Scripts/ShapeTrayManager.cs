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

    // Shape geometry pool, shared by Classic and Adventure mode.
    [SerializeField] private Shape[] classicShapePrefabs;

    [Header("Adventure")]
    [Tooltip("Used to weight symbol assignment toward whichever ColectionTypes still need the most.")]
    [SerializeField] private AdventureManager adventureManager;

    [Tooltip("Fallback shape source for ShapeWaves name-matching when Addressables aren't used/loaded.")]
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

    [Header("Assist Mode")]
    [SerializeField] private bool useSmartBestShapes;
    [SerializeField] private int smartBestShapesPoolLimit = 50;

    private readonly List<Shape> activeShapes = new List<Shape>();
    private bool noMovesReviveTriggered;

    [Header("Revive")]
    [SerializeField] private ReviveManager reviveManager;

    private readonly List<GameObject> loadedPrefabs = new List<GameObject>();
    private AsyncOperationHandle<IList<GameObject>> loadHandle;
    private bool addressablesLoaded;

    private bool waitingForAdventureLevelData;

    // ===== Per-level override: if a level's JSON defines ShapeWaves, use that fixed
    // sequence instead of the adaptive algorithm (e.g. Level 1's tutorial, which
    // depends on specific shapes appearing in a specific order). Levels that omit
    // ShapeWaves fall through to the adaptive picker in RefillIfNeeded(). =====
    private List<ShapeWave> shapeWaves;
    private int currentWaveIndex;
    private bool useAdventureWaves;

    // Fixed sorting order for any shape sitting idle in a tray slot, so it always
    // renders above blocks already placed on the grid (sortingOrder 2) regardless
    // of whatever order the source prefab happened to be authored with.
    private const int TraySortingOrder = 20;

    private void SetTraySortingOrder(Shape shape)
    {
        if (shape == null)
            return;

        var renderers = shape.GetComponentsInChildren<SpriteRenderer>();
        foreach (var r in renderers)
            r.sortingOrder = TraySortingOrder;
    }

    /// <summary>
    /// Builds a pool of ColectionTypes weighted by how many are still needed to
    /// complete the level (each remaining count adds one entry), so types needed
    /// more are drawn more often and stop being drawn once satisfied.
    /// </summary>
    private List<ColectionTypes> BuildWeightedNeededTypes()
    {
        var result = new List<ColectionTypes>();

        if (adventureManager == null)
            return result;

        foreach (var kvp in adventureManager.RemainingTargets)
        {
            for (int i = 0; i < kvp.Value; i++)
                result.Add(kvp.Key);
        }

        return result;
    }

    /// <summary>
    /// Assigns a random symbol type (drawn from the weighted needed-types pool) to
    /// one random block of the given shape.
    /// </summary>
    private void AssignRandomSymbol(Shape shape, List<ColectionTypes> neededTypes)
    {
        var blocks = shape.GetComponentsInChildren<BlockSymbol>();
        if (blocks.Length == 0 || neededTypes.Count == 0)
            return;

        var block = blocks[Random.Range(0, blocks.Length)];
        var type = neededTypes[Random.Range(0, neededTypes.Count)];
        block.SetSymbolType(type);
    }

    private void OnEnable()
    {
        if (placer != null)
            placer.OnShapePlaced += HandleShapePlaced;
    }

    private struct ShapeCandidateScore
    {
        public Shape Prefab;
        public int BestScore;
        public int BestClearedLines;
        public int BestFilledCells;
    }

    private List<Shape> PickSmartBestSet(Shape[] prefabs)
    {
        var result = new List<Shape>();
        if (prefabs == null || prefabs.Length == 0)
            return result;

        if (board == null)
            return PickBalancedClassicSet(prefabs);

        var pool = prefabs.Where(p => p != null).Distinct().ToList();
        if (pool.Count == 0)
            return result;

        if (smartBestShapesPoolLimit > 0 && pool.Count > smartBestShapesPoolLimit)
            pool = pool.OrderBy(_ => Random.value).Take(smartBestShapesPoolLimit).ToList();

        var scored = new List<ShapeCandidateScore>(pool.Count);
        foreach (var prefab in pool)
        {
            var score = EvaluateBestPlacementScore(prefab);
            if (score.BestScore <= int.MinValue / 2)
                continue;
            scored.Add(score);
        }

        // If everything is unplaceable, fall back to existing logic
        if (scored.Count == 0)
            return PickBalancedClassicSet(prefabs);

        // Pick top 3 by best achievable outcome
        var top = scored
            .OrderByDescending(s => s.BestClearedLines)
            .ThenByDescending(s => s.BestScore)
            .ThenByDescending(s => s.BestFilledCells)
            .Take(3)
            .Select(s => s.Prefab)
            .ToList();

        // Ensure exactly 3 results (fill with placeable shapes if needed)
        result.AddRange(top);

        if (result.Count < 3)
        {
            var fillers = scored
                .OrderByDescending(s => s.BestScore)
                .Select(s => s.Prefab)
                .Where(p => !result.Contains(p))
                .ToList();

            foreach (var f in fillers)
            {
                result.Add(f);
                if (result.Count == 3)
                    break;
            }
        }

        // Last-resort: duplicates allowed to keep tray full
        while (result.Count < 3)
            result.Add(pool[Random.Range(0, pool.Count)]);

        return result;
    }

    private ShapeCandidateScore EvaluateBestPlacementScore(Shape prefab)
    {
        var candidate = new ShapeCandidateScore
        {
            Prefab = prefab,
            BestScore = int.MinValue,
            BestClearedLines = 0,
            BestFilledCells = 0
        };

        if (prefab == null || board == null)
            return candidate;

        var offsets = prefab.GetCells(board.cellSize);
        if (offsets == null || offsets.Length == 0)
            return candidate;

        // Precompute missing counts in current grid
        int[] rowMissing = new int[board.height];
        int[] colMissing = new int[board.width];

        for (int y = 0; y < board.height; y++)
        {
            int missing = 0;
            for (int x = 0; x < board.width; x++)
                if (!board.IsOccupied(new Vector2Int(x, y))) missing++;
            rowMissing[y] = missing;
        }
        for (int x = 0; x < board.width; x++)
        {
            int missing = 0;
            for (int y = 0; y < board.height; y++)
                if (!board.IsOccupied(new Vector2Int(x, y))) missing++;
            colMissing[x] = missing;
        }

        // Try every anchor cell as a potential placement
        for (int ax = 0; ax < board.width; ax++)
        {
            for (int ay = 0; ay < board.height; ay++)
            {
                var anchor = new Vector2Int(ax, ay);
                if (!CanPlaceOffsetsAt(anchor, offsets))
                    continue;

                // Simulate effect on line completion without mutating the board
                int filledCells = offsets.Length;

                // Count how many new cells we cover per row/col
                var filledInRow = new Dictionary<int, int>();
                var filledInCol = new Dictionary<int, int>();

                foreach (var off in offsets)
                {
                    var cell = anchor + off;
                    if (!filledInRow.ContainsKey(cell.y)) filledInRow[cell.y] = 0;
                    if (!filledInCol.ContainsKey(cell.x)) filledInCol[cell.x] = 0;
                    filledInRow[cell.y]++;
                    filledInCol[cell.x]++;
                }

                int clearedLines = 0;
                foreach (var kv in filledInRow)
                {
                    int y = kv.Key;
                    if (y >= 0 && y < rowMissing.Length && rowMissing[y] == kv.Value)
                        clearedLines++;
                }
                foreach (var kv in filledInCol)
                {
                    int x = kv.Key;
                    if (x >= 0 && x < colMissing.Length && colMissing[x] == kv.Value)
                        clearedLines++;
                }

                // Score: prioritize clears heavily, then general fill.
                // This is intentionally generous to make the game easier.
                int score = (clearedLines * 1000) + (filledCells * 10);

                if (score > candidate.BestScore)
                {
                    candidate.BestScore = score;
                    candidate.BestClearedLines = clearedLines;
                    candidate.BestFilledCells = filledCells;
                }
            }
        }

        return candidate;
    }

    private bool CanPlaceOffsetsAt(Vector2Int anchor, Vector2Int[] offsets)
    {
        if (board == null || offsets == null)
            return false;

        foreach (var off in offsets)
        {
            var cell = anchor + off;
            if (!board.IsInside(cell) || board.IsOccupied(cell))
                return false;
        }
        return true;
    }

    private void OnDisable()
    {
        if (placer != null)
            placer.OnShapePlaced -= HandleShapePlaced;
    }

    private void Start()
    {
        if (adventureManager == null)
            adventureManager = FindFirstObjectByType<AdventureManager>();

        GameManager.instance.OnLevelRestartedEvent += HandleOnLevelRestartedEvent;

        // Reset revive state whenever the tray is created
        noMovesReviveTriggered = false;

        var app = AppManager.instance;

        if (app != null && app.CurrentGameMode == AppManager.GameMode.Adventure)
        {
            if (app.CurrentLevelData != null)
            {
                waitingForAdventureLevelData = false;
                InitAdventureWaves();

                if (useAddressables)
                    LoadAddressablesAndRefill();
                else
                    RefillIfNeeded();
            }
            else
            {
                waitingForAdventureLevelData = true;
                app.OnDataLoaded -= HandleLevelDataLoadedForTray; // avoid double subscription
                app.OnDataLoaded += HandleLevelDataLoadedForTray;
            }

            return;
        }

        // Classic (or no AppManager): use random refill immediately.
        noMovesReviveTriggered = false;

        if (useAddressables)
        {
            LoadAddressablesAndRefill();
            return;
        }
        RefillIfNeeded();
    }

    private void HandleLevelDataLoadedForTray(LevelData levelData)
    {
        var app = AppManager.instance;
        if (app == null || app.CurrentGameMode != AppManager.GameMode.Adventure)
            return;

        if (!waitingForAdventureLevelData)
            return;

        if (levelData == null)
            return;

        waitingForAdventureLevelData = false;
        app.OnDataLoaded -= HandleLevelDataLoadedForTray;
        InitAdventureWaves();

        if (useAddressables)
        {
            LoadAddressablesAndRefill();
        }
        else
        {
            RefillIfNeeded();
        }
    }

    /// <summary>
    /// Checks whether the current level's JSON defines a fixed ShapeWaves sequence
    /// (used to pin curated/tutorial levels like Level 1 to specific shapes). If not,
    /// RefillIfNeeded() falls through to the adaptive picker instead.
    /// </summary>
    private void InitAdventureWaves()
    {
        useAdventureWaves = false;
        currentWaveIndex = 0;
        shapeWaves = null;

        if (AppManager.instance == null)
            return;

        if (AppManager.instance.CurrentGameMode != AppManager.GameMode.Adventure)
            return;

        var levelData = AppManager.instance.CurrentLevelData;
        if (levelData == null || levelData.ShapeWaves == null || levelData.ShapeWaves.Count == 0)
            return;

        shapeWaves = levelData.ShapeWaves;
        useAdventureWaves = true;
        currentWaveIndex = 0;

        Debug.Log($"[ShapeTrayManager] Level defines {shapeWaves.Count} fixed ShapeWaves — using them instead of the adaptive picker.");
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

        if (AppManager.instance != null)
            AppManager.instance.OnDataLoaded -= HandleLevelDataLoadedForTray;

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

        bool isAdventureMode = AppManager.instance != null &&
                               AppManager.instance.CurrentGameMode == AppManager.GameMode.Adventure;

        // Per-level override: a level whose JSON defines ShapeWaves (e.g. Level 1's
        // tutorial) uses that fixed sequence instead of the adaptive picker below.
        if (useAdventureWaves && shapeWaves != null)
        {
            if (shapeWaves.Count > 0)
            {
                // Loop waves instead of stopping when the last wave is reached.
                if (currentWaveIndex >= shapeWaves.Count)
                    currentWaveIndex = 0;

                RefillFromWave(shapeWaves[currentWaveIndex]);
                currentWaveIndex++;
            }

            CheckNoMovesAndMaybeRevive();
            return;
        }

        if (classicShapePrefabs == null || classicShapePrefabs.Length == 0)
            return;

        if (useSpaceAwareDifficulty)
        {
            UpdateDifficultyBasedOnGridSpace();
        }

        // Classic mode always uses the score-free picker: it re-checks the actual
        // grid on every refill and looks for shapes that structurally complete a
        // nearly-full row/column (TryPickClearOpportunitySet), falling back to
        // small/medium shapes sized to the free space (GetMaxComplexityForSpace) —
        // no scoring heuristic involved. Adventure keeps using PickSmartBestSet
        // when useSmartBestShapes is on (unchanged).
        var selectedPrefabs = (useSmartBestShapes && isAdventureMode)
            ? PickSmartBestSet(classicShapePrefabs)
            : PickBalancedClassicSet(classicShapePrefabs);

        // Adventure: weight symbol assignment toward whichever ColectionTypes still need the most.
        var neededTypes = isAdventureMode ? BuildWeightedNeededTypes() : null;

        for (int i = 0; i < 3; i++)
        {
            var slot = slots[i];
            if (slot == null)
                continue;

            Shape shape = null;

            if (i < selectedPrefabs.Count && selectedPrefabs[i] != null)
                shape = Instantiate(selectedPrefabs[i], slot.position, slot.rotation, slot);

            if (shape == null)
                continue;

            SetTraySortingOrder(shape);
            activeShapes.Add(shape);

            if (neededTypes != null && neededTypes.Count > 0)
                AssignRandomSymbol(shape, neededTypes);

            var handler = shape.GetComponent<ShapeDragHandler>();
            if (handler != null)
                handler.Init(board, placer, shape);
        }

        // If none of the 3 can be placed, swap the hardest one for the simplest placeable shape
        EnsureAtLeastOnePlaceable(classicShapePrefabs);

        CheckNoMovesAndMaybeRevive();
    }

    /// <summary>
    /// Spawns shapes defined in a ShapeWave by matching names to loaded prefabs.
    /// Used only for levels that define a fixed ShapeWaves sequence in their JSON.
    /// </summary>
    private void RefillFromWave(ShapeWave wave)
    {
        if (wave == null || wave.Shapes == null || wave.Shapes.Count == 0)
            return;

        // Always spawn 3 shapes in Adventure mode
        int shapesToSpawn = 3;

        for (int i = 0; i < shapesToSpawn && i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null)
                continue;

            // Cycle through wave shapes if we need more than defined
            int shapeIndex = i % wave.Shapes.Count;
            string shapeName = wave.Shapes[shapeIndex].Name;

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

            SetTraySortingOrder(shape);
            activeShapes.Add(shape);

            // Apply symbols from JSON data to the shape
            var shapeData = wave.Shapes[shapeIndex];
            if (shapeData.Symbols != null && shapeData.Symbols.Count > 0)
            {
                ApplySymbolsToShape(shape, shapeData.Symbols);
            }

            var handler = shape.GetComponent<ShapeDragHandler>();
            if (handler != null)
                handler.Init(board, placer, shape);
        }
    }

    /// <summary>
    /// Apply symbols from JSON data to shape blocks, matching each symbol's JSON
    /// grid position to the block sitting at that normalized position in the shape.
    /// </summary>
    private void ApplySymbolsToShape(Shape shape, List<SymbolData> symbols)
    {
        if (shape == null || symbols == null)
            return;

        var blocks = shape.GetComponentsInChildren<BlockSymbol>();
        if (blocks.Length == 0)
            return;

        // Find min x/y and the spacing between blocks
        float minX = float.MaxValue, minY = float.MaxValue;
        foreach (var block in blocks)
        {
            Vector3 lp = block.transform.localPosition;
            if (lp.x < minX) minX = lp.x;
            if (lp.y < minY) minY = lp.y;
        }

        // Find the smallest non-zero distance between blocks (the cell spacing)
        float spacing = float.MaxValue;
        for (int i = 0; i < blocks.Length; i++)
        {
            for (int j = i + 1; j < blocks.Length; j++)
            {
                float dx = Mathf.Abs(blocks[i].transform.localPosition.x - blocks[j].transform.localPosition.x);
                float dy = Mathf.Abs(blocks[i].transform.localPosition.y - blocks[j].transform.localPosition.y);
                if (dx > 0.01f && dx < spacing) spacing = dx;
                if (dy > 0.01f && dy < spacing) spacing = dy;
            }
        }
        if (spacing == float.MaxValue || spacing < 0.01f) spacing = 1f;

        // Build dictionary of normalized grid position → block
        var gridMap = new Dictionary<Vector2Int, BlockSymbol>();
        foreach (var block in blocks)
        {
            Vector3 lp = block.transform.localPosition;
            int gx = Mathf.RoundToInt((lp.x - minX) / spacing);
            int gy = Mathf.RoundToInt((lp.y - minY) / spacing);
            var gridPos = new Vector2Int(gx, gy);

            if (!gridMap.ContainsKey(gridPos))
                gridMap[gridPos] = block;
        }

        // Apply each symbol from JSON to the matching grid block
        foreach (var symbolData in symbols)
        {
            var jsonPos = new Vector2Int(
                Mathf.RoundToInt(symbolData.Position.x),
                Mathf.RoundToInt(symbolData.Position.y)
            );

            if (gridMap.TryGetValue(jsonPos, out BlockSymbol targetBlock))
            {
                targetBlock.SetSymbolType(symbolData.Type);
            }
            else
            {
                Debug.LogWarning($"[ShapeTrayManager] No block at grid ({jsonPos.x},{jsonPos.y}) for symbol {symbolData.Type} on shape '{shape.name}'.");
            }
        }
    }

    private void CheckNoMovesAndMaybeRevive()
    {
        // Revive is only available in Classic mode.
        var app = AppManager.instance;
        if (app == null || app.CurrentGameMode != AppManager.GameMode.Classic)
            return;

        if (noMovesReviveTriggered)
            return;

        if (board == null || placer == null)
            return;

        if (HasAnyMove())
            return;

        if (reviveManager != null && reviveManager.CanRevive)
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
            return true;

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

        var app = AppManager.instance;

        // If we are in Adventure mode, re-init the wave override and refill once LevelData is ready.
        if (app != null && app.CurrentGameMode == AppManager.GameMode.Adventure)
        {
            if (app.CurrentLevelData == null)
            {
                waitingForAdventureLevelData = true;
                app.OnDataLoaded -= HandleLevelDataLoadedForTray;
                app.OnDataLoaded += HandleLevelDataLoadedForTray;
                return;
            }

            InitAdventureWaves();

            if (useAddressables)
                LoadAddressablesAndRefill();
            else
                RefillIfNeeded();
            return;
        }

        if (useAddressables)
            LoadAddressablesAndRefill();
        else
            RefillIfNeeded();
    }

    // Analyzes the grid and returns shapes that together can help clear a row or column.
    // Falls back to balanced random if no clear opportunity is found.
    private List<Shape> PickBalancedClassicSet(Shape[] prefabs)
    {
        var result = new List<Shape>();
        if (prefabs == null || prefabs.Length == 0)
            return result;

        var pool = prefabs.Where(p => p != null).ToList();
        if (pool.Count == 0)
            return result;

        // Still prefer a shape that completes a nearly-full row/column right now,
        // when one exists, so placing it actually progresses toward an empty board.
        var clearOpportunity = TryPickClearOpportunitySet(pool);
        if (clearOpportunity != null && clearOpportunity.Count == 3)
            return clearOpportunity;

        // Otherwise: pick 3 shapes weighted toward larger ones (by cell count), so
        // the board fills up — and lines complete — faster.
        var weightedPool = new List<Shape>();
        foreach (var p in pool)
        {
            int weight = GetShapeComplexity(p);
            for (int w = 0; w < weight; w++)
                weightedPool.Add(p);
        }

        for (int i = 0; i < 3; i++)
            result.Add(weightedPool[Random.Range(0, weightedPool.Count)]);

        return result;
    }

    // Try to build a set where at least 1 shape can contribute to completing a line.
    // "Can contribute" is verified by actually simulating every valid placement of
    // each shape on the current board (EvaluateBestPlacementScore, the same exact
    // check used for Adventure's PickSmartBestSet) rather than a size/position
    // heuristic — this also means it correctly keeps helping when the board is
    // nearly *empty* (close to a full board-clear), which a "free space < 85%"
    // early-exit used to skip entirely.
    private List<Shape> TryPickClearOpportunitySet(List<Shape> pool)
    {
        if (board == null) return null;

        // Score every shape's best possible placement once, then reuse it below.
        var scored = pool.Select(p => (Shape: p, Score: EvaluateBestPlacementScore(p))).ToList();

        var helpers = scored.Where(s => s.Score.BestClearedLines > 0).Select(s => s.Shape).ToList();
        if (helpers.Count == 0) return null;

        // "Gift" shapes: clear 2+ lines in a single placement — the satisfying,
        // Block-Blast-style moment. Whenever one is available, guarantee it's
        // offered instead of a merely-adequate single-line helper.
        var giftShapes = scored.Where(s => s.Score.BestClearedLines >= 2).Select(s => s.Shape).ToList();

        // Top priority: a shape that would empty the ENTIRE board if placed at the
        // right spot. A multi-line clear can still leave scattered occupied cells
        // elsewhere, so this needs its own dedicated check rather than reusing
        // BestClearedLines.
        var fullClearShapes = scored.Select(s => s.Shape)
                                     .Where(p => WouldFullyClearBoard(p))
                                     .ToList();

        // Shapes that are placeable anywhere (fill in the rest of the tray)
        var placeable = pool.Where(p => HasAnyMoveForShape(p)).ToList();
        if (placeable.Count == 0) placeable = pool;

        var result = new List<Shape>();

        // Slot 0: a full-board-clear shape beats a gift, which beats a plain helper.
        var slot0Pool = fullClearShapes.Count > 0 ? fullClearShapes
                       : giftShapes.Count > 0 ? giftShapes
                       : helpers;
        result.Add(slot0Pool[Random.Range(0, slot0Pool.Count)]);

        // Slot 1: when a full-board-clear opportunity exists, keep offering
        // full-clear-capable shapes (a second chance at it, and less risk that
        // this slot's shape gets placed somewhere that breaks the setup) before
        // falling back to a plain helper.
        var fullClear1 = fullClearShapes.Where(p => p != result[0]).ToList();
        var helpers2 = helpers.Where(p => p != result[0]).ToList();
        var slot1Pool = fullClear1.Count > 0 ? fullClear1
                       : helpers2.Count > 0 ? helpers2
                       : placeable;
        result.Add(slot1Pool[Random.Range(0, slot1Pool.Count)]);

        // Slot 2: same priority — another full-clear shape if one's still
        // available, otherwise any placeable shape.
        var fullClear2 = fullClearShapes.Where(p => p != result[0] && p != result[1]).ToList();
        var slot2Pool = fullClear2.Count > 0 ? fullClear2 : placeable;
        result.Add(slot2Pool[Random.Range(0, slot2Pool.Count)]);

        return result;
    }

    /// <summary>
    /// Checks every valid placement of this shape on the CURRENT live board and
    /// returns true if any of them would leave the board completely empty after
    /// the resulting rows/columns clear (a full board-clear bonus).
    /// </summary>
    private bool WouldFullyClearBoard(Shape prefab)
    {
        if (prefab == null || board == null)
            return false;

        var offsets = prefab.GetCells(board.cellSize);
        if (offsets == null || offsets.Length == 0)
            return false;

        var currentlyOccupied = new List<Vector2Int>();
        for (int x = 0; x < board.width; x++)
            for (int y = 0; y < board.height; y++)
                if (board.IsOccupied(new Vector2Int(x, y)))
                    currentlyOccupied.Add(new Vector2Int(x, y));

        for (int ax = 0; ax < board.width; ax++)
        {
            for (int ay = 0; ay < board.height; ay++)
            {
                var anchor = new Vector2Int(ax, ay);
                if (!CanPlaceOffsetsAt(anchor, offsets))
                    continue;

                var occupied = new HashSet<Vector2Int>(currentlyOccupied);
                foreach (var off in offsets)
                    occupied.Add(anchor + off);

                bool[] fullRows = new bool[board.height];
                bool[] fullCols = new bool[board.width];

                for (int y = 0; y < board.height; y++)
                {
                    bool full = true;
                    for (int x = 0; x < board.width; x++)
                        if (!occupied.Contains(new Vector2Int(x, y))) { full = false; break; }
                    fullRows[y] = full;
                }
                for (int x = 0; x < board.width; x++)
                {
                    bool full = true;
                    for (int y = 0; y < board.height; y++)
                        if (!occupied.Contains(new Vector2Int(x, y))) { full = false; break; }
                    fullCols[x] = full;
                }

                occupied.RemoveWhere(c => fullRows[c.y] || fullCols[c.x]);

                if (occupied.Count == 0)
                    return true;
            }
        }

        return false;
    }

    // If no active shape can be placed on the board, replace the most complex one
    // with the simplest shape that actually fits somewhere.
    private void EnsureAtLeastOnePlaceable(Shape[] prefabs)
    {
        if (board == null || placer == null || activeShapes.Count == 0)
            return;

        if (HasAnyMove())
            return;

        // Find simplest placeable prefab
        var candidates = prefabs != null
            ? prefabs.Where(p => p != null).OrderBy(p => GetShapeComplexity(p)).ToList()
            : new List<Shape>();

        Shape placeablePrefab = null;
        foreach (var candidate in candidates)
        {
            var temp = Instantiate(candidate);
            temp.gameObject.SetActive(false);
            if (HasAnyMoveForShape(temp))
            {
                placeablePrefab = candidate;
                Destroy(temp.gameObject);
                break;
            }
            Destroy(temp.gameObject);
        }

        if (placeablePrefab == null)
            return; // truly no move possible — revive will handle it

        // Find most complex active shape and replace it
        Shape toReplace = activeShapes
            .Where(s => s != null)
            .OrderByDescending(s => GetShapeComplexity(s))
            .FirstOrDefault();

        if (toReplace == null)
            return;

        int slotIndex = -1;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && toReplace.transform.parent == slots[i])
            {
                slotIndex = i;
                break;
            }
        }

        if (slotIndex < 0)
            return;

        activeShapes.Remove(toReplace);
        Destroy(toReplace.gameObject);

        var replacement = Instantiate(placeablePrefab, slots[slotIndex].position, slots[slotIndex].rotation, slots[slotIndex]);
        if (replacement != null)
        {
            SetTraySortingOrder(replacement);
            activeShapes.Add(replacement);
            var handler = replacement.GetComponent<ShapeDragHandler>();
            if (handler != null)
                handler.Init(board, placer, replacement);
        }
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
        // Easier thresholds: smaller shapes appear sooner
        if (spaceRatio >= 0.8f)
            return 4;
        else if (spaceRatio >= 0.6f)
            return 3;
        else if (spaceRatio >= 0.4f)
            return 2;
        else
            return 1;
    }
}
