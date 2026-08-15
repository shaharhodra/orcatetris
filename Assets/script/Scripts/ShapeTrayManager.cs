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
    [Tooltip("0 = Easy: all 3 tray slots stay genuinely placeable, and fallback ties prefer whichever option leaves the board healthiest. 1 = Hard: only 1 of the 3 slots is guaranteed placeable — the other 2 are decoys that don't fit anywhere on the board right now (once the board has enough clutter for decoys to exist), and fallback ties ignore board health. Line clears themselves always win regardless of this value.")]
    [SerializeField, Range(0f, 1f)] private float difficulty = 0.3f;

    [Header("Score-based Difficulty Ramp")]
    [Tooltip("0 = gift chance at its highest and difficulty at its lowest (start of game). Automatically overwrites Difficulty and Gift Chance above as the score climbs — editing them directly will just get overwritten on the next score update. Read-only display of the current ramp value; edit the two fields below to change its behavior.")]
    [SerializeField, Range(0f, 1f)] private float scoreProgress = 0f;
    [Tooltip("How many points must be scored for one Score Progress Step.")]
    [SerializeField] private int scoreProgressStepPoints = 10000;
    [Tooltip("How much Score Progress rises per Score Progress Step Points scored.")]
    [SerializeField] private float scoreProgressStep = 0.1f;

    // Adventure: each level's LevelData.DifficultyLevel (0-100, unused by Adventure until now —
    // Classic mode reads it separately via palceManager) seeds the automation's starting
    // toughness, so a late-campaign level starts harder than an early one instead of every level
    // starting equally easy and only escalating from in-level score. The score-based ramp above
    // still climbs from this base up to fully-hard, so even late levels keep some in-level room
    // to escalate further. Stays 0 for Classic mode / levels without a set DifficultyLevel.
    private float baseDifficulty;

    private void SeedDifficultyForLevel()
    {
        baseDifficulty = 0f;

        var app = AppManager.instance;
        if (app == null || app.CurrentGameMode != AppManager.GameMode.Adventure || app.CurrentLevelData == null)
            return;

        baseDifficulty = Mathf.Clamp01(app.CurrentLevelData.DifficultyLevel / 100f);
    }

    private readonly ShapeSelectionAlgorithm shapeSelectionAlgorithm = new ShapeSelectionAlgorithm();

    [Header("Gift Opportunities")]
    [Tooltip("When enabled, occasionally checks whether 2-3 fully empty rows/columns can be exactly filled by a small combo of shapes from the pool, and if so offers exactly those shapes so the player can pull off a satisfying multi-line clear.")]
    [SerializeField] private bool enableGiftOpportunities = true;
    [Tooltip("Chance (per refill, once the cooldown below has passed) to actually offer a found gift combo instead of the normal smart pick. Overwritten by the score-based ramp above once the game is running.")]
    [SerializeField, Range(0f, 1f)] private float giftChance = 0.5f;
    [Tooltip("Minimum number of refills that must pass between gifts, so they can't happen back-to-back even if opportunities keep appearing.")]
    [SerializeField] private int giftCooldownRefills = 3;

    private readonly GiftOpportunityDetector giftOpportunityDetector = new GiftOpportunityDetector();
    // Large enough that a gift can still fire on the very first refill of a level.
    private int refillsSinceLastGift = int.MaxValue / 2;

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

    public int CurrentWaveIndex => currentWaveIndex;

    // Guards RefillIfNeeded's cache-restore check so it only fires on the very first
    // fill after this component loads, not on every subsequent tray refill during play.
    private bool adventureCacheRestoreAttempted;

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

        if (ScoreManager.instance != null)
            ScoreManager.instance.OnScoreUpdatedEvent += HandleScoreUpdatedForDifficultyRamp;
    }

    // Drives difficulty/giftChance from the current score, ramping from the level's
    // baseDifficulty (0 for Classic/levels without a DifficultyLevel) up to fully-hard as
    // score climbs — so a late-campaign Adventure level starts tougher than an early one
    // instead of every level starting equally easy, while still leaving room for in-level
    // score progression to escalate further even from a high base.
    private void HandleScoreUpdatedForDifficultyRamp(int score)
    {
        if (scoreProgressStepPoints <= 0)
            return;

        int steps = score / scoreProgressStepPoints;
        scoreProgress = Mathf.Clamp01(steps * scoreProgressStep);
        difficulty = Mathf.Clamp01(baseDifficulty + scoreProgress * (1f - baseDifficulty));
        giftChance = 1f - difficulty;
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

        // Adventure never gets decoys: the tray only refills once every shape is placed, and
        // Adventure has no revive, so an unplaceable slot ends the level outright instead of
        // making it harder. Adventure's difficulty lives in its move limit instead.
        bool isAdventureMode = AppManager.instance != null &&
                               AppManager.instance.CurrentGameMode == AppManager.GameMode.Adventure;

        var result = shapeSelectionAlgorithm.SelectTray(
            BuildOccupancyGrid(), prefabs, board.cellSize, helpfulness, allowDecoys: !isAdventureMode);

        if (debugLogShapeSelection)
        {
            Debug.Log($"[ShapeTrayManager] Refill (occupied={GetOccupiedCellCount()}, difficulty={difficulty}) chose: " +
                $"{string.Join(", ", result.Select(s => s.name))}");
        }

        return result;
    }

    /// <summary>
    /// Occasionally (giftChance, gated by giftCooldownRefills so it can't repeat
    /// back-to-back) checks whether 2-3 completely empty rows/columns can be filled
    /// exactly by a small combo of shapes from the pool, and if so returns exactly
    /// those shapes so the player can pull off a satisfying multi-line clear. Returns
    /// null when no gift is offered this refill — opportunity missing, cooldown still
    /// active, or the chance roll didn't hit — so the caller falls back to the normal
    /// smart pick.
    /// </summary>
    private List<Shape> TryOfferGiftCombo()
    {
        refillsSinceLastGift++;

        if (!enableGiftOpportunities || board == null)
            return null;

        if (refillsSinceLastGift <= giftCooldownRefills)
            return null;

        if (Random.value > giftChance)
            return null;

        var combo = giftOpportunityDetector.TryFindGift(BuildOccupancyGrid(), classicShapePrefabs, board.cellSize);
        if (combo == null || combo.Count == 0)
            return null;

        var selected = new List<Shape>(combo);
        if (selected.Count < 3)
        {
            foreach (var extra in PickSmartBestSet(classicShapePrefabs))
            {
                if (selected.Count >= 3)
                    break;
                selected.Add(extra);
            }
        }

        refillsSinceLastGift = 0;

        if (debugLogShapeSelection)
            Debug.Log($"[ShapeTrayManager] Gift opportunity offered: {string.Join(", ", selected.Select(s => s.name))}");

        return selected;
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

        if (ScoreManager.instance != null)
            ScoreManager.instance.OnScoreUpdatedEvent -= HandleScoreUpdatedForDifficultyRamp;
    }

    private void Start()
    {
        if (adventureManager == null)
            adventureManager = FindFirstObjectByType<AdventureManager>();

        GameManager.instance.OnLevelRestartedEvent += HandleOnLevelRestartedEvent;

        // Seed before the initial ramp calculation below so it starts from the right base
        // (a no-op — stays 0 — if CurrentLevelData isn't loaded yet; HandleLevelDataLoadedForTray
        // re-seeds and recomputes once it is).
        SeedDifficultyForLevel();

        // ScoreManager may not have run its own Awake yet when this component's
        // OnEnable fired, so make sure the subscription is actually attached here too.
        if (ScoreManager.instance != null)
        {
            ScoreManager.instance.OnScoreUpdatedEvent -= HandleScoreUpdatedForDifficultyRamp;
            ScoreManager.instance.OnScoreUpdatedEvent += HandleScoreUpdatedForDifficultyRamp;
            HandleScoreUpdatedForDifficultyRamp(ScoreManager.instance.Score);
        }

        // Reset revive state whenever the tray is created
        noMovesReviveTriggered = false;
        adventureCacheRestoreAttempted = false;

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

        // Level data wasn't ready when Start() ran its initial seed, so redo it now that
        // CurrentLevelData is actually available, and recompute the ramp against it.
        SeedDifficultyForLevel();
        if (ScoreManager.instance != null)
            HandleScoreUpdatedForDifficultyRamp(ScoreManager.instance.Score);

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

        // Resume a saved mid-level tray instead of drawing a fresh one, but only on the
        // first fill after load — later refills during play must behave normally.
        if (isAdventureMode && !adventureCacheRestoreAttempted && AppManager.instance.CurrentLevelData != null)
        {
            adventureCacheRestoreAttempted = true;

            var snapshot = AdventureSessionCache.GetSnapshotFor(AppManager.instance.CurrentLevelData.Level);
            if (snapshot != null)
            {
                RestoreTrayState(snapshot.traySlots, snapshot.currentWaveIndex);
                return;
            }
        }

        // Per-level override: a level whose JSON defines ShapeWaves (e.g. Level 1's
        // tutorial) uses that fixed, curated sequence first. Once every wave has been
        // played through once, fall through to the adaptive picker below (the same
        // "automation" Classic mode and wave-less levels use) for the rest of the
        // level, instead of looping the same handful of curated waves forever.
        if (useAdventureWaves && shapeWaves != null)
        {
            if (shapeWaves.Count > 0 && currentWaveIndex < shapeWaves.Count)
            {
                RefillFromWave(shapeWaves[currentWaveIndex]);
                currentWaveIndex++;

                CheckNoMovesAndMaybeRevive();
                return;
            }

            useAdventureWaves = false;
        }

        if (classicShapePrefabs == null || classicShapePrefabs.Length == 0)
            return;

        List<Shape> selectedPrefabs = TryOfferGiftCombo();

        if (selectedPrefabs == null)
        {
            // Always pick the 3 shapes that best help complete rows/columns on the
            // current board right now, in every mode.
            selectedPrefabs = PickSmartBestSet(classicShapePrefabs);
        }

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

            Shape shape = InstantiateShapeByName(shapeName, slot);

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

        var gridMap = BuildShapeGridMap(shape);
        if (gridMap.Count == 0)
            return;

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

    /// <summary>
    /// Maps each of a shape's blocks to its normalized (spacing-relative) grid
    /// position, shared by ApplySymbolsToShape (JSON → block) and CaptureTrayState
    /// (block → JSON) so both directions agree on the same coordinate system.
    /// </summary>
    private Dictionary<Vector2Int, BlockSymbol> BuildShapeGridMap(Shape shape)
    {
        var gridMap = new Dictionary<Vector2Int, BlockSymbol>();

        if (shape == null)
            return gridMap;

        var blocks = shape.GetComponentsInChildren<BlockSymbol>();
        if (blocks.Length == 0)
            return gridMap;

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

        foreach (var block in blocks)
        {
            Vector3 lp = block.transform.localPosition;
            int gx = Mathf.RoundToInt((lp.x - minX) / spacing);
            int gy = Mathf.RoundToInt((lp.y - minY) / spacing);
            var gridPos = new Vector2Int(gx, gy);

            if (!gridMap.ContainsKey(gridPos))
                gridMap[gridPos] = block;
        }

        return gridMap;
    }

    /// <summary>
    /// Finds a shape prefab by name (Addressables first, then inspector fallback pools)
    /// and instantiates it into the given slot. Used by both the fixed-wave refill and
    /// the session-restore path, which both need to spawn a shape from a saved name.
    /// </summary>
    private Shape InstantiateShapeByName(string shapeName, Transform slot)
    {
        if (string.IsNullOrEmpty(shapeName) || slot == null)
            return null;

        if (useAddressables && addressablesLoaded && loadedPrefabs.Count > 0)
        {
            var goPrefab = loadedPrefabs.Find(p => p != null && p.name == shapeName);
            if (goPrefab != null)
            {
                var go = Instantiate(goPrefab, slot.position, slot.rotation, slot);
                var fromAddressable = go != null ? go.GetComponent<Shape>() : null;
                if (fromAddressable != null)
                    return fromAddressable;
            }
        }

        Shape prefab = null;
        if (shapePrefabs != null)
            prefab = shapePrefabs.FirstOrDefault(p => p != null && p.name == shapeName);
        if (prefab == null && classicShapePrefabs != null)
            prefab = classicShapePrefabs.FirstOrDefault(p => p != null && p.name == shapeName);

        return prefab != null ? Instantiate(prefab, slot.position, slot.rotation, slot) : null;
    }

    private static string StripCloneSuffix(string instanceName)
    {
        const string suffix = "(Clone)";
        return instanceName != null && instanceName.EndsWith(suffix)
            ? instanceName.Substring(0, instanceName.Length - suffix.Length).TrimEnd()
            : instanceName;
    }

    /// <summary>
    /// Snapshots the currently visible tray (which prefab sits in each slot, plus its
    /// symbol assignments) so it can be handed to RestoreTrayState later.
    /// </summary>
    public List<TraySlotSnapshot> CaptureTrayState()
    {
        var result = new List<TraySlotSnapshot>();

        if (slots == null)
            return result;

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null)
                continue;

            var shape = slot.GetComponentInChildren<Shape>();
            if (shape == null)
                continue;

            var symbols = new List<SymbolData>();
            foreach (var kvp in BuildShapeGridMap(shape))
            {
                if (kvp.Value != null && kvp.Value.HasSymbol)
                    symbols.Add(new SymbolData { Type = kvp.Value.SymbolType, Position = new Vector2(kvp.Key.x, kvp.Key.y) });
            }

            result.Add(new TraySlotSnapshot
            {
                slotIndex = i,
                shapeName = StripCloneSuffix(shape.name),
                symbols = symbols
            });
        }

        return result;
    }

    /// <summary>
    /// Rebuilds the tray from a saved snapshot instead of drawing new shapes, resuming a
    /// level exactly where the player left it.
    /// </summary>
    public void RestoreTrayState(List<TraySlotSnapshot> savedSlots, int waveIndex)
    {
        for (int i = 0; i < activeShapes.Count; i++)
        {
            if (activeShapes[i] != null)
                Destroy(activeShapes[i].gameObject);
        }
        activeShapes.Clear();

        currentWaveIndex = waveIndex;
        noMovesReviveTriggered = false;

        if (savedSlots != null)
        {
            foreach (var saved in savedSlots)
            {
                if (slots == null || saved.slotIndex < 0 || saved.slotIndex >= slots.Length)
                    continue;

                var slot = slots[saved.slotIndex];
                if (slot == null)
                    continue;

                Shape shape = InstantiateShapeByName(saved.shapeName, slot);
                if (shape == null)
                {
                    Debug.LogWarning($"[ShapeTrayManager] Could not restore tray shape '{saved.shapeName}' — prefab not found.");
                    continue;
                }

                SetTraySortingOrder(shape);
                activeShapes.Add(shape);

                if (saved.symbols != null && saved.symbols.Count > 0)
                    ApplySymbolsToShape(shape, saved.symbols);

                var handler = shape.GetComponent<ShapeDragHandler>();
                if (handler != null)
                    handler.Init(board, placer, shape);
            }
        }

        CheckNoMovesAndMaybeRevive();
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
        adventureCacheRestoreAttempted = false;

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

            SeedDifficultyForLevel();
            if (ScoreManager.instance != null)
                HandleScoreUpdatedForDifficultyRamp(ScoreManager.instance.Score);

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
