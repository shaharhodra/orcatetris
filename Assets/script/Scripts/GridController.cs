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

        board.ApplySize(levelData.GridColumns, levelData.GridRows);
    }
}
