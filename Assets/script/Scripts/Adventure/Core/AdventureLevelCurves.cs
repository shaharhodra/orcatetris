using System.Collections.Generic;
using UnityEngine;

namespace OrcaTetris.Adventure
{
    /// <summary>
    /// The numbers behind every procedurally generated Adventure level. Kept here — free of
    /// LevelData, prefabs and MonoBehaviours — so the level curves and the solver that validates
    /// them (see <see cref="AdventureLevelSimulator"/>) live in one testable assembly.
    /// <see cref="AdventureLevelFactory"/> is the thin layer that turns these numbers into a
    /// LevelData the game can load.
    ///
    /// === Difficulty model ===
    /// Every knob below is a *saturating* function of the level number: it rises quickly through
    /// the early campaign, then flattens so that by roughly <see cref="SaturationLevel"/> a level
    /// is essentially at its final size and stays there forever after. This is deliberate — levels
    /// are generated on demand with no upper bound, so any knob that grows without limit (as the
    /// old linear target curve did, reaching 277 symbols by level 300) eventually produces a level
    /// no player can finish. Saturation means level 300 and level 3000 are equally playable.
    ///
    /// Difficulty is expressed through *more symbol types to juggle*, *a bigger target* and *a
    /// fuller starting board* — never by offering shapes that don't fit or by capping how many
    /// moves the player gets. A level simply runs until its targets are met.
    /// </summary>
    public static class AdventureLevelCurves
    {
        /// <summary>
        /// Level 4 is the first generated level (1-3 are hand-authored tutorials), so every curve
        /// measures its progress from there.
        /// </summary>
        public const int FirstGeneratedLevel = 4;

        /// <summary>
        /// The level by which each curve has covered ~95% of its range. Past this point levels
        /// keep varying in shape and layout, but stop meaningfully getting harder.
        /// </summary>
        public const int SaturationLevel = 150;

        // exp(-Rate * (SaturationLevel - FirstGeneratedLevel)) = 0.05  =>  Rate = 3 / 146
        private const float SaturationRate = 3f / (SaturationLevel - FirstGeneratedLevel);

        public const int TargetFloor = 12;
        public const int TargetCeiling = 45;

        private const int FillFloor = 8;
        private const int FillCeiling = 18;

        public const int DifficultyCeiling = 55;

        /// <summary>
        /// Smallest number of any one symbol type a level will ask for, so a type never shows up
        /// as a token "collect 1 of these" entry in the target UI.
        /// </summary>
        public const int MinPerType = 3;

        /// <summary>
        /// How far along its difficulty range a level sits: 0 at the first generated level, ~0.95
        /// at <see cref="SaturationLevel"/>, asymptotically approaching (but never reaching) 1
        /// beyond it. Every curve here is this one shape scaled into its own range, so they all
        /// ramp and flatten together rather than each plateauing at a different level.
        /// </summary>
        public static float GetProgress(int level)
        {
            int steps = Mathf.Max(0, level - FirstGeneratedLevel);
            return 1f - Mathf.Exp(-SaturationRate * steps);
        }

        /// <summary>
        /// Total symbols the level asks for, growing from <see cref="TargetFloor"/> toward
        /// <see cref="TargetCeiling"/>. The small per-level jitter keeps consecutive levels from
        /// feeling mechanically identical without changing the underlying curve.
        /// </summary>
        public static int GetTotalTarget(int level)
        {
            float value = Mathf.Lerp(TargetFloor, TargetCeiling, GetProgress(level));
            int jitter = (level % 3) - 1;
            return Mathf.Max(TargetFloor, Mathf.RoundToInt(value) + jitter);
        }

        /// <summary>
        /// Which of the 4 collection types are active this level and how the total splits between
        /// them. The *count* of active types rises with the difficulty curve (2 early, 4 once
        /// saturated) — juggling more types at once is a real difficulty axis. Which types those
        /// are rotates by level number, which is pure variety and carries no difficulty at all.
        ///
        /// Weights always keep one clearly dominant type. An even 4-way split (which this used to
        /// produce every 4th level) reads as a sudden spike because there's nothing to focus on,
        /// and it landed on some levels and not others — exactly the inconsistent ramp these
        /// curves exist to avoid.
        /// </summary>
        public static List<int> GetActiveTypes(int level, out List<float> weights)
        {
            int typeCount = Mathf.Clamp(2 + Mathf.RoundToInt(GetProgress(level) * 2f), 2, 4);

            weights = new List<float>();
            switch (typeCount)
            {
                case 2: weights.AddRange(new[] { 0.6f, 0.4f }); break;
                case 3: weights.AddRange(new[] { 0.45f, 0.3f, 0.25f }); break;
                default: weights.AddRange(new[] { 0.4f, 0.25f, 0.2f, 0.15f }); break;
            }

            int startType = level % 4;
            var types = new List<int>();
            for (int i = 0; i < typeCount; i++)
                types.Add((startType + i) % 4);
            return types;
        }

        /// <summary>
        /// Splits <paramref name="total"/> across the weighted types. Every type is guaranteed at
        /// least <see cref="MinPerType"/>, and only the remainder above those floors is weighted —
        /// so the amounts always sum to exactly <paramref name="total"/> rather than drifting
        /// above it when a small weight rounds up into its floor.
        /// </summary>
        public static List<int> SplitTarget(int total, List<float> weights)
        {
            var amounts = new List<int>();
            if (weights == null || weights.Count == 0)
                return amounts;

            int pool = Mathf.Max(0, total - MinPerType * weights.Count);

            int assigned = 0;
            for (int i = 0; i < weights.Count - 1; i++)
            {
                int amount = Mathf.RoundToInt(pool * weights[i]);
                amounts.Add(MinPerType + amount);
                assigned += amount;
            }

            amounts.Add(MinPerType + Mathf.Max(0, pool - assigned));
            return amounts;
        }

        /// <summary>
        /// How many cells of the level's pattern start filled. Saturates like everything else, so
        /// a late level never starts so cluttered that the board can't breathe.
        /// </summary>
        public static int GetFillCount(int level)
        {
            return Mathf.RoundToInt(Mathf.Lerp(FillFloor, FillCeiling, GetProgress(level)));
        }

        /// <summary>
        /// Seeds the tray automation's toughness (0-100). With decoys gone this no longer gates
        /// how many tray slots are usable — all 3 always are — it only steers how hard the picker
        /// tries to hand the player a board-healthy shape, so a high value makes a level grindier
        /// rather than unwinnable.
        /// </summary>
        public static int GetBaseDifficultyPct(int level)
        {
            return Mathf.Clamp(Mathf.RoundToInt(DifficultyCeiling * GetProgress(level)), 0, DifficultyCeiling);
        }

        /// <summary>
        /// 6 rotating starting-block layouts (centered on an 8x8 grid): diamond ring, four
        /// corners, cross, checkerboard, diagonal stripe, hollow ring. Pure variety — which one a
        /// level gets carries no difficulty weight.
        /// </summary>
        public static List<Vector2Int> GetPatternCells(int patternIndex)
        {
            var cells = new List<Vector2Int>();
            switch (patternIndex)
            {
                case 0:
                    for (int x = 0; x < 8; x++)
                        for (int y = 0; y < 8; y++)
                        {
                            float d = Mathf.Abs(x - 3.5f) + Mathf.Abs(y - 3.5f);
                            if (d >= 1.5f && d <= 3.0f) cells.Add(new Vector2Int(x, y));
                        }
                    break;
                case 1:
                    foreach (int cx in new[] { 1, 5 })
                        foreach (int cy in new[] { 1, 5 })
                            foreach (int dx in new[] { 0, 1 })
                                foreach (int dy in new[] { 0, 1 })
                                    cells.Add(new Vector2Int(cx + dx, cy + dy));
                    break;
                case 2:
                    for (int x = 2; x <= 5; x++) { cells.Add(new Vector2Int(x, 3)); cells.Add(new Vector2Int(x, 4)); }
                    for (int y = 2; y <= 5; y++) { cells.Add(new Vector2Int(3, y)); cells.Add(new Vector2Int(4, y)); }
                    break;
                case 3:
                    for (int x = 1; x < 7; x++)
                        for (int y = 1; y < 7; y++)
                            if ((x + y) % 2 == 0) cells.Add(new Vector2Int(x, y));
                    break;
                case 4:
                    for (int i = 0; i < 8; i++)
                    {
                        cells.Add(new Vector2Int(i, i));
                        cells.Add(new Vector2Int(i, 7 - i));
                    }
                    break;
                default:
                    for (int x = 1; x < 7; x++) { cells.Add(new Vector2Int(x, 1)); cells.Add(new Vector2Int(x, 6)); }
                    for (int y = 2; y < 6; y++) { cells.Add(new Vector2Int(1, y)); cells.Add(new Vector2Int(6, y)); }
                    break;
            }

            // Dedup while preserving order — a pattern's own geometry shouldn't repeat a cell, but
            // guard anyway so this can never produce a duplicate-coordinate bug.
            var seen = new HashSet<Vector2Int>();
            var unique = new List<Vector2Int>();
            foreach (var c in cells)
                if (seen.Add(c)) unique.Add(c);
            return unique;
        }

        /// <summary>
        /// The starting board for a level: which cells are pre-filled and which symbol type each
        /// one carries. Shared by the factory (which turns it into InitialBlockData) and the
        /// simulator (which plays it), so the level the solver validates is exactly the level the
        /// player gets.
        /// </summary>
        public static List<(Vector2Int cell, int symbol)> GetInitialBlocks(int level, List<int> activeTypes)
        {
            var cells = GetPatternCells(level % 6);
            int fillCount = GetFillCount(level);

            if (cells.Count > fillCount)
            {
                double step = cells.Count / (double)fillCount;
                var picked = new List<Vector2Int>();
                for (int i = 0; i < fillCount; i++)
                    picked.Add(cells[(int)(i * step)]);
                cells = picked;
            }

            var result = new List<(Vector2Int, int)>();
            for (int i = 0; i < cells.Count; i++)
                result.Add((cells[i], activeTypes[i % activeTypes.Count]));

            return result;
        }
    }
}
