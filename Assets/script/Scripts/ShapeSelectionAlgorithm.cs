using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Picks which 3 shapes to offer next in the tray. No lookahead, no hypothetical
/// combos — each candidate shape is judged only on what IT achieves by itself,
/// placed at its single best spot on the board exactly as it looks right now.
/// The 3 shapes offered are simply the top 3 by that score: a full clear beats
/// everything, then whoever clears the most lines, then (as a tiebreak) the
/// smaller/simpler shape. If fewer than 3 shapes can clear anything, the
/// remaining slots go to whichever placeable shapes leave the board healthiest.
/// This keeps every offered shape something the player can actually look at
/// and see why it was offered — no 2-3-move-deep setup a player has no way to
/// perceive.
///
/// Difficulty also controls how many of the 3 slots are guaranteed placeable
/// at all: at max helpfulness all 3 are real options, at max difficulty only
/// 1 is — the other 2 are genuine decoys (shapes that don't fit anywhere on
/// the board right now), scaling linearly in between. On a mostly empty board
/// almost everything fits somewhere, so this only bites once the board has
/// enough clutter for decoys to actually exist.
///
/// Plain C# class, no MonoBehaviour/scene dependency — can be driven directly
/// from a test with a hand-built grid and shape list.
/// </summary>
public class ShapeSelectionAlgorithm
{
    /// <param name="grid">Current board occupancy, [x, y].</param>
    /// <param name="shapePool">All shapes that may be offered.</param>
    /// <param name="cellSize">Passed through to Shape.GetCells to read each shape's footprint.</param>
    /// <param name="difficulty">
    /// 1 = most helpful/easy, 0 = most chaotic/hard. Controls two things:
    /// how many of the 3 slots are guaranteed placeable at all (3 at
    /// difficulty 1 down to 1 at difficulty 0, the rest filled with decoys
    /// that don't fit anywhere right now), and — as a fallback when fewer
    /// than the real-slot count can clear anything on their own — whether
    /// remaining real slots prefer whichever placeable shape leaves the
    /// board healthiest (1) or are picked without regard to board health (0).
    /// </param>
    public List<Shape> SelectTray(bool[,] grid, IReadOnlyList<Shape> shapePool, float cellSize, float difficulty)
    {
        var result = new List<Shape>();
        if (grid == null || shapePool == null || shapePool.Count == 0)
            return result;

        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        var pool = shapePool.Where(p => p != null).Distinct().ToList();
        if (pool.Count == 0)
            return result;

        // Shuffle so ties don't always resolve to whichever prefab happens to
        // sit first in the pool.
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (pool[i], pool[swapIndex]) = (pool[swapIndex], pool[i]);
        }

        // Score every shape independently on the CURRENT board — what you see
        // is what you get: each shape's reported clear is exactly what placing
        // it alone, right now, achieves. No other shape's placement is assumed.
        var placeable = new List<(Shape shape, bool fullClear, int clearedLines, int cellCount, bool[,] resultGrid)>();
        foreach (var prefab in pool)
        {
            var offsets = prefab.GetCells(cellSize);
            if (offsets == null || offsets.Length == 0)
                continue;

            if (!TryFindBestAnchor(grid, width, height, offsets, out var anchor, out int clearedLines))
                continue; // not placeable anywhere right now

            var previewGrid = (bool[,])grid.Clone();
            SimulatePlaceAndClear(previewGrid, width, height, anchor, offsets);
            bool fullClear = IsGridEmpty(previewGrid, width, height);

            placeable.Add((prefab, fullClear, clearedLines, offsets.Length, previewGrid));
        }

        // How many of the 3 slots must actually be placeable right now. 1 at max
        // difficulty (helpfulness 0) up to all 3 at max helpfulness (1).
        int realSlotCount = Mathf.Clamp(Mathf.RoundToInt(1 + difficulty * 2), 1, 3);

        // Primary picks: shapes that clear something on their own, ranked by
        // full clear first, then lines cleared, then simplicity (fewer cells —
        // an obvious small fix over a sprawling one, when both clear equally).
        var clearing = placeable.Where(p => p.clearedLines > 0)
            .OrderByDescending(p => p.fullClear)
            .ThenByDescending(p => p.clearedLines)
            .ThenBy(p => p.cellCount)
            .ToList();

        foreach (var entry in clearing)
        {
            result.Add(entry.shape);
            if (result.Count == realSlotCount)
                break;
        }

        // Fallback: nothing left to clear (or not enough shapes that clear) —
        // fill remaining real slots from whatever's still placeable, preferring
        // whichever leaves the board healthiest, blended with difficulty.
        if (result.Count < realSlotCount)
        {
            var leftover = placeable.Where(p => !result.Contains(p.shape) && p.clearedLines == 0).ToList();

            if (Random.value < difficulty)
            {
                leftover = leftover
                    .OrderByDescending(p => EvaluateBoardHealth(p.resultGrid, width, height, pool, cellSize))
                    .ThenBy(p => p.cellCount)
                    .ToList();
            }

            foreach (var entry in leftover)
            {
                result.Add(entry.shape);
                if (result.Count == realSlotCount)
                    break;
            }
        }

        // Remaining slots (above realSlotCount) become decoys: shapes from the
        // pool that cannot be placed anywhere on the board right now, so higher
        // difficulty means fewer genuinely usable options in the tray.
        if (result.Count < 3)
        {
            var unplaceable = pool.Where(p => !result.Contains(p) && !placeable.Any(pl => pl.shape == p)).ToList();

            foreach (var decoy in unplaceable)
            {
                result.Add(decoy);
                if (result.Count == 3)
                    break;
            }
        }

        // Not enough decoys existed (board too empty) — fall back to filling
        // with whatever's still placeable rather than leaving slots empty.
        if (result.Count < 3)
        {
            var stillPlaceable = placeable.Where(p => !result.Contains(p.shape)).Select(p => p.shape).ToList();
            foreach (var shape in stillPlaceable)
            {
                result.Add(shape);
                if (result.Count == 3)
                    break;
            }
        }

        // Last-resort: nothing placeable anywhere (or empty pool) — random fill.
        while (result.Count < 3 && pool.Count > 0)
            result.Add(pool[Random.Range(0, pool.Count)]);

        return result;
    }

    /// <summary>
    /// Higher is better. Blends mobility (fraction of the shape pool that can
    /// still be placed somewhere on this grid) with a hole penalty (empty
    /// cells boxed in on 3+ sides — likely permanently unusable without a
    /// lucky single-cell piece landing exactly there).
    /// </summary>
    private static float EvaluateBoardHealth(bool[,] grid, int width, int height, List<Shape> pool, float cellSize)
    {
        int placeableCount = 0;
        foreach (var prefab in pool)
        {
            var offsets = prefab.GetCells(cellSize);
            if (offsets == null || offsets.Length == 0)
                continue;

            if (HasAnyPlacement(grid, width, height, offsets))
                placeableCount++;
        }
        float mobility = pool.Count > 0 ? (float)placeableCount / pool.Count : 0f;

        int holeCount = 0;
        int emptyCount = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y])
                    continue;

                emptyCount++;
                int blockedSides = 0;
                if (x == 0 || grid[x - 1, y]) blockedSides++;
                if (x == width - 1 || grid[x + 1, y]) blockedSides++;
                if (y == 0 || grid[x, y - 1]) blockedSides++;
                if (y == height - 1 || grid[x, y + 1]) blockedSides++;

                if (blockedSides >= 3)
                    holeCount++;
            }
        }
        float holePenalty = emptyCount > 0 ? (float)holeCount / emptyCount : 0f;

        return mobility - holePenalty;
    }

    private static bool HasAnyPlacement(bool[,] grid, int width, int height, Vector2Int[] offsets)
    {
        for (int ax = 0; ax < width; ax++)
            for (int ay = 0; ay < height; ay++)
                if (CanPlaceOffsetsAtGrid(grid, width, height, new Vector2Int(ax, ay), offsets))
                    return true;
        return false;
    }

    // Finds the anchor that clears the most lines for a shape on a simulated
    // grid. Judged on clearedLines alone — no cell-count tiebreak of any kind.
    // Ties keep the first anchor found; the pool itself is shuffled by the
    // caller so ties don't always resolve to the same shape.
    private static bool TryFindBestAnchor(bool[,] grid, int width, int height, Vector2Int[] offsets, out Vector2Int bestAnchor, out int bestClearedLines)
    {
        bestAnchor = default;
        bestClearedLines = -1;
        bool found = false;

        int[] rowMissing = new int[height];
        int[] colMissing = new int[width];
        for (int y = 0; y < height; y++)
        {
            int missing = 0;
            for (int x = 0; x < width; x++)
                if (!grid[x, y]) missing++;
            rowMissing[y] = missing;
        }
        for (int x = 0; x < width; x++)
        {
            int missing = 0;
            for (int y = 0; y < height; y++)
                if (!grid[x, y]) missing++;
            colMissing[x] = missing;
        }

        int[] filledInRow = new int[height];
        int[] filledInCol = new int[width];

        for (int ax = 0; ax < width; ax++)
        {
            for (int ay = 0; ay < height; ay++)
            {
                var anchor = new Vector2Int(ax, ay);
                if (!CanPlaceOffsetsAtGrid(grid, width, height, anchor, offsets))
                    continue;

                System.Array.Clear(filledInRow, 0, height);
                System.Array.Clear(filledInCol, 0, width);

                foreach (var off in offsets)
                {
                    var cell = anchor + off;
                    filledInRow[cell.y]++;
                    filledInCol[cell.x]++;
                }

                int clearedLines = 0;
                for (int y = 0; y < height; y++)
                    if (filledInRow[y] > 0 && rowMissing[y] == filledInRow[y])
                        clearedLines++;
                for (int x = 0; x < width; x++)
                    if (filledInCol[x] > 0 && colMissing[x] == filledInCol[x])
                        clearedLines++;

                if (clearedLines > bestClearedLines)
                {
                    bestClearedLines = clearedLines;
                    bestAnchor = anchor;
                    found = true;
                }
            }
        }

        return found;
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

    private static void SimulatePlaceAndClear(bool[,] grid, int width, int height, Vector2Int anchor, Vector2Int[] offsets)
    {
        foreach (var off in offsets)
        {
            var cell = anchor + off;
            grid[cell.x, cell.y] = true;
        }

        var fullRows = new List<int>();
        for (int y = 0; y < height; y++)
        {
            bool full = true;
            for (int x = 0; x < width; x++)
                if (!grid[x, y]) { full = false; break; }
            if (full) fullRows.Add(y);
        }

        var fullCols = new List<int>();
        for (int x = 0; x < width; x++)
        {
            bool full = true;
            for (int y = 0; y < height; y++)
                if (!grid[x, y]) { full = false; break; }
            if (full) fullCols.Add(x);
        }

        foreach (var y in fullRows)
            for (int x = 0; x < width; x++)
                grid[x, y] = false;

        foreach (var x in fullCols)
            for (int y = 0; y < height; y++)
                grid[x, y] = false;
    }

    private static bool IsGridEmpty(bool[,] grid, int width, int height)
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (grid[x, y])
                    return false;
        return true;
    }
}
