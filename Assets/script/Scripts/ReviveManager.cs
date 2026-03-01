using UnityEngine;
using UnityEngine.SceneManagement;

public class ReviveManager : MonoBehaviour
{
    [SerializeField] private GridBoard board;
    [SerializeField] private int maxRevives = 3;
    [SerializeField] private GameObject revivePopup;

    [SerializeField] private ShapeTrayManager shapeTrayManager;

    private int usedRevives;
    private bool popupOpen;

    public int RemainingRevives => Mathf.Max(0, maxRevives - usedRevives);

    public bool CanRevive => RemainingRevives > 0;

    public bool IsPopupOpen => popupOpen;

    private void Awake()
    {
        if (board == null)
            board = FindFirstObjectByType<GridBoard>();

        if (shapeTrayManager == null)
            shapeTrayManager = FindFirstObjectByType<ShapeTrayManager>();
    }

    public void RequestRevive()
    {
        if (popupOpen)
            return;

        // Safety check: if there is at least one valid move available now, do not request revive.
        if (shapeTrayManager != null && shapeTrayManager.HasAnyMoveAvailable())
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
            return;

        // אם זה ה-revive השלישי (או האחרון שנשאר), לא מבצעים עוד ניקוי אלא מאתחלים את השלב.
        // לדוגמה: maxRevives=3 -> אחרי 2 Revives usedRevives=2, RemainingRevives=1 -> כאן נעשה RestartLevel.
        if (RemainingRevives <= 1)
        {
            RestartLevel();
            return;
        }

        // ננסה לבצע revive חכם: ננקה שורה+עמודה כמה פעמים ברצף (עד שנגמרים ה-revives)
        // עד שנמצא מהלך אחד לפחות, או שלא נשארו עוד revives (מלבד ה-revive האחרון שמוביל לריסטארט).
        do
        {
            usedRevives++;
            board.ReviveClearOneRowAndOneColumn();

            // אם אין ShapeTrayManager, לא נוכל לבדוק – מפסיקים אחרי revive אחד.
            if (shapeTrayManager == null)
                break;

        } while (!shapeTrayManager.HasAnyMoveAvailable() && CanRevive);

        // אם גם אחרי כל ה-revives אין אף מהלך זמין, נאתחל את השלב (Game Over / התחלה מחדש)
        if (shapeTrayManager != null && !shapeTrayManager.HasAnyMoveAvailable())
        {
            RestartLevel();
        }
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
