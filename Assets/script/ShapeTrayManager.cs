using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ShapeTrayManager : MonoBehaviour
{
    [SerializeField] private GridBoard board;
    [SerializeField] private GridPlacer placer;
    [SerializeField] private ReviveManager reviveManager;

    [SerializeField] private Transform[] slots;
    [SerializeField] private Shape[] shapePrefabs;

    [Header("Addressables")]
    [SerializeField] private bool useAddressables;
    [SerializeField] private string shapesLabel;

    private readonly List<Shape> activeShapes = new List<Shape>(3);
    private bool noMovesReviveTriggered;

    private readonly List<GameObject> loadedPrefabs = new List<GameObject>();
    private AsyncOperationHandle<IList<GameObject>> loadHandle;
    private bool addressablesLoaded;

    private void Awake()
    {
        if (board == null)
            board = FindFirstObjectByType<GridBoard>();

        if (placer == null)
            placer = FindFirstObjectByType<GridPlacer>();

        if (reviveManager == null)
            reviveManager = FindFirstObjectByType<ReviveManager>();
    }

    private void OnEnable()
    {
        if (placer != null)
            placer.OnShapePlaced += HandleShapePlaced;
    }

    private void OnDisable()
    {
        if (placer != null)
            placer.OnShapePlaced -= HandleShapePlaced;
    }

    private void Start()
    {
        if (useAddressables)
        {
            LoadAddressablesAndRefill();
            return;
        }
        RefillIfNeeded();
    }

    private void LoadAddressablesAndRefill()
    {
        if (addressablesLoaded)
        {
            RefillIfNeeded();
            return;
        }

        if (string.IsNullOrEmpty(shapesLabel))
        {
            Debug.LogError("[ShapeTrayManager] useAddressables is enabled but shapesLabel is empty.");
            RefillIfNeeded();
            return;
        }

        loadHandle = Addressables.LoadAssetsAsync<GameObject>(shapesLabel, null);
        loadHandle.Completed += handle =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                loadedPrefabs.Clear();
                foreach (var go in handle.Result)
                {
                    if (go != null)
                        loadedPrefabs.Add(go);
                }

                addressablesLoaded = true;
            }
            else
            {
                Debug.LogError($"[ShapeTrayManager] Failed to load addressables by label '{shapesLabel}'. Falling back to inspector prefabs.");
                addressablesLoaded = false;
            }

            RefillIfNeeded();
        };
    }

    private void OnDestroy()
    {
        if (useAddressables && loadHandle.IsValid())
            Addressables.Release(loadHandle);
    }

    private void HandleShapePlaced(Shape placed)
    {
        if (placed != null)
            activeShapes.Remove(placed);

        if (activeShapes.Count == 0)
        {
            noMovesReviveTriggered = false;
            RefillIfNeeded();
            return;
        }

        CheckNoMovesAndMaybeRevive();
    }

    private void RefillIfNeeded()
    {
        if (activeShapes.Count > 0)
            return;

        if (slots == null || slots.Length < 3)
            return;

        if ((shapePrefabs == null || shapePrefabs.Length == 0)
            && (!useAddressables || !addressablesLoaded || loadedPrefabs.Count == 0))
            return;

        for (int i = 0; i < 3; i++)
        {
            var slot = slots[i];
            if (slot == null)
                continue;

            Shape shape = null;

            if (useAddressables && addressablesLoaded && loadedPrefabs.Count > 0)
            {
                var goPrefab = loadedPrefabs[Random.Range(0, loadedPrefabs.Count)];
                var go = Instantiate(goPrefab, slot.position, slot.rotation, slot);
                shape = go != null ? go.GetComponent<Shape>() : null;
            }

            if (shape == null)
            {
                if (shapePrefabs == null || shapePrefabs.Length == 0)
                    continue;

                var prefab = shapePrefabs[Random.Range(0, shapePrefabs.Length)];
                if (prefab == null)
                    continue;

                shape = Instantiate(prefab, slot.position, slot.rotation, slot);
            }

            if (shape == null)
                continue;

            activeShapes.Add(shape);

            var handler = shape.GetComponent<ShapeDragHandler>();
            if (handler != null)
                handler.Init(board, placer, shape);
        }

        CheckNoMovesAndMaybeRevive();
    }

    private void CheckNoMovesAndMaybeRevive()
    {
        if (noMovesReviveTriggered)
            return;

        if (board == null || placer == null)
            return;

        if (HasAnyMove())
            return;

        if (reviveManager != null && reviveManager.CanRevive)
        {
            noMovesReviveTriggered = true;
            reviveManager.RequestRevive();
        }
    }

    private bool HasAnyMove()
    {
        if (activeShapes.Count == 0)
            return true;

        for (int i = 0; i < activeShapes.Count; i++)
        {
            var s = activeShapes[i];
            if (s == null)
                continue;

            if (HasAnyMoveForShape(s))
                return true;
        }

        return false;
    }

    private bool HasAnyMoveForShape(Shape s)
    {
        for (int x = 0; x < board.width; x++)
        {
            for (int y = 0; y < board.height; y++)
            {
                if (placer.CanPlaceShape(s, new Vector2Int(x, y)))
                    return true;
            }
        }

        return false;
    }
}
