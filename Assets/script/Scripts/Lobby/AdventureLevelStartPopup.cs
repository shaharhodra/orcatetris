using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// Shows level number, target symbols + counts, and a Start button before gameplay begins.
/// Pauses the game (Time.timeScale = 0) until the player presses Start.
/// </summary>
public class AdventureLevelStartPopup : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Image overlay;
    [SerializeField] private RectTransform popupPanel;
    [SerializeField] private TextMeshProUGUI levelNumberText;
    [SerializeField] private Button startButton;

    [Header("Target Display")]
    [SerializeField] private Transform targetsContainer;
    [SerializeField] private GameObject targetItemPrefab; // Should have Image + TMP_Text children

    [Header("Animation")]
    [SerializeField] private float overlayFadeDuration = 0.3f;
    [SerializeField] private float panelScaleDuration = 0.4f;
    [SerializeField] private Ease panelEase = Ease.OutBack;

    private bool isShowing;

    private void Start()
    {
        Debug.Log($"[StartPopup] Start() called. AppManager exists: {AppManager.instance != null}");

        if (popupRoot != null)
            popupRoot.SetActive(false);

        if (AppManager.instance == null)
        {
            Debug.LogWarning("[StartPopup] AppManager.instance is null!");
            return;
        }

        Debug.Log($"[StartPopup] CurrentGameMode = {AppManager.instance.CurrentGameMode}");

        // Only show in Adventure mode
        if (AppManager.instance.CurrentGameMode != AppManager.GameMode.Adventure)
        {
            Debug.Log("[StartPopup] Not Adventure mode, skipping.");
            return;
        }

        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        // Wait for level data then show
        if (AppManager.instance.CurrentLevelData != null)
        {
            Debug.Log($"[StartPopup] LevelData already loaded: Level {AppManager.instance.CurrentLevelData.Level}");
            ShowPopup(AppManager.instance.CurrentLevelData);
        }
        else
        {
            Debug.Log("[StartPopup] LevelData not yet loaded, subscribing to OnDataLoaded...");
            AppManager.instance.OnDataLoaded += HandleDataLoaded;
        }
    }

    private void OnDestroy()
    {
        if (AppManager.instance != null)
            AppManager.instance.OnDataLoaded -= HandleDataLoaded;
    }

    private void HandleDataLoaded(LevelData levelData)
    {
        if (AppManager.instance != null)
            AppManager.instance.OnDataLoaded -= HandleDataLoaded;

        ShowPopup(levelData);
    }

    private void ShowPopup(LevelData levelData)
    {
        if (levelData == null || isShowing)
            return;

        isShowing = true;

        // Pause gameplay
        Time.timeScale = 0f;

        // Set level number
        if (levelNumberText != null)
            levelNumberText.text = $"Level {levelData.Level}";

        // Populate targets
        PopulateTargets(levelData.LevelTargets);

        // Show with animation (using unscaled time since game is paused)
        if (popupRoot != null)
            popupRoot.SetActive(true);

        if (overlay != null)
        {
            overlay.color = new Color(0, 0, 0, 0);
            overlay.DOFade(0.7f, overlayFadeDuration).SetUpdate(true);
        }

        if (popupPanel != null)
        {
            popupPanel.localScale = Vector3.zero;
            popupPanel.DOScale(1f, panelScaleDuration).SetEase(panelEase).SetUpdate(true);
        }
    }

    private void PopulateTargets(List<LevelTargetData> targets)
    {
        if (targetsContainer == null || targetItemPrefab == null)
            return;

        // Clear existing
        foreach (Transform child in targetsContainer)
            Destroy(child.gameObject);

        if (targets == null || targets.Count == 0)
            return;

        foreach (var target in targets)
        {
            if (target.Target <= 0)
                continue;

            var item = Instantiate(targetItemPrefab, targetsContainer);
            item.SetActive(true);

            // Set icon color
            var icon = item.GetComponentInChildren<Image>();
            if (icon != null && icon.transform != item.transform)
                icon.color = target.Color;

            // Set count text
            var countText = item.GetComponentInChildren<TextMeshProUGUI>();
            if (countText != null)
                countText.text = $"x{target.Target}";
        }
    }

    private void OnStartClicked()
    {
        if (!isShowing)
            return;

        if (SoundManager.instance != null)
            SoundManager.instance.PlayButtonClick();

        // Hide popup
        Sequence seq = DOTween.Sequence().SetUpdate(true);

        if (popupPanel != null)
            seq.Append(popupPanel.DOScale(0f, 0.25f).SetEase(Ease.InBack));

        if (overlay != null)
            seq.Append(overlay.DOFade(0f, 0.2f));

        seq.OnComplete(() =>
        {
            if (popupRoot != null)
                popupRoot.SetActive(false);

            // Resume gameplay
            Time.timeScale = 1f;
            isShowing = false;
        });
    }
}
