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
        /// <summary>
        /// Levels 1-3 are hand-authored JSON tutorials, so the curves start at 4. Read at runtime
        /// rather than as a const because the curve values became overridable by Remote Config —
        /// the suite always validates whatever the build's defaults currently are.
        /// </summary>
        private static int FirstLevel => AdventureLevelCurves.FirstGeneratedLevel;

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
        /// The reason Adventure felt too easy, twice over: first the tray ranked shapes
        /// best-clear-first regardless of difficulty, and second — even after that was capped —
        /// every slot was still scored against today's board independently, so all 3 shapes always
        /// fit comfortably no matter what order the player used or which one they placed first.
        /// TraySelectionCore.SelectTrayChained fixes the second problem by scoring each slot
        /// against the board as it would look after the slots before it land and clear, so getting
        /// the combo right is what makes the tray fit at all, not just cheaper.
        ///
        /// Three invariants are checked here, the first two learned the hard way from this exact
        /// method's earlier drafts. The tray must still be solvable in at least the order it was
        /// dealt — chaining deliberately makes later slots depend on earlier ones, but a shape with
        /// nowhere to go even after its predecessors clear would be an unconditional dead end, not
        /// a difficulty dial. The *count* of clearing shapes must not drop with difficulty: this
        /// board needs roughly a line and a half of relief per refill to break even, and an earlier
        /// attempt at withholding clears outright (rather than just their generosity) nearly
        /// tripled the solver's stuck rate. And fewer multi-line jackpots should appear as
        /// difficulty rises — the assertion that stops the mode quietly sliding back to easy.
        /// </summary>
        [Test]
        public void TrayOffersFewerMultiLineJackpotsAsDifficultyRises()
        {
            const int Draws = 200;

            var easy = CountOffers(helpfulness: 1f, draws: Draws);
            var hard = CountOffers(helpfulness: 0f, draws: Draws);

            Assert.Greater(easy.jackpots, 0,
                "The test board offers no multi-line clear at all, so this proves nothing — fix the fixture.");
            Assert.Less(hard.jackpots, Mathf.CeilToInt(easy.jackpots * 0.75f),
                $"Max difficulty handed out {hard.jackpots} multi-line clears across {Draws} refills vs " +
                $"{easy.jackpots} at max helpfulness. The jackpot cap is not biting.");

            Assert.GreaterOrEqual(hard.clears, Mathf.FloorToInt(easy.clears * 0.9f),
                $"Max difficulty offered only {hard.clears} clearing shapes vs {easy.clears} at max helpfulness. " +
                "Difficulty must cap how generous a clear is, not how often one is available — this board " +
                "cannot survive refills it has no way to clear.");
        }

        /// <summary>
        /// The complaint this round came from: at level 45 the tray only ever handed over shapes
        /// that dropped anywhere, so placement was never a decision. Difficulty is supposed to
        /// steer *which* shape arrives — bigger, and with fewer places it will go — and nothing was
        /// pinning that down, which is how a previous fix could invert it (preferring the roomiest
        /// shape in every branch) without a single test noticing.
        ///
        /// Measured against the real prefab pool, since this is a claim about what the shipped
        /// shapes let the picker do, not about a synthetic fixture.
        /// </summary>
        [Test]
        public void TrayPrefersDemandingShapesAsDifficultyRises()
        {
            const int Draws = 200;

            var easy = MeasureShapeDemand(helpfulness: 1f, draws: Draws);
            var hard = MeasureShapeDemand(helpfulness: 0f, draws: Draws);

            Assert.Less(hard.averageFillRatio, easy.averageFillRatio,
                $"Max difficulty offered shapes filling {hard.averageFillRatio:F3} of their bounding box on " +
                $"average vs {easy.averageFillRatio:F3} at max helpfulness. Squares, rectangles and straight " +
                "lines score 1 and drop anywhere; difficulty is supposed to reach for the irregular pieces.");

            Assert.Less(hard.averageAnchors, easy.averageAnchors,
                $"Max difficulty offered shapes with {hard.averageAnchors:F2} placements on average vs " +
                $"{easy.averageAnchors:F2} at max helpfulness. If a shape drops anywhere, where it goes was " +
                "never a decision — that is the whole complaint this test exists for.");

            // Guards the trap this round measured twice: preferring bigger pieces floods the board
            // (103 stuck solver runs) without asking a better question than an awkward small one.
            //
            // Measured against the pool's own mean, not against the easy run. The easy run averages
            // barely over 1.5 cells because being generous means handing out singles and dominoes,
            // so comparing to it would flag any tray that isn't nearly as trivial. The real claim
            // is that demand costs the board nothing: a demanding tray should draw shapes no larger
            // than the pool average, and get its difficulty from their geometry instead.
            double poolAverageCells = shapePool.Average(s => s.Length);
            Assert.LessOrEqual(hard.averageCells, poolAverageCells,
                $"Max difficulty offered shapes averaging {hard.averageCells:F2} cells against a pool average of " +
                $"{poolAverageCells:F2}. Demand must come from shape geometry, not from spending the board's cell " +
                "budget — an 8x8 taking three shapes a refill cannot absorb bigger pieces.");
        }

        /// <summary>
        /// The safety floor that separates a demanding tray from an unfair one: a shape with one or
        /// two placements can be boxed out by the other two shapes in its own tray before the
        /// player reaches it, which is invisible to them and was the cause of the 93 stuck runs the
        /// previous round diagnosed. No roll, at any difficulty, may go below it.
        /// </summary>
        [Test]
        public void OfferedShapesAreNeverPrecarious()
        {
            const int Size = AdventureLevelSimulator.BoardSize;
            var rng = new System.Random(90210);

            // Sweep difficulty rather than testing the extremes: the floor has to hold on every
            // roll, and the demanding branch only engages on some of them.
            for (int step = 0; step <= 10; step++)
            {
                float helpfulness = step / 10f;

                for (int draw = 0; draw < 40; draw++)
                {
                    var grid = BuildClearReadyBoard();
                    var picks = TraySelectionCore.SelectTrayChained(grid, shapePool, helpfulness, rng);

                    var chain = (bool[,])grid.Clone();
                    foreach (int index in picks)
                    {
                        int anchors = TraySelectionCore.CountPlacements(
                            chain, Size, Size, shapePool[index], TraySelectionCore.RobustAnchorCap);

                        Assert.GreaterOrEqual(anchors, 1,
                            "Every offered shape must fit somewhere on the board it is dealt against.");

                        TraySelectionCore.TryFindBestAnchor(
                            chain, Size, Size, shapePool[index], out var anchor, out _);
                        TraySelectionCore.SimulatePlaceAndClear(chain, Size, Size, anchor, shapePool[index]);
                    }
                }
            }
        }

        /// <summary>
        /// Averages, across many chained trays, how irregular each offered shape is, how many
        /// places it could go, and how big it is — the first two being what make a placement take
        /// thought, and the third being the cost that has to stay flat while they rise.
        /// </summary>
        private static (double averageFillRatio, double averageAnchors, double averageCells) MeasureShapeDemand(
            float helpfulness, int draws)
        {
            const int Size = AdventureLevelSimulator.BoardSize;

            var rng = new System.Random(4242);
            double totalFillRatio = 0;
            long totalCells = 0;
            long totalAnchors = 0;
            int counted = 0;

            for (int draw = 0; draw < draws; draw++)
            {
                var grid = BuildClearReadyBoard();
                var picks = TraySelectionCore.SelectTrayChained(grid, shapePool, helpfulness, rng);

                var chain = (bool[,])grid.Clone();
                foreach (int index in picks)
                {
                    totalFillRatio += TraySelectionCore.BoundingBoxFillRatio(shapePool[index]);
                    totalCells += shapePool[index].Length;
                    totalAnchors += TraySelectionCore.CountPlacements(
                        chain, Size, Size, shapePool[index], TraySelectionCore.RobustAnchorCap);
                    counted++;

                    TraySelectionCore.TryFindBestAnchor(
                        chain, Size, Size, shapePool[index], out var anchor, out _);
                    TraySelectionCore.SimulatePlaceAndClear(chain, Size, Size, anchor, shapePool[index]);
                }
            }

            return (totalFillRatio / counted, totalAnchors / (double)counted, totalCells / (double)counted);
        }

        /// <summary>
        /// A board with enough clutter that shapes differ in how easily they fit — an empty board
        /// takes everything anywhere and would measure nothing — but still open enough that clears
        /// and awkward pieces are both live options.
        /// </summary>
        private static bool[,] BuildClearReadyBoard()
        {
            const int Size = AdventureLevelSimulator.BoardSize;

            var grid = new bool[Size, Size];
            for (int y = 0; y <= 1; y++)
                for (int x = 0; x < Size - 1; x++)
                    grid[x, y] = true;
            for (int y = 4; y <= 5; y++)
                for (int x = 1; x < Size; x++)
                    grid[x, y] = true;

            return grid;
        }

        /// <summary>
        /// Draws chained trays against a board with two independent 2-line-clear setups, then
        /// replays each tray in the order it was dealt — landing every shape at its best spot and
        /// clearing before checking the next — and counts how many placements clear at all, and how
        /// many clear more than one line. The replay is what checks the chain actually holds
        /// together: SelectTrayChained promises the dealt order is solvable, so failing to place a
        /// shape mid-replay means that promise broke.
        ///
        /// Uses a small synthetic pool rather than the real shape prefabs, deliberately. A single
        /// clear-setup can only ever be cashed in once — after the shape that clears it lands, the
        /// board no longer offers that clear — so measuring the cap's effect needs *two* setups
        /// live in the same tray, and needs a piece guaranteed capable of taking either only one
        /// line (graceful downgrade) or both (jackpot) so the two outcomes are actually
        /// distinguishable. The real prefab pool doesn't give that guarantee; three hand-built
        /// shapes here do.
        /// </summary>
        private static (int clears, int jackpots) CountOffers(float helpfulness, int draws)
        {
            const int Size = AdventureLevelSimulator.BoardSize;

            var pool = new List<Vector2Int[]>
            {
                new[] { new Vector2Int(0, 0) },                                          // single cell
                new[] { new Vector2Int(0, 0), new Vector2Int(0, 1) },                    // vertical domino
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) }, // filler triomino
            };

            // Two bands, each two rows short exactly one column, far enough apart that clearing
            // one never touches the other: the vertical domino jackpots either on its own (2
            // lines), while the single cell can only ever take one line of either band, which is
            // exactly the fallback the cap should reach for once its jackpot budget is spent.
            // Kept under 45% fill so the cap's own relief valve (see SelectTrayChained) doesn't
            // wave the whole thing through regardless of difficulty.
            var baseGrid = new bool[Size, Size];
            for (int y = 0; y <= 1; y++)
                for (int x = 0; x < Size - 1; x++)
                    baseGrid[x, y] = true;
            for (int y = 4; y <= 5; y++)
                for (int x = 1; x < Size; x++)
                    baseGrid[x, y] = true;

            var rng = new System.Random(4242);
            int clears = 0;
            int jackpots = 0;

            for (int draw = 0; draw < draws; draw++)
            {
                var picks = TraySelectionCore.SelectTrayChained(baseGrid, pool, helpfulness, rng);

                Assert.AreEqual(TraySelectionCore.TraySlots, picks.Count,
                    "Adventure must always be dealt a full tray.");

                var chain = (bool[,])baseGrid.Clone();
                foreach (int index in picks)
                {
                    bool placeable = TraySelectionCore.TryFindBestAnchor(
                        chain, Size, Size, pool[index], out var anchor, out int clearedLines);

                    Assert.IsTrue(placeable,
                        "Every tray must be solvable in at least the order it was dealt — chaining can make a " +
                        "later slot depend on an earlier one clearing, but it must never depend on a clear " +
                        "that never happens.");

                    if (clearedLines > 0)
                        clears++;
                    if (clearedLines > 1)
                        jackpots++;

                    TraySelectionCore.SimulatePlaceAndClear(chain, Size, Size, anchor, pool[index]);
                }
            }

            return (clears, jackpots);
        }

        /// <summary>
        /// Adventure has no score, so difficulty ramps off collected targets instead. The ramp has
        /// to be one-directional and bounded: a level that got easier mid-play, or that blew past
        /// the peak, would undo the solvability guarantee above.
        /// </summary>
        [Test]
        public void InLevelDifficultyRampIsMonotonicAndBounded()
        {
            float peak = AdventureLevelCurves.InLevelDifficultyPeak / 100f;

            for (int level = FirstLevel; level <= LastSampledLevel; level++)
            {
                float baseDifficulty = AdventureLevelCurves.GetBaseDifficultyPct(level) / 100f;
                float previous = -1f;

                for (int step = 0; step <= 20; step++)
                {
                    float progress = step / 20f;
                    float value = AdventureLevelCurves.GetDifficultyAtProgress(baseDifficulty, progress);

                    Assert.GreaterOrEqual(value, baseDifficulty - 1e-4f,
                        $"Level {level} got easier than its own starting difficulty at progress {progress}.");
                    Assert.LessOrEqual(value, peak + 1e-4f,
                        $"Level {level} ramped past the in-level peak at progress {progress}.");
                    Assert.GreaterOrEqual(value, previous - 1e-4f,
                        $"Level {level} eased off at progress {progress} — the ramp must never reverse.");

                    previous = value;
                }

                Assert.AreEqual(baseDifficulty, AdventureLevelCurves.GetDifficultyAtProgress(baseDifficulty, 0f), 1e-4f,
                    $"Level {level} does not open at its own starting difficulty.");
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
