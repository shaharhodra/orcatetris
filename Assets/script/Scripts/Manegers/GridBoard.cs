using UnityEngine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using DG.Tweening;

public class GridBoard : MonoBehaviour
{
    [Header("Grid Size")]
    public int width = 6;
    public int height = 8;

    [SerializeField] private bool buildOnStart = true;

    [Header("Cell Settings")]
    public float cellSize = 1f;
    public Vector2 origin = Vector2.zero;
    [Header("Cell Prefab")]
    public GridCell cellPrefab;

    private GridCell[,] cells;
    private GameObject[,] placedBlocks;
    private int[,] cellSymbolTypes;
    private GameObject[,] symbolIcons;
    private System.Collections.Generic.HashSet<Vector2Int> hoveredCells;
    private System.Collections.Generic.HashSet<Vector2Int> previewClearCells;

    [Header("Clear Animation")]
    [SerializeField] private bool animateClearedBlocks = true;
    [SerializeField] private float clearJumpPower = 1.2f;
    [SerializeField] private float clearJumpDuration = 0.25f;
    [SerializeField] private float clearFallDistance = 8f;
    [SerializeField] private float clearFallDuration = 0.55f;
    [SerializeField] private float clearScatterX = 0.45f;
    [SerializeField] private GridBoard board;

    [Header("Pre-fill")]
    [SerializeField] private GameObject initialBlockPrefab; // prefab for pre-filled blocks
    private List<InitialBlockData> pendingInitialBlocks;
    private List<InitialBlockData> storedInitialBlocks; // kept for restart

    [Header("Random Initial Fill")]
    [Tooltip("Shape prefabs to draw from when pre-filling the grid at game start (e.g. the same pool used by the tray) — placed as whole connected shapes rather than scattered single cells.")]
    [SerializeField] private Shape[] randomFillShapePrefabs;

    [Tooltip("How many random shapes to pre-place on the grid at game start in Classic mode. 0 = disabled. A fresh random layout is generated on every build/restart.")]
    [SerializeField] private int classicRandomInitialShapeCount = 0;

    [Tooltip("Same as above, but for Adventure mode. Only applies to levels whose JSON doesn't already define InitialBlocks — a curated level layout always takes priority over this.")]
    [SerializeField] private int adventureRandomInitialShapeCount = 0;

    [Header("Initial Block Entry Animation")]
    [SerializeField] private bool animateInitialBlockEntry = true;
    [SerializeField] private float initialBlockEntryDuration = 0.25f;
    [SerializeField] private Ease initialBlockEntryEase = Ease.OutBack;
    [SerializeField] private float initialBlockEntryDistance = 8f;
    [SerializeField] private float initialBlockEntryDelayPerBlock = 0f;

    // ✅ תיקון: הוסף getters נכונים
    public int Rows => height;
    public int Columns => width;

    private void Start()
    {
        // Debug.Log($"[GridBoard] Start on {gameObject.name}, buildOnStart = {buildOnStart}, size = {width}x{height}");
        GameManager.instance.OnLevelRestartedEvent += HandleOnLevelRestartedEvent;

        if (buildOnStart)
        {
            BuildGrid();
        }
    }

    private void OnDestroy()
    {
        GameManager.instance.OnLevelRestartedEvent -= HandleOnLevelRestartedEvent;
    }

    public void ClearHover()
    {
        if (hoveredCells == null || hoveredCells.Count == 0 || cells == null)
            return;

        foreach (var pos in hoveredCells)
        {
            if (IsInside(pos) && cells[pos.x, pos.y] != null)
                cells[pos.x, pos.y].SetShapeOver(false);
        }

        hoveredCells.Clear();
    }

    public void ClearPreviewClear()
    {
        if (previewClearCells == null || previewClearCells.Count == 0 || cells == null)
            return;

        foreach (var pos in previewClearCells)
        {
            if (IsInside(pos) && cells[pos.x, pos.y] != null)
                cells[pos.x, pos.y].SetPreviewClear(false);
        }

        previewClearCells.Clear();
    }

    public void SetHoverCells(System.Collections.Generic.IEnumerable<Vector2Int> positions)
    {
        if (cells == null)
            return;

        if (hoveredCells == null)
            hoveredCells = new System.Collections.Generic.HashSet<Vector2Int>();

        ClearHover();

        foreach (var pos in positions)
        {
            if (!IsInside(pos))
                continue;

            var cell = cells[pos.x, pos.y];
            if (cell == null)
                continue;

            cell.SetShapeOver(true);
            hoveredCells.Add(pos);
        }
    }

    public void SetPreviewClearCells(System.Collections.Generic.IEnumerable<Vector2Int> positions)
    {
        if (cells == null)
            return;

        if (previewClearCells == null)
            previewClearCells = new System.Collections.Generic.HashSet<Vector2Int>();

        var newCells = new System.Collections.Generic.HashSet<Vector2Int>();
        foreach (var pos in positions)
        {
            if (IsInside(pos) && cells[pos.x, pos.y] != null)
                newCells.Add(pos);
        }

        // Only toggle cells whose state actually changed, so cells that stay
        // highlighted across small drag movements aren't re-triggered every
        // call (which was restarting the preview-clear particles constantly).
        foreach (var pos in previewClearCells)
        {
            if (!newCells.Contains(pos))
                cells[pos.x, pos.y].SetPreviewClear(false);
        }

        foreach (var pos in newCells)
        {
            if (!previewClearCells.Contains(pos))
                cells[pos.x, pos.y].SetPreviewClear(true);
        }

        previewClearCells = newCells;
    }

    public void ApplySize(int newWidth, int newHeight)
    {
        width = Mathf.Max(1, newWidth);
        height = Mathf.Max(1, newHeight);
        RebuildGrid();
    }

    public void RebuildGrid()
    {
        // Debug.Log("[GridBoard] RebuildGrid requested");

        if (Application.isPlaying)
        {
            //  Debug.Log("[GridBoard] RebuildGrid (play mode) requested");

            StopAllCoroutines();
            StartCoroutine(RebuildGridCoroutine());
            return;
        }

        ClearHover();
        ClearPreviewClear();
        ClearGridObjects();
        BuildGrid();
    }

    private IEnumerator RebuildGridCoroutine()
    {
        // Debug.Log("[GridBoard] RebuildGridCoroutine started");
        ClearHover();
        ClearPreviewClear();
        ClearGridObjects();
        yield return null;
        BuildGrid();
    }

    public void Clear()
    {
        // מנקה את מצב התפוס לתאים קיימים
        if (cells == null) return;

        // ודא שניקוי לוח מוחק גם כל היילייט של preview
        ClearPreviewClear();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (cells[x, y] != null)
                {
                    cells[x, y].SetOccupied(false);
                }

                if (placedBlocks != null && placedBlocks[x, y] != null)
                {
                    Destroy(placedBlocks[x, y]);
                    placedBlocks[x, y] = null;
                }

                ClearSymbolAt(x, y);
            }
        }
    }

    private void BuildGrid()
    {
        if (cellPrefab == null)
        {
            //    Debug.LogWarning($"[GridBoard] cellPrefab is NULL on {gameObject.name}, cannot build grid");
            return;
        }

        //  Debug.Log($"[GridBoard] Building grid on {gameObject.name}, size = {width}x{height}");

        // Clear any existing children to prevent duplicates
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        CenterOrigin();

        cells = new GridCell[width, height];
        placedBlocks = new GameObject[width, height];
        cellSymbolTypes = new int[width, height];
        symbolIcons = new GameObject[width, height];
        for (int sx = 0; sx < width; sx++)
            for (int sy = 0; sy < height; sy++)
                cellSymbolTypes[sx, sy] = -1;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 pos = GridToWorld(new Vector2Int(x, y));
                GridCell cell = Instantiate(cellPrefab, pos, Quaternion.identity, transform);
                cell.gridPos = new Vector2Int(x, y);
                cells[x, y] = cell;
            }
        }

        // Apply pending initial blocks after grid is built
        if (pendingInitialBlocks != null && pendingInitialBlocks.Count > 0)
        {
            ApplyInitialBlocks(pendingInitialBlocks);
        }
        else if (ActiveRandomInitialShapeCount() > 0)
        {
            ApplyInitialBlocks(BuildRandomInitialBlocks());
        }

        // Apply current theme to newly created cells
        if (ThemeManager.instance != null)
            ThemeManager.instance.ApplyThemeToGrid(this);

        if (SoundManager.instance != null)
            SoundManager.instance.PlayGameStart();
    }

    /// <summary>
    /// Store initial blocks to be placed after the grid is built.
    /// Call this before ApplySize/RebuildGrid.
    /// </summary>
    public void SetInitialBlocks(List<InitialBlockData> blocks)
    {
        pendingInitialBlocks = blocks;
        storedInitialBlocks = blocks; // keep for restart
    }

    /// <summary>
    /// Place pre-filled blocks on the grid from JSON data.
    /// </summary>
    private void ApplyInitialBlocks(List<InitialBlockData> blocks)
    {
        if (blocks == null || cells == null)
            return;

        if (initialBlockPrefab == null)
        {
            Debug.LogWarning("[GridBoard] initialBlockPrefab is not assigned! Cannot create pre-filled blocks.");
            return;
        }

        // Get symbol sprites from AdventureTargetUI
        Sprite[] symbolSprites = null;
        var adventureUI = FindObjectOfType<AdventureTargetUI>();
        if (adventureUI != null)
        {
            var field = typeof(AdventureTargetUI).GetField("symbolSprites",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
                symbolSprites = field.GetValue(adventureUI) as Sprite[];
        }

        Debug.Log($"[GridBoard] Applying {blocks.Count} initial blocks to grid");
        int blockIndex = 0;

        foreach (var blockData in blocks)
        {
            Vector2Int pos = new Vector2Int(blockData.x, blockData.y);

            if (!IsInside(pos))
            {
                Debug.LogWarning($"[GridBoard] Initial block at ({blockData.x},{blockData.y}) is outside grid!");
                continue;
            }

            // Mark cell as occupied
            SetOccupied(pos, true);

            // Instantiate the prefab at the grid position
            Vector3 worldPos = GridToWorld(pos);
            var blockObj = Instantiate(initialBlockPrefab, worldPos, Quaternion.identity, transform);
            blockObj.name = $"InitialBlock_{blockData.x}_{blockData.y}";

            // Match the sorting order of normally-placed blocks so tray shapes
            // (sortingOrder 20) reliably render above every block on the grid.
            var blockRenderer = blockObj.GetComponent<SpriteRenderer>();
            if (blockRenderer != null)
                blockRenderer.sortingOrder = 2;

            if (animateInitialBlockEntry)
            {
                AnimateInitialBlockEntry(blockObj, worldPos, blockIndex * initialBlockEntryDelayPerBlock);
            }

            placedBlocks[pos.x, pos.y] = blockObj;

            // Mark as placed so ThemeBlock uses squareColor
            var themeBlock = blockObj.GetComponent<ThemeBlock>();
            if (themeBlock != null)
                themeBlock.MarkAsPlaced();

            // Apply symbol if defined
            if (blockData.Symbol >= 0)
            {
                var symbolType = (ColectionTypes)blockData.Symbol;

                Sprite symSprite = null;
                if (symbolSprites != null && blockData.Symbol < symbolSprites.Length)
                    symSprite = symbolSprites[blockData.Symbol];

                if (symSprite != null)
                {
                    var iconObj = new GameObject("SymbolIcon");
                    iconObj.transform.SetParent(blockObj.transform, false);
                    if (animateInitialBlockEntry)
                    {
                        Vector3 entryDir = GetRandomEntryDirectionForInitialBlock();
                        iconObj.transform.localPosition = entryDir * initialBlockEntryDistance;
                        iconObj.transform.DOLocalMove(Vector3.zero, initialBlockEntryDuration)
                            .SetDelay(blockIndex * initialBlockEntryDelayPerBlock)
                            .SetEase(initialBlockEntryEase);
                    }
                    else
                    {
                        iconObj.transform.localPosition = Vector3.zero;
                    }
                    iconObj.transform.localScale = Vector3.one * 0.6f;

                    var iconSr = iconObj.AddComponent<SpriteRenderer>();
                    iconSr.sprite = symSprite;
                    iconSr.sortingOrder = 10;

                    SetSymbol(pos, symbolType, iconObj);
                    Debug.Log($"[GridBoard] Initial block at ({blockData.x},{blockData.y}) with symbol {symbolType}");
                }
                else
                {
                    cellSymbolTypes[pos.x, pos.y] = blockData.Symbol;
                    Debug.Log($"[GridBoard] Initial block at ({blockData.x},{blockData.y}) with symbol type {blockData.Symbol} (no sprite found)");
                }
            }
            else
            {
                Debug.Log($"[GridBoard] Initial block at ({blockData.x},{blockData.y}) without symbol");
            }
            
            blockIndex++;
        }

        // Verify all initial blocks are occupied
        int occupiedCount = 0;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (cells[x, y] != null && cells[x, y].occupied)
                    occupiedCount++;
        Debug.Log($"[GridBoard] After ApplyInitialBlocks: {occupiedCount} cells are occupied");
    }

    /// <summary>
    /// Classic or Adventure's random-fill shape count, whichever the current
    /// AppManager.GameMode selects (defaults to Classic's if AppManager isn't ready yet).
    /// </summary>
    private int ActiveRandomInitialShapeCount()
    {
        bool isAdventure = AppManager.instance != null &&
                            AppManager.instance.CurrentGameMode == AppManager.GameMode.Adventure;
        return isAdventure ? adventureRandomInitialShapeCount : classicRandomInitialShapeCount;
    }

    private const int RandomFillPlacementAttemptsPerShape = 20;

    /// <summary>
    /// Places ActiveRandomInitialShapeCount() random shapes (drawn from
    /// randomFillShapePrefabs) at random non-overlapping spots on the empty grid, so
    /// the pre-fill reads as whole connected shapes instead of cells scattered
    /// individually across the board. A shape that can't find a free spot after a
    /// few tries is simply skipped. Returns null if there's nothing to place.
    /// </summary>
    private List<InitialBlockData> BuildRandomInitialBlocks()
    {
        int shapeCount = ActiveRandomInitialShapeCount();
        if (shapeCount <= 0 || randomFillShapePrefabs == null || randomFillShapePrefabs.Length == 0)
            return null;

        var pool = randomFillShapePrefabs.Where(p => p != null).ToList();
        if (pool.Count == 0)
            return null;

        var occupied = new HashSet<Vector2Int>();
        var blocks = new List<InitialBlockData>();

        for (int i = 0; i < shapeCount; i++)
        {
            var prefab = pool[Random.Range(0, pool.Count)];
            var offsets = prefab.GetCells(cellSize);
            if (offsets == null || offsets.Length == 0)
                continue;

            for (int attempt = 0; attempt < RandomFillPlacementAttemptsPerShape; attempt++)
            {
                var anchor = new Vector2Int(Random.Range(0, width), Random.Range(0, height));

                bool fits = true;
                foreach (var off in offsets)
                {
                    var cell = anchor + off;
                    if (!IsInside(cell) || occupied.Contains(cell))
                    {
                        fits = false;
                        break;
                    }
                }

                if (!fits)
                    continue;

                foreach (var off in offsets)
                {
                    var cell = anchor + off;
                    occupied.Add(cell);
                    blocks.Add(new InitialBlockData { x = cell.x, y = cell.y, Symbol = -1 });
                }
                break;
            }
        }

        return blocks.Count > 0 ? blocks : null;
    }

    private void CenterOrigin()
    {
        Vector2 pivot = transform.position;
        origin = pivot - new Vector2(width * cellSize * 0.5f, height * cellSize * 0.5f);
    }

    private void ClearGridObjects()
    {
        //  Debug.Log($"[GridBoard] ClearGridObjects on {gameObject.name}");

        cells = null;
        placedBlocks = null;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        CenterOrigin();
        Vector2 local = (Vector2)worldPos - origin;
        int x = Mathf.FloorToInt(local.x / cellSize);
        int y = Mathf.FloorToInt(local.y / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        CenterOrigin();
        return new Vector3(
            origin.x + (gridPos.x + 0.5f) * cellSize,
            origin.y + (gridPos.y + 0.5f) * cellSize,
            0f
        );
    }

    public bool IsInside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < width &&
               cell.y >= 0 && cell.y < height;
    }

    public bool IsOccupied(Vector2Int cell)
    {
        if (cells == null)
            return false;

        return cells[cell.x, cell.y] != null && cells[cell.x, cell.y].occupied;
    }

    public bool IsBoardEmpty()
    {
        if (cells == null)
            return true;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (cells[x, y] != null && cells[x, y].occupied)
                    return false;

        return true;
    }

    public void SetOccupied(Vector2Int cell, bool value)
    {
        if (cells == null)
            return;

        if (cells[cell.x, cell.y] != null)
        {
            cells[cell.x, cell.y].SetOccupied(value);
        }
    }

    public void SetPlacedBlock(Vector2Int cell, GameObject block)
    {
        if (placedBlocks == null)
            return;

        if (!IsInside(cell))
            return;

        if (placedBlocks[cell.x, cell.y] != null && placedBlocks[cell.x, cell.y] != block)
            Destroy(placedBlocks[cell.x, cell.y]);

        placedBlocks[cell.x, cell.y] = block;
    }

    // ===== Symbol tracking for Adventure mode =====

    public void SetSymbol(Vector2Int cell, ColectionTypes symbolType, GameObject iconObj)
    {
        if (!IsInside(cell) || cellSymbolTypes == null)
            return;

        cellSymbolTypes[cell.x, cell.y] = (int)symbolType;

        if (symbolIcons != null)
        {
            if (symbolIcons[cell.x, cell.y] != null)
                Destroy(symbolIcons[cell.x, cell.y]);
            symbolIcons[cell.x, cell.y] = iconObj;
        }
    }

    public int GetSymbolType(Vector2Int cell)
    {
        if (!IsInside(cell) || cellSymbolTypes == null)
            return -1;
        return cellSymbolTypes[cell.x, cell.y];
    }

    private GameObject DetachSymbolAt(int x, int y)
    {
        if (cellSymbolTypes != null)
            cellSymbolTypes[x, y] = -1;

        if (symbolIcons == null || symbolIcons[x, y] == null)
            return null;

        GameObject icon = symbolIcons[x, y];
        symbolIcons[x, y] = null;

        if (icon != null)
        {
            icon.transform.DOKill();
            icon.transform.SetParent(null, true);
        }

        return icon;
    }

    private void ClearSymbolAt(int x, int y)
    {
        GameObject icon = DetachSymbolAt(x, y);
        if (icon != null)
            Destroy(icon);
    }

    /// <summary>
    /// Fully clears a single cell: destroys the placed block (cube) and its symbol.
    /// Also handles initial blocks from JSON whose cube may not be tracked in
    /// placedBlocks: their symbol icon is parented to the cube, so we destroy that
    /// parent as a fallback. This prevents leftover cubes after a line clear.
    /// </summary>
    private void DestroyCellContents(int x, int y)
    {
        GameObject blockToAnimate = null;

        if (placedBlocks != null && placedBlocks[x, y] != null)
        {
            blockToAnimate = placedBlocks[x, y];
            placedBlocks[x, y] = null;
        }

        // Also check for initial blocks by name (fallback)
        string initialBlockName = $"InitialBlock_{x}_{y}";
        Transform initialBlock = transform.Find(initialBlockName);
        if (initialBlock != null)
        {
            if (blockToAnimate == null)
                blockToAnimate = initialBlock.gameObject;
            else if (initialBlock.gameObject != blockToAnimate)
                Destroy(initialBlock.gameObject); // Destroy duplicate
        }

        if (blockToAnimate != null)
            AnimateAndDestroyClearedBlock(blockToAnimate);

        // Destroy any orphaned blocks sitting at this cell position
        DestroyOrphanedBlocksAt(x, y);

        ClearSymbolAt(x, y);
    }

    /// <summary>
    /// Safety sweep: destroy any child objects sitting at a cell position
    /// that aren't GridCells. Prevents leftover visual blocks after clearing.
    /// </summary>
    private void DestroyOrphanedBlocksAt(int x, int y)
    {
        Vector3 cellWorldPos = GridToWorld(new Vector2Int(x, y));
        float threshold = cellSize * 0.4f;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child == null)
                continue;

            // Skip GridCell objects — those are the grid itself
            if (child.GetComponent<GridCell>() != null)
                continue;

            float dist = Vector2.Distance(child.position, cellWorldPos);
            if (dist < threshold)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private ClearedSymbolVisual CaptureSymbolVisual(int x, int y)
    {
        if (cellSymbolTypes == null || cellSymbolTypes[x, y] < 0)
            return null;

        var symbolType = (ColectionTypes)cellSymbolTypes[x, y];
        GameObject icon = DetachSymbolAt(x, y);
        Vector3 worldPosition = GridToWorld(new Vector2Int(x, y));

        if (icon != null)
        {
            worldPosition = icon.transform.position;
            icon.transform.SetParent(null, true);
        }

        return new ClearedSymbolVisual(symbolType, worldPosition, icon);
    }

    private void AnimateAndDestroyClearedBlock(GameObject block)
    {
        if (block == null)
            return;

        if (!animateClearedBlocks)
        {
            Destroy(block);
            return;
        }

        Transform t = block.transform;
        t.DOKill();
        t.SetParent(null, true);

        Vector3 start = t.position;
        Vector3 jumpTarget = start + new Vector3(Random.Range(-clearScatterX, clearScatterX), 0.25f, 0f);
        Vector3 fallTarget = jumpTarget + Vector3.down * clearFallDistance;

        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(true); // Run in unscaled time so pausing doesn't leave blocks stuck
        seq.Append(t.DOJump(jumpTarget, clearJumpPower, 1, clearJumpDuration).SetEase(Ease.OutQuad));
        seq.Append(t.DOMove(fallTarget, clearFallDuration).SetEase(Ease.InQuad));
        seq.Join(t.DORotate(new Vector3(0f, 0f, Random.Range(-180f, 180f)), clearFallDuration, RotateMode.FastBeyond360));

        // Fade out all sprites during the fall
        var sprites = block.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in sprites)
        {
            if (sr != null)
                seq.Join(sr.DOFade(0f, clearFallDuration));
        }

        seq.OnComplete(() =>
        {
            if (block != null)
                Destroy(block);
        });
    }

    public int ClearFullLines()
    {
        return ClearFullLinesDetailed().CellsCleared;
    }

    public LineClearResult ClearFullLinesDetailed()
    {
        if (cells == null)
            return new LineClearResult(0, 0, 0);

        // לפני ניקוי בפועל של שורות/עמודות – הסר היילייט preview, כי עכשיו זה כבר קורה באמת
        ClearPreviewClear();

        bool[] fullRows = new bool[height];
        bool[] fullCols = new bool[width];

        for (int y = 0; y < height; y++)
        {
            bool full = true;
            for (int x = 0; x < width; x++)
            {
                if (cells[x, y] == null || !cells[x, y].occupied)
                {
                    full = false;
                    break;
                }
            }
            fullRows[y] = full;
        }

        for (int x = 0; x < width; x++)
        {
            bool full = true;
            for (int y = 0; y < height; y++)
            {
                if (cells[x, y] == null || !cells[x, y].occupied)
                {
                    full = false;
                    break;
                }
            }
            fullCols[x] = full;
        }

        bool[,] shouldClear = new bool[width, height];

        for (int y = 0; y < height; y++)
        {
            if (!fullRows[y])
                continue;

            for (int x = 0; x < width; x++)
                shouldClear[x, y] = true;
        }

        for (int x = 0; x < width; x++)
        {
            if (!fullCols[x])
                continue;

            for (int y = 0; y < height; y++)
                shouldClear[x, y] = true;
        }

        int cleared = 0;
        int rowsCleared = 0;
        int colsCleared = 0;

        for (int y = 0; y < height; y++)
        {
            if (fullRows[y])
                rowsCleared++;
        }

        for (int x = 0; x < width; x++)
        {
            if (fullCols[x])
                colsCleared++;
        }

        Dictionary<ColectionTypes, int> clearedSymbols = null;
        List<ClearedSymbolVisual> clearedSymbolVisuals = null;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!shouldClear[x, y])
                    continue;

                // Collect symbol data before clearing
                if (cellSymbolTypes != null && cellSymbolTypes[x, y] >= 0)
                {
                    var st = (ColectionTypes)cellSymbolTypes[x, y];
                    if (clearedSymbols == null)
                        clearedSymbols = new Dictionary<ColectionTypes, int>();
                    if (clearedSymbols.ContainsKey(st))
                        clearedSymbols[st]++;
                    else
                        clearedSymbols[st] = 1;

                    var visual = CaptureSymbolVisual(x, y);
                    if (visual != null)
                    {
                        if (clearedSymbolVisuals == null)
                            clearedSymbolVisuals = new List<ClearedSymbolVisual>();
                        clearedSymbolVisuals.Add(visual);
                    }
                }

                if (cells[x, y] != null && cells[x, y].occupied)
                {
                    cells[x, y].SetOccupied(false);
                    cleared++;
                }

                DestroyCellContents(x, y);
            }
        }

        return new LineClearResult(rowsCleared, colsCleared, cleared, clearedSymbols, clearedSymbolVisuals);
    }

    /// <summary>
    /// מחזיר את כל התאים שהיו נמחקים אם היינו ממקמים את הצורה בתא הנתון, ללא שינוי בפועל של הגריד.
    /// משתמש באותה לוגיקת שורות/עמודות מלאות כמו ClearFullLinesDetailed, אבל על בסיס מצב היפותטי.
    /// </summary>
    public System.Collections.Generic.List<Vector2Int> GetPreviewClearCells(Shape shape, Vector2Int targetCell, System.Collections.Generic.List<Vector2Int> buffer = null)
    {
        var result = buffer ?? new System.Collections.Generic.List<Vector2Int>();
        result.Clear();

        if (shape == null || cells == null)
            return result;

        // בניית סט של תאים שהצורה תתפוס אם נמקם אותה ב-targetCell
        var shapeCells = new System.Collections.Generic.HashSet<Vector2Int>();

        var blocks = shape.GetComponentsInChildren<Transform>()
            .Where(t => t != shape.transform)
            .ToList();

        if (blocks.Count == 0)
            return result;

        foreach (var block in blocks)
        {
            if (block == null)
                continue;

            Vector2 localPos = block.localPosition;
            Vector2Int blockOffset = new Vector2Int(
                Mathf.RoundToInt(localPos.x / cellSize),
                Mathf.RoundToInt(localPos.y / cellSize)
            );

            Vector2Int cellPos = targetCell + blockOffset;

            if (IsInside(cellPos))
                shapeCells.Add(cellPos);
        }

        bool[] fullRows = new bool[height];
        bool[] fullCols = new bool[width];

        // בדיקת שורות מלאות היפותטיות
        for (int y = 0; y < height; y++)
        {
            bool full = true;
            for (int x = 0; x < width; x++)
            {
                bool occupiedNow = cells[x, y] != null && cells[x, y].occupied;
                bool occupiedWithShape = occupiedNow || shapeCells.Contains(new Vector2Int(x, y));

                if (!occupiedWithShape)
                {
                    full = false;
                    break;
                }
            }
            fullRows[y] = full;
        }

        // בדיקת עמודות מלאות היפותטיות
        for (int x = 0; x < width; x++)
        {
            bool full = true;
            for (int y = 0; y < height; y++)
            {
                bool occupiedNow = cells[x, y] != null && cells[x, y].occupied;
                bool occupiedWithShape = occupiedNow || shapeCells.Contains(new Vector2Int(x, y));

                if (!occupiedWithShape)
                {
                    full = false;
                    break;
                }
            }
            fullCols[x] = full;
        }

        // איסוף כל התאים שיימחקו (בשורות ובעמודות המלאות)
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!fullRows[y] && !fullCols[x])
                    continue;

                result.Add(new Vector2Int(x, y));
            }
        }

        return result;
    }

    public int ClearRow(int y)
    {
        if (cells == null)
            return 0;

        if (y < 0 || y >= height)
            return 0;

        int cleared = 0;

        for (int x = 0; x < width; x++)
        {
            if (cells[x, y] != null && cells[x, y].occupied)
            {
                cells[x, y].SetOccupied(false);
                cleared++;
            }

            DestroyCellContents(x, y);
        }

        return cleared;
    }

    public int ClearColumn(int x)
    {
        if (cells == null)
            return 0;

        if (x < 0 || x >= width)
            return 0;

        int cleared = 0;

        for (int y = 0; y < height; y++)
        {
            if (cells[x, y] != null && cells[x, y].occupied)
            {
                cells[x, y].SetOccupied(false);
                cleared++;
            }

            DestroyCellContents(x, y);
        }

        return cleared;
    }

    // ===== פונקציות REVIVE משולבות - אפשרות 1 + 2 =====

    /// <summary>
    /// פונקציית Revive חכמה משולבת: מוחקת שורות/עמודות תפוסות ובודקת שיש מקום לצורות
    /// </summary>
    public int SmartReviveWithValidation(System.Collections.Generic.List<Shape> remainingShapes)
    {
        if (cells == null)
        {
            Debug.LogError("[SmartRevive] Cells array is null!");
            return 0;
        }

        if (remainingShapes == null || remainingShapes.Count == 0)
        {
            Debug.LogWarning("[SmartRevive] No shapes to validate");
            return ReviveClearMostOccupiedRowAndColumn();
        }

        int totalCleared = 0;
        int maxAttempts = 3;

        Debug.Log($"[SmartRevive] Starting revive for {remainingShapes.Count} shapes");

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // מחק את השורה והעמודה הכי תפוסים (אפשרות 1)
            int cleared = ReviveClearMostOccupiedRowAndColumn();
            totalCleared += cleared;

            Debug.Log($"[SmartRevive] Attempt {attempt + 1}/{maxAttempts}: Cleared {cleared} cells (total: {totalCleared})");

            // בדוק אם יש מקום לכל הצורות (אפשרות 2)
            bool hasSpace = ValidateSpaceForShapes(remainingShapes);

            if (hasSpace)
            {
                Debug.Log($"[SmartRevive] ✓ Success! Cleared {totalCleared} cells in {attempt + 1} attempts");
                return totalCleared;
            }

            Debug.Log($"[SmartRevive] ⚠ Attempt {attempt + 1}/{maxAttempts} - still no space, trying again...");
        }

        Debug.LogError($"[SmartRevive] ✗ Failed after {maxAttempts} attempts! Total cleared: {totalCleared}");
        return totalCleared;
    }

    /// <summary>
    /// אפשרות 1: מוחק את השורה והעמודה הכי תפוסים
    /// </summary>
    private int ReviveClearMostOccupiedRowAndColumn()
    {
        if (cells == null)
            return 0;

        // מצא את השורה הכי תפוסה
        int mostOccupiedRow = -1;
        int maxRowOccupancy = 0;

        for (int y = 0; y < height; y++)
        {
            int occupancy = 0;
            for (int x = 0; x < width; x++)
            {
                if (cells[x, y] != null && cells[x, y].occupied)
                    occupancy++;
            }

            if (occupancy > maxRowOccupancy)
            {
                maxRowOccupancy = occupancy;
                mostOccupiedRow = y;
            }
        }

        // מצא את העמודה הכי תפוסה
        int mostOccupiedCol = -1;
        int maxColOccupancy = 0;

        for (int x = 0; x < width; x++)
        {
            int occupancy = 0;
            for (int y = 0; y < height; y++)
            {
                if (cells[x, y] != null && cells[x, y].occupied)
                    occupancy++;
            }

            if (occupancy > maxColOccupancy)
            {
                maxColOccupancy = occupancy;
                mostOccupiedCol = x;
            }
        }

        int cleared = 0;

        if (mostOccupiedRow >= 0)
        {
            cleared += ClearRow(mostOccupiedRow);
            Debug.Log($"[ReviveClear] Cleared most occupied row {mostOccupiedRow} ({maxRowOccupancy} cells)");
        }

        if (mostOccupiedCol >= 0)
        {
            cleared += ClearColumn(mostOccupiedCol);
            Debug.Log($"[ReviveClear] Cleared most occupied column {mostOccupiedCol} ({maxColOccupancy} cells)");
        }

        return cleared;
    }

    /// <summary>
    /// אפשרות 2: בודק אם יש מקום בגריד לכל הצורות
    /// </summary>
    private bool ValidateSpaceForShapes(System.Collections.Generic.List<Shape> shapes)
    {
        if (shapes == null || shapes.Count == 0)
            return true;

        foreach (var shape in shapes)
        {
            if (shape == null)
                continue;

            if (!HasAnyValidPlacementForShape(shape))
            {
                Debug.LogWarning($"[ValidateSpace] Shape '{shape.name}' has no valid placement");
                return false;
            }
        }

        Debug.Log($"[ValidateSpace] ✓ All {shapes.Count} shapes have valid placements");
        return true;
    }

    /// <summary>
    /// בודק אם יש מיקום אפשרי לצורה בגריד
    /// </summary>
    private bool HasAnyValidPlacementForShape(Shape shape)
    {
        if (shape == null || cells == null)
            return false;

        // קבל את הבלוקים של הצורה
        var blocks = shape.GetComponentsInChildren<Transform>()
            .Where(t => t != shape.transform)
            .ToList();

        if (blocks == null || blocks.Count == 0)
            return false;

        // נסה כל מיקום אפשרי בגריד
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int targetPos = new Vector2Int(x, y);

                if (CanPlaceShapeAtInternal(shape, blocks, targetPos))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// בודק אם אפשר למקם צורה במיקום ספציפי (internal helper)
    /// </summary>
    private bool CanPlaceShapeAtInternal(Shape shape, System.Collections.Generic.List<Transform> blocks, Vector2Int position)
    {
        if (shape == null || blocks == null || cells == null)
            return false;

        foreach (var block in blocks)
        {
            if (block == null)
                continue;

            // חשב את המיקום של הבלוק ביחס לצורה
            Vector2 localPos = block.localPosition;
            Vector2Int blockOffset = new Vector2Int(
                Mathf.RoundToInt(localPos.x / cellSize),
                Mathf.RoundToInt(localPos.y / cellSize)
            );

            Vector2Int cellPos = position + blockOffset;

            // בדוק אם התא בתוך הגריד ופנוי
            if (!IsInside(cellPos) || IsOccupied(cellPos))
            {
                return false;
            }
        }

        return true;
    }

    // ===== פונקציית Revive המקורית (לתאימות לאחור) =====
    public int ReviveClearOneRowAndOneColumn()
    {
        if (cells == null)
            return 0;

        var occupiedRows = new System.Collections.Generic.List<int>(height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (cells[x, y] != null && cells[x, y].occupied)
                {
                    occupiedRows.Add(y);
                    break;
                }
            }
        }

        var occupiedCols = new System.Collections.Generic.List<int>(width);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (cells[x, y] != null && cells[x, y].occupied)
                {
                    occupiedCols.Add(x);
                    break;
                }
            }
        }

        int cleared = 0;

        if (occupiedRows.Count > 0)
        {
            int y = occupiedRows[Random.Range(0, occupiedRows.Count)];
            cleared += ClearRow(y);
        }

        if (occupiedCols.Count > 0)
        {
            int x = occupiedCols[Random.Range(0, occupiedCols.Count)];
            cleared += ClearColumn(x);
        }

        return cleared;
    }

    // ✅ תיקון: CanPlaceShape עכשיו עובד נכון!
    public bool CanPlaceShape(Shape shape, Vector2Int targetCell)
    {
        if (shape == null)
        {
            Debug.LogError("[CanPlaceShape] Shape is null!");
            return false;
        }

        if (!IsInside(targetCell))
        {
            return false;
        }

        // קבל את כל הבלוקים של הצורה
        var blocks = shape.GetComponentsInChildren<Transform>()
            .Where(t => t != shape.transform)
            .ToList();

        if (blocks.Count == 0)
            return false;

        // בדוק כל בלוק
        foreach (var block in blocks)
        {
            Vector2 localPos = block.localPosition;
            Vector2Int blockOffset = new Vector2Int(
                Mathf.RoundToInt(localPos.x / cellSize),
                Mathf.RoundToInt(localPos.y / cellSize)
            );

            Vector2Int cellPos = targetCell + blockOffset;

            if (!IsInside(cellPos) || IsOccupied(cellPos))
            {
                return false;
            }
        }

        return true;
    }

    // ✅ תיקון: ClearCellsForShape עכשיו מנקה תאים!
    public void ClearCellsForShape(Shape shape, Vector2Int targetCell)
    {
        if (shape == null || cells == null)
        {
            Debug.LogError("[ClearCellsForShape] Shape or cells is null!");
            return;
        }

        // קבל את כל הבלוקים של הצורה
        var blocks = shape.GetComponentsInChildren<Transform>()
            .Where(t => t != shape.transform)
            .ToList();

        Debug.Log($"[ClearCellsForShape] Clearing {blocks.Count} cells for shape '{shape.name}' at {targetCell}");

        // נקה כל תא שהצורה תופסת
        foreach (var block in blocks)
        {
            Vector2 localPos = block.localPosition;
            Vector2Int blockOffset = new Vector2Int(
                Mathf.RoundToInt(localPos.x / cellSize),
                Mathf.RoundToInt(localPos.y / cellSize)
            );

            Vector2Int cellPos = targetCell + blockOffset;

            if (IsInside(cellPos) && IsOccupied(cellPos))
            {
                SetOccupied(cellPos, false);

                if (placedBlocks != null && placedBlocks[cellPos.x, cellPos.y] != null)
                {
                    Destroy(placedBlocks[cellPos.x, cellPos.y]);
                    placedBlocks[cellPos.x, cellPos.y] = null;
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (width <= 0 || height <= 0)
            return;

        CenterOrigin();

        Gizmos.color = Color.gray;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 center = GridToWorld(new Vector2Int(x, y));
                Gizmos.DrawWireCube(center, Vector3.one * cellSize * 0.95f);
            }
        }
    }

    public void HandleOnLevelRestartedEvent (LevelData levelData)
    {
        Restart();
    }

    public void Restart ()
    {
        Clear();

        // Re-apply initial blocks after clearing
        if (storedInitialBlocks != null && storedInitialBlocks.Count > 0)
        {
            ApplyInitialBlocks(storedInitialBlocks);
        }
        else if (ActiveRandomInitialShapeCount() > 0)
        {
            ApplyInitialBlocks(BuildRandomInitialBlocks());
        }
    }

    private void AnimateInitialBlockEntry(GameObject blockObj, Vector3 targetPosition, float delay)
    {
        // Choose random side to enter from
        Vector3 entryDirection = GetRandomEntryDirectionForInitialBlock();
        Vector3 startPosition = targetPosition + entryDirection * initialBlockEntryDistance;
        
        blockObj.transform.position = startPosition;
        
        // Animate to target position
        blockObj.transform.DOMove(targetPosition, initialBlockEntryDuration)
            .SetDelay(delay)
            .SetEase(initialBlockEntryEase);
    }

    private Vector3 GetRandomEntryDirectionForInitialBlock()
    {
        // Randomly choose one of 4 sides (left, right, top, bottom)
        int side = UnityEngine.Random.Range(0, 4);
        
        switch (side)
        {
            case 0: return Vector3.left;   // From left
            case 1: return Vector3.right;  // From right
            case 2: return Vector3.up;     // From top
            case 3: return Vector3.down;   // From bottom
            default: return Vector3.left;
        }
    }
}
