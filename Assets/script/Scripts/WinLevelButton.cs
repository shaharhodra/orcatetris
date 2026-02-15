using UnityEngine;

public class WinLevelButton : MonoBehaviour
{
    void Start()
{
    Debug.Log("Persist path: " + Application.persistentDataPath);
}
    [SerializeField] private int levelIndex = 1;

    public void OnWinButtonClicked()
    {
           Debug.Log("WinLevelButton clicked, levelIndex = " + levelIndex);
        GameManager.instance.SetLevelCompleted(levelIndex);
       
    }
}