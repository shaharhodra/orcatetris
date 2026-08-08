using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generator for Adventure JSON levels based on an existing template level.
/// It clones Level2.json into Level3..Level35 and gradually increases targets.
///
/// WARNING: Levels 6-9, 11-20 and 22-35 were hand-tuned afterward (2026-08-08) with a
/// much gentler, varied difficulty curve and lighter InitialBlocks layouts than this
/// tool produces — running "Generate Levels 3-35" again will silently overwrite that
/// tuning with this tool's old linear-growth formula. Levels 1-5, 10 and 21 were always
/// hand-curated and are also untouched by this tool (it only ever writes 3-35, and 10/21
/// happen to have been re-edited by hand after generation).
/// </summary>
public static class AdventureLevelGenerator
{
    // Path to the template level we use as a base (Level 2 you already configured).
    private const string TemplatePath = "Assets/Levels/Level2.json";

    // Output folder for generated levels.
    private const string OutputFolder = "Assets/Levels";

    // First and last level indexes to generate (inclusive).
    private const int FirstLevelToGenerate = 3;
    private const int LastLevelToGenerate = 35;

    // Base target for Level 2 is 8; from Level 3 and on we increase by +1 per level.
    private const int BaseTargetLevel2 = 8;

    [MenuItem("Tools/Adventure/Generate Levels 3-35")] 
    public static void GenerateAdventureLevels()
    {
        if (!File.Exists(TemplatePath))
        {
            Debug.LogError($"[AdventureLevelGenerator] Template file not found at: {TemplatePath}");
            return;
        }

        string templateJson = File.ReadAllText(TemplatePath);
        if (string.IsNullOrWhiteSpace(templateJson))
        {
            Debug.LogError("[AdventureLevelGenerator] Template JSON is empty.");
            return;
        }

        // We use LevelData class from the game runtime to deserialize.
        LevelData templateData = JsonUtility.FromJson<LevelData>(templateJson);
        if (templateData == null)
        {
            Debug.LogError("[AdventureLevelGenerator] Failed to parse template Level2.json into LevelData.");
            return;
        }

        // Safety: ensure we have LevelTargets list.
        if (templateData.LevelTargets == null || templateData.LevelTargets.Count == 0)
        {
            Debug.LogError("[AdventureLevelGenerator] Template Level2.json has no LevelTargets defined.");
            return;
        }

        for (int level = FirstLevelToGenerate; level <= LastLevelToGenerate; level++)
        {
            // Clone the template data by serializing and deserializing.
            string cloneJson = JsonUtility.ToJson(templateData);
            LevelData levelData = JsonUtility.FromJson<LevelData>(cloneJson);
            if (levelData == null)
            {
                Debug.LogError($"[AdventureLevelGenerator] Failed to clone LevelData for level {level}.");
                continue;
            }

            // Update basic info.
            levelData.Level = level;
            levelData.LevelName = $"Level {level}";

            // Increase targets linearly with level index.
            int extra = level - 2; // Level 2 is base; Level 3 -> +1, Level 4 -> +2, ...
            int targetValue = Mathf.Max(1, BaseTargetLevel2 + extra);

            for (int i = 0; i < levelData.LevelTargets.Count; i++)
            {
                var t = levelData.LevelTargets[i];
                t.Target = targetValue;
                levelData.LevelTargets[i] = t;
            }

            // Vary grid initial blocks and shape waves to add difficulty and variety.
            AdjustInitialBlocks(levelData, level, templateData);
            AdjustShapeWaves(levelData, level, templateData);

            string outJson = JsonUtility.ToJson(levelData, true);
            string outPath = Path.Combine(OutputFolder, $"Level{level}.json");

            File.WriteAllText(outPath, outJson);
            Debug.Log($"[AdventureLevelGenerator] Generated {outPath} with targets {targetValue} and custom layout for level {level}.");
        }

        AssetDatabase.Refresh();
        Debug.Log("[AdventureLevelGenerator] Finished generating Adventure levels 3-35.");
    }

    /// <summary>
    /// Create progressively denser and slightly shifted initial block layouts
    /// based on the level index. Uses the template's InitialBlocks as a base.
    /// </summary>
    private static void AdjustInitialBlocks(LevelData levelData, int level, LevelData template)
    {
        if (template.InitialBlocks == null || template.InitialBlocks.Count == 0)
            return;

        // Start from a fresh clone of the template blocks for every level.
        var blocks = new System.Collections.Generic.List<InitialBlockData>();
        foreach (var b in template.InitialBlocks)
        {
            blocks.Add(new InitialBlockData { x = b.x, y = b.y, Symbol = b.Symbol });
        }

        int rows = levelData.GridRows;
        int cols = levelData.GridColumns;

        // Difficulty bands: the higher the level, the more we duplicate and shift blocks.
        // Start making things denser already from early levels so the change is visible.
        // From level 4+ add an extra band of blocks one row below.
        if (level >= 4)
        {
            // Add a second band of blocks one row below where possible.
            var extra = new System.Collections.Generic.List<InitialBlockData>();
            foreach (var b in blocks)
            {
                int ny = b.y + 1;
                if (ny < rows)
                {
                    extra.Add(new InitialBlockData { x = b.x, y = ny, Symbol = b.Symbol });
                }
            }
            blocks.AddRange(extra);
        }

        // From level 7+ start adding more blocks shifted right to increase density further.
        if (level >= 7)
        {
            // Add more blocks shifted right, where possible, to increase density.
            var extra = new System.Collections.Generic.List<InitialBlockData>();
            foreach (var b in blocks)
            {
                int nx = b.x + 1;
                if (nx < cols)
                {
                    extra.Add(new InitialBlockData { x = nx, y = b.y, Symbol = b.Symbol });
                }
            }
            blocks.AddRange(extra);
        }

        // For some variety between neighbouring levels, slightly shift pattern every few levels.
        int shiftPattern = level % 3;
        if (shiftPattern == 1)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].x + 1 < cols)
                    blocks[i].x += 1;
            }
        }
        else if (shiftPattern == 2)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].y + 1 < rows)
                    blocks[i].y += 1;
            }
        }

        // The bands added above can land on the same (x,y) as an earlier band after the
        // shift-pattern step (e.g. a block shifted down by the level>=4 band overlapping
        // one shifted right by the level>=7 band). Keep only the last entry per
        // coordinate — GridBoard.ApplyInitialBlocks instantiates a block per list entry
        // and only tracks the last one written per cell, so earlier duplicates would
        // otherwise become orphaned GameObjects that are never cleaned up.
        var deduped = new System.Collections.Generic.Dictionary<(int, int), InitialBlockData>();
        foreach (var b in blocks)
            deduped[(b.x, b.y)] = b;

        levelData.InitialBlocks = new System.Collections.Generic.List<InitialBlockData>(deduped.Values);
    }

    /// <summary>
    /// Adjust shape waves per level so later levels have more waves and
    /// some variation in order, making the stage longer and more complex.
    /// </summary>
    private static void AdjustShapeWaves(LevelData levelData, int level, LevelData template)
    {
        if (template.ShapeWaves == null || template.ShapeWaves.Count == 0)
            return;

        var waves = new System.Collections.Generic.List<ShapeWave>();

        // Deep copy helper for a wave.
        System.Func<ShapeWave, ShapeWave> cloneWave = (src) =>
        {
            var w = new ShapeWave();
            w.Shapes = new System.Collections.Generic.List<ShapeLevelData>();
            if (src.Shapes != null)
            {
                foreach (var s in src.Shapes)
                {
                    var ns = new ShapeLevelData();
                    ns.Name = s.Name;
                    ns.Symbols = new System.Collections.Generic.List<SymbolData>();
                    if (s.Symbols != null)
                    {
                        foreach (var sym in s.Symbols)
                        {
                            ns.Symbols.Add(new SymbolData
                            {
                                Type = sym.Type,
                                Position = sym.Position
                            });
                        }
                    }
                    w.Shapes.Add(ns);
                }
            }
            return w;
        };

        // Base: always include all template waves once.
        foreach (var w in template.ShapeWaves)
            waves.Add(cloneWave(w));

        // From level 10+, duplicate the last wave group once (more shapes overall).
        if (level >= 10)
        {
            foreach (var w in template.ShapeWaves)
                waves.Add(cloneWave(w));
        }

        // From level 20+, duplicate all waves again (even longer stage).
        if (level >= 20)
        {
            foreach (var w in template.ShapeWaves)
                waves.Add(cloneWave(w));
        }

        // Slight variation in shape order within each wave depending on level.
        int rotate = level % 3; // 0,1,2
        if (rotate > 0)
        {
            foreach (var w in waves)
            {
                if (w.Shapes == null || w.Shapes.Count <= 1)
                    continue;

                int count = w.Shapes.Count;
                var rotated = new System.Collections.Generic.List<ShapeLevelData>(count);
                for (int i = 0; i < count; i++)
                {
                    int idx = (i + rotate) % count;
                    rotated.Add(w.Shapes[idx]);
                }
                w.Shapes = rotated;
            }
        }

        levelData.ShapeWaves = waves;
    }
}
