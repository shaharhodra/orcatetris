using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Looks for a "gift" opportunity: a run of 2-3 completely empty rows (or columns)
/// that could be cleared all at once. When found, greedily builds a sequence of up
/// to 3 shapes from the pool that together fill the run exactly, so the tray can be
/// made to offer exactly the pieces needed to pull off a satisfying multi-line clear.
///
/// Plain C# class, no MonoBehaviour/scene dependency — can be driven directly from
/// a test with a hand-built grid and shape list, same as ShapeSelectionAlgorithm.
/// </summary>
public class GiftOpportunityDetector
{
    public const int MinEmptyLines = 2;
    public const int MaxEmptyLines = 3;
    private const int MaxComboShapes = 3;

    /// <summary>
    /// Returns the shapes to offer (2-3 entries) if a combo was found that exactly
    /// fills a run of empty rows/columns, or null if no such opportunity exists.
    /// </summary>
    public List<Shape> TryFindGift(bool[,] grid, IReadOnlyList<Shape> shapePool, float cellSize)
    {
        if (grid == null || shapePool == null || shapePool.Count == 0)
            return null;

        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        var pool = shapePool.Where(p => p != null).ToList();
        if (pool.Count == 0)
            return null;

        return TryFindComboForOrientation(grid, width, height, pool, cellSize, horizontal: true)
            ?? TryFindComboForOrientation(grid, width, height, pool, cellSize, horizontal: false);
    }

    private List<Shape> TryFindComboForOrientation(bool[,] grid, int width, int height, List<Shape> pool, float cellSize, bool horizontal)
    {
        FindLongestEmptyRun(grid, width, height, horizontal, out int runStart, out int runLen);

        if (runLen < MinEmptyLines)
            return null;

        int lineLength = horizontal ? width : height;
        int targetLen = Mathf.Min(runLen, MaxEmptyLines);

        // Prefer the biggest run we can fully solve, down to the minimum worth gifting.
        for (int n = targetLen; n >= MinEmptyLines; n--)
        {
            var combo = SolveGreedyCombo(grid, width, height, pool, cellSize, horizontal, runStart, n, lineLength);
            if (combo != null)
                return combo;
        }

        return null;
    }

    private void FindLongestEmptyRun(bool[,] grid, int width, int height, bool horizontal, out int start, out int length)
    {
        int lineCount = horizontal ? height : width;

        start = -1;
        length = 0;

        int runStart = -1;
        for (int i = 0; i <= lineCount; i++)
        {
            bool empty = i < lineCount && IsLineEmpty(grid, width, height, horizontal, i);
            if (empty)
            {
                if (runStart < 0) runStart = i;
            }
            else if (runStart >= 0)
            {
                int len = i - runStart;
                if (len > length)
                {
                    length = len;
                    start = runStart;
                }
                runStart = -1;
            }
        }
    }

    private bool IsLineEmpty(bool[,] grid, int width, int height, bool horizontal, int index)
    {
        int lineLength = horizontal ? width : height;
        for (int j = 0; j < lineLength; j++)
        {
            bool occupied = horizontal ? grid[j, index] : grid[index, j];
            if (occupied)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Greedily picks up to MaxComboShapes shapes, each time taking whichever
    /// (shape, anchor) covers the most still-empty cells inside the target run,
    /// until the run is either fully covered (success) or nothing can make further
    /// progress within the shape budget (failure — no gift for this run size).
    /// </summary>
    private List<Shape> SolveGreedyCombo(bool[,] grid, int width, int height, List<Shape> pool, float cellSize, bool horizontal, int lineStart, int lineSpan, int lineLength)
    {
        var working = (bool[,])grid.Clone();
        var result = new List<Shape>();

        int targetCells = lineSpan * lineLength;
        int coveredCells = 0;

        for (int step = 0; step < MaxComboShapes && coveredCells < targetCells; step++)
        {
            if (!TryPickBestCoveringShape(working, width, height, pool, cellSize, horizontal, lineStart, lineSpan,
                    out Shape bestShape, out Vector2Int bestAnchor, out Vector2Int[] bestOffsets, out int bestGain))
            {
                return null; // nothing left can make progress — this run size doesn't work
            }

            foreach (var off in bestOffsets)
            {
                var cell = bestAnchor + off;
                working[cell.x, cell.y] = true;
            }

            result.Add(bestShape);
            coveredCells += bestGain;
        }

        return coveredCells >= targetCells ? result : null;
    }

    private bool TryPickBestCoveringShape(bool[,] grid, int width, int height, List<Shape> pool, float cellSize, bool horizontal, int lineStart, int lineSpan,
        out Shape bestShape, out Vector2Int bestAnchor, out Vector2Int[] bestOffsets, out int bestGain)
    {
        bestShape = null;
        bestAnchor = default;
        bestOffsets = null;
        bestGain = 0;
        int bestScore = 0;

        foreach (var prefab in pool)
        {
            var offsets = prefab.GetCells(cellSize);
            if (offsets == null || offsets.Length == 0)
                continue;

            for (int ax = 0; ax < width; ax++)
            {
                for (int ay = 0; ay < height; ay++)
                {
                    var anchor = new Vector2Int(ax, ay);
                    if (!CanPlaceOffsetsAtGrid(grid, width, height, anchor, offsets))
                        continue;

                    int gain = 0;
                    bool touchesOutsideRun = false;
                    foreach (var off in offsets)
                    {
                        var cell = anchor + off;
                        int lineIndex = horizontal ? cell.y : cell.x;
                        if (lineIndex >= lineStart && lineIndex < lineStart + lineSpan)
                            gain++;
                        else
                            touchesOutsideRun = true;
                    }

                    if (gain == 0)
                        continue;

                    // Prefer combos that don't spill outside the target run (keeps the
                    // clear clean); spillover is still allowed as a last resort.
                    int score = touchesOutsideRun ? gain - 1 : gain;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestGain = gain;
                        bestShape = prefab;
                        bestAnchor = anchor;
                        bestOffsets = offsets;
                    }
                }
            }
        }

        return bestShape != null;
    }

    private static bool CanPlaceOffsetsAtGrid(bool[,] grid, int width, int height, Vector2Int anchor, Vector2Int[] offsets)
    {
        foreach (var off in offsets)
        {
            var cell = anchor + off;
            if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= height)
                return false;
            if (grid[cell.x, cell.y])
                return false;
        }
        return true;
    }
}
