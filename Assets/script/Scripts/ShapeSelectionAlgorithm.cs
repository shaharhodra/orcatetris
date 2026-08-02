using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Picks which 3 shapes to offer next in the tray. Looks a few moves ahead via
/// beam search: reaching a full clear always wins, then total lines cleared —
/// nothing else is considered while sequences differ on those. Only once
/// sequences tie on clearing outcome does "board health" (how many of the
/// remaining pool shapes can still be placed, how many empty cells are boxed
/// in on 3+ sides) come in as a tiebreak, blended with a difficulty setting.
///
/// Plain C# class, no MonoBehaviour/scene dependency — can be driven directly
/// from a test with a hand-built grid and shape list.
/// </summary>
public class ShapeSelectionAlgorithm
{
    private const int LookaheadDepth = 3;
    private const int BeamWidth = 5;

    private class BeamState
    {
        public bool[,] Grid;
        public List<Shape> Sequence;
        public HashSet<Shape> Used;
        public int TotalLinesCleared;
        public int ContributingShapes;
        public bool AchievedFullClear;
    }

    /// <param name="grid">Current board occupancy, [x, y].</param>
    /// <param name="shapePool">All shapes that may be offered.</param>
    /// <param name="cellSize">Passed through to Shape.GetCells to read each shape's footprint.</param>
    /// <param name="difficulty">
    /// 0 = ignore board health entirely and pick randomly among clear-tied
    /// options (most chaotic/hard). 1 = always pick whichever clear-tied
    /// option leaves the board healthiest (most helpful/easy). Line clears
    /// themselves are never affected by this — only ties between otherwise
    /// equally-good sequences are.
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

        var beam = new List<BeamState>
        {
            new BeamState
            {
                Grid = (bool[,])grid.Clone(),
                Sequence = new List<Shape>(),
                Used = new HashSet<Shape>(),
                TotalLinesCleared = 0,
                ContributingShapes = 0,
                AchievedFullClear = false
            }
        };

        for (int depth = 0; depth < LookaheadDepth; depth++)
        {
            var expanded = new List<BeamState>();

            foreach (var state in beam)
            {
                foreach (var prefab in pool)
                {
                    if (state.Used.Contains(prefab))
                        continue;

                    var offsets = prefab.GetCells(cellSize);
                    if (offsets == null || offsets.Length == 0)
                        continue;

                    if (!TryFindBestAnchor(state.Grid, width, height, offsets, out var anchor, out int clearedLines))
                        continue;

                    var newGrid = (bool[,])state.Grid.Clone();
                    SimulatePlaceAndClear(newGrid, width, height, anchor, offsets);

                    bool fullClear = IsGridEmpty(newGrid, width, height);

                    expanded.Add(new BeamState
                    {
                        Grid = newGrid,
                        Sequence = new List<Shape>(state.Sequence) { prefab },
                        Used = new HashSet<Shape>(state.Used) { prefab },
                        TotalLinesCleared = state.TotalLinesCleared + clearedLines,
                        ContributingShapes = state.ContributingShapes + (clearedLines > 0 ? 1 : 0),
                        AchievedFullClear = state.AchievedFullClear || fullClear
                    });
                }
            }

            if (expanded.Count == 0)
                break;

            expanded.Sort((a, b) => CompareByClearing(b, a));
            beam = expanded.Take(BeamWidth).ToList();
        }

        var finalists = beam.Where(s => s.Sequence.Count > 0).ToList();
        if (finalists.Count > 0)
        {
            finalists.Sort((a, b) => CompareByClearing(b, a));
            var bestClearing = finalists[0];
            var tied = finalists.Where(s => CompareByClearing(s, bestClearing) == 0).ToList();

            BeamState chosen;
            if (tied.Count == 1)
            {
                chosen = tied[0];
            }
            else if (Random.value < difficulty)
            {
                // Helpful pick: whichever tied option leaves the board healthiest.
                chosen = tied
                    .Select(s => (state: s, health: EvaluateBoardHealth(s.Grid, width, height, pool, cellSize)))
                    .OrderByDescending(t => t.health)
                    .First().state;
            }
            else
            {
                // Chaotic pick: board health isn't considered at all.
                chosen = tied[Random.Range(0, tied.Count)];
            }

            result.AddRange(chosen.Sequence);

            if (result.Count < 3)
            {
                var remaining = pool.Where(p => !result.Contains(p))
                    .OrderBy(p => p.GetCells(cellSize)?.Length ?? 0);
                foreach (var p in remaining)
                {
                    result.Add(p);
                    if (result.Count == 3)
                        break;
                }
            }
        }

        // Last-resort: nothing placeable anywhere (or empty pool) — random fill.
        while (result.Count < 3 && pool.Count > 0)
            result.Add(pool[Random.Range(0, pool.Count)]);

        return result;
    }

    // Ranks by: reaching a full clear, then total lines cleared, then how many
    // of the (up to 3) shapes in the sequence each contributed at least one
    // clear of their own. That third tier is what stops the algorithm from
    // being satisfied with "one shape clears 2 lines, the other two do nothing"
    // when "each of the three clears 1 line" scores the same total — spreading
    // the help across the whole tray instead of concentrating it in one shape.
    private static int CompareByClearing(BeamState a, BeamState b)
    {
        int fullClearCompare = (a.AchievedFullClear ? 1 : 0) - (b.AchievedFullClear ? 1 : 0);
        if (fullClearCompare != 0)
            return fullClearCompare;

        int totalCompare = a.TotalLinesCleared - b.TotalLinesCleared;
        if (totalCompare != 0)
            return totalCompare;

        return a.ContributingShapes - b.ContributingShapes;
    }

    /// <summary>
    /// Higher is better. Blends mobility (fraction of the shape pool that can
    /// still be placed somewhere on this grid) with a hole penalty (empty
    /// cells boxed in on 3+ sides — likely permanently unusable without a
    /// lucky single-cell piece landing exactly there).
    /// </summary>
    private static float EvaluateBoardHealth(bool[,] grid, int width, int height, List<Shape> pool, float cellSize)
    {
        int placeable = 0;
        foreach (var prefab in pool)
        {
            var offsets = prefab.GetCells(cellSize);
            if (offsets == null || offsets.Length == 0)
                continue;

            if (HasAnyPlacement(grid, width, height, offsets))
                placeable++;
        }
        float mobility = pool.Count > 0 ? (float)placeable / pool.Count : 0f;

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
