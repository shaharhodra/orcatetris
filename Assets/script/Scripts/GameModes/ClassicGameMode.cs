using UnityEngine;

/// <summary>
/// Classic game mode implementation
/// Features: Random shapes, revive system, combo system
/// </summary>
public class ClassicGameMode : IGameMode
{
    private ShapeTrayManager shapeTrayManager;
    private GridBoard board;
    private ReviveManager reviveManager;
    private ComboController comboController;
    private GridPlacer gridPlacer;
    
    public void Initialize()
    {
        Debug.Log("[ClassicGameMode] Initializing Classic mode...");
        
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
        
        // Enable combo system
        if (comboController != null)
        {
            comboController.enabled = true;
        }
        
        Debug.Log("[ClassicGameMode] Classic mode initialized");
    }
    
    public void HandleShapePlaced(Shape shape)
    {
        // Classic mode uses existing logic through PlaceManager and ReviveManager
        // The combo system will automatically handle combo counting
        Debug.Log("[ClassicGameMode] Shape placed, checking for game over...");
        CheckGameOver();
    }
    
    public void CheckGameOver()
    {
        // Let PlaceManager handle the game over check
        // It will call ReviveManager if needed
        var placeManager = Object.FindFirstObjectByType<PlaceManager>();
        if (placeManager != null)
        {
            placeManager.EvaluateMovesAndMaybeRequestRevive();
        }
    }
    
    public bool CanSpawnNewShapes()
    {
        return true; // Classic mode always allows spawning new random shapes
    }
    
    public void TriggerGameOver()
    {
        if (reviveManager != null)
        {
            reviveManager.RequestRevive();
        }
    }
    
    public void Reset()
    {
        if (reviveManager != null)
        {
            reviveManager.ResetRevives();
        }
        
        if (shapeTrayManager != null)
        {
            shapeTrayManager.Restart();
        }
        
        Debug.Log("[ClassicGameMode] Classic mode reset");
    }
    
    public GameManager.GameMode GetGameModeType()
    {
        return GameManager.GameMode.Classic;
    }
    
    public void Cleanup()
    {
        // Unsubscribe from events
        if (gridPlacer != null)
        {
            gridPlacer.OnShapePlaced -= HandleShapePlaced;
        }
        
        Debug.Log("[ClassicGameMode] Classic mode cleaned up");
    }
}
