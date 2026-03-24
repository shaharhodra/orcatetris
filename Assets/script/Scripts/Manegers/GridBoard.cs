using UnityEngine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

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
    private System.Collections.Generic.HashSet<Vector2Int> hoveredCells;
    private System.Collections.Generic.HashSet<Vector2Int> previewClearCells;
    [SerializeField] private GridBoard board;

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

        ClearPreviewClear();

        foreach (var pos in positions)
        {
            if (!IsInside(pos))
                continue;

            var cell = cells[pos.x, pos.y];
            if (cell == null)
                continue;

            cell.SetPreviewClear(true);
            previewClearCells.Add(pos);
        }
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

        CenterOrigin();

        cells = new GridCell[width, height];
        placedBlocks = new GameObject[width, height];

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

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!shouldClear[x, y])
                    continue;

                if (cells[x, y] != null && cells[x, y].occupied)
                {
                    cells[x, y].SetOccupied(false);
                    cleared++;
                }

                if (placedBlocks != null && placedBlocks[x, y] != null)
                {
                    Destroy(placedBlocks[x, y]);
                    placedBlocks[x, y] = null;
                }
            }
        }

        return new LineClearResult(rowsCleared, colsCleared, cleared);
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

            if (placedBlocks != null && placedBlocks[x, y] != null)
            {
                Destroy(placedBlocks[x, y]);
                placedBlocks[x, y] = null;
            }
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

            if (placedBlocks != null && placedBlocks[x, y] != null)
            {
                Destroy(placedBlocks[x, y]);
                placedBlocks[x, y] = null;
            }
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
    }
}
