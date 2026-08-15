using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OrcaTetris.Adventure
{
    /// <summary>
    /// Plays a generated Adventure level headlessly, so a level's difficulty curve can be checked
    /// against a played-through run instead of just eyeballed. Levels are produced on demand for
    /// any level number, which means nobody can play-test level 487 — this stands in for that.
    ///
    /// The simulation mirrors the real loop exactly where it matters: an 8x8 board, a 3-slot tray
    /// refilled only once every shape is placed, one symbol per offered shape drawn from the same
    /// weighted needed-types pool ShapeTrayManager uses, symbols collected only when a line clear
    /// removes the cell carrying them, and tray picks coming from the shared
    /// <see cref="TraySelectionCore"/> so the solver can't be validating a different tray policy
    /// than the one the player faces. There is no mulligan for a stuck tray, matching the real
    /// game: Adventure has no revive, so the player's own placements can end a level early.
    ///
    /// The built-in player is deliberately *greedy, not optimal* — it looks one placement ahead
    /// and never plans a combo. A <see cref="Outcome.Stuck"/> result here means this simple
    /// player got stranded, not that the level is unwinnable — a level only needs to be
    /// completable by placing shapes in the right spot, not by every possible line of play.
    ///
    /// There is no move limit — matching the real game, a level just runs until its targets are
    /// met or the tray gets stuck. <see cref="MaxMovesSafetyCap"/> exists purely so a genuinely
    /// broken level fails the test instead of looping forever; it is not a game-facing budget.
    /// </summary>
    public static class AdventureLevelSimulator
    {
        public const int BoardSize = 8;

        /// <summary>
        /// Far beyond anything a real level should ever need (the worst case measured across
        /// levels 4-500 finishes in well under 200 moves). Only exists to bound the loop below —
        /// hitting it means the level is broken, not that a player ran out of moves.
        /// </summary>
        private const int MaxMovesSafetyCap = 5000;

        public enum Outcome
        {
            /// <summary>All targets collected.</summary>
            Won,
            /// <summary>Board clogged so badly that no offered shape fit anywhere.</summary>
            Stuck
        }

        public struct Result
        {
            public Outcome Outcome;
            public int MovesUsed;
            public int SymbolsRemaining;

            public bool Won => Outcome == Outcome.Won;

            public override string ToString()
                => $"{Outcome} after {MovesUsed} moves, {SymbolsRemaining} symbols left";
        }

        /// <summary>
        /// Plays one attempt at <paramref name="level"/> with the given shape pool.
        /// </summary>
        /// <param name="seed">Fixes every random draw, so a failure is always reproducible.</param>
        public static Result Play(int level, IReadOnlyList<Vector2Int[]> shapePool, int seed)
        {
            var rng = new System.Random(seed);

            var activeTypes = AdventureLevelCurves.GetActiveTypes(level, out var weights);
            var amounts = AdventureLevelCurves.SplitTarget(AdventureLevelCurves.GetTotalTarget(level), weights);

            var remaining = new Dictionary<int, int>();
            for (int i = 0; i < activeTypes.Count; i++)
                remaining[activeTypes[i]] = amounts[i];

            float helpfulness = 1f - AdventureLevelCurves.GetBaseDifficultyPct(level) / 100f;

            var occupied = new bool[BoardSize, BoardSize];
            var symbols = new int[BoardSize, BoardSize];
            for (int x = 0; x < BoardSize; x++)
                for (int y = 0; y < BoardSize; y++)
                    symbols[x, y] = -1;

            foreach (var (cell, symbol) in AdventureLevelCurves.GetInitialBlocks(level, activeTypes))
            {
                occupied[cell.x, cell.y] = true;
                symbols[cell.x, cell.y] = symbol;
            }

            // A tray entry is a shape plus which of its cells carries the symbol, matching
            // ShapeTrayManager.AssignRandomSymbol (one random block of one random needed type).
            var tray = new List<(int shapeIndex, int symbolCell, int symbolType)>();
            int movesUsed = 0;

            while (movesUsed < MaxMovesSafetyCap)
            {
                if (tray.Count == 0)
                    RefillTray(tray, occupied, shapePool, remaining, helpfulness, rng);

                if (!TryPickBestPlacement(occupied, symbols, tray, shapePool, remaining, out int trayIndex, out var anchor))
                {
                    // The shapes still sitting in the tray no longer fit anywhere. Mirrors the
                    // real game: Adventure has no revive and no mulligan for this, so a tray the
                    // player's own placements left with nowhere to go is a genuine loss.
                    return Fail(movesUsed, remaining);
                }

                var entry = tray[trayIndex];
                tray.RemoveAt(trayIndex);

                Apply(occupied, symbols, shapePool[entry.shapeIndex], anchor, entry.symbolCell, entry.symbolType, remaining);
                movesUsed++;

                if (remaining.Values.All(v => v <= 0))
                {
                    return new Result
                    {
                        Outcome = Outcome.Won,
                        MovesUsed = movesUsed,
                        SymbolsRemaining = 0
                    };
                }
            }

            // Only reachable if a level is broken badly enough to outrun MaxMovesSafetyCap —
            // report it the same way as a genuine dead end.
            return Fail(movesUsed, remaining);
        }

        /// <summary>
        /// Draws a fresh tray and gives each shape one symbol of a still-needed type, matching
        /// ShapeTrayManager's refill (SelectTray, then AssignRandomSymbol per shape).
        /// </summary>
        private static void RefillTray(
            List<(int shapeIndex, int symbolCell, int symbolType)> tray,
            bool[,] occupied,
            IReadOnlyList<Vector2Int[]> shapePool,
            Dictionary<int, int> remaining,
            float helpfulness,
            System.Random rng)
        {
            var picks = TraySelectionCore.SelectTray(occupied, shapePool, helpfulness, allowDecoys: false, rng);
            var neededPool = BuildWeightedNeededTypes(remaining);

            foreach (int shapeIndex in picks)
            {
                var offsets = shapePool[shapeIndex];
                int symbolCell = offsets.Length > 0 ? rng.Next(offsets.Length) : -1;
                int symbolType = neededPool.Count > 0 ? neededPool[rng.Next(neededPool.Count)] : -1;
                tray.Add((shapeIndex, symbolCell, symbolType));
            }
        }

        private static Result Fail(int movesUsed, Dictionary<int, int> remaining)
        {
            return new Result
            {
                Outcome = Outcome.Stuck,
                MovesUsed = movesUsed,
                SymbolsRemaining = remaining.Values.Sum()
            };
        }

        /// <summary>
        /// Mirrors ShapeTrayManager.BuildWeightedNeededTypes: one entry per outstanding unit, so
        /// the type furthest from done is drawn most often and a finished type stops appearing.
        /// </summary>
        private static List<int> BuildWeightedNeededTypes(Dictionary<int, int> remaining)
        {
            var pool = new List<int>();
            foreach (var kvp in remaining)
                for (int i = 0; i < kvp.Value; i++)
                    pool.Add(kvp.Key);
            return pool;
        }

        /// <summary>
        /// The greedy player: scores every legal placement of every tray shape one move deep and
        /// takes the best. Collecting a still-needed symbol dominates, then clearing lines, then
        /// leaving the board healthy. No lookahead, no combo setup — an average player, not a
        /// perfect one.
        /// </summary>
        private static bool TryPickBestPlacement(
            bool[,] occupied,
            int[,] symbols,
            List<(int shapeIndex, int symbolCell, int symbolType)> tray,
            IReadOnlyList<Vector2Int[]> shapePool,
            Dictionary<int, int> remaining,
            out int bestTrayIndex,
            out Vector2Int bestAnchor)
        {
            bestTrayIndex = -1;
            bestAnchor = default;
            float bestScore = float.NegativeInfinity;

            for (int t = 0; t < tray.Count; t++)
            {
                var offsets = shapePool[tray[t].shapeIndex];
                if (offsets == null || offsets.Length == 0)
                    continue;

                for (int ax = 0; ax < BoardSize; ax++)
                {
                    for (int ay = 0; ay < BoardSize; ay++)
                    {
                        var anchor = new Vector2Int(ax, ay);
                        if (!TraySelectionCore.CanPlaceAt(occupied, BoardSize, BoardSize, anchor, offsets))
                            continue;

                        var previewOccupied = (bool[,])occupied.Clone();
                        var previewSymbols = (int[,])symbols.Clone();

                        int collected = Apply(
                            previewOccupied, previewSymbols, offsets, anchor,
                            tray[t].symbolCell, tray[t].symbolType,
                            remaining, applyToRemaining: false);

                        int linesCleared = CountClearedLines(occupied, offsets, anchor);

                        // A cheap stand-in for TraySelectionCore.EvaluateBoardHealth: penalise
                        // boxed-in empty cells and general clutter. Deliberately local and
                        // O(board) rather than probing the whole shape pool, both because this
                        // runs for every candidate placement and because a greedy player
                        // shouldn't be modelled as evaluating the full pool either.
                        float clutter = CountHoles(previewOccupied) * 2f + CountOccupied(previewOccupied) * 0.1f;

                        float score = collected * 100f + linesCleared * 10f - clutter;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestTrayIndex = t;
                            bestAnchor = anchor;
                        }
                    }
                }
            }

            return bestTrayIndex >= 0;
        }

        /// <summary>
        /// Places a shape, then clears any full rows/columns, collecting the symbols that sat in
        /// them. Returns how many still-needed symbols the clear collected.
        /// </summary>
        private static int Apply(
            bool[,] occupied,
            int[,] symbols,
            Vector2Int[] offsets,
            Vector2Int anchor,
            int symbolCell,
            int symbolType,
            Dictionary<int, int> remaining,
            bool applyToRemaining = true)
        {
            for (int i = 0; i < offsets.Length; i++)
            {
                var cell = anchor + offsets[i];
                occupied[cell.x, cell.y] = true;
                symbols[cell.x, cell.y] = i == symbolCell ? symbolType : -1;
            }

            var fullRows = new List<int>();
            for (int y = 0; y < BoardSize; y++)
            {
                bool full = true;
                for (int x = 0; x < BoardSize; x++)
                    if (!occupied[x, y]) { full = false; break; }
                if (full) fullRows.Add(y);
            }

            var fullCols = new List<int>();
            for (int x = 0; x < BoardSize; x++)
            {
                bool full = true;
                for (int y = 0; y < BoardSize; y++)
                    if (!occupied[x, y]) { full = false; break; }
                if (full) fullCols.Add(x);
            }

            // Gather before wiping so a cell in both a full row and a full column is only counted
            // once, matching GridBoard's single-pass symbol collection.
            var clearedCells = new HashSet<Vector2Int>();
            foreach (int y in fullRows)
                for (int x = 0; x < BoardSize; x++)
                    clearedCells.Add(new Vector2Int(x, y));
            foreach (int x in fullCols)
                for (int y = 0; y < BoardSize; y++)
                    clearedCells.Add(new Vector2Int(x, y));

            int collected = 0;
            foreach (var cell in clearedCells)
            {
                int type = symbols[cell.x, cell.y];
                if (type >= 0 && remaining.TryGetValue(type, out int left) && left > 0)
                {
                    collected++;
                    if (applyToRemaining)
                        remaining[type] = left - 1;
                }

                occupied[cell.x, cell.y] = false;
                symbols[cell.x, cell.y] = -1;
            }

            return collected;
        }

        /// <summary>Empty cells walled in on 3+ sides — the ones a big shape can never reach.</summary>
        private static int CountHoles(bool[,] grid)
        {
            int holes = 0;
            for (int x = 0; x < BoardSize; x++)
            {
                for (int y = 0; y < BoardSize; y++)
                {
                    if (grid[x, y])
                        continue;

                    int blocked = 0;
                    if (x == 0 || grid[x - 1, y]) blocked++;
                    if (x == BoardSize - 1 || grid[x + 1, y]) blocked++;
                    if (y == 0 || grid[x, y - 1]) blocked++;
                    if (y == BoardSize - 1 || grid[x, y + 1]) blocked++;

                    if (blocked >= 3)
                        holes++;
                }
            }
            return holes;
        }

        private static int CountOccupied(bool[,] grid)
        {
            int count = 0;
            for (int x = 0; x < BoardSize; x++)
                for (int y = 0; y < BoardSize; y++)
                    if (grid[x, y]) count++;
            return count;
        }

        private static int CountClearedLines(bool[,] occupied, Vector2Int[] offsets, Vector2Int anchor)
        {
            var preview = (bool[,])occupied.Clone();
            foreach (var off in offsets)
            {
                var cell = anchor + off;
                preview[cell.x, cell.y] = true;
            }

            int lines = 0;
            for (int y = 0; y < BoardSize; y++)
            {
                bool full = true;
                for (int x = 0; x < BoardSize; x++)
                    if (!preview[x, y]) { full = false; break; }
                if (full) lines++;
            }
            for (int x = 0; x < BoardSize; x++)
            {
                bool full = true;
                for (int y = 0; y < BoardSize; y++)
                    if (!preview[x, y]) { full = false; break; }
                if (full) lines++;
            }
            return lines;
        }
    }
}
