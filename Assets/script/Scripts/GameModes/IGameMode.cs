using UnityEngine;

/// <summary>
/// Interface for different game mode implementations
/// Each game mode handles its own unique gameplay logic
/// </summary>
public interface IGameMode
{
    /// <summary>
    /// Initialize the game mode with necessary components
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Called when a shape is placed on the board
    /// </summary>
    void HandleShapePlaced(Shape shape);
    
    /// <summary>
    /// Check if game should end (no available moves)
    /// </summary>
    void CheckGameOver();
    
    /// <summary>
    /// Can this game mode spawn new shapes?
    /// </summary>
    bool CanSpawnNewShapes();
    
    /// <summary>
    /// Handle game over logic
    /// </summary>
    void TriggerGameOver();
    
    /// <summary>
    /// Reset the game mode for new game
    /// </summary>
    void Reset();
    
    /// <summary>
    /// Get the game mode type
    /// </summary>
    GameManager.GameMode GetGameModeType();
    
    /// <summary>
    /// Cleanup when game mode is destroyed
    /// </summary>
    void Cleanup();
}
