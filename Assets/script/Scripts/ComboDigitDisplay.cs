using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders an integer as a left-to-right row of digit sprites (0-9), so the combo popup isn't
/// capped at however many "whole number" sprites someone happened to draw — with individual
/// digit art, any combo count composes automatically (12 = digit '1' + digit '2'), instead of
/// needing a dedicated sprite per combo number that stops existing past whatever was drawn.
///
/// Pools one Image per digit position and reuses them across calls rather than
/// instantiating/destroying every combo step.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ComboDigitDisplay : MonoBehaviour
{
    [Tooltip("Sprite for each digit 0-9. Index must match the digit value (index 3 = the glyph for '3').")]
    [SerializeField] private Sprite[] digitSprites = new Sprite[10];

    [Tooltip("Template Image cloned once per digit position. Gets disabled automatically so it never renders itself — it's only ever used as a stamp.")]
    [SerializeField] private Image digitTemplate;

    [Tooltip("Parent the digit images are placed under. Defaults to this object's own RectTransform.")]
    [SerializeField] private RectTransform container;

    [Tooltip("Gap between digits, in the container's HorizontalLayoutGroup units. Only applied the first time this component adds the layout group itself — has no effect if one is already present.")]
    [SerializeField] private float digitSpacing = 0f;

    private readonly List<Image> pooledDigits = new List<Image>();

    private void Awake()
    {
        if (container == null)
            container = (RectTransform)transform;

        var layout = container.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = digitSpacing;
        }

        // So the container's own width tracks however many digits are showing (a 3-digit combo
        // needs more room than a 1-digit one) instead of clipping/leaving dead space at a fixed
        // size. Combined with the container staying center-pivoted, it grows and shrinks in
        // place rather than drifting sideways as the digit count changes.
        var fitter = container.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = container.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        if (digitTemplate != null)
            digitTemplate.gameObject.SetActive(false);
    }

    /// <summary>Displays value as a left-to-right sequence of digit sprites.</summary>
    public void SetNumber(int value)
    {
        if (digitTemplate == null)
        {
            Debug.LogWarning("[ComboDigitDisplay] No digit template assigned — cannot render digits.");
            return;
        }

        string digits = Mathf.Max(0, value).ToString();

        for (int i = 0; i < digits.Length; i++)
        {
            var image = GetOrCreateDigit(i);
            int digit = digits[i] - '0';
            image.sprite = digitSprites != null && digit < digitSprites.Length ? digitSprites[digit] : null;
            image.enabled = image.sprite != null;
            image.gameObject.SetActive(true);
        }

        for (int i = digits.Length; i < pooledDigits.Count; i++)
            pooledDigits[i].gameObject.SetActive(false);
    }

    private Image GetOrCreateDigit(int index)
    {
        if (index < pooledDigits.Count)
            return pooledDigits[index];

        var image = Instantiate(digitTemplate, container);
        image.gameObject.SetActive(true);
        pooledDigits.Add(image);
        return image;
    }
}
