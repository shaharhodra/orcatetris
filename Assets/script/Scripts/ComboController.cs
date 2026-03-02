using UnityEngine;

public class ComboController : MonoBehaviour
{
    [SerializeField] private GridPlacer gridPlacer;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    public ComboManager ComboManager { get; private set; }

    private void Awake()
    {
        ComboManager = new ComboManager();

        if (gridPlacer == null)
            gridPlacer = FindFirstObjectByType<GridPlacer>();
    }

    private void OnEnable()
    {
        if (gridPlacer == null)
            return;

        gridPlacer.OnLinesCleared += HandleLinesCleared;
        gridPlacer.OnNoLinesCleared += HandleNoLinesCleared;

        if (debugLogs)
            Debug.Log("[ComboController] Subscribed to GridPlacer events");
    }

    private void OnDisable()
    {
        if (gridPlacer == null)
            return;

        gridPlacer.OnLinesCleared -= HandleLinesCleared;
        gridPlacer.OnNoLinesCleared -= HandleNoLinesCleared;

        if (debugLogs)
            Debug.Log("[ComboController] Unsubscribed from GridPlacer events");
    }

    private void HandleLinesCleared(LineClearResult result)
    {
        int lines = result.LinesCleared;
        if (lines > 0)
        {
            if (debugLogs)
                Debug.Log($"[ComboController] HandleLinesCleared: lines={lines} (rows={result.RowsCleared}, cols={result.ColumnsCleared})");
            ComboManager.RegisterClear(lines);
        }
    }

    private void HandleNoLinesCleared()
    {
        if (debugLogs)
            Debug.Log("[ComboController] HandleNoLinesCleared -> BreakCombo");
        ComboManager.BreakCombo();
    }
}
