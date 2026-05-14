using UnityEngine;

/// <summary>
/// Adventure game mode implementation
/// Features: Predefined waves, no revive system, no combo system
/// </summary>
public class AdventureGameMode : IGameMode
{
    private ShapeTrayManager shapeTrayManager;
    private GridBoard board;
    private ReviveManager reviveManager;
    private ComboController comboController;
    private GridPlacer gridPlacer;
    
    public void Initialize()
    {
        Debug.Log("[AdventureGameMode] Initializing Adventure mode...");
        
        // Get required components
        shapeTrayManager = Object.FindFirstObjectByType<ShapeTrayManager>();
        board = Object.FindFirstObjectByType<GridBoard>();
        reviveManager = Object.FindFirstObjectByType<ReviveManager>();
        comboController = Object.FindFirstObjectByType<ComboController>();
        gridPlacer = Object.FindFirstObjectByType<GridPlacer>();
        
        // Subscribe to events
        if (gridPlacer != null)
        {
            gridPlacer.OnShapePlaced += HandleShapePlaced;
        }
        
        // Disable combo system for Adventure mode
        if (comboController != null)
        {
            comboController.enabled = false;
        }
        
        Debug.Log("[AdventureGameMode] Adventure mode initialized");
    }
    
    public void HandleShapePlaced(Shape shape)
    {
        // Adventure mode handles shape placement through ShapeTrayManager waves
        Debug.Log("[AdventureGameMode] Shape placed, checking for game over...");
        CheckGameOver();
    }
    
    public void CheckGameOver()
    {
        // Check if we have any available moves
        if (!HasAnyMoveAvailable())
        {
            Debug.Log("[AdventureGameMode] No moves available, triggering game over");
            TriggerGameOver();
        }
    }
    
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
    
    public bool CanSpawnNewShapes()
    {
        return false; // Adventure mode uses predefined waves, not random spawning
    }
    
    public void TriggerGameOver()
    {
        // Direct game over for Adventure mode (no revive)
        if (reviveManager != null)
        {
            reviveManager.RequestRevive(); // This will trigger game over directly for Adventure
        }
    }
    
    public void Reset()
    {
        if (shapeTrayManager != null)
        {
            shapeTrayManager.Restart();
        }
        
        Debug.Log("[AdventureGameMode] Adventure mode reset");
    }
    
    public GameManager.GameMode GetGameModeType()
    {
        return GameManager.GameMode.Adventure;
    }
    
    public void Cleanup()
    {
        // Unsubscribe from events
        if (gridPlacer != null)
        {
            gridPlacer.OnShapePlaced -= HandleShapePlaced;
        }
        
        Debug.Log("[AdventureGameMode] Adventure mode cleaned up");
    }
}
