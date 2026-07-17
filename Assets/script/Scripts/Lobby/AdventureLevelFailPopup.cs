using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Shows a "Level Failed" popup when the player has no moves left in Adventure mode.
/// Offers a Retry button that restarts the same level.
/// Listens to the existing PopUpService condition system (OnAdventureLose).
/// </summary>
public class AdventureLevelFailPopup : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Image overlay;
    [SerializeField] private RectTransform popupPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Button retryButton;

    [Header("Animation")]
    [SerializeField] private float overlayFadeDuration = 0.3f;
    [SerializeField] private float panelScaleDuration = 0.4f;
    [SerializeField] private Ease panelEase = Ease.OutBack;

    [Header("References")]
    [SerializeField] private ReviveManager reviveManager;

    private bool isShowing;
    private CanvasGroup canvasGroup;

    private void Start()
    {
        Debug.Log($"[FailPopup] Start() — AppManager: {AppManager.instance != null}, GameMode: {(AppManager.instance != null ? AppManager.instance.CurrentGameMode.ToString() : "N/A")}");

        if (popupRoot != null)
            popupRoot.SetActive(false);

        // Only active in Adventure mode
        if (AppManager.instance == null || AppManager.instance.CurrentGameMode != AppManager.GameMode.Adventure)
        {
            Debug.Log("[FailPopup] Not Adventure mode, skipping.");
            return;
        }

        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetryClicked);

        // Listen to ReviveManager's game over event for Adventure mode
        if (reviveManager == null)
            reviveManager = FindFirstObjectByType<ReviveManager>();

        Debug.Log($"[FailPopup] ReviveManager found: {reviveManager != null}");

        if (reviveManager != null)
            reviveManager.OnGameOver += HandleGameOver;
    }

    private void OnDestroy()
    {
        if (reviveManager != null)
            reviveManager.OnGameOver -= HandleGameOver;
    }

    private void HandleGameOver()
    {
        Debug.Log("[FailPopup] HandleGameOver fired!");

        // Only handle Adventure mode
        if (AppManager.instance == null || AppManager.instance.CurrentGameMode != AppManager.GameMode.Adventure)
            return;

        Show();
    }

    public void Show()
    {
        Debug.Log("[FailPopup] Show() called!");

        if (isShowing)
            return;

        isShowing = true;
        ShapeDragHandler.InputBlocked = true;

        var levelData = AppManager.instance != null ? AppManager.instance.CurrentLevelData : null;
        int levelNum = levelData != null ? levelData.Level : 0;

        if (titleText != null)
            titleText.text = "Level Failed";

        if (levelText != null)
            levelText.text = $"Level {levelNum}";

        if (popupRoot != null)
            popupRoot.SetActive(true);

        if (overlay != null)
            overlay.gameObject.SetActive(true);

        // Whole popup (dim + card + text + button) fades in as one unit — no scaling.
        if (popupPanel != null)
        {
            popupPanel.localScale = Vector3.one;

            if (canvasGroup == null)
                canvasGroup = popupPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = popupPanel.gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, overlayFadeDuration).SetEase(Ease.OutSine).SetUpdate(true);
        }
    }

    private void OnRetryClicked()
    {
        if (!isShowing)
            return;

        if (SoundManager.instance != null)
            SoundManager.instance.PlayButtonClick();

        isShowing = false;
        ShapeDragHandler.InputBlocked = false;

        // Reload same scene (same level)
        GameManager.instance.ReloadCurrentScene();
    }
}
