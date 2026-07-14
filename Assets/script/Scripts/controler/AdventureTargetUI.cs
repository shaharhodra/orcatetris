using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;
using System.Collections.Generic;

public class AdventureTargetUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AdventureManager adventureManager;

    [Header("Manual UI Elements")]
    [Tooltip("Assign UI elements manually for each symbol type. Order: Circles, Squares, Stars, Triangles")]
    [SerializeField] private TargetUIItem[] manualUIItems;

    [Header("Animation")]
    [SerializeField] private float punchScale = 0.2f;
    [SerializeField] private float punchDuration = 0.25f;
    [SerializeField] private float countTweenDuration = 0.35f;
    [SerializeField] private float symbolFlyDuration = 0.55f;
    [SerializeField] private Ease symbolFlyEase = Ease.InOutCubic;
    [SerializeField] private float symbolPreFlyScaleMultiplier = 3f;
    [SerializeField] private float symbolPreFlyScaleDuration = 0.18f;
    [SerializeField] private Ease symbolPreFlyScaleEase = Ease.OutBack;
    [SerializeField] private float symbolIntoTargetScaleMultiplier = 0f;
    [SerializeField] private Ease symbolIntoTargetScaleEase = Ease.InBack;
    [SerializeField] private bool fadeSymbolIntoTarget = true;

    [Header("Win Popup")]
    [SerializeField] private AdventureLevelCompletePopup completePopup;

    [Header("UI Icon Scale")]
    [SerializeField] private float iconScale = 1.0f; // Scale for UI icons only
    [SerializeField] private Sprite[] symbolSprites;

    [System.Serializable]
    public class TargetUIItem
    {
        public Transform Root;
        public ColectionTypes SymbolType;
        public Image Icon;
        public TextMeshProUGUI CountText;
    }

    // Runtime: one UI entry per target type
    private Dictionary<ColectionTypes, TargetUIEntry> entries = new Dictionary<ColectionTypes, TargetUIEntry>();

    private class TargetUIEntry
    {
        public GameObject Root;
        public Image Icon;
        public TextMeshProUGUI CountText;
        public int LastCount;
    }

    private void Start()
    {
        if (adventureManager == null)
            adventureManager = FindFirstObjectByType<AdventureManager>();

        if (adventureManager != null)
        {
            adventureManager.OnTargetsUpdated += HandleTargetsUpdated;
            adventureManager.OnClearedSymbolVisuals += HandleClearedSymbolVisuals;
            adventureManager.OnAllTargetsCompleted += HandleAllCompleted;
        }

        // Hide if not adventure mode
        if (adventureManager == null || !adventureManager.IsAdventureMode)
        {
            gameObject.SetActive(false);
            return;
        }

        // Initialize symbolSprites array from manual UI icons so gameplay systems can use them.
        InitializeSymbolSpritesFromManualItems();

        // If targets already loaded, setup manual UI
        if (adventureManager.RemainingTargets != null && adventureManager.RemainingTargets.Count > 0)
            SetupManualUI(adventureManager.RemainingTargets);
    }

    private void OnDestroy()
    {
        if (adventureManager != null)
        {
            adventureManager.OnTargetsUpdated -= HandleTargetsUpdated;
            adventureManager.OnClearedSymbolVisuals -= HandleClearedSymbolVisuals;
            adventureManager.OnAllTargetsCompleted -= HandleAllCompleted;
        }
    }

    private void InitializeSymbolSpritesFromManualItems()
    {
        if (manualUIItems == null || manualUIItems.Length == 0)
            return;

        int maxTypeIndex = -1;
        for (int i = 0; i < manualUIItems.Length; i++)
        {
            int idx = (int)manualUIItems[i].SymbolType;
            if (idx > maxTypeIndex)
                maxTypeIndex = idx;
        }

        if (maxTypeIndex < 0)
            return;

        if (symbolSprites == null || symbolSprites.Length <= maxTypeIndex)
        {
            var newArray = new Sprite[maxTypeIndex + 1];
            if (symbolSprites != null)
            {
                for (int i = 0; i < Mathf.Min(symbolSprites.Length, newArray.Length); i++)
                    newArray[i] = symbolSprites[i];
            }
            symbolSprites = newArray;
        }

        for (int i = 0; i < manualUIItems.Length; i++)
        {
            var item = manualUIItems[i];
            if (item.Icon == null)
                continue;

            int idx = (int)item.SymbolType;
            if (idx < 0 || idx >= symbolSprites.Length)
                continue;

            if (symbolSprites[idx] == null)
                symbolSprites[idx] = item.Icon.sprite;
        }
    }

    private void HandleTargetsUpdated(Dictionary<ColectionTypes, int> targets)
    {
        if (entries.Count == 0)
            SetupManualUI(targets);

        UpdateUI(targets);
    }

    private void SetupManualUI(Dictionary<ColectionTypes, int> targets)
    {
        entries.Clear();

        if (manualUIItems == null)
            return;

        foreach (var item in manualUIItems)
        {
            if (item.Root == null || item.Icon == null || item.CountText == null)
                continue;

			item.Root.gameObject.SetActive(true); // Hide if not in targets
												   // Set initial count
			int count = 0;
            if (targets.ContainsKey(item.SymbolType))
                count = targets[item.SymbolType];
            else
                item.Root.gameObject.SetActive(false); // Hide if not in targets

            // Apply icon scale
            item.Icon.transform.localScale = Vector3.one * iconScale;

            entries[item.SymbolType] = new TargetUIEntry
            {
                Root = item.Root.gameObject,
                Icon = item.Icon,
                CountText = item.CountText,
                LastCount = count
            };

            // Update initial display
            item.CountText.text = count.ToString();
        }
    }

    private void UpdateUI(Dictionary<ColectionTypes, int> targets)
    {
        // Skip bulk counter update if symbols are animating (counters will decrement one-by-one on arrival)
        if (isAnimatingSymbols)
            return;

        foreach (var kvp in targets)
        {
            if (!entries.ContainsKey(kvp.Key))
                continue;

            var entry = entries[kvp.Key];
            if (entry.CountText == null)
                continue;

            if (entry.Root != null && !entry.Root.activeSelf)
                entry.Root.SetActive(true);

            if (entry.Icon != null)
            {
                var activeColor = entry.Icon.color;
                activeColor.a = kvp.Value > 0 ? 1f : 0.35f;
                entry.Icon.color = activeColor;
            }

            bool changed = entry.LastCount != kvp.Value;
            int from = entry.LastCount;
            entry.LastCount = kvp.Value;

            entry.CountText.DOKill();
            if (changed)
            {
                DOTween.To(() => from, value =>
                {
                    from = value;
                    entry.CountText.text = from.ToString();
                }, kvp.Value, countTweenDuration).SetEase(Ease.OutCubic).SetTarget(entry.CountText);
            }
            else
            {
                entry.CountText.text = kvp.Value.ToString();
            }

            // Punch animation when count decreases
            if (changed && entry.Root != null)
            {
                entry.Root.transform.DOKill(true);
                entry.Root.transform.localScale = Vector3.one;
                entry.Root.transform.DOPunchScale(Vector3.one * punchScale, punchDuration, 1, 0.5f);
            }

            // Dim completed targets
            if (kvp.Value <= 0 && entry.Icon != null)
            {
                var c = entry.Icon.color;
                c.a = 0.35f;
                entry.Icon.color = c;
            }
        }
    }

    [Header("Stagger")]
    [SerializeField] private float symbolStaggerDelay = 0f;
    [SerializeField] private float symbolHoldDuration = 0.15f;
    [SerializeField] private float symbolSequentialDelay = 0f;

    private bool isAnimatingSymbols;

    private void HandleClearedSymbolVisuals(List<ClearedSymbolVisual> visuals)
    {
        if (visuals == null || visuals.Count == 0)
            return;

        isAnimatingSymbols = true;

        // Phase 1: All symbols grow simultaneously (with small stagger)
        // Phase 2: After hold, symbols fly to bank one by one sequentially
        // Phase 3: Counter decrements on each symbol arrival

        var preparedSymbols = new List<PreparedSymbol>();

        int staggerIndex = 0;
        foreach (var visual in visuals)
        {
            if (visual == null || !entries.ContainsKey(visual.Type))
                continue;

            var entry = entries[visual.Type];
            if (entry.Icon == null)
                continue;

            GameObject flyingIcon = visual.IconObject;
            if (flyingIcon == null)
                flyingIcon = CreateFallbackFlyingIcon(visual.Type, visual.WorldPosition);

            if (flyingIcon == null)
                continue;

            Transform flyingTransform = flyingIcon.transform;
            flyingTransform.DOKill();
            flyingTransform.position = visual.WorldPosition;
            flyingTransform.SetParent(null, true);

            Vector3 baseScale = flyingTransform.localScale;
            if (baseScale == Vector3.zero)
                baseScale = Vector3.one;

            var sr = flyingIcon.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                var c = sr.color;
                c.a = 1f;
                sr.color = c;
            }

            preparedSymbols.Add(new PreparedSymbol
            {
                flyingIcon = flyingIcon,
                flyingTransform = flyingTransform,
                baseScale = baseScale,
                sr = sr,
                entry = entry,
                type = visual.Type,
                staggerIndex = staggerIndex
            });

            staggerIndex++;
        }

        if (preparedSymbols.Count == 0)
        {
            isAnimatingSymbols = false;
            return;
        }

        // Phase 1: Grow all symbols with small stagger
        Sequence masterSeq = DOTween.Sequence();

        foreach (var ps in preparedSymbols)
        {
            float growDelay = ps.staggerIndex * symbolStaggerDelay;
            var growSeq = DOTween.Sequence();

            // Sound
            growSeq.AppendCallback(() =>
            {
                if (SoundManager.instance != null && SoundManager.instance.SymbolGrowClipLength > 0f)
                {
                    float pitch = SoundManager.instance.SymbolGrowClipLength / symbolPreFlyScaleDuration;
                    SoundManager.instance.PlaySymbolGrow(pitch);
                }
                else if (SoundManager.instance != null)
                {
                    SoundManager.instance.PlaySymbolGrow();
                }
            });
            growSeq.Append(ps.flyingTransform.DOScale(ps.baseScale * symbolPreFlyScaleMultiplier, symbolPreFlyScaleDuration).SetEase(symbolPreFlyScaleEase));

            masterSeq.Insert(growDelay, growSeq);
        }

        // Phase 2: Hold all symbols at enlarged size
        masterSeq.AppendInterval(symbolHoldDuration);

        // Phase 3: Fly to bank one by one sequentially
        for (int i = 0; i < preparedSymbols.Count; i++)
        {
            var ps = preparedSymbols[i];
            float flyDelay = i * symbolSequentialDelay;

            Vector3 targetPosition = GetWorldPosition(ps.entry.Icon.transform);

            var flySeq = DOTween.Sequence();
            flySeq.Append(ps.flyingTransform.DOMove(targetPosition, symbolFlyDuration).SetEase(symbolFlyEase));
            flySeq.Join(ps.flyingTransform.DOScale(ps.baseScale * symbolIntoTargetScaleMultiplier, symbolFlyDuration).SetEase(symbolIntoTargetScaleEase));
            if (fadeSymbolIntoTarget && ps.sr != null)
                flySeq.Join(ps.sr.DOFade(0f, symbolFlyDuration));

            // Capture for closure
            var capturedPs = ps;
            flySeq.OnComplete(() =>
            {
                // Sound on arrival
                if (SoundManager.instance != null && SoundManager.instance.SymbolReachedTargetClipLength > 0f)
                {
                    float pitch = SoundManager.instance.SymbolReachedTargetClipLength / symbolFlyDuration;
                    SoundManager.instance.PlaySymbolReachedTarget(pitch);
                }
                else if (SoundManager.instance != null)
                {
                    SoundManager.instance.PlaySymbolReachedTarget();
                }

                // Punch the target icon
                if (capturedPs.entry.Root != null)
                {
                    capturedPs.entry.Root.transform.DOKill(true);
                    capturedPs.entry.Root.transform.localScale = Vector3.one;
                    capturedPs.entry.Root.transform.DOPunchScale(Vector3.one * punchScale, punchDuration, 1, 0.5f);
                }

                // Decrement counter by 1
                if (capturedPs.entry.CountText != null)
                {
                    capturedPs.entry.LastCount = Mathf.Max(0, capturedPs.entry.LastCount - 1);
                    capturedPs.entry.CountText.text = capturedPs.entry.LastCount.ToString();

                    // Dim if completed
                    if (capturedPs.entry.LastCount <= 0 && capturedPs.entry.Icon != null)
                    {
                        var c = capturedPs.entry.Icon.color;
                        c.a = 0.35f;
                        capturedPs.entry.Icon.color = c;
                    }
                }

                if (capturedPs.flyingIcon != null)
                    Destroy(capturedPs.flyingIcon);
            });

            masterSeq.Insert(masterSeq.Duration() + flyDelay, flySeq);
        }

        masterSeq.OnComplete(() =>
        {
            isAnimatingSymbols = false;
        });
    }

    private struct PreparedSymbol
    {
        public GameObject flyingIcon;
        public Transform flyingTransform;
        public Vector3 baseScale;
        public SpriteRenderer sr;
        public TargetUIEntry entry;
        public ColectionTypes type;
        public int staggerIndex;
    }

    private GameObject CreateFallbackFlyingIcon(ColectionTypes type, Vector3 worldPosition)
    {
        int index = (int)type;
        if (symbolSprites == null || index < 0 || index >= symbolSprites.Length || symbolSprites[index] == null)
            return null;

        var go = new GameObject($"FlyingSymbol_{type}");
        go.transform.position = worldPosition;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = symbolSprites[index];
        sr.sortingOrder = 50;

        return go;
    }

    private Vector3 GetWorldPosition(Transform target)
    {
        if (target == null)
            return Vector3.zero;

        RectTransform rect = target as RectTransform;
        Canvas canvas = target.GetComponentInParent<Canvas>();

        if (rect == null || canvas == null || canvas.renderMode == RenderMode.WorldSpace)
            return target.position;

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector3 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, rect.position);
        Camera worldCamera = Camera.main;

        if (worldCamera == null)
            return target.position;

        float z = Mathf.Abs(worldCamera.transform.position.z);
        return worldCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, z));
    }

    private void HandleAllCompleted()
    {
        // Reset counters
        foreach (var kvp in entries)
        {
            if (kvp.Value.CountText != null)
                kvp.Value.CountText.text = "0";
        }

        // Show victory popup
        if (completePopup != null)
        {
            completePopup.Show();
        }
        else
        {
            Debug.LogWarning("[AdventureTargetUI] completePopup is not assigned in Inspector!");
        }
    }

    // Public methods for manual control
    public void ShowUI()
    {
        gameObject.SetActive(true);
    }

    public void HideUI()
    {
        gameObject.SetActive(false);
    }

    public void SetAdventureMode(bool isAdventure)
    {
        if (isAdventure)
            ShowUI();
        else
            HideUI();
    }
}
