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

    [Header("Difficulty")]
    [Tooltip("0 = Easy: always pick whichever clear-tied tray option leaves the board healthiest. 1 = Hard: ignore board health and pick randomly among clear-tied options (chaotic). Line clears themselves always win regardless of this value — it only breaks ties between sequences that are otherwise equally good at clearing.")]
    [SerializeField, Range(0f, 1f)] private float difficulty = 0.3f;

    private readonly ShapeSelectionAlgorithm shapeSelectionAlgorithm = new ShapeSelectionAlgorithm();

    [Header("Debug")]
    [Tooltip("Logs every candidate shape's best achievable score for each refill, so a confusing tray choice can be diagnosed from the Console instead of guessed at.")]
    [SerializeField] private bool debugLogShapeSelection = false;

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

    // Picks the 3 tray shapes via ShapeSelectionAlgorithm (see that file for the
    // beam-search + board-health + difficulty logic). This class only builds the
    // current occupancy grid to hand off and logs the debug summary.
    private List<Shape> PickSmartBestSet(Shape[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0 || board == null)
            return new List<Shape>();

        // ShapeSelectionAlgorithm takes a "helpfulness" weight (1 = always healthiest),
        // which is the inverse of this component's Difficulty field (0 = easy).
        float helpfulness = 1f - difficulty;
        var result = shapeSelectionAlgorithm.SelectTray(BuildOccupancyGrid(), prefabs, board.cellSize, helpfulness);

        if (debugLogShapeSelection)
        {
            Debug.Log($"[ShapeTrayManager] Refill (occupied={GetOccupiedCellCount()}, difficulty={difficulty}) chose: " +
                $"{string.Join(", ", result.Select(s => s.name))}");
        }

        return result;
    }

    private bool[,] BuildOccupancyGrid()
    {
        var grid = new bool[board.width, board.height];
        for (int x = 0; x < board.width; x++)
            for (int y = 0; y < board.height; y++)
                grid[x, y] = board.IsOccupied(new Vector2Int(x, y));
        return grid;
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

    private static int ShapeCellCount(Shape shapePrefab)
    {
        if (shapePrefab == null)
            return 0;

        var cells = shapePrefab.GetCells(1f);
        return cells?.Length ?? 0;
    }

    private int GetOccupiedCellCount()
    {
        if (board == null)
            return 0;

        int count = 0;
        for (int x = 0; x < board.width; x++)
            for (int y = 0; y < board.height; y++)
                if (board.IsOccupied(new Vector2Int(x, y)))
                    count++;

        return count;
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

        // Always pick the 3 shapes that best help complete rows/columns on the
        // current board right now, in every mode.
        var selectedPrefabs = PickSmartBestSet(classicShapePrefabs);

        // Guarantee at least one is placeable BEFORE anything is shown in the tray,
        // so shapes never change after the player already sees them.
        selectedPrefabs = EnsureSelectionHasPlaceable(selectedPrefabs, classicShapePrefabs);

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

    // Checks whether any prefab in a candidate set has at least one valid placement
    // on the current board, using a temporary hidden instance (nothing visible in
    // the tray yet at this point).
    private bool IsPrefabPlaceableNow(Shape prefab)
    {
        if (prefab == null || board == null || placer == null)
            return false;

        var temp = Instantiate(prefab);
        temp.gameObject.SetActive(false);
        bool placeable = HasAnyMoveForShape(temp);
        Destroy(temp.gameObject);
        return placeable;
    }

    /// <summary>
    /// If none of the selected prefabs can be placed anywhere, swaps the most
    /// complex one for the simplest prefab (from the full pool) that actually
    /// fits. Runs on the prefab list BEFORE the tray shapes are instantiated, so
    /// once a shape appears in a slot it's never swapped out from under the player.
    /// </summary>
    private List<Shape> EnsureSelectionHasPlaceable(List<Shape> selected, Shape[] allPrefabs)
    {
        if (board == null || placer == null || selected == null || selected.Count == 0)
            return selected;

        if (selected.Any(p => IsPrefabPlaceableNow(p)))
            return selected;

        var candidates = allPrefabs != null
            ? allPrefabs.Where(p => p != null).OrderBy(p => ShapeCellCount(p))
            : Enumerable.Empty<Shape>();

        Shape placeablePrefab = candidates.FirstOrDefault(c => IsPrefabPlaceableNow(c));
        if (placeablePrefab == null)
            return selected; // truly no move possible anywhere — revive will handle it

        int replaceIndex = 0;
        int maxCells = -1;
        for (int i = 0; i < selected.Count; i++)
        {
            int cells = ShapeCellCount(selected[i]);
            if (cells > maxCells)
            {
                maxCells = cells;
                replaceIndex = i;
            }
        }

        selected[replaceIndex] = placeablePrefab;
        return selected;
    }

}
