using UnityEngine;

public class GameOverChecker : MonoBehaviour
{
    [SerializeField] private ShapeTrayManager shapeTrayManager;
    [SerializeField] private GridBoard board;
    [SerializeField] private ReviveManager reviveManager;

    private void Start()
    {
        Debug.Log("[GameOverChecker] DISABLED - PlaceManager already handles game over detection");
        
        // Disabled because PlaceManager.EvaluateMovesAndMaybeRequestRevive already handles this
        // This was causing duplicate calls and infinite loops
        return;
        
        // Get components if not assigned
        if (shapeTrayManager == null)
            shapeTrayManager = FindFirstObjectByType<ShapeTrayManager>();
        
        if (board == null)
            board = FindFirstObjectByType<GridBoard>();
        
        if (reviveManager == null)
            reviveManager = FindFirstObjectByType<ReviveManager>();

        Debug.Log($"[GameOverChecker] Components found - ShapeTrayManager: {shapeTrayManager != null}, Board: {board != null}, ReviveManager: {reviveManager != null}");

        // Subscribe to shape placement events to check after each move
        var gridPlacer = FindFirstObjectByType<GridPlacer>();
        if (gridPlacer != null)
        {
            gridPlacer.OnShapePlaced += CheckForGameOver;
            Debug.Log("[GameOverChecker] Successfully subscribed to GridPlacer.OnShapePlaced");
        }
        else
        {
            Debug.LogError("[GameOverChecker] Could not find GridPlacer!");
        }
    }

    private void OnDestroy()
    {
        var gridPlacer = FindFirstObjectByType<GridPlacer>();
        if (gridPlacer != null)
        {
            gridPlacer.OnShapePlaced -= CheckForGameOver;
        }
    }

    /// <summary>
    /// Checks if game should end (no available moves)
    /// Called after each shape placement
    /// </summary>
    public void CheckForGameOver(Shape placedShape)
    {
        // Wait a frame for any board updates to complete
        StartCoroutine(CheckGameOverNextFrame());
    }

    private System.Collections.IEnumerator CheckGameOverNextFrame()
    {
        yield return null;

        Debug.Log("[GameOverChecker] Checking for game over...");

        if (shapeTrayManager == null || board == null)
        {
            Debug.LogError("[GameOverChecker] Missing required components");
            yield break;
        }

        // Check if any shape can be placed
        bool hasAnyMove = HasAnyMoveAvailable();
        Debug.Log($"[GameOverChecker] Has any move available: {hasAnyMove}");

        if (!hasAnyMove)
        {
            Debug.Log("[GameOverChecker] No moves available, triggering game over");
            
            // In Adventure mode, trigger game over directly
            if (GameManager.instance != null && GameManager.instance.CurrentGameMode != GameManager.GameMode.Classic)
            {
                Debug.Log("[GameOverChecker] Adventure mode detected, triggering game over");
                if (reviveManager != null)
                {
                    Debug.Log("[GameOverChecker] Calling reviveManager.RequestRevive()");
                    reviveManager.RequestRevive(); // This will trigger game over for Adventure mode
                }
                else
                {
                    Debug.LogWarning("[GameOverChecker] No revive manager found, using fallback");
                    // Fallback if no revive manager
                    GameManager.instance.InvokeOnLevelRestartedEvent();
                }
            }
            else
            {
                Debug.Log("[GameOverChecker] Classic mode detected, letting revive manager handle");
                // In Classic mode, let revive manager handle it
                if (reviveManager != null)
                {
                    reviveManager.RequestRevive();
                }
            }
        }
        else
        {
            Debug.Log("[GameOverChecker] Game can continue - moves are available");
        }
    }

    /// <summary>
    /// Checks if any available shape can be placed on the board
    /// </summary>
    private bool HasAnyMoveAvailable()
    {
        if (shapeTrayManager == null || board == null)
            return false;

        var availableShapes = shapeTrayManager.GetAvailableShapes();
        
        foreach (var shape in availableShapes)
        {
            if (shape == null) continue;

            if (CanPlaceShapeAnywhere(shape))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a specific shape can be placed anywhere on the board
    /// </summary>
    private bool CanPlaceShapeAnywhere(Shape shape)
    {
        if (board == null || shape == null)
            return false;

        for (int row = 0; row < board.Rows; row++)
        {
            for (int col = 0; col < board.Columns; col++)
            {
                Vector2Int targetCell = new Vector2Int(col, row);
                if (board.CanPlaceShape(shape, targetCell))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Public method to manually check game over state
    /// </summary>
    public bool IsGameOver()
    {
        return !HasAnyMoveAvailable();
    }
}
