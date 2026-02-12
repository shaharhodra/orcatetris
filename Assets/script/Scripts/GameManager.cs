using UnityEngine;
using System;

// every manager will derive from the Singleton class - this makes sure there is only one single manager of this type in the whole app.
public class GameManager : Singleton<GameManager>
{
    // this is how to define an event - how the manager communicates with the rest of the app components
    public event Action<LevelData> OnDataLoaded;

    [SerializeField] private LevelData _levelData;
    [SerializeField] private string _levelJson;

    public void InitData()
    {
        // take LevelData and convert it to json string
        var json = JsonUtility.ToJson(_levelData);
        Debug.Log("This is the level data : " + json);

        // take json string and convert it into a LevelData object
        var levelData = JsonUtility.FromJson<LevelData>(_levelJson);
        Debug.Log("Rows = " + levelData.GridRows);
        Debug.Log("Columns = " + levelData.GridColumns);

        // this is how to trigger the event to all the listeners
        InvokeOnDataLoaded(levelData);
    }

    public void InvokeOnDataLoaded(LevelData levelData)
    {
        OnDataLoaded?.Invoke(levelData);
    }
}