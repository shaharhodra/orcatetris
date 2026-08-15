using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OrcaTetris.Adventure
{
    /// <summary>
    /// The tray-picking policy, expressed purely over cell offsets and a boolean grid — no
    /// prefabs, no MonoBehaviours. ShapeSelectionAlgorithm is the thin adapter that maps Shape
    /// prefabs onto this, and <see cref="AdventureLevelSimulator"/> drives this same code to
    /// validate that generated levels are actually beatable. Both go through here so the tray the
    /// solver reasons about can never drift from the tray the player is handed.
    ///
    /// See ShapeSelectionAlgorithm's file comment for the design rationale.
    /// </summary>
    public static class TraySelectionCore
    {
        public const int TraySlots = 3;

        /// <summary>
        /// Picks which shapes to offer next, as indices into <paramref name="shapes"/>.
        /// </summary>
        /// <param name="grid">Current board occupancy, [x, y].</param>
        /// <param name="shapes">Cell offsets of every shape that may be offered.</param>
        /// <param name="helpfulness">
        /// 1 = most helpful/easy, 0 = most chaotic/hard. Controls how many slots are guaranteed
        /// placeable (when decoys are allowed) and whether leftover slots prefer the shape that
        /// leaves the board healthiest.
        /// </param>
        /// <param name="allowDecoys">
        /// Whether unplaceable shapes may fill the slots above the real-slot count. False forces
        /// every slot to be genuinely placeable — required in Adventure, where an unplayable slot
        /// is an instant loss rather than a challenge.
        /// </param>
        /// <param name="rng">Randomness source, so a simulation can run deterministically.</param>
        public static List<int> SelectTray(
            bool[,] grid,
            IReadOnlyList<Vector2Int[]> shapes,
            float helpfulness,
            bool allowDecoys,
            System.Random rng)
        {
            var result = new List<int>();
            if (grid == null || shapes == null || shapes.Count == 0)
                return result;

            int width = grid.GetLength(0);
            int height = grid.GetLength(1);

            // Shuffle the candidate order so ties don't always resolve to whichever shape happens
            // to sit first in the pool.
            var order = Enumerable.Range(0, shapes.Count).ToList();
            for (int i = order.Count - 1; i > 0; i--)
            {
                int swap = rng.Next(i + 1);
                (order[i], order[swap]) = (order[swap], order[i]);
            }

            // Score every shape independently on the CURRENT board — what you see is what you
            // get: each shape's reported clear is exactly what placing it alone, right now,
            // achieves. No other shape's placement is assumed.
            var placeable = new List<(int index, bool fullClear, int clearedLines, int cellCount, bool[,] resultGrid)>();
            foreach (int index in order)
            {
                var offsets = shapes[index];
                if (offsets == null || offsets.Length == 0)
                    continue;

                if (!TryFindBestAnchor(grid, width, height, offsets, out var anchor, out int clearedLines))
                    continue; // not placeable anywhere right now

                var previewGrid = (bool[,])grid.Clone();
                SimulatePlaceAndClear(previewGrid, width, height, anchor, offsets);
                bool fullClear = IsGridEmpty(previewGrid, width, height);

                placeable.Add((index, fullClear, clearedLines, offsets.Length, previewGrid));
            }

            // How many slots must actually be playable right now. 1 at max difficulty up to all 3
            // at max helpfulness — unless decoys are off, in which case every slot must be.
            int realSlotCount = allowDecoys
                ? Mathf.Clamp(Mathf.RoundToInt(1 + helpfulness * 2), 1, TraySlots)
                : TraySlots;

            // Primary picks: shapes that clear something on their own, ranked by full clear first,
            // then lines cleared, then simplicity (fewer cells).
            var clearing = placeable.Where(p => p.clearedLines > 0)
                .OrderByDescending(p => p.fullClear)
                .ThenByDescending(p => p.clearedLines)
                .ThenBy(p => p.cellCount)
                .ToList();

            foreach (var entry in clearing)
            {
                result.Add(entry.index);
                if (result.Count == realSlotCount)
                    break;
            }

            // Fallback: nothing left to clear (or not enough shapes that clear) — fill remaining
            // real slots from whatever's still placeable, preferring whichever leaves the board
            // healthiest, blended with difficulty.
            if (result.Count < realSlotCount)
            {
                var leftover = placeable.Where(p => !result.Contains(p.index) && p.clearedLines == 0).ToList();

                if (rng.NextDouble() < helpfulness)
                {
                    leftover = leftover
                        .OrderByDescending(p => EvaluateBoardHealth(p.resultGrid, width, height, shapes))
                        .ThenBy(p => p.cellCount)
                        .ToList();
                }

                foreach (var entry in leftover)
                {
                    result.Add(entry.index);
                    if (result.Count == realSlotCount)
                        break;
                }
            }

            // Remaining slots become decoys: shapes that cannot be placed anywhere right now, so
            // higher difficulty means fewer genuinely usable options in the tray.
            if (allowDecoys && result.Count < TraySlots)
            {
                foreach (int index in order)
                {
                    if (result.Contains(index) || placeable.Any(p => p.index == index))
                        continue;

                    result.Add(index);
                    if (result.Count == TraySlots)
                        break;
                }
            }

            // Not enough decoys existed (board too empty) — fall back to filling with whatever's
            // still placeable rather than leaving slots empty.
            if (result.Count < TraySlots)
            {
                foreach (var entry in placeable)
                {
                    if (result.Contains(entry.index))
                        continue;

                    result.Add(entry.index);
                    if (result.Count == TraySlots)
                        break;
                }
            }

            // Fewer than 3 distinct shapes fit the board at all. With decoys off we still owe the
            // player 3 playable slots, so repeat the ones that do fit rather than padding with
            // shapes they can't use.
            if (!allowDecoys && result.Count < TraySlots && placeable.Count > 0)
            {
                int i = 0;
                while (result.Count < TraySlots)
                    result.Add(placeable[i++ % placeable.Count].index);
            }

            // Last-resort: nothing placeable anywhere — random fill.
            while (result.Count < TraySlots && shapes.Count > 0)
                result.Add(rng.Next(shapes.Count));

            return result;
        }

        /// <summary>
        /// Higher is better. Blends mobility (fraction of the shape pool that can still be placed
        /// somewhere) with a hole penalty (empty cells boxed in on 3+ sides — likely permanently
        /// unusable without a lucky single-cell piece landing exactly there).
        /// </summary>
        public static float EvaluateBoardHealth(bool[,] grid, int width, int height, IReadOnlyList<Vector2Int[]> shapes)
        {
            int placeableCount = 0;
            foreach (var offsets in shapes)
            {
                if (offsets == null || offsets.Length == 0)
                    continue;

                if (HasAnyPlacement(grid, width, height, offsets))
                    placeableCount++;
            }
            float mobility = shapes.Count > 0 ? (float)placeableCount / shapes.Count : 0f;

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

        public static bool HasAnyPlacement(bool[,] grid, int width, int height, Vector2Int[] offsets)
        {
            for (int ax = 0; ax < width; ax++)
                for (int ay = 0; ay < height; ay++)
                    if (CanPlaceAt(grid, width, height, new Vector2Int(ax, ay), offsets))
                        return true;
            return false;
        }

        /// <summary>
        /// Finds the anchor that clears the most lines for a shape. Judged on cleared lines alone —
        /// no cell-count tiebreak. Ties keep the first anchor found.
        /// </summary>
        public static bool TryFindBestAnchor(bool[,] grid, int width, int height, Vector2Int[] offsets, out Vector2Int bestAnchor, out int bestClearedLines)
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
                    if (!CanPlaceAt(grid, width, height, anchor, offsets))
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

        public static bool CanPlaceAt(bool[,] grid, int width, int height, Vector2Int anchor, Vector2Int[] offsets)
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

        public static void SimulatePlaceAndClear(bool[,] grid, int width, int height, Vector2Int anchor, Vector2Int[] offsets)
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

        public static bool IsGridEmpty(bool[,] grid, int width, int height)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (grid[x, y])
                        return false;
            return true;
        }
    }
}
