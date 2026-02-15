using UnityEngine;

public class GridController : MonoBehaviour
{
    [SerializeField] private GridBoard board;

    private GameManager gameManager;

    private void Awake()
    {
        gameManager = GameManager.instance;
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        if (gameManager == null)
            return;

        gameManager.OnDataLoaded += HandleOnDataLoadedEvent;

        if (gameManager.CurrentLevelData != null)
            HandleOnDataLoadedEvent(gameManager.CurrentLevelData);
    }

    void OnDestroy()
    {
        if (gameManager != null)
            gameManager.OnDataLoaded -= HandleOnDataLoadedEvent;
    }

    protected void HandleOnDataLoadedEvent(LevelData levelData)
    {
        if (board == null)
            return;

        board.ApplySize(levelData.GridColumns, levelData.GridRows);
    }
}
