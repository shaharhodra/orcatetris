using UnityEngine;
using System;
using System.Collections.Generic;
using DG.Tweening;

public class GridPlacer : MonoBehaviour
{
    [SerializeField] private GridBoard board;
    // No need to serialize ScoreManager; use the singleton instance instead.

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    public event Action<Shape> OnShapePlaced;
    public event Action<LineClearResult> OnLinesCleared;
    public event Action OnNoLinesCleared;
    public event Action<List<Vector2Int>> OnBlocksPlacedBeforeClear;

    [Header("Scoring")]
    [SerializeField] private int scorePerPlacedCell = 1;
    [SerializeField] private int scorePerClearedCell = 2;

    [Header("Block Entry Animation")]
    [SerializeField] private bool animateBlockEntry = false;
    [SerializeField] private float blockEntryDuration = 0.5f;
    [SerializeField] private Ease blockEntryEase = Ease.OutBack;
    [SerializeField] private float blockEntryDistance = 6f;
    [SerializeField] private float blockEntryDelayPerBlock = 0.05f;

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

        var placedCells = new List<Vector2Int>(offsets.Length);
        int blockIndex = 0;

        foreach (var offset in offsets)
        {
            Vector2Int cell = targetCell + offset;
            board.SetOccupied(cell, true);
            placedCells.Add(cell);

            if (childBlocks.TryGetValue(offset, out var block) && block != null)
            {
                block.SetParent(board.transform, true);
                Vector3 targetPosition = board.GridToWorld(cell);
                
                if (animateBlockEntry)
                {
                    AnimateBlockEntry(block, targetPosition, blockIndex * blockEntryDelayPerBlock);
                }
                else
                {
                    block.position = targetPosition;
                }
                
                board.SetPlacedBlock(cell, block.gameObject);

                // Mark as placed so ThemeBlock uses squareColor
                var themeBlock = block.GetComponent<ThemeBlock>();
                if (themeBlock != null)
                    themeBlock.MarkAsPlaced();

                // If the block has a predefined symbol, register it on the grid
                var blockSymbol = block.GetComponent<BlockSymbol>();
                if (blockSymbol != null && blockSymbol.HasSymbol)
                {
                    var icon = blockSymbol.DetachIcon();
                    if (icon != null)
                        icon.transform.SetParent(board.transform, true);
                    board.SetSymbol(cell, blockSymbol.SymbolType, icon);
                }
                
                blockIndex++;
            }
        }

        // Fire before line clearing so Adventure symbols can be assigned to just-placed blocks
        OnBlocksPlacedBeforeClear?.Invoke(placedCells);

        var scoreManager = ScoreManager.instance;

        LineClearResult clearResult;

        bool isClassicMode = AppManager.instance != null &&
                             AppManager.instance.CurrentGameMode == AppManager.GameMode.Classic;

        if (scoreManager != null && isClassicMode)
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
                SoundManager.instance.PlayLineClear();
                SoundManager.instance.PlayCombo(clearResult.LinesCleared);
            }

            if (board.IsBoardEmpty() && ThemeManager.instance != null)
                ThemeManager.instance.TriggerBoardCleared();

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

    private void AnimateBlockEntry(Transform block, Vector3 targetPosition, float delay)
    {
        // Choose random side to enter from
        Vector3 entryDirection = GetRandomEntryDirection();
        Vector3 startPosition = targetPosition + entryDirection * blockEntryDistance;
        
        block.position = startPosition;
        
        // Animate to target position
        block.DOMove(targetPosition, blockEntryDuration)
            .SetDelay(delay)
            .SetEase(blockEntryEase);
    }

    private Vector3 GetRandomEntryDirection()
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
