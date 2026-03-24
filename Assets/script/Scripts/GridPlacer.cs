using UnityEngine;
using System;

public class GridPlacer : MonoBehaviour
{
    [SerializeField] private GridBoard board;
    // No need to serialize ScoreManager; use the singleton instance instead.

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    public event Action<Shape> OnShapePlaced;
    public event Action<LineClearResult> OnLinesCleared;
    public event Action OnNoLinesCleared;

    [Header("Scoring")]
    [SerializeField] private int scorePerPlacedCell = 1;
    [SerializeField] private int scorePerClearedCell = 2;

    public bool CanPlaceShape(Shape shape, Vector2Int targetCell)
    {
        var offsets = shape.GetCells(board.cellSize);
        foreach (var offset in offsets)
        {
            Vector2Int cell = targetCell + offset;

            if (!board.IsInside(cell))
            {
                //Debug.Log($"[CanPlaceShape] '{shape.name}' FAIL at {targetCell}: cell {cell} is outside grid.");
                return false;
            }

            if (board.IsOccupied(cell))
            {
                //Debug.Log($"[CanPlaceShape] '{shape.name}' FAIL at {targetCell}: cell {cell} is occupied.");
                return false;
            }
        }

        //Debug.Log($"[CanPlaceShape] '{shape.name}' can be placed at {targetCell}.");
        return true;
    }

    public void PlaceShape(Shape shape, Vector2Int targetCell)
    {
        if (shape == null)
            return;

        var offsets = shape.GetCells(board.cellSize);

        var childBlocks = new System.Collections.Generic.Dictionary<Vector2Int, Transform>(offsets.Length);
        int childCount = shape.transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            var child = shape.transform.GetChild(i);
            if (child == null)
                continue;

            if (child.GetComponent<Collider2D>() == null)
                continue;

            Vector3 lp = child.localPosition;
            int x = Mathf.RoundToInt(lp.x / board.cellSize);
            int y = Mathf.RoundToInt(lp.y / board.cellSize);
            var key = new Vector2Int(x, y);
            if (!childBlocks.ContainsKey(key))
                childBlocks.Add(key, child);
        }

        foreach (var offset in offsets)
        {
            Vector2Int cell = targetCell + offset;
            board.SetOccupied(cell, true);

            if (childBlocks.TryGetValue(offset, out var block) && block != null)
            {
                block.SetParent(board.transform, true);
                block.position = board.GridToWorld(cell);
                board.SetPlacedBlock(cell, block.gameObject);
            }
        }

        var scoreManager = ScoreManager.instance;

        LineClearResult clearResult;

        if (scoreManager != null)
        {
            scoreManager.AddScore(offsets.Length * scorePerPlacedCell);
            clearResult = board.ClearFullLinesDetailed();
            if (clearResult.CellsCleared > 0)
                scoreManager.AddScore(clearResult.CellsCleared * scorePerClearedCell);
        }
        else
        {
            clearResult = board.ClearFullLinesDetailed();
        }

        if (clearResult.LinesCleared > 0)
        {
            if (debugLogs)
               // Debug.Log($"[GridPlacer] Lines cleared: lines={clearResult.LinesCleared} (rows={clearResult.RowsCleared}, cols={clearResult.ColumnsCleared}), cells={clearResult.CellsCleared}");

            if (SoundManager.instance != null)
            {
                SoundManager.instance.PlayCombo(clearResult.LinesCleared);
            }

            OnLinesCleared?.Invoke(clearResult);
        }
        else
        {
            if (debugLogs)
              //  Debug.Log("[GridPlacer] No lines cleared -> breaking combo");
            OnNoLinesCleared?.Invoke();
        }

        // אחרי שהלוח עודכן וקווים נמחקו, נודיע על הצבת הצורה.
        OnShapePlaced?.Invoke(shape);

        // כעת אפשר להשמיד את אובייקט הצורה המקורי
        Destroy(shape.gameObject);
    }
}
