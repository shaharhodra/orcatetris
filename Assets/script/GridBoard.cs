using UnityEngine;
using System.Collections;

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

    private void Start()
    {
        Debug.Log($"[GridBoard] Start on {gameObject.name}, buildOnStart = {buildOnStart}, size = {width}x{height}");

        if (buildOnStart)
        {
            BuildGrid();
        }
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

    public void ApplySize(int newWidth, int newHeight)
    {
        width = Mathf.Max(1, newWidth);
        height = Mathf.Max(1, newHeight);
        RebuildGrid();
    }

    public void RebuildGrid()
    {
        Debug.Log("[GridBoard] RebuildGrid requested");

        if (Application.isPlaying)
        {
            Debug.Log("[GridBoard] RebuildGrid (play mode) requested");

            StopAllCoroutines();
            StartCoroutine(RebuildGridCoroutine());
            return;
        }

        ClearHover();
        ClearGridObjects();
        BuildGrid();
    }

    private IEnumerator RebuildGridCoroutine()
    {
        Debug.Log("[GridBoard] RebuildGridCoroutine started");
        ClearHover();
        ClearGridObjects();
        yield return null;
        BuildGrid();
    }

    public void Clear()
    {
        // מנקה את מצב התפוס לתאים קיימים
        if (cells == null) return;

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
            Debug.LogWarning($"[GridBoard] cellPrefab is NULL on {gameObject.name}, cannot build grid");
            return;
        }

        Debug.Log($"[GridBoard] Building grid on {gameObject.name}, size = {width}x{height}");

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
        Debug.Log($"[GridBoard] ClearGridObjects on {gameObject.name}");

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
        if (cells == null)
            return 0;

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

        return cleared;
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
}
