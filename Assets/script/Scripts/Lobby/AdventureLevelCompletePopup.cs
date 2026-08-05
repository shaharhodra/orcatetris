using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple, clean popup for Adventure level completion.
/// </summary>
public class AdventureLevelCompletePopup : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Image overlay;
    [SerializeField] private RectTransform popupPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Button nextLevelButton;

    [Header("Effects")]
    [Tooltip("Container GameObject holding one or more ParticleSystems (e.g. a composite confetti prefab).")]
    [SerializeField] private GameObject confetti;

    [Header("Settings")]
    [SerializeField] private float animDuration = 0.5f;
    [SerializeField] private float showDelay = 0.5f;

    private AdventureManager adventureManager;
    private bool isActive;
    private float overlayTargetAlpha = 1f;

    private void Awake()
    {
        Debug.Log($"[CompletePopup] ===== AWAKE ===== on GameObject: {gameObject.name}, active={gameObject.activeInHierarchy}, enabled={enabled}");

        if (popupRoot != null)
            popupRoot.SetActive(false);
        else
            Debug.LogError("[CompletePopup] popupRoot is NULL in Awake!");

        if (overlay != null)
            overlayTargetAlpha = overlay.color.a;

        if (nextLevelButton != null)
            nextLevelButton.onClick.AddListener(OnNextLevel);
        else
            Debug.LogError("[CompletePopup] nextLevelButton is NULL in Awake!");
    }

    private void Start()
    {
        Debug.Log($"[CompletePopup] ===== START ===== on GameObject: {gameObject.name}");
        
        adventureManager = FindFirstObjectByType<AdventureManager>();
        
        if (adventureManager != null)
        {
            adventureManager.OnAllTargetsCompleted += Show;
            Debug.Log($"[CompletePopup] ✓ Successfully subscribed to AdventureManager.OnAllTargetsCompleted");
        }
        else
        {
            Debug.LogError($"[CompletePopup] ✗ AdventureManager NOT FOUND!");
        }
    }

    private void OnDestroy()
    {
        Debug.Log($"[CompletePopup] OnDestroy called");
        if (adventureManager != null)
            adventureManager.OnAllTargetsCompleted -= Show;
    }

    public void Show()
    {
        Debug.Log($"[CompletePopup] ===== SHOW ===== called! isActive={isActive}, gameObject.activeInHierarchy={gameObject.activeInHierarchy}");
        
        if (isActive)
        {
            Debug.LogWarning($"[CompletePopup] Already active, ignoring");
            return;
        }

        isActive = true;
        var levelTime = Time.realtimeSinceStartup - AnalyticsManager.instance.LevelStartTime;
		AnalyticsManager.instance.SendEvent(AnalyticsManager.AnalyticsEvent.LevelComplete.ToString(), new List<AnalyticsManager.AnalyticsEventData>() {
			new AnalyticsManager.AnalyticsEventData("GameType", "adventure"), new AnalyticsManager.AnalyticsEventData("LevelTime", levelTime.ToString())});
		Debug.Log($"[CompletePopup] Invoking ShowInternal in {showDelay} seconds...");
        Invoke(nameof(ShowInternal), showDelay);
    }

    private void ShowInternal()
    {
        Debug.Log($"[CompletePopup] ===== SHOW INTERNAL ===== displaying popup now!");
        
        ShapeDragHandler.InputBlocked = true;

		// Update texts
		int nextLevel = 1;
        if (PlayerManeger.instance?.PlayerProgress != null)
            nextLevel = PlayerManeger.instance.PlayerProgress.HighestUnlockedLevel + 2;

        if (titleText != null)
            titleText.text = "Level Complete!";

        if (levelText != null)
            levelText.text = $"Next Level: {nextLevel}";

        // Show UI
        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
            Debug.Log("[CompletePopup] popupRoot activated");
        }
        else
        {
            Debug.LogError("[CompletePopup] popupRoot is NULL!");
        }

        if (overlay != null)
        {
            overlay.gameObject.SetActive(true);
            Color oc = overlay.color;
            oc.a = 0f;
            overlay.color = oc;
            overlay.DOFade(overlayTargetAlpha, animDuration).SetEase(Ease.OutSine).SetUpdate(true);
        }

        if (popupPanel != null)
        {
            popupPanel.localScale = Vector3.zero;
            popupPanel.DOScale(1f, animDuration).SetEase(Ease.OutBack).SetUpdate(true);
            Debug.Log("[CompletePopup] Panel scaling up");
        }

        // Effects
        if (confetti != null)
        {
            confetti.SetActive(true);
            foreach (var ps in confetti.GetComponentsInChildren<ParticleSystem>(true))
                ps.Play();
        }

        if (SoundManager.instance != null)
            SoundManager.instance.PlayLevelComplete();
    }

    private void OnNextLevel()
    {
        Debug.Log($"[CompletePopup] OnNextLevel clicked");

        if (!isActive)
            return;

        // Dedicated transition sound instead of stacking it on top of the generic
        // button click — this button always means "go to next level".
        if (SoundManager.instance != null)
            SoundManager.instance.PlayNextLevel();

        // Save progress
        int currentLevel = 0;
        if (PlayerManeger.instance?.PlayerProgress != null)
            currentLevel = PlayerManeger.instance.PlayerProgress.HighestUnlockedLevel;

        GameManager.instance.SetLevelCompleted(currentLevel);

        // The level is done — drop any saved mid-level state so it can't leak into the next one.
        AdventureSessionCache.Clear();

        // Show the interstitial now (on the player's own action) instead of the moment
        // the level finished, and only advance to the next level once it's closed —
        // that's what triggers the next level's own start popup.
        System.Action advanceToNextLevel = () => Hide(() => GameManager.instance.ReloadCurrentScene());

        if (AdsManager.instance != null)
            AdsManager.instance.ShowInterstitialAd(advanceToNextLevel);
        else
            advanceToNextLevel();
    }

    private void Hide(System.Action onComplete)
    {
        Debug.Log("[CompletePopup] Hiding popup");
        isActive = false;
        ShapeDragHandler.InputBlocked = false;

        if (confetti != null)
        {
            foreach (var ps in confetti.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop();
        }

        Sequence seq = DOTween.Sequence();

        if (popupPanel != null)
            seq.Append(popupPanel.DOScale(0f, animDuration * 0.5f).SetEase(Ease.InBack).SetUpdate(true));

        if (overlay != null)
            seq.Join(overlay.DOFade(0f, animDuration * 0.5f).SetUpdate(true));

        seq.OnComplete(() =>
        {
            if (popupRoot != null)
                popupRoot.SetActive(false);
            
            onComplete?.Invoke();
        });
    }
}
