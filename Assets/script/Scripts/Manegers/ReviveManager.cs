using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;
using System;
using DG.Tweening;

public class ReviveManager : MonoBehaviour
{
    [SerializeField] private GridBoard board;
    [SerializeField] private int maxRevives = 3;
    [SerializeField] private GameObject revivePopup;

    [SerializeField] private ShapeTrayManager shapeTrayManager;
    [SerializeField] private PlaceManager placeManager;
    [SerializeField] private PopUpService popUpService;

    [Header("Revive countdown")]
    [SerializeField] private float reviveCountdownDuration = 5f;
    [SerializeField] private Text reviveCountdownText;
    [Tooltip("Circular Image (Type=Filled, Radial360) that surrounds the countdown number.")]
    [SerializeField] private Image countdownCircle;

    [Header("Countdown Shake")]
    [Tooltip("Transform to shake each second (e.g. the countdown text or circle).")]
    [SerializeField] private RectTransform countdownShakeTarget;
    [SerializeField] private float shakeStrength = 5f;
    [SerializeField] private float shakeDuration = 0.3f;

    [Header("Game over")]

    private int usedRevives;
    private bool popupOpen;
    private Coroutine reviveCountdownCoroutine;
    private Tween countdownShakeTween;
    private int lastDisplayedSecond = -1;

    public int RemainingRevives => Mathf.Max(0, maxRevives - usedRevives);

    // Revive is only allowed in Classic mode.
    public bool CanRevive
    {
        get
        {
            var app = AppManager.instance;
            if (app == null || app.CurrentGameMode != AppManager.GameMode.Classic)
                return false;

            return RemainingRevives > 0;
        }
    }

    public bool IsPopupOpen => popupOpen;

    private void Start()
    {
        GameManager.instance.OnLevelRestartedEvent += HandleOnLevelRestartedEvent;

        // Ensure references are wired when entering the gameplay scene even if this
        // Singleton instance was created earlier (e.g. in the menu scene).
        AutoWireReferencesIfNeeded();
    }

    /// <summary>
    /// Try to automatically locate core references (board, tray, place manager)
    /// in the currently loaded gameplay scene. This is important because the
    /// ReviveManager Singleton can be created in the menu scene and survive
    /// into the game scene, where all these objects are different instances.
    /// </summary>
    private void AutoWireReferencesIfNeeded()
    {
        // Only try to auto-wire once we are in a gameplay scene where a board exists.

        if (board == null)
        {
            board = FindObjectOfType<GridBoard>();
        }

        if (shapeTrayManager == null)
        {
            shapeTrayManager = FindObjectOfType<ShapeTrayManager>();
        }

        if (placeManager == null)
        {
            placeManager = FindObjectOfType<PlaceManager>();
        }

        // UI references (revivePopup, reviveCountdownText, countdownCircle,
        // countdownShakeTarget) אתה יכול לחבר ידנית באינספקטור על האינסטנס
        // של הסצנת המשחק. אין כאן Auto-Wire כי השמות/היררכיה יכולים להשתנות.
    }

    private void OnDestroy()
    {
        GameManager.instance.OnLevelRestartedEvent -= HandleOnLevelRestartedEvent;
    }

    public void RequestRevive()
    {
        // Revive can only be requested in Classic mode.
        var app = AppManager.instance;
        if (app == null || app.CurrentGameMode != AppManager.GameMode.Classic)
            return;

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
        lastDisplayedSecond = -1;

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

        countdownShakeTween?.Kill();
        countdownShakeTween = null;

        if (revivePopup != null)
            revivePopup.SetActive(false);

        if (reviveCountdownText != null)
            reviveCountdownText.text = string.Empty;

        if (countdownCircle != null)
            countdownCircle.fillAmount = 1f;

        lastDisplayedSecond = -1;
    }

    private IEnumerator ReviveCountdownRoutine()
    {
        float remaining = reviveCountdownDuration;
        UpdateCountdownUI(remaining);

        while (remaining > 0f)
        {
            yield return null;
            remaining -= Time.deltaTime;
            UpdateCountdownUI(remaining);

            if (!popupOpen)
                yield break;
        }

        // Countdown expired -> auto game over
        reviveCountdownCoroutine = null;
        ClosePopup();
        TriggerGameOver();
    }

    private void UpdateCountdownUI(float remaining)
    {
        float clamped = Mathf.Max(0f, remaining);
        int seconds = Mathf.CeilToInt(clamped);

        if (reviveCountdownText != null)
        {
            reviveCountdownText.text = seconds.ToString();
        }

        if (countdownCircle != null)
        {
            countdownCircle.fillAmount = clamped / reviveCountdownDuration;
        }

        // Shake each time the displayed second changes
        if (seconds != lastDisplayedSecond)
        {
            lastDisplayedSecond = seconds;
            DoCountdownShake();
        }
    }

    private void DoCountdownShake()
    {
        if (countdownShakeTarget == null)
            return;

        countdownShakeTween?.Kill();
        countdownShakeTarget.localScale = Vector3.one;

        countdownShakeTween = countdownShakeTarget
            .DOShakeScale(shakeDuration, shakeStrength * 0.05f, 10, 90f)
            .SetUpdate(true)
            .OnComplete(() => countdownShakeTarget.localScale = Vector3.one);
    }

    public async Task WatchAdAndReviveAsync()
    {
        // Extra safety: do not perform revive logic outside Classic mode.
        var app = AppManager.instance;
        if (app == null || app.CurrentGameMode != AppManager.GameMode.Classic)
        {
            TriggerGameOver();
            return;
        }

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
              //  Debug.Log("[WatchAdAndRevive] Reached max revives (3), triggering game over");
                TriggerGameOver();
                // PerformSmartRevive משתמש ב-ShapeTrayManager.GetAvailableShapes + SmartReviveWithValidation
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
    /// מפעילה את OnLose popup – הפופאפ נשאר פתוח עד שהשחקן לוחץ כפתור
    /// </summary>
    private void TriggerGameOver()
    {
        Debug.Log("[ReviveManager] Triggering Game Over");

        if (popUpService != null)
        {
            popUpService.OnPopupDismissed += OnGameOverPopupDismissed;
            popUpService.RunIfConditionMetAndStay(PopUpCondition.OnLose);
        }
        else
        {
            GameManager.instance.InvokeOnLevelRestartedEvent();
        }
    }

    private void OnGameOverPopupDismissed()
    {
        if (popUpService != null)
            popUpService.OnPopupDismissed -= OnGameOverPopupDismissed;

        GameManager.instance.InvokeOnLevelRestartedEvent();
    }

    public void HandleOnLevelRestartedEvent (LevelData levelData)
    {
        RestartLevel();
    }

    public void RestartLevel()
    {
       // Debug.Log("[RestartLevel] Restarting current scene");
        ResetRevives();
    }

    public void ResetRevives()
    {
        usedRevives = 0;
        // Debug.Log("[ResetRevives] Revives reset to 0");
    }
}
