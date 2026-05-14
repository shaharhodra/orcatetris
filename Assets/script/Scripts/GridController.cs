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

        // Pass initial blocks to the board before rebuilding (they'll be applied after BuildGrid)
        if (levelData.InitialBlocks != null && levelData.InitialBlocks.Count > 0)
        {
            board.SetInitialBlocks(levelData.InitialBlocks);
        }

        board.ApplySize(levelData.GridColumns, levelData.GridRows);
    }
}
