using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class ReviveManager : MonoBehaviour
{
    [SerializeField] private GridBoard board;
    [SerializeField] private int maxRevives = 3;
    [SerializeField] private GameObject revivePopup;
    [SerializeField] private GameObject gameOverPopup;

    [SerializeField] private ShapeTrayManager shapeTrayManager;
    [SerializeField] private PlaceManager placeManager;

    [Header("Revive countdown")]
    [SerializeField] private float reviveCountdownDuration = 5f;
    [SerializeField] private UnityEngine.UI.Text reviveCountdownText;
    [Header("Game over")]
    [SerializeField] private float gameOverPopupDelay = 0.5f;

    private int usedRevives;
    private bool popupOpen;
    private Coroutine reviveCountdownCoroutine;

    public int RemainingRevives => Mathf.Max(0, maxRevives - usedRevives);

    public bool CanRevive => RemainingRevives > 0;

    public bool IsPopupOpen => popupOpen;

    private void Awake()
    {
        if (board == null)
            board = FindObjectOfType<GridBoard>();

        if (shapeTrayManager == null)
            shapeTrayManager = FindObjectOfType<ShapeTrayManager>();

        if (placeManager == null)
            placeManager = FindObjectOfType<PlaceManager>();
    }

    public void RequestRevive()
    {
        Debug.Log($"[ReviveManager] RequestRevive called. RemainingRevives={RemainingRevives}, CanRevive={CanRevive}, popupOpen={popupOpen}");

        if (popupOpen)
        {
            Debug.Log("[ReviveManager] popup already open, returning");
            return;
        }

        // Safety check: if there is at least one valid move available now, do not request revive.
        if (shapeTrayManager != null && shapeTrayManager.HasAnyMoveAvailable())
        {
            Debug.Log("[ReviveManager] HasAnyMoveAvailable == true, returning");
            return;
        }

        if (!CanRevive)
        {
            Debug.Log("[ReviveManager] CanRevive == false, restarting level");
            RestartLevel();
            return;
        }

        // If this is the last available revive (RemainingRevives <= 1), show game over and restart.
        if (RemainingRevives <= 1)
        {
            Debug.Log($"[ReviveManager] Last revive (RemainingRevives={RemainingRevives}), showing game over");
            StartCoroutine(ShowGameOverAndRestart());
            return;
        }

        // Show revive popup with countdown
        Debug.Log("[ReviveManager] Opening revive countdown popup");
        popupOpen = true;
        if (revivePopup != null)
            revivePopup.SetActive(true);

        if (reviveCountdownCoroutine != null)
            StopCoroutine(reviveCountdownCoroutine);

        reviveCountdownCoroutine = StartCoroutine(ReviveCountdownRoutine());
    }

    public void ConfirmRevive()
    {
        if (!popupOpen)
        {
            Debug.Log("[ReviveManager] ConfirmRevive: popup not open, returning");
            return;
        }

        Debug.Log("[ReviveManager] ConfirmRevive: player confirmed");
        
        if (reviveCountdownCoroutine != null)
        {
            StopCoroutine(reviveCountdownCoroutine);
            reviveCountdownCoroutine = null;
        }

        ClosePopup();
        WatchAdAndRevive();
    }

    public void DeclineRevive()
    {
        if (!popupOpen)
        {
            Debug.Log("[ReviveManager] DeclineRevive: popup not open, returning");
            return;
        }

        Debug.Log("[ReviveManager] DeclineRevive: player declined, restarting");

        if (reviveCountdownCoroutine != null)
        {
            StopCoroutine(reviveCountdownCoroutine);
            reviveCountdownCoroutine = null;
        }

        ClosePopup();
        RestartLevel();
    }

    public void ClosePopup()
    {
        popupOpen = false;
        if (revivePopup != null)
            revivePopup.SetActive(false);

        if (reviveCountdownText != null)
            reviveCountdownText.text = string.Empty;
    }

    private IEnumerator ReviveCountdownRoutine()
    {
        float remaining = reviveCountdownDuration;
        UpdateCountdownText(remaining);

        while (remaining > 0f)
        {
            yield return null;
            remaining -= Time.deltaTime;
            UpdateCountdownText(remaining);

            if (!popupOpen)
                yield break;
        }

        // Countdown expired -> auto-restart
        Debug.Log("[ReviveManager] Countdown expired, restarting level");
        reviveCountdownCoroutine = null;
        ClosePopup();
        RestartLevel();
    }

    private void UpdateCountdownText(float remaining)
    {
        if (reviveCountdownText == null)
            return;

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, remaining));
        reviveCountdownText.text = seconds.ToString();
    }

    public void WatchAdAndRevive()
    {
        if (!CanRevive)
        {
            Debug.Log("[WatchAdAndRevive] CanRevive == false, restarting");
            RestartLevel();
            return;
        }

        if (board == null)
        {
            Debug.LogWarning("[WatchAdAndRevive] board == null, restarting");
            RestartLevel();
            return;
        }

        // If last revive remaining, restart directly
        if (RemainingRevives <= 1)
        {
            Debug.Log("[WatchAdAndRevive] Last revive, restarting level");
            RestartLevel();
            return;
        }

        // Perform revive: increment counter and clear row+column
        do
        {
            usedRevives++;
            Debug.Log($"[WatchAdAndRevive] Performing revive #{usedRevives}");

            if (placeManager != null)
            {
                var result = placeManager.PerformSmartRevive();
                Debug.Log($"[WatchAdAndRevive] SmartRevive: rowsCleared={result.rowsCleared}, colsCleared={result.colsCleared}, cellsCleared={result.cellsCleared}");
            }
            else if (board != null)
            {
                int cleared = board.ReviveClearOneRowAndOneColumn();
                Debug.Log($"[WatchAdAndRevive] Fallback board cleared {cleared} cells");
            }

            if (shapeTrayManager == null)
            {
                Debug.LogWarning("[WatchAdAndRevive] shapeTrayManager == null, breaking");
                break;
            }

        } while (!shapeTrayManager.HasAnyMoveAvailable() && CanRevive);

        // After revives, check if still no moves
        if (shapeTrayManager != null && !shapeTrayManager.HasAnyMoveAvailable())
        {
            Debug.Log("[WatchAdAndRevive] Still no moves after revive, restarting");
            RestartLevel();
        }
    }

    private IEnumerator ShowGameOverAndRestart()
    {
        popupOpen = true;
        if (gameOverPopup != null)
        {
            gameOverPopup.SetActive(true);
            Debug.Log("[ShowGameOverAndRestart] Showing game over popup");
        }

        yield return new WaitForSeconds(gameOverPopupDelay);

        if (gameOverPopup != null)
            gameOverPopup.SetActive(false);

        popupOpen = false;
        Debug.Log("[ShowGameOverAndRestart] Restarting level after game over");
        RestartLevel();
    }

    public void RestartLevel()
    {
        Debug.Log("[RestartLevel] Restarting current scene");
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    public void ResetRevives()
    {
        usedRevives = 0;
        Debug.Log("[ResetRevives] Revives reset to 0");
    }
}
