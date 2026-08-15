using System.Collections.Generic;
using OrcaTetris.Adventure;
using UnityEngine;

/// <summary>
/// Turns a level number into a full Adventure <see cref="LevelData"/>, so Adventure mode has no
/// level ceiling — it never runs out of levels to serve, the same way Classic mode never runs out
/// of board. Every level built here runs purely on ShapeTrayManager's adaptive automation (no
/// ShapeWaves), so a level only needs to specify which symbol types it needs and how much of each
/// (LevelTargets), a light starting board pattern (InitialBlocks), and a base automation toughness
/// (DifficultyLevel). There is no move limit — a level simply runs until its targets are met, or
/// until the player's own placements leave the tray with nowhere to go (see
/// ShapeSelectionAlgorithm's allowDecoys: every dealt slot is placeable at deal time, so a dead
/// end is on the player, not the deal).
///
/// All the actual numbers live in <see cref="AdventureLevelCurves"/> — this class is only the
/// adapter that shapes them into LevelData. They're split that way so
/// <see cref="AdventureLevelSimulator"/> can play-test the exact same curves headlessly (see
/// Assets/Tests/EditMode/AdventureSolvabilityTests.cs), which is what makes "no generated level is
/// impossible" a property the project verifies rather than assumes.
/// </summary>
public static class AdventureLevelFactory
{
    public static LevelData Generate(int level)
    {
        var types = AdventureLevelCurves.GetActiveTypes(level, out var weights);
        var amounts = AdventureLevelCurves.SplitTarget(AdventureLevelCurves.GetTotalTarget(level), weights);

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

        var initialBlocks = new List<InitialBlockData>();
        foreach (var (cell, symbol) in AdventureLevelCurves.GetInitialBlocks(level, types))
        {
            initialBlocks.Add(new InitialBlockData
            {
                x = cell.x,
                y = cell.y,
                Symbol = symbol
            });
        }

        return new LevelData
        {
            GameType = GameTypes.Adventure,
            LevelName = $"Level {level}",
            Level = level,
            GridRows = AdventureLevelSimulator.BoardSize,
            GridColumns = AdventureLevelSimulator.BoardSize,
            NumberOfShapes = 0,
            DifficultyLevel = AdventureLevelCurves.GetBaseDifficultyPct(level),
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
}
