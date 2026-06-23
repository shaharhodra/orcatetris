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

    private void Start()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);

        // Only active in Adventure mode
        if (AppManager.instance == null || AppManager.instance.CurrentGameMode != AppManager.GameMode.Adventure)
            return;

        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetryClicked);

        // Listen to ReviveManager's game over event for Adventure mode
        if (reviveManager == null)
            reviveManager = FindFirstObjectByType<ReviveManager>();

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
        // Only handle Adventure mode
        if (AppManager.instance == null || AppManager.instance.CurrentGameMode != AppManager.GameMode.Adventure)
            return;

        Show();
    }

    public void Show()
    {
        if (isShowing)
            return;

        isShowing = true;
        Time.timeScale = 0f;

        var levelData = AppManager.instance != null ? AppManager.instance.CurrentLevelData : null;
        int levelNum = levelData != null ? levelData.Level : 0;

        if (titleText != null)
            titleText.text = "Level Failed";

        if (levelText != null)
            levelText.text = $"Level {levelNum}";

        if (popupRoot != null)
            popupRoot.SetActive(true);

        if (overlay != null)
        {
            overlay.color = new Color(0, 0, 0, 0);
            overlay.DOFade(0.8f, overlayFadeDuration).SetUpdate(true);
        }

        if (popupPanel != null)
        {
            popupPanel.localScale = Vector3.zero;
            popupPanel.DOScale(1f, panelScaleDuration).SetEase(panelEase).SetUpdate(true);
        }
    }

    private void OnRetryClicked()
    {
        if (!isShowing)
            return;

        if (SoundManager.instance != null)
            SoundManager.instance.PlayButtonClick();

        isShowing = false;
        Time.timeScale = 1f;

        // Reload same scene (same level)
        GameManager.instance.ReloadCurrentScene();
    }
}
