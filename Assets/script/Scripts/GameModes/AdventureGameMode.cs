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
        if (!HasAnyMoveAvailable())
        {
            Debug.Log("[AdventureGameMode] No moves available, triggering game over");
            TriggerGameOver();
        }
    }
    
    /// <summary>
    /// Whether the shapes currently sitting in the tray can still go anywhere on the board. A
    /// level is meant to be beatable by placing shapes in the right spot, but Adventure has no
    /// revive and no other fail condition — if the player's own placements leave the tray with
    /// nowhere to go, that's a loss, not something the game quietly papers over.
    /// </summary>
    private bool HasAnyMoveAvailable()
    {
        if (shapeTrayManager == null || board == null)
            return false;

        return shapeTrayManager.HasAnyMoveAvailable();
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
