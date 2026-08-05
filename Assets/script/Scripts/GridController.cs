using UnityEngine;

public class GridController : MonoBehaviour
{
    [SerializeField] private GridBoard board;

   
    private void Awake()
    {
       
        GameManager.instance.OnDataLoaded += HandleOnDataLoadedEvent;

    
    }

    void OnDestroy()
    {
        if (GameManager.instance != null)
			GameManager.instance.OnDataLoaded -= HandleOnDataLoadedEvent;
    }

    protected void HandleOnDataLoadedEvent(LevelData levelData)
    {
        if (board == null)
            return;

        bool isAdventure = AppManager.instance != null
            && AppManager.instance.CurrentGameMode == AppManager.GameMode.Adventure;

        var snapshot = isAdventure ? AdventureSessionCache.GetSnapshotFor(levelData.Level) : null;

        if (snapshot != null)
        {
            // Resume the exact board the player left, instead of the level's authored
            // layout — including the case where they'd cleared the board completely.
            board.SetInitialBlocks(snapshot.gridBlocks, suppressRandomFallback: true);
        }
        else if (levelData.InitialBlocks != null && levelData.InitialBlocks.Count > 0)
        {
            // Pass initial blocks to the board before rebuilding (they'll be applied after BuildGrid)
            board.SetInitialBlocks(levelData.InitialBlocks);
        }

        board.ApplySize(levelData.GridColumns, levelData.GridRows);
    }
}
