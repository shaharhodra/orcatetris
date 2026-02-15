using UnityEngine;
using UnityEngine.SceneManagement;

public class ReviveManager : MonoBehaviour
{
    [SerializeField] private GridBoard board;
    [SerializeField] private int maxRevives = 3;
    [SerializeField] private GameObject revivePopup;

    private int usedRevives;
    private bool popupOpen;

    public int RemainingRevives => Mathf.Max(0, maxRevives - usedRevives);

    public bool CanRevive => RemainingRevives > 0;

    public bool IsPopupOpen => popupOpen;

    public void RequestRevive()
    {
        if (popupOpen)
            return;

        if (!CanRevive)
            return;

        if (revivePopup == null)
        {
            WatchAdAndRevive();
            return;
        }

        popupOpen = true;
        revivePopup.SetActive(true);
    }

    public void ConfirmRevive()
    {
        if (!popupOpen)
            return;

        ClosePopup();
        WatchAdAndRevive();
    }

    public void DeclineRevive()
    {
        if (!popupOpen)
            return;

        ClosePopup();
    }

    public void ClosePopup()
    {
        popupOpen = false;
        if (revivePopup != null)
            revivePopup.SetActive(false);
    }

    public void WatchAdAndRevive()
    {
        if (!CanRevive)
            return;

        if (board == null)
            board = FindFirstObjectByType<GridBoard>();

        if (board == null)
            return;

        usedRevives++;
        board.ReviveClearOneRowAndOneColumn();
    }

    public void RestartLevel()
    {
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    public void ResetRevives()
    {
        usedRevives = 0;
    }
}
