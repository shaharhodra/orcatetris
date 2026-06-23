using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;

/// <summary>
/// Shows a "Level Complete!" popup with confetti particles, then transitions to next level.
/// Listens to AdventureManager.OnAllTargetsCompleted.
/// </summary>
public class AdventureLevelCompletePopup : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Image overlay;
    [SerializeField] private RectTransform popupPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Button nextLevelButton;

    [Header("Confetti")]
    [SerializeField] private ParticleSystem confettiParticles;

    [Header("Animation")]
    [SerializeField] private float showDelay = 0.5f;
    [SerializeField] private float overlayFadeDuration = 0.3f;
    [SerializeField] private float panelScaleDuration = 0.5f;
    [SerializeField] private Ease panelEase = Ease.OutBack;

    [Header("References")]
    [SerializeField] private AdventureManager adventureManager;

    private bool isShowing;

    private void Start()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);

        if (adventureManager == null)
            adventureManager = FindFirstObjectByType<AdventureManager>();

        if (adventureManager != null)
            adventureManager.OnAllTargetsCompleted += HandleLevelComplete;

        if (nextLevelButton != null)
            nextLevelButton.onClick.AddListener(OnNextLevelClicked);
    }

    private void OnDestroy()
    {
        if (adventureManager != null)
            adventureManager.OnAllTargetsCompleted -= HandleLevelComplete;
    }

    private void HandleLevelComplete()
    {
        if (isShowing)
            return;

        StartCoroutine(ShowCompleteSequence());
    }

    private IEnumerator ShowCompleteSequence()
    {
        isShowing = true;

        // Small delay to let clear animations finish
        yield return new WaitForSeconds(showDelay);

        // Pause gameplay
        Time.timeScale = 0f;

        // Play confetti
        if (confettiParticles != null)
        {
            confettiParticles.gameObject.SetActive(true);
            confettiParticles.Play(true);
        }

        // Play sound
        if (SoundManager.instance != null)
            SoundManager.instance.PlayAmazingPopup();

        // Set texts
        var levelData = AppManager.instance != null ? AppManager.instance.CurrentLevelData : null;
        int levelNum = levelData != null ? levelData.Level : 0;

        if (titleText != null)
            titleText.text = "Level Complete!";

        if (levelText != null)
            levelText.text = $"Level {levelNum}";

        // Show popup
        if (popupRoot != null)
            popupRoot.SetActive(true);

        if (overlay != null)
        {
            overlay.color = new Color(0, 0, 0, 0);
            overlay.DOFade(0.75f, overlayFadeDuration).SetUpdate(true);
        }

        if (popupPanel != null)
        {
            popupPanel.localScale = Vector3.zero;
            popupPanel.DOScale(1f, panelScaleDuration).SetEase(panelEase).SetUpdate(true);
        }
    }

    private void OnNextLevelClicked()
    {
        if (!isShowing)
            return;

        if (SoundManager.instance != null)
            SoundManager.instance.PlayButtonClick();

        isShowing = false;

        // Advance level
        var levelData = AppManager.instance != null ? AppManager.instance.CurrentLevelData : null;
        int currentLevel = levelData != null ? levelData.Level : 0;

        if (currentLevel > 0)
            GameManager.instance.SetLevelCompleted(currentLevel);

        // Resume time and reload scene for next level
        Time.timeScale = 1f;

        if (confettiParticles != null)
            confettiParticles.Stop(true);

        GameManager.instance.ReloadCurrentScene();
    }
}
