using UnityEngine;

public class GridBoard : MonoBehaviour
{
    [Header("Grid Size")]
    public int width = 6;
    public int height = 8;

    [Header("Cell Settings")]
    public float cellSize = 1f;
    public Vector2 origin = Vector2.zero;
    [Header("Cell Prefab")]
    public GridCell cellPrefab;

    private GridCell[,] cells;
    private System.Collections.Generic.HashSet<Vector2Int> hoveredCells;

    private void Start()
    {
        BuildGrid();
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
        ClearHover();
        ClearGridObjects();
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
            }
        }
    }

    private void BuildGrid()
    {
        if (cellPrefab == null)
        {
            Debug.LogError("GridBoard: cellPrefab is not assigned");
            return;
        }

        CenterOrigin();

        cells = new GridCell[width, height];

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
        cells = null;

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
