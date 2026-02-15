using UnityEngine;
using UnityEngine.UI;

public class LobbyLevelButton : MonoBehaviour
{
    [SerializeField] private int levelIndex = 0;
    [SerializeField] private Button button;
    [SerializeField] private GameObject completedIcon;

    public int LevelIndex => levelIndex;
    public Button Button => button;

    private void Reset()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    public void SetState(bool isUnlocked, bool isCompleted)
    {
        if (button != null)
            button.interactable = isUnlocked;

        if (completedIcon != null)
            completedIcon.SetActive(isCompleted);
    }
}
