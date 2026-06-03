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
        if (adventureManager.RemainingTargets != null)// && adventureManager.RemainingTargets.Count > 0)
            SetupManualUI(adventureManager.RemainingTargets);
    }

    private void OnDestroy()
    {
        if (adventureManager != null)
        {
            adventureManager.OnTargetsUpdated -= HandleTargetsUpdated;
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
            if (item.Icon == null || item.CountText == null)
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
        foreach (var kvp in targets)
        {
            if (!entries.ContainsKey(kvp.Key))
                continue;

            var entry = entries[kvp.Key];
            if (entry.CountText == null)
                continue;

            bool changed = entry.LastCount != kvp.Value;
            entry.LastCount = kvp.Value;
            entry.CountText.text = kvp.Value.ToString();

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

    private void HandleAllCompleted()
    {
        // Optional: animate all entries or show a "level complete" effect
        foreach (var kvp in entries)
        {
            if (kvp.Value.CountText != null)
                kvp.Value.CountText.text = "0";
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
