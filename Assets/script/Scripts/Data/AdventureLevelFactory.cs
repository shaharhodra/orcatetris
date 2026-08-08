using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedurally builds a full Adventure <see cref="LevelData"/> for any level number, so Adventure
/// mode has no level ceiling — it never runs out of levels to serve, the same way Classic mode
/// never runs out of board. Every level built here runs purely on ShapeTrayManager's adaptive
/// automation (no ShapeWaves): the automation itself guarantees a legal move every refill, so a
/// level only needs to specify which symbol types it needs and how much of each (LevelTargets), a
/// light starting board pattern (InitialBlocks), and a base automation toughness (DifficultyLevel)
/// for ShapeTrayManager to seed itself with before its in-level score ramp takes over.
///
/// This is a plain runtime class (unlike Assets/Editor/AdventureLevelGenerator.cs, which is
/// Editor-only and stripped from player builds) so it can be called during actual gameplay —
/// AppManager/GameManager fall back to it once the pre-authored Level4-35.json files run out.
/// AdventureLevelGenerator.cs also calls into this same class so the pre-baked files and the
/// live fallback can never drift out of sync with each other.
/// </summary>
public static class AdventureLevelFactory
{
    public static LevelData Generate(int level)
    {
        int total = GetTotalTarget(level);
        var types = GetActiveTypes(level, out var weights);
        var amounts = SplitTarget(total, weights);

        var levelTargets = new List<LevelTargetData>();
        for (int i = 0; i < types.Count; i++)
        {
            levelTargets.Add(new LevelTargetData
            {
                Target = amounts[i],
                TargetItem = (ColectionTypes)types[i],
                Color = new Color(0f, 0f, 0f, 0f)
            });
        }

        int patternIndex = level % 6;
        var cells = GetPatternCells(patternIndex);
        int fillCount = GetFillCount(level);
        if (cells.Count > fillCount)
        {
            double step = cells.Count / (double)fillCount;
            var picked = new List<Vector2Int>();
            for (int i = 0; i < fillCount; i++)
                picked.Add(cells[(int)(i * step)]);
            cells = picked;
        }

        var initialBlocks = new List<InitialBlockData>();
        for (int i = 0; i < cells.Count; i++)
        {
            initialBlocks.Add(new InitialBlockData
            {
                x = cells[i].x,
                y = cells[i].y,
                Symbol = types[i % types.Count]
            });
        }

        return new LevelData
        {
            GameType = GameTypes.Adventure,
            LevelName = $"Level {level}",
            Level = level,
            GridRows = 8,
            GridColumns = 8,
            NumberOfShapes = 0,
            DifficultyLevel = GetBaseDifficultyPct(level),
            DifficultyThreshold = 0,
            ScorePerPlaceCell = 0,
            ScorePerClearCell = 0,
            ColorLevels = new List<ColorLevels>(),
            ScorePerCoinThreshold = 0,
            CoinsPerThreshold = 0,
            LevelTargets = levelTargets,
            ShapeWaves = null,
            InitialBlocks = initialBlocks
        };
    }

    // Gentle growth with a little per-level jitter so it isn't perfectly linear, clamped to a
    // sane floor. Same curve used to regenerate Level4-35.json this session.
    public static int GetTotalTarget(int level)
    {
        int baseValue = 12 + Mathf.FloorToInt((level - 4) * 0.9f + 0.5f);
        int jitter = (level % 3) - 1;
        return Mathf.Max(8, baseValue + jitter);
    }

    // Cycles which of the 4 ColectionTypes are "active" this level (2-4 of them, starting type
    // rotating too) and how the total splits between them (sometimes even, sometimes one
    // dominant type) — so consecutive levels don't all look the same shape.
    public static List<int> GetActiveTypes(int level, out List<float> weights)
    {
        int startType = level % 4;
        int band = level % 4;

        weights = new List<float>();
        int n;
        switch (band)
        {
            case 0: n = 2; weights.AddRange(new[] { 0.65f, 0.35f }); break;
            case 1: n = 3; weights.AddRange(new[] { 0.5f, 0.25f, 0.25f }); break;
            case 2: n = 4; weights.AddRange(new[] { 0.25f, 0.25f, 0.25f, 0.25f }); break;
            default: n = 3; weights.AddRange(new[] { 0.4f, 0.35f, 0.25f }); break;
        }

        var types = new List<int>();
        for (int i = 0; i < n; i++)
            types.Add((startType + i) % 4);
        return types;
    }

    public static List<int> SplitTarget(int total, List<float> weights)
    {
        var amounts = new List<int>();
        int sum = 0;
        for (int i = 0; i < weights.Count - 1; i++)
        {
            int amt = Mathf.Max(4, Mathf.RoundToInt(total * weights[i]));
            amounts.Add(amt);
            sum += amt;
        }
        amounts.Add(Mathf.Max(4, total - sum));
        return amounts;
    }

    // 6 rotating InitialBlocks layouts (centered on an 8x8 grid): diamond ring, four corners,
    // cross, checkerboard, diagonal stripe, hollow ring.
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
        // guard anyway so this can never produce the duplicate-coordinate bug fixed this session.
        var seen = new HashSet<Vector2Int>();
        var unique = new List<Vector2Int>();
        foreach (var c in cells)
            if (seen.Add(c)) unique.Add(c);
        return unique;
    }

    public static int GetFillCount(int level)
    {
        int count = Mathf.RoundToInt(8 + (level - 4) * 0.25f);
        return Mathf.Min(count, 20);
    }

    // 0 at Level 4 up to 55 at Level 35, staying at 55 forever beyond that — leaving 55-100
    // headroom for the in-level score ramp (ShapeTrayManager.HandleScoreUpdatedForDifficultyRamp)
    // to keep escalating further no matter how far the player has progressed.
    public static int GetBaseDifficultyPct(int level)
    {
        int pct = Mathf.RoundToInt((level - 4) / 31f * 55f);
        return Mathf.Clamp(pct, 0, 55);
    }
}
