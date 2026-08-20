using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OrcaTetris.EditorTools
{
    /// <summary>
    /// One-shot authoring tool that adds awkward 5-cell pentominoes to the shape pool.
    ///
    /// Adventure's tray picker can only be as demanding as the pool lets it be, and the pool it
    /// inherited was not built for that: of 39 prefabs, 13 are 1-3 cells (four of them diagonally
    /// disconnected pieces that drop almost anywhere), 19 are tetrominoes, and of the seven 5+ cell
    /// shapes only three are genuinely irregular — the rest are rectangles and straight lines,
    /// which are easy to place precisely because they tile. Asking the picker to "offer something
    /// that needs thought" had almost nothing to reach for. These eight fill that gap: every one is
    /// irregular inside a 3x3 bounding box, so where it goes is a real decision.
    ///
    /// Prefabs are built by copying <see cref="TemplatePath"/> — cross.prefab, which is already a
    /// root plus exactly five identical child blocks — and rewriting the cell data and the child
    /// positions. Deliberately done through AssetDatabase/PrefabUtility rather than by writing the
    /// YAML: a shape prefab is ~1000 lines of interlinked fileIDs and needs a fresh .meta GUID, and
    /// the API gets both right for free.
    ///
    /// Re-running is safe — existing prefabs are rebuilt in place, and the scene wiring step skips
    /// shapes already present.
    /// </summary>
    public static class PentominoShapeGenerator
    {
        private const string TemplatePath = "Assets/shapes/cross.prefab";
        private const string OutputFolder = "Assets/shapes";

        /// <summary>
        /// Local units per grid cell, read off the template's own child offsets (cross's cells are
        /// at ±1 and its blocks sit at ±0.5).
        /// </summary>
        private const float CellSpacing = 0.5f;

        /// <summary>
        /// Eight irregular pentominoes, each inside a 3x3 box. Rotations are separate entries
        /// because the pool already models rotation that way (every tetromino ships as four
        /// prefabs) and because the picker treats each entry as its own candidate — a U opening
        /// upward and one opening downward genuinely fit different holes.
        /// </summary>
        private static readonly Dictionary<string, Vector2Int[]> Pentominoes = new Dictionary<string, Vector2Int[]>
        {
            ["pentT"] = new[] { V(-1, 1), V(0, 1), V(1, 1), V(0, 0), V(0, -1) },
            ["pentT180"] = new[] { V(-1, -1), V(0, -1), V(1, -1), V(0, 0), V(0, 1) },
            ["pentU"] = new[] { V(-1, 1), V(1, 1), V(-1, 0), V(0, 0), V(1, 0) },
            ["pentU180"] = new[] { V(-1, -1), V(1, -1), V(-1, 0), V(0, 0), V(1, 0) },
            ["pentW"] = new[] { V(-1, 1), V(-1, 0), V(0, 0), V(0, -1), V(1, -1) },
            ["pentW180"] = new[] { V(1, -1), V(1, 0), V(0, 0), V(0, 1), V(-1, 1) },
            ["pentZ"] = new[] { V(-1, 1), V(0, 1), V(0, 0), V(0, -1), V(1, -1) },
            ["pentF"] = new[] { V(0, 1), V(1, 1), V(-1, 0), V(0, 0), V(0, -1) },
        };

        private static Vector2Int V(int x, int y) => new Vector2Int(x, y);

        [MenuItem("Tools/OrcaTetris/Generate Pentomino Shapes")]
        public static void Generate()
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
            if (template == null)
            {
                Debug.LogError($"[PentominoShapeGenerator] Template not found at {TemplatePath}.");
                return;
            }

            int templateCells = template.GetComponent<Shape>()?.shapeData?.cells?.Length ?? 0;
            if (templateCells != 5)
            {
                Debug.LogError($"[PentominoShapeGenerator] {TemplatePath} has {templateCells} cells, expected 5. " +
                    "The template must have exactly one child block per pentomino cell.");
                return;
            }

            int built = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var kvp in Pentominoes)
                {
                    if (BuildPrefab(kvp.Key, kvp.Value))
                        built++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[PentominoShapeGenerator] Built {built} pentomino prefabs in {OutputFolder}. " +
                "Run 'Add Pentominoes To Scene Pool' with gameScene open to wire them into the tray.");
        }

        private static bool BuildPrefab(string shapeName, Vector2Int[] cells)
        {
            string path = Path.Combine(OutputFolder, shapeName + ".prefab").Replace('\\', '/');

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                AssetDatabase.DeleteAsset(path);

            if (!AssetDatabase.CopyAsset(TemplatePath, path))
            {
                Debug.LogError($"[PentominoShapeGenerator] Could not copy template to {path}.");
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = shapeName;

                var shape = root.GetComponent<Shape>();
                if (shape == null)
                {
                    Debug.LogError($"[PentominoShapeGenerator] {path} has no Shape component.");
                    return false;
                }

                shape.shapeData = new ShapeOffset { cells = cells.ToArray() };

                // Blocks are visual only — Shape.GetCells reads shapeData — but they must describe
                // the same footprint or the piece won't look like where it lands. Any child-to-cell
                // pairing works since the blocks are identical; index order is simplest.
                var blocks = new List<Transform>();
                foreach (Transform child in root.transform)
                    blocks.Add(child);

                if (blocks.Count != cells.Length)
                {
                    Debug.LogError($"[PentominoShapeGenerator] {shapeName}: template has {blocks.Count} blocks " +
                        $"but the shape needs {cells.Length}.");
                    return false;
                }

                for (int i = 0; i < cells.Length; i++)
                {
                    blocks[i].localPosition = new Vector3(
                        cells[i].x * CellSpacing,
                        cells[i].y * CellSpacing,
                        0f);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // Deliberately not re-loading the asset to confirm: StartAssetEditing defers the
            // import, so LoadAssetAtPath returns null here even though the file is written
            // correctly. The write above either threw or succeeded.
            return true;
        }

        /// <summary>
        /// Appends the generated pentominoes to the ShapeTrayManager's classicShapePrefabs array in
        /// the currently open scene. Separate from generation because it edits the scene, which the
        /// caller has to have open and will have to save.
        /// </summary>
        [MenuItem("Tools/OrcaTetris/Add Pentominoes To Scene Pool")]
        public static void AddToScenePool()
        {
            var tray = Object.FindFirstObjectByType<ShapeTrayManager>();
            if (tray == null)
            {
                Debug.LogError("[PentominoShapeGenerator] No ShapeTrayManager in the open scene — open gameScene first.");
                return;
            }

            var serialized = new SerializedObject(tray);
            var poolProperty = serialized.FindProperty("classicShapePrefabs");
            if (poolProperty == null || !poolProperty.isArray)
            {
                Debug.LogError("[PentominoShapeGenerator] classicShapePrefabs not found — was the field renamed?");
                return;
            }

            var existing = new HashSet<Object>();
            for (int i = 0; i < poolProperty.arraySize; i++)
            {
                var value = poolProperty.GetArrayElementAtIndex(i).objectReferenceValue;
                if (value != null)
                    existing.Add(value);
            }

            int added = 0;
            foreach (string shapeName in Pentominoes.Keys)
            {
                string path = Path.Combine(OutputFolder, shapeName + ".prefab").Replace('\\', '/');
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[PentominoShapeGenerator] {path} missing — run Generate first.");
                    continue;
                }

                var shape = prefab.GetComponent<Shape>();
                if (shape == null || existing.Contains(shape))
                    continue;

                poolProperty.arraySize++;
                poolProperty.GetArrayElementAtIndex(poolProperty.arraySize - 1).objectReferenceValue = shape;
                added++;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(tray);

            Debug.Log($"[PentominoShapeGenerator] Added {added} pentominoes to the tray pool " +
                $"({poolProperty.arraySize} entries total). Save the scene to keep this.");
        }

        /// <summary>
        /// Batch entry point: generate the prefabs, open gameScene, wire them into the pool and
        /// save. Intended for `Unity.exe -batchmode -executeMethod`, so both halves happen in one
        /// pass without anyone having to remember the scene step.
        /// </summary>
        public static void GenerateAndWireForBatch()
        {
            const string ScenePath = "Assets/Scenes/gameScene.unity";

            Generate();

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            AddToScenePool();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[PentominoShapeGenerator] Batch generate + wire complete.");
        }
    }
}
