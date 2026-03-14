using UnityEngine;
using System;
using Random = UnityEngine.Random;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ShapeTrayManager : MonoBehaviour
{
    public event Action OnNoMovesDetected; // notify higher-level managers (PlaceManager) when no moves detected

    [SerializeField] private GridBoard board;
    [SerializeField] private GridPlacer placer;
    [SerializeField] private ReviveManager reviveManager;

    [SerializeField] private Transform[] slots;
    [SerializeField] private Shape[] shapePrefabs;

    [Header("Addressables")]
    [SerializeField] private bool useAddressables;
    [SerializeField] private string shapesLabel;

    [Header("Revive Timing")]
    [SerializeField] private float noMovesReviveDelay = 0.7f;

    [Header("Move Threshold")]
    [SerializeField] private int minPlaceableToConsiderMovable = 1; // set to 2 or 3 to be stricter

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
            StartCoroutine(NoMovesReviveRoutine());
        }
    }

    private IEnumerator NoMovesReviveRoutine()
    {
        yield return new WaitForSeconds(noMovesReviveDelay);

        if (board == null || placer == null)
        {
            noMovesReviveTriggered = false;
            yield break;
        }

        if (HasAnyMove())
        {
            noMovesReviveTriggered = false;
            yield break;
        }

        // Notify higher-level manager instead of calling ReviveManager directly.
        OnNoMovesDetected?.Invoke();
        // leave noMovesReviveTriggered as true until higher-level flow resolves it
    }

    private bool HasAnyMove()
    {
        // If core refs are missing, be conservative and treat as "moves available"
        if (board == null || placer == null)
        {
            Debug.LogWarning("[HasAnyMove] board or placer is null -> returning true to avoid premature revive");
            return true;
        }

        if (activeShapes.Count == 0)
        {
            Debug.Log("[HasAnyMove] activeShapes is empty -> returning false (treat as NO moves)");
            return false;
        }

        int placeableCount = 0;
        int needed = Mathf.Max(1, minPlaceableToConsiderMovable);

        for (int i = 0; i < activeShapes.Count; i++)
        {
            var s = activeShapes[i];
            if (s == null)
                continue;

            if (HasAnyMoveForShape(s))
            {
                placeableCount++;
                // short-circuit when threshold reached
                if (placeableCount >= needed)
                {
                    Debug.Log($"[HasAnyMove] reached threshold {needed} at shape {i}, short-circuiting");
                    return true;
                }
            }
        }

        bool anyMove = placeableCount >= needed;
        Debug.Log($"[HasAnyMove] placeableCount={placeableCount}, threshold={needed}, anyMove={anyMove}");
        return anyMove;
    }

    // Public wrapper so other systems (e.g. ReviveManager) can safely query if there are any valid moves.
    public bool HasAnyMoveAvailable()
    {
        return HasAnyMove();
    }

    private bool HasAnyMoveForShape(Shape s)
    {
        bool found = false;

        for (int x = 0; x < board.width; x++)
        {
            for (int y = 0; y < board.height; y++)
            {
                var cell = new Vector2Int(x, y);
                if (placer.CanPlaceShape(s, cell))
                {
                    Debug.Log($"[HasAnyMoveForShape] shape '{s.name}' CAN be placed at {cell}");
                    found = true;
                    // לא שוברים את הלולאה, כדי לראות כל התאים החוקיים ללוג
                }
            }
        }

        if (!found)
        {
            Debug.Log($"[HasAnyMoveForShape] shape '{s.name}' has NO valid placement");
        }

        return found;
    }
}
