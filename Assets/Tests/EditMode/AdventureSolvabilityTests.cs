using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace OrcaTetris.Adventure.Tests
{
    /// <summary>
    /// The safety net for procedurally generated Adventure levels. Nobody can hand-play level 487,
    /// so these tests play it instead: <see cref="AdventureLevelSimulator"/> runs a deliberately
    /// greedy (non-optimal) player against the real curves and the real tray-selection policy, and
    /// the suite fails if any level turns out to be unbeatable.
    ///
    /// This exists because of a concrete regression: levels in the low 20s became unwinnable once
    /// the tray started offering decoy shapes it knew the player couldn't place, and nothing in
    /// the project noticed.
    /// </summary>
    public class AdventureSolvabilityTests
    {
        /// <summary>Levels 1-3 are hand-authored JSON tutorials, so the curves start at 4.</summary>
        private const int FirstLevel = AdventureLevelCurves.FirstGeneratedLevel;

        /// <summary>
        /// Every level up to here is checked individually — this is the stretch a real player
        /// actually walks through, and where a bad level does the most damage.
        /// </summary>
        private const int DenseCheckThrough = 120;

        /// <summary>
        /// Past the dense range, sample every Nth level. The curves have saturated well before
        /// this, so neighbouring levels differ only in layout and which types are active.
        /// </summary>
        private const int SampleStride = 10;
        private const int LastSampledLevel = 500;

        /// <summary>
        /// Seeds per level. More than one because the tray draw and symbol placement are random,
        /// and a level that only sometimes works is still a broken level.
        /// </summary>
        private static readonly int[] Seeds = { 1, 7, 99, 1234 };

        private static List<Vector2Int[]> shapePool;
        private static List<(int level, int seed, AdventureLevelSimulator.Result result)> runs;

        [OneTimeSetUp]
        public void PlayEveryLevelOnce()
        {
            shapePool = LoadShapePoolFromPrefabs();
            Assert.IsNotEmpty(shapePool,
                "No shape prefabs with cell data were found — the solver would be testing nothing.");

            // One sweep shared by every assertion below; each simulation is deterministic in its
            // seed, so running them once and asserting many times costs nothing in fidelity.
            runs = new List<(int, int, AdventureLevelSimulator.Result)>();
            foreach (int level in LevelsUnderTest())
                foreach (int seed in Seeds)
                    runs.Add((level, seed, AdventureLevelSimulator.Play(level, shapePool, seed)));
        }

        private static IEnumerable<int> LevelsUnderTest()
        {
            for (int level = FirstLevel; level <= DenseCheckThrough; level++)
                yield return level;

            for (int level = DenseCheckThrough + SampleStride; level <= LastSampledLevel; level += SampleStride)
                yield return level;
        }

        /// <summary>
        /// The core guarantee: a level is winnable by a player who places shapes well. Adventure
        /// has no revive and no mulligan for a tray that no longer fits anywhere, so this greedy
        /// (one-ply, non-optimal) solver getting stuck on a level is a real signal — either that
        /// level's curve leaves too little margin for anything short of perfect play, or the
        /// generator produced a genuinely unwinnable board. Either way it's worth a look: a
        /// player who "didn't play well" should lose, but a level should still be clearable by
        /// reasonable play, not a knife's-edge only a perfect solver could thread.
        /// </summary>
        [Test]
        public void EveryGeneratedLevelIsEventuallyBeatable()
        {
            var failures = runs
                .Where(r => !r.result.Won)
                .Select(r => $"Level {r.level} (seed {r.seed}): {r.result}")
                .ToList();

            Assert.IsEmpty(failures,
                "Levels a greedy solver could not finish:\n" + string.Join("\n", failures));
        }

        /// <summary>
        /// The regression this whole effort came from: difficulty must never step. Each knob
        /// should be non-decreasing across the campaign, so no single level spikes relative to
        /// its neighbours the way level 22 did when the active-type count cycled on level % 4.
        /// </summary>
        [Test]
        public void DifficultyKnobsRiseMonotonically()
        {
            int previousTypeCount = 0;
            int previousDifficulty = -1;
            int previousFill = -1;

            for (int level = FirstLevel; level <= LastSampledLevel; level++)
            {
                var types = AdventureLevelCurves.GetActiveTypes(level, out _);
                int difficulty = AdventureLevelCurves.GetBaseDifficultyPct(level);
                int fill = AdventureLevelCurves.GetFillCount(level);

                Assert.GreaterOrEqual(types.Count, previousTypeCount,
                    $"Active symbol types dropped then rose again at level {level} — that reads as a difficulty spike.");
                Assert.GreaterOrEqual(difficulty, previousDifficulty,
                    $"Base difficulty is not monotonic at level {level}.");
                Assert.GreaterOrEqual(fill, previousFill,
                    $"Starting fill count is not monotonic at level {level}.");

                previousTypeCount = types.Count;
                previousDifficulty = difficulty;
                previousFill = fill;
            }
        }

        /// <summary>
        /// The old curves grew without bound (277 symbols by level 300). Saturation is the whole
        /// reason arbitrarily deep levels stay playable, so pin it down.
        /// </summary>
        [Test]
        public void CurvesSaturateInsteadOfGrowingForever()
        {
            Assert.LessOrEqual(AdventureLevelCurves.GetTotalTarget(10_000), AdventureLevelCurves.TargetCeiling + 1,
                "Target count is still growing without bound at extreme levels.");

            int atSaturation = AdventureLevelCurves.GetTotalTarget(AdventureLevelCurves.SaturationLevel);
            int farBeyond = AdventureLevelCurves.GetTotalTarget(AdventureLevelCurves.SaturationLevel * 10);

            Assert.LessOrEqual(farBeyond - atSaturation, 4,
                "Levels well past saturation should be near-identical in size to the saturation point.");
        }

        /// <summary>
        /// Every symbol type in a level's target must be worth showing in the UI, and the split
        /// must add up to exactly the level's total.
        /// </summary>
        [Test]
        public void TargetSplitsAreExactAndNeverTokenAmounts()
        {
            for (int level = FirstLevel; level <= LastSampledLevel; level++)
            {
                int total = AdventureLevelCurves.GetTotalTarget(level);
                AdventureLevelCurves.GetActiveTypes(level, out var weights);
                var amounts = AdventureLevelCurves.SplitTarget(total, weights);

                Assert.AreEqual(total, amounts.Sum(),
                    $"Level {level}: target split sums to {amounts.Sum()} but the level asks for {total}.");

                foreach (int amount in amounts)
                {
                    Assert.GreaterOrEqual(amount, AdventureLevelCurves.MinPerType,
                        $"Level {level}: a symbol type is only worth {amount}, below the {AdventureLevelCurves.MinPerType} floor.");
                }
            }
        }

        /// <summary>
        /// Reads the real shape prefabs so the solver plays with the pieces the game ships.
        ///
        /// Deliberately reflective: Shape lives in Assembly-CSharp, which an assembly-definition
        /// test assembly cannot reference, so the cells are read through SerializedObject by name
        /// instead of through the type. If Shape's serialized layout changes this finds nothing,
        /// and the assertion in PlayEveryLevelOnce fails loudly rather than silently testing an
        /// empty pool.
        /// </summary>
        private static List<Vector2Int[]> LoadShapePoolFromPrefabs()
        {
            var pool = new List<Vector2Int[]>();

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/shapes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                var shapeComponent = prefab.GetComponents<Component>()
                    .FirstOrDefault(c => c != null && c.GetType().Name == "Shape");
                if (shapeComponent == null)
                    continue;

                var cellsProperty = new SerializedObject(shapeComponent).FindProperty("shapeData.cells");
                if (cellsProperty == null || !cellsProperty.isArray || cellsProperty.arraySize == 0)
                    continue;

                var cells = new Vector2Int[cellsProperty.arraySize];
                for (int i = 0; i < cellsProperty.arraySize; i++)
                    cells[i] = cellsProperty.GetArrayElementAtIndex(i).vector2IntValue;

                pool.Add(cells);
            }

            return pool;
        }
    }
}
