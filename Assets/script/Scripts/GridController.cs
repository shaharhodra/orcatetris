using UnityEngine;

public class GridController : MonoBehaviour
{
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

    }
}
