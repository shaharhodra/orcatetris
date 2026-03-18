using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;
using System;

public class ReviveManager : Singleton<ReviveManager>
{
    [SerializeField] private GridBoard board;
    [SerializeField] private int maxRevives = 3;
    [SerializeField] private GameObject revivePopup;

    [SerializeField] private ShapeTrayManager shapeTrayManager;
    [SerializeField] private PlaceManager placeManager;
    [SerializeField] private PopUpService popUpService;

    [Header("Revive countdown")]
    [SerializeField] private float reviveCountdownDuration = 5f;
    [SerializeField] private UnityEngine.UI.Text reviveCountdownText;
    [Header("Game over")]

    private int usedRevives;
    private bool popupOpen;
    private Coroutine reviveCountdownCoroutine;

    public int RemainingRevives => Mathf.Max(0, maxRevives - usedRevives);

    public bool CanRevive => RemainingRevives > 0;

    public bool IsPopupOpen => popupOpen;

    private void Start()
    {
        GameManager.instance.OnLevelRestartedEvent += HandleOnLevelRestartedEvent;
    }

    private void OnDestroy()
    {
        GameManager.instance.OnLevelRestartedEvent -= HandleOnLevelRestartedEvent;
    }

    public void RequestRevive()
    {
        if (popupOpen)
        {
            return;
        }

        // Safety check: if there is at least one valid move available now, do not request revive.
        if (shapeTrayManager != null && shapeTrayManager.HasAnyMoveAvailable())
        {
            return;
        }

        // ✅ תיקון: אם הגענו ל-3 revives (כלומר usedRevives == 2), לך ישר ל-OnLose
        if (usedRevives >= 2)
        {
            Debug.Log("[ReviveManager] Already used 2 revives, going straight to OnLose");
            TriggerGameOver();
            return;
        }

        if (!CanRevive)
        {
            TriggerGameOver();
            return;
        }

        // Show revive popup with countdown
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
            return;
        }

        if (reviveCountdownCoroutine != null)
        {
            StopCoroutine(reviveCountdownCoroutine);
            reviveCountdownCoroutine = null;
        }

        ClosePopup();
        WatchAdAndReviveAsync();
    }

    public void DeclineRevive()
    {
        if (!popupOpen)
        {
            return;
        }

        if (reviveCountdownCoroutine != null)
        {
            StopCoroutine(reviveCountdownCoroutine);
            reviveCountdownCoroutine = null;
        }

        ClosePopup();
        TriggerGameOver();
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

        // Countdown expired -> auto game over
        reviveCountdownCoroutine = null;
        ClosePopup();
        TriggerGameOver();
    }

    private void UpdateCountdownText(float remaining)
    {
        if (reviveCountdownText == null)
            return;

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, remaining));
        reviveCountdownText.text = seconds.ToString();
    }

    public async Task WatchAdAndReviveAsync()
    {
        if (!CanRevive)
        {
            TriggerGameOver();
            return;
        }

        if (board == null)
        {
            TriggerGameOver();
            return;
        }

        // Perform revive: increment counter and clear row+column
        do
        {
            usedRevives++;
            Debug.Log($"[WatchAdAndRevive] Used revives: {usedRevives}/{maxRevives}");

            // ✅ תיקון: בדוק אם הגענו ל-3 revives אחרי ההגדלה
            if (usedRevives >= 3)
            {
                Debug.Log("[WatchAdAndRevive] Reached max revives (3), triggering game over");
                TriggerGameOver();
                return;
            }

            // Clear cells to ensure at least one shape can fit
            if (placeManager != null)
            {
                var result = placeManager.PerformSmartRevive();
                Debug.Log($"[WatchAdAndRevive] SmartRevive: rowsCleared={result.rowsCleared}, colsCleared={result.colsCleared}, cellsCleared={result.cellsCleared}");
            }
            else if (board != null)
            {
                bool cleared = EnsureSpaceForOneShape();
                if (!cleared)
                {
                    Debug.LogWarning("[WatchAdAndRevive] Failed to clear space for at least one shape.");
                    TriggerGameOver();
                    return;
                }
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
            Debug.Log("[WatchAdAndRevive] Still no moves after revive, triggering game over");
            TriggerGameOver();
        }
    }

    private bool EnsureSpaceForOneShape()
    {
        if (shapeTrayManager == null || board == null)
        {
            Debug.LogError("[EnsureSpaceForOneShape] shapeTrayManager or board is null!");
            return false;
        }

        foreach (var shape in shapeTrayManager.GetAvailableShapes())
        {
            if (shape == null)
            {
                Debug.LogWarning("[EnsureSpaceForOneShape] Found a null shape in available shapes.");
                continue;
            }

            for (int row = 0; row < board.Rows; row++)
            {
                for (int col = 0; col < board.Columns; col++)
                {
                    Vector2Int targetCell = new Vector2Int(col, row);
                    if (board.CanPlaceShape(shape, targetCell))
                    {
                        Debug.Log($"[EnsureSpaceForOneShape] Found space for shape '{shape.name}' at {targetCell}. Clearing cells...");
                        board.ClearCellsForShape(shape, targetCell);
                        return true;
                    }
                }
            }
        }

        Debug.LogWarning("[EnsureSpaceForOneShape] No space could be cleared for any shape.");
        return false;
    }

    /// <summary>
    /// ✅ פונקציה חדשה: מפעילה את OnLose popup ואז עושה Restart
    /// </summary>
    private async void TriggerGameOver()
    {
        Debug.Log("[ReviveManager] Triggering Game Over");

        // הצג OnLose popup
        if (popUpService != null)
        {
            popUpService.RunIfConditionMet(PopUpCondition.OnLose);
            await UniTask.Delay(TimeSpan.FromSeconds(2)); // מחכה 2 שניות
        }

        GameManager.instance.InvokeOnLevelRestartedEvent();
    }

    public void HandleOnLevelRestartedEvent (LevelData levelData)
    {
        RestartLevel();
    }

    public void RestartLevel()
    {
        Debug.Log("[RestartLevel] Restarting current scene");
        ResetRevives();
    }

    public void ResetRevives()
    {
        usedRevives = 0;
        Debug.Log("[ResetRevives] Revives reset to 0");
    }
}
