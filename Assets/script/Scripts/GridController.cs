using UnityEngine;

public class GridController : MonoBehaviour
{
    [SerializeField] private GridBoard board;

    void Start()
    {
        GameManager.instance.OnDataLoaded += HandleOnDataLoadedEvent;
    }

    void OnDestroy()
    {
        GameManager.instance.OnDataLoaded -= HandleOnDataLoadedEvent;
    }

    protected void HandleOnDataLoadedEvent(LevelData levelData)
    {
        if (board == null)
            return;

        board.ApplySize(levelData.GridColumns, levelData.GridRows);
    }
}
