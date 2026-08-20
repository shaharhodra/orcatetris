using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using System.Linq;

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

    [Header("Auto Start")]
    [SerializeField] private bool autoStart = true;
    [SerializeField, Min(0f)] private float autoStartDelay = 3.0f;

    [Header("Target Layout")]
    [SerializeField] private float targetItemPreferredSize = 110f;

    [Header("Target Icons")]
    [Tooltip("Optional: assign sprites by ColectionTypes index (Circles, Squares, Stars, Triangles). If empty, tries to fetch once from AdventureTargetUI.")]
    [SerializeField] private Sprite[] targetSymbolSprites;

    private static Sprite[] cachedTargetSymbolSprites;

    [Header("Target Display")]
    [SerializeField] private Transform targetsContainer;
    [SerializeField] private GameObject targetItemPrefab; // Should have Image + TMP_Text children

    [Header("Animation")]
    [SerializeField] private float overlayFadeDuration = 0.3f;
    [SerializeField] private float panelScaleDuration = 0.4f;
    [SerializeField] private Ease panelEase = Ease.OutBack;

    [Header("Dismiss Animation")]
    [SerializeField] private float symbolFlyOutDuration = 0.35f;
    [SerializeField] private float symbolFlyOutDelay = 0.12f;
    [SerializeField] private float symbolFlyOutDistance = 1200f;
    [SerializeField] private Ease symbolFlyOutEase = Ease.InBack;

    private bool isShowing;
    private Tween autoStartTween;
    private float overlayTargetAlpha = 1f;

    private void Start()
    {
        Debug.Log($"[StartPopup] Start() called. AppManager exists: {AppManager.instance != null}");

        if (popupRoot != null)
            popupRoot.SetActive(false);

        if (overlay != null)
            overlayTargetAlpha = overlay.color.a;

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

        if (startButton != null)
            startButton.gameObject.SetActive(!autoStart);

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
        autoStartTween?.Kill();
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

        ShapeDragHandler.InputBlocked = true;

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
            overlay.gameObject.SetActive(true);
            Color c = overlay.color;
            c.a = 0f;
            overlay.color = c;
            overlay.DOFade(overlayTargetAlpha, overlayFadeDuration).SetEase(Ease.OutSine).SetUpdate(true);
        }

        if (popupPanel != null)
        {
            popupPanel.localScale = Vector3.zero;
            popupPanel.DOScale(1f, panelScaleDuration).SetEase(panelEase).SetUpdate(true);
        }

        if (autoStart)
        {
            autoStartTween?.Kill();
            autoStartTween = DOVirtual.DelayedCall(autoStartDelay, () =>
            {
                if (this != null)
                    OnStartClicked();
            }).SetUpdate(true);
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

        Sprite[] symbolSprites = null;
        if (targetSymbolSprites != null && targetSymbolSprites.Length > 0)
        {
            symbolSprites = targetSymbolSprites;
        }
        else if (cachedTargetSymbolSprites != null && cachedTargetSymbolSprites.Length > 0)
        {
            symbolSprites = cachedTargetSymbolSprites;
        }
        else
        {
            var adventureUI = FindObjectOfType<AdventureTargetUI>();
            if (adventureUI != null)
            {
                var field = typeof(AdventureTargetUI).GetField("symbolSprites",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    symbolSprites = field.GetValue(adventureUI) as Sprite[];
                    if (symbolSprites != null && symbolSprites.Length > 0)
                        cachedTargetSymbolSprites = symbolSprites;
                }
            }
        }

        foreach (var target in targets)
        {
            if (target.Target <= 0)
                continue;

            var item = Instantiate(targetItemPrefab, targetsContainer);
            item.SetActive(true);

            var itemRt = item.GetComponent<RectTransform>();
            if (itemRt != null)
                itemRt.localScale = Vector3.one;

            var layout = item.GetComponent<LayoutElement>();
            if (layout == null)
                layout = item.AddComponent<LayoutElement>();
            layout.minWidth = targetItemPreferredSize;
            layout.minHeight = targetItemPreferredSize;
            layout.preferredWidth = targetItemPreferredSize;
            layout.preferredHeight = targetItemPreferredSize;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            var icon = item.GetComponentsInChildren<Image>(true).FirstOrDefault();
            if (icon != null)
            {
                int idx = (int)target.TargetItem;
                if (symbolSprites != null && idx >= 0 && idx < symbolSprites.Length && symbolSprites[idx] != null)
                    icon.sprite = symbolSprites[idx];

                var c = target.Color;
                if (c.r == 0f && c.g == 0f && c.b == 0f && c.a == 0f)
                    c = Color.white;
                c.a = 1f;
                icon.color = c;
                icon.gameObject.SetActive(true);
            }

            // Set count text
            var countText = item.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();
            if (countText != null)
                countText.text = $"x{target.Target}";
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(targetsContainer as RectTransform);
    }

    private void OnStartClicked()
    {
        if (!isShowing)
            return;

        autoStartTween?.Kill();

        // The real start of play. AppManager stamps a baseline at level load, but that is several
        // seconds early — it happens while this popup is still up and before any interstitial has
        // finished — so LevelTime measured from there overstated every level. Re-stamping here
        // makes the Adventure numbers measure play rather than waiting.
        AnalyticsManager.instance?.MarkLevelStarted();

        if (SoundManager.instance != null)
            SoundManager.instance.PlayButtonClick();

        // Animate symbols flying off screen one by one, then close popup
        Sequence seq = DOTween.Sequence().SetUpdate(true);

        // Fly out each target item
        if (targetsContainer != null)
        {
            int childCount = targetsContainer.childCount;
            for (int i = 0; i < childCount; i++)
            {
                var child = targetsContainer.GetChild(i);
                if (child == null) continue;

                var rt = child.GetComponent<RectTransform>();
                if (rt == null) continue;

                float delay = i * symbolFlyOutDelay;
                // Fly upward off screen
                seq.Insert(delay, rt.DOAnchorPosY(rt.anchoredPosition.y + symbolFlyOutDistance, symbolFlyOutDuration)
                    .SetEase(symbolFlyOutEase));
                // Scale down slightly while flying
                seq.Insert(delay, rt.DOScale(0.3f, symbolFlyOutDuration).SetEase(symbolFlyOutEase));
            }
        }

        // After symbols fly out, scale down the panel
        if (popupPanel != null)
            seq.Append(popupPanel.DOScale(0f, 0.25f).SetEase(Ease.InBack));

        if (overlay != null)
            seq.Join(overlay.DOFade(0f, overlayFadeDuration).SetEase(Ease.InSine));

        seq.OnComplete(() =>
        {
            if (overlay != null)
                overlay.gameObject.SetActive(false);

            if (popupRoot != null)
                popupRoot.SetActive(false);

            ShapeDragHandler.InputBlocked = false;
            isShowing = false;
        });
    }
}
