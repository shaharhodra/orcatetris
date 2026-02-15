using UnityEngine;
using System;

public class GridPlacer : MonoBehaviour
{
    [SerializeField] private GridBoard board;
    [SerializeField] private ScoreManager scoreManager;

    public event Action<Shape> OnShapePlaced;

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
                return false;

            if (board.IsOccupied(cell))
                return false;
        }

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

        OnShapePlaced?.Invoke(shape);

        Destroy(shape.gameObject);

        if (scoreManager != null)
        {
            scoreManager.AddScore(offsets.Length * scorePerPlacedCell);
            int cleared = board.ClearFullLines();
            if (cleared > 0)
                scoreManager.AddScore(cleared * scorePerClearedCell);
        }
        else
        {
            board.ClearFullLines();
        }
    }
}
