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

    [System.Serializable]
    public class TargetUIItem
    {
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

        // If targets already loaded, setup manual UI
        if (adventureManager.RemainingTargets != null && adventureManager.RemainingTargets.Count > 0)
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

    private void HandleTargetsUpdated(Dictionary<ColectionTypes, int> targets)
    {
        if (entries.Count == 0)
        {
            SetupManualUI(targets);
            return;
        }

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

            // Set initial count
            int count = 0;
            if (targets.ContainsKey(item.SymbolType))
                count = targets[item.SymbolType];

            // Apply icon scale
            item.Icon.transform.localScale = Vector3.one * iconScale;

            entries[item.SymbolType] = new TargetUIEntry
            {
                Root = item.Icon.gameObject,
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
