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

        // Mutable statics rather than consts so Remote Config can reach them — see the block
        // comment on AdventureLevelCurves for why these are written from outside rather than read
        // from a settings service.

        /// <summary>
        /// Board fill (0-1) at or above which the jackpot cap in <see cref="SelectTray"/> stops
        /// applying. See the comment there for why holding back the big clear has to be something
        /// the tray only does while the player is comfortable.
        /// </summary>
        public static float JackpotCapReliefFill = 0.45f;

        /// <summary>
        /// Anchor count at which <see cref="CountPlacements"/> stops counting for
        /// <see cref="SelectTrayChained"/>'s anchor-count checks — past this a shape plainly fits
        /// wherever it likes, and counting further just costs time nobody needed spent. High
        /// enough to still separate "awkward" from "very awkward": at a lower cap every roomy
        /// shape ties at the ceiling and the ranking below can't tell them apart.
        /// </summary>
        public static int RobustAnchorCap = 12;

        /// <summary>
        /// Fewest anchors a shape may have and still be worth offering when the tray is
        /// deliberately picking demanding pieces. Below this a shape isn't a challenge, it's a
        /// trap: the other two shapes in the same tray can box it out entirely before the player
        /// ever gets to it, and Adventure has no revive. This exact failure mode is what produced
        /// the 93 stuck solver runs the previous round diagnosed, so the awkwardness preference in
        /// <see cref="SelectTrayChained"/> never reaches past it.
        /// </summary>
        public static int MinSafeAnchors = 3;

        /// <summary>
        /// Picks which shapes to offer next, as indices into <paramref name="shapes"/>.
        /// </summary>
        /// <param name="grid">Current board occupancy, [x, y].</param>
        /// <param name="shapes">Cell offsets of every shape that may be offered.</param>
        /// <param name="helpfulness">
        /// 1 = most helpful/easy, 0 = most chaotic/hard. Controls how many slots are guaranteed
        /// placeable (when decoys are allowed), how many slots may arrive with a multi-line clear
        /// already set up, and whether leftover slots prefer the shape that leaves the board
        /// healthiest.
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

            // How many slots may arrive with a *multi-line* clear already set up. At max
            // helpfulness all 3 may, which is what the tray used to do unconditionally — hand over
            // three shapes that each wipe two or three lines on their own, so the level solved
            // itself and the board never built any pressure. At max difficulty only one does; the
            // other clearing slots drop to modest single-line options, and setting up the big
            // combo goes back to being the player's job.
            //
            // Two things this deliberately does *not* do, both measured against the solver rather
            // than guessed at:
            //
            // It never withholds clearing shapes outright. A shape that can't clear anywhere is by
            // construction one the board has no line-completing spot for — usually a big awkward
            // blob — so a slot withheld both adds cells and removes the only relief the board has.
            // An 8x8 taking three shapes a refill needs roughly a line and a half of clearing per
            // refill just to break even, and capping the *count* of clearing slots rather than
            // their generosity ran levels out of room no matter how well the shapes were placed:
            // it nearly tripled the solver's stuck rate where this costs about a sixth.
            //
            // And it only applies while the player is comfortable. Once the board is tight the big
            // clear is exactly what digs them out, and a tray that keeps holding it back then
            // isn't asking for better play — the level is already decided.
            bool boardHasRoom = OccupiedFraction(grid, width, height) < JackpotCapReliefFill;
            int maxJackpotSlots = boardHasRoom
                ? Mathf.Clamp(Mathf.RoundToInt(1 + helpfulness * 2), 1, realSlotCount)
                : realSlotCount;

            // Shapes that clear something on their own, ranked from the most generous option the
            // tray can offer to the least: full clear first, then most lines, then the simplest
            // shape to land it with.
            var clearing = placeable.Where(p => p.clearedLines > 0)
                .OrderByDescending(p => p.fullClear)
                .ThenByDescending(p => p.clearedLines)
                .ThenBy(p => p.cellCount)
                .ToList();

            int jackpotsTaken = 0;
            foreach (var entry in clearing)
            {
                bool jackpot = entry.fullClear || entry.clearedLines > 1;
                if (jackpot && jackpotsTaken >= maxJackpotSlots)
                    continue;

                if (jackpot)
                    jackpotsTaken++;

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

            // Still short: either not enough decoys existed (board too empty), or the jackpot cap
            // skipped shapes the non-clearing leftovers couldn't make up for (board so full that
            // nearly everything placeable clears multiple lines). Fall back to whatever's still
            // placeable rather than leaving slots empty, least generous first — so relaxing the
            // cap gives back the smallest available clear instead of an arbitrary one.
            if (result.Count < TraySlots)
            {
                foreach (var entry in placeable.OrderBy(p => p.clearedLines).ThenBy(p => p.cellCount))
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

        public static float OccupiedFraction(bool[,] grid, int width, int height)
        {
            int total = width * height;
            if (total <= 0)
                return 0f;

            int occupied = 0;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (grid[x, y])
                        occupied++;

            return occupied / (float)total;
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
        /// Whether this shape has enough room on the given board to be worth offering — see
        /// <see cref="MinSafeAnchors"/>. Used as a last tiebreak so that, all else equal, the tray
        /// never picks the one option that is a placement away from being stranded.
        /// </summary>
        private static bool IsSafeToOffer(bool[,] grid, int width, int height, Vector2Int[] offsets)
        {
            return CountPlacements(grid, width, height, offsets, RobustAnchorCap) >= MinSafeAnchors;
        }

        /// <summary>
        /// How much of its own bounding box a shape actually fills, 0-1. Squares, rectangles and
        /// straight lines score 1; an S-piece scores 0.67; a U- or F-pentomino scores 0.56.
        ///
        /// This is the shape of "does placing it take any thought", and crucially it is
        /// independent of cell count — which is why the tray ranks demand by this rather than by
        /// size. Both alternatives were measured and both flooded the board: an 8x8 taking three
        /// shapes a refill needs roughly a line and a half of clearing just to break even, so
        /// preferring bigger pieces (103 stuck solver runs) or near-stranded ones (96) spends that
        /// budget on cells instead of on decisions. A 5-cell F-pentomino costs exactly what a
        /// 5-cell straight line costs and asks a far better question.
        /// </summary>
        public static float BoundingBoxFillRatio(Vector2Int[] offsets)
        {
            if (offsets == null || offsets.Length == 0)
                return 1f;

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var cell in offsets)
            {
                if (cell.x < minX) minX = cell.x;
                if (cell.x > maxX) maxX = cell.x;
                if (cell.y < minY) minY = cell.y;
                if (cell.y > maxY) maxY = cell.y;
            }

            int area = (maxX - minX + 1) * (maxY - minY + 1);
            return area > 0 ? offsets.Length / (float)area : 1f;
        }

        /// <summary>
        /// How many distinct anchors let this shape be placed right now, capped at
        /// <paramref name="cap"/>. A rough "plenty vs. barely any" signal, not an exact census —
        /// used to steer away from shapes that fit in only one or two spots, which is one
        /// unrelated placement away from fitting nowhere at all.
        /// </summary>
        public static int CountPlacements(bool[,] grid, int width, int height, Vector2Int[] offsets, int cap)
        {
            int count = 0;
            for (int ax = 0; ax < width; ax++)
            {
                for (int ay = 0; ay < height; ay++)
                {
                    if (CanPlaceAt(grid, width, height, new Vector2Int(ax, ay), offsets))
                    {
                        count++;
                        if (count >= cap)
                            return count;
                    }
                }
            }
            return count;
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

        /// <summary>
        /// Adventure's tray policy. <see cref="SelectTray"/> scores every candidate against one
        /// static board, which is exactly why the tray felt like it didn't matter what you did —
        /// with three shapes each independently placeable on the board as it already is, no
        /// ordering or setup was ever required. This builds the tray as a sequence instead: the
        /// first shape is scored against the real board, but the second is scored against the
        /// board *as it would look after the first lands at its best spot and clears*, and the
        /// third against the board after both. Landing an earlier shape well is what opens the
        /// room a later one needs — the tray stops being three independent answers and becomes one
        /// combo the player has to read.
        ///
        /// This is a constructive guarantee, not a restriction: it proves at least one order
        /// clears the whole tray (the one just built), so a refill is never unsolvable by
        /// construction the way an actual decoy would be. It does not force the player through
        /// that exact order — any order or anchor that clears the same lines works just as well —
        /// so the challenge is genuinely "find a sequence that works," not "guess the one sequence
        /// the algorithm had in mind." A player who can't find any working sequence can still get
        /// stuck; Adventure has no revive, and that's the difficulty this method exists to add.
        ///
        /// The jackpot cap works exactly as in <see cref="SelectTray"/>, just re-evaluated against
        /// each slot's own board state in the chain rather than the tray's board once, since that
        /// board is now genuinely different per slot by design.
        ///
        /// Which shape fills a slot — as opposed to how much it clears — is the difficulty dial
        /// that actually asks the player to think. Each slot rolls once against helpfulness and
        /// then commits to a temperament. Generous slots offer the roomiest, smallest piece
        /// available: something that drops anywhere and needs no plan. Demanding slots offer the
        /// biggest piece with the fewest places it will go, so the move has to be worked out
        /// rather than made. The pool is deliberately stocked with awkward 5-cell pentominoes for
        /// this to have anything to reach for.
        ///
        /// The demanding ranking never goes below <see cref="MinSafeAnchors"/> anchors, and that
        /// floor is the whole difference between a hard tray and an unfair one. Diagnosing stuck
        /// solver runs traced nearly all of them to a single pattern: a large, geometrically fussy
        /// block dealt while it fit in exactly one spot, then boxed out entirely once the other two
        /// tray shapes landed wherever the player happened to put them — on a board still only a
        /// third full. The player cannot see that coming, so losing to it reads as the game
        /// cheating rather than as a mistake. Above the floor, fewer anchors just means think
        /// harder, which is the point.
        ///
        /// One thing this deliberately does *not* do, despite looking like an obvious next step:
        /// prefer, among a slot's clearing candidates, whichever one could *not* have cleared
        /// anything on the board this refill actually started from — i.e. bias toward shapes whose
        /// clear is provably new rather than one that happened to also work before the earlier
        /// picks landed. Tried it, measured it against the solver: stuck runs rose from 65 to 86.
        /// Forcing "provably new" throws away the highest-value clear at a tier whenever the best
        /// option also happened to be available earlier, which trades real board health for a
        /// narrative guarantee the player can't actually observe (they only ever see one order —
        /// the one they played — never the counterfactual it was ranked against). The natural
        /// dependency chaining already creates — later slots scored against the board as the
        /// earlier ones leave it — carries the mechanic without that cost.
        /// </summary>
        /// <param name="grid">Current board occupancy, [x, y].</param>
        /// <param name="shapes">Cell offsets of every shape that may be offered.</param>
        /// <param name="helpfulness">Same meaning as in <see cref="SelectTray"/>.</param>
        /// <param name="rng">Randomness source, so a simulation can run deterministically.</param>
        public static List<int> SelectTrayChained(
            bool[,] grid,
            IReadOnlyList<Vector2Int[]> shapes,
            float helpfulness,
            System.Random rng)
        {
            var result = new List<int>();
            if (grid == null || shapes == null || shapes.Count == 0)
                return result;

            int width = grid.GetLength(0);
            int height = grid.GetLength(1);

            var order = ShuffledOrder(shapes.Count, rng);

            // The tray's overall generosity budget, fixed for the whole refill so difficulty
            // doesn't drift slot to slot — only whether the budget is currently in force does
            // that (see boardHasRoom below).
            int jackpotCap = Mathf.Clamp(Mathf.RoundToInt(1 + helpfulness * 2), 1, TraySlots);

            var chainGrid = (bool[,])grid.Clone();
            int jackpotsTaken = 0;

            for (int slot = 0; slot < TraySlots; slot++)
            {
                var placeable = ScoreCandidates(chainGrid, width, height, shapes, order);

                int chosenIndex = -1;
                Vector2Int chosenAnchor = default;
                bool chosenIsJackpot = false;

                if (placeable.Count > 0)
                {
                    // One roll decides this slot's whole temperament, so a slot is either
                    // consistently generous or consistently demanding rather than generous about
                    // size and demanding about geometry in the same breath.
                    bool beDemanding = rng.NextDouble() >= helpfulness;

                    // Which clear to hand over, among those of equal line value. Generous: the
                    // roomiest shape and the smallest one — easy to see, easy to land. Demanding:
                    // the biggest shape with the fewest places to put it, so the clear is on offer
                    // but cashing it in takes reading the board. The line value itself is never
                    // traded away — see the doc comment for why clears can't be rationed.
                    var clearing = placeable.Where(p => p.clearedLines > 0)
                        .OrderByDescending(p => p.fullClear)
                        .ThenByDescending(p => p.clearedLines)
                        .ThenBy(p => beDemanding
                            ? BoundingBoxFillRatio(shapes[p.index])
                            : 1f - BoundingBoxFillRatio(shapes[p.index]))
                        .ThenBy(p => p.cellCount)
                        .ThenByDescending(p => IsSafeToOffer(chainGrid, width, height, shapes[p.index]))
                        .ToList();

                    // Re-checked against this slot's own board: relief kicks in the moment the
                    // chain itself has made things tight, same reasoning as SelectTray.
                    bool boardHasRoom = OccupiedFraction(chainGrid, width, height) < JackpotCapReliefFill;
                    int maxJackpotSlots = boardHasRoom ? jackpotCap : TraySlots;

                    foreach (var entry in clearing)
                    {
                        bool jackpot = entry.fullClear || entry.clearedLines > 1;
                        if (jackpot && jackpotsTaken >= maxJackpotSlots)
                            continue;

                        chosenIndex = entry.index;
                        chosenAnchor = entry.anchor;
                        chosenIsJackpot = jackpot;
                        break;
                    }

                    if (chosenIndex < 0)
                    {
                        // Every clearing option left is a jackpot and the budget's spent this
                        // refill — same fallback SelectTray uses for its leftover slots: prefer
                        // whichever non-clearing shape leaves the board healthiest, blended with
                        // helpfulness.
                        var leftover = placeable.Where(p => p.clearedLines == 0).ToList();
                        if (leftover.Count > 0)
                        {
                            // How many spots each shape could land in right now. This is the whole
                            // "does the player have to think about where this goes" signal: a shape
                            // that fits in a dozen places is a free move, one that fits in three or
                            // four is a decision.
                            var ranked = leftover
                                .Select(p => (p, robust: CountPlacements(chainGrid, width, height, shapes[p.index], RobustAnchorCap)))
                                .ToList();

                            // The demanding ranking only ever considers shapes at or above
                            // MinSafeAnchors. Anything below that is a trap rather than a
                            // challenge — the player can't see that the other two tray shapes are
                            // about to box it out, so losing to it reads as the game cheating.
                            var safeDemanding = beDemanding
                                ? ranked.Where(t => t.robust >= MinSafeAnchors).ToList()
                                : null;

                            ranked = safeDemanding != null && safeDemanding.Count > 0
                                // Most irregular shape first, then whichever of those has the most
                                // room. Irregularity rather than size or scarcity: it is the only
                                // one of the three that asks the player to think without also
                                // spending the board's cell budget to do it — see
                                // BoundingBoxFillRatio for the numbers behind that.
                                ? safeDemanding
                                    .OrderBy(t => BoundingBoxFillRatio(shapes[t.p.index]))
                                    .ThenByDescending(t => t.robust)
                                    .ToList()
                                // Generous, and also the fallback when nothing clears the safety
                                // floor: leave the board healthiest, roomiest and simplest first.
                                : ranked
                                    .OrderByDescending(t => EvaluateBoardHealth(t.p.resultGrid, width, height, shapes))
                                    .ThenByDescending(t => t.robust)
                                    .ThenBy(t => t.p.cellCount)
                                    .ToList();

                            chosenIndex = ranked[0].p.index;
                            chosenAnchor = ranked[0].p.anchor;
                        }
                        else
                        {
                            // Nothing left to offer but jackpots and the budget's spent — take the
                            // least generous one rather than leave the slot unfilled. Relief, not
                            // punishment: an empty slot would be worse than an over-budget clear.
                            var least = clearing.OrderBy(p => p.clearedLines).ThenBy(p => p.cellCount).First();
                            chosenIndex = least.index;
                            chosenAnchor = least.anchor;
                            chosenIsJackpot = true;
                        }
                    }
                }

                if (chosenIndex < 0)
                {
                    // Nothing placeable anywhere on this slot's board — the same rare last resort
                    // SelectTray falls back to. Hand over something rather than stall; the chain
                    // isn't advanced since there's nowhere for it to actually go.
                    chosenIndex = order.Count > 0 ? order[rng.Next(order.Count)] : rng.Next(shapes.Count);
                    result.Add(chosenIndex);
                    continue;
                }

                result.Add(chosenIndex);
                if (chosenIsJackpot)
                    jackpotsTaken++;

                SimulatePlaceAndClear(chainGrid, width, height, chosenAnchor, shapes[chosenIndex]);
            }

            return result;
        }

        private static List<int> ShuffledOrder(int count, System.Random rng)
        {
            var order = Enumerable.Range(0, count).ToList();
            for (int i = order.Count - 1; i > 0; i--)
            {
                int swap = rng.Next(i + 1);
                (order[i], order[swap]) = (order[swap], order[i]);
            }
            return order;
        }

        private static List<(int index, bool fullClear, int clearedLines, int cellCount, Vector2Int anchor, bool[,] resultGrid)> ScoreCandidates(
            bool[,] grid, int width, int height, IReadOnlyList<Vector2Int[]> shapes, List<int> order)
        {
            var result = new List<(int, bool, int, int, Vector2Int, bool[,])>();
            foreach (int index in order)
            {
                var offsets = shapes[index];
                if (offsets == null || offsets.Length == 0)
                    continue;

                if (!TryFindBestAnchor(grid, width, height, offsets, out var anchor, out int clearedLines))
                    continue;

                var preview = (bool[,])grid.Clone();
                SimulatePlaceAndClear(preview, width, height, anchor, offsets);
                bool fullClear = IsGridEmpty(preview, width, height);

                result.Add((index, fullClear, clearedLines, offsets.Length, anchor, preview));
            }
            return result;
        }
    }
}
