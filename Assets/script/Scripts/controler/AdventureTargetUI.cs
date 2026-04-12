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

    [Header("Target Item Prefab")]
    [Tooltip("Prefab with an Image (icon) and a TMP text (count). Will be instantiated per target.")]
    [SerializeField] private GameObject targetItemPrefab;

    [Header("Container")]
    [Tooltip("Horizontal layout parent where target items will be spawned.")]
    [SerializeField] private Transform container;

    [Header("Symbol Sprites")]
    [Tooltip("Same order as ColectionTypes: 0=Circles, 1=Squares, 2=Stars, 3=Triangles")]
    [SerializeField] private Sprite[] symbolSprites = new Sprite[4];

    [Header("Animation")]
    [SerializeField] private float punchScale = 0.2f;
    [SerializeField] private float punchDuration = 0.25f;

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

        // If targets already loaded, build UI
        if (adventureManager.RemainingTargets != null && adventureManager.RemainingTargets.Count > 0)
            BuildUI(adventureManager.RemainingTargets);
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
            BuildUI(targets);
            return;
        }

        UpdateUI(targets);
    }

    private void BuildUI(Dictionary<ColectionTypes, int> targets)
    {
        // Clear old entries
        foreach (var kvp in entries)
        {
            if (kvp.Value.Root != null)
                Destroy(kvp.Value.Root);
        }
        entries.Clear();

        if (targetItemPrefab == null || container == null)
            return;

        foreach (var kvp in targets)
        {
            var go = Instantiate(targetItemPrefab, container);
            go.SetActive(true);

            var icon = go.GetComponentInChildren<Image>();
            var text = go.GetComponentInChildren<TextMeshProUGUI>();

            int spriteIndex = (int)kvp.Key;
            if (icon != null && symbolSprites != null && spriteIndex >= 0 && spriteIndex < symbolSprites.Length)
                icon.sprite = symbolSprites[spriteIndex];

            if (text != null)
                text.text = kvp.Value.ToString();

            entries[kvp.Key] = new TargetUIEntry
            {
                Root = go,
                Icon = icon,
                CountText = text,
                LastCount = kvp.Value
            };
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
}
