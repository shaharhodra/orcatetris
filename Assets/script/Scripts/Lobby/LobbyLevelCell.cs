using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

// Attach to each cell prefab inside the GridLayoutGroup.
// Each cell represents one level slot. Shows locked/completed state visually.
public class LobbyLevelCell : MonoBehaviour
{
    [Header("Cell Identity")]
    [SerializeField] private int levelIndex = 0;
    public int LevelIndex => levelIndex;

    [Tooltip("Displays LevelIndex + 1. Set by LobbyLevelManager when it slides this cell's window over the (unbounded) level range instead of leaving every cell permanently tied to whatever level it was baked to show.")]
    [SerializeField] private TextMeshProUGUI levelNumberText;

    // Re-labels this cell to represent a different level without needing a distinct
    // GameObject per level — lets a fixed number of physical cells show a sliding
    // window over an arbitrarily large level range. Clears any completed-cover
    // instance from whatever level this cell previously showed, since that cover
    // no longer applies once the cell is re-windowed onto a different level.
    public void SetLevelIndex(int newLevelIndex)
    {
        if (newLevelIndex == levelIndex)
            return;

        levelIndex = newLevelIndex;
        if (levelNumberText != null)
            levelNumberText.text = (levelIndex + 1).ToString();

        ResetCover();
    }

    [Header("Visuals")]
    [SerializeField] private Image lockedOverlay;      // shown when level is locked (dark/greyed)
    [SerializeField] private GameObject completedIcon; // checkmark or icon shown when completed
    [SerializeField] private GameObject staticCover;   // child already in hierarchy — shown when completed, hidden otherwise

    [Header("Cover Animation (optional prefab)")]
    [SerializeField] private GameObject coverPrefab;   // if set, instantiates animated cover instead of staticCover
    [SerializeField] private float flyDuration = 0.45f;
    [SerializeField] private float flyOffscreen = 500f;
    [SerializeField] private Ease flyEase = Ease.OutBack;

    private bool coverPlaced = false;
    private GameObject coverInstance;

    private void Awake()
    {
        // Always start hidden — shown only when level is completed
        if (staticCover != null)
            staticCover.SetActive(false);
    }

    // Set visual state instantly (on first load, no animation)
    public void SetState(bool isUnlocked, bool isCompleted)
    {
        if (lockedOverlay != null)
            lockedOverlay.gameObject.SetActive(!isUnlocked);

        if (completedIcon != null)
            completedIcon.SetActive(isCompleted);

        // Static cover: only show when completed (and no animated prefab is used)
        if (staticCover != null && coverPrefab == null)
            staticCover.SetActive(isCompleted);
    }

    // Animate a cover flying in from outside to mark this level as completed
    public void AnimateCoverIn(float delay = 0f)
    {
        if (coverPrefab == null || coverPlaced) return;
        coverPlaced = true;

        var go = Instantiate(coverPrefab, transform);
        coverInstance = go;
        var rect = go.GetComponent<RectTransform>();
        if (rect == null) return;

        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;

        // Start from a random off-screen direction
        Vector2[] offsets = {
            new Vector2(-flyOffscreen, 0f),
            new Vector2( flyOffscreen, 0f),
            new Vector2(0f,  flyOffscreen),
            new Vector2(0f, -flyOffscreen)
        };
        Vector2 startOffset = offsets[Random.Range(0, offsets.Length)];

        rect.anchoredPosition = startOffset;
        rect.DOAnchorPos(Vector2.zero, flyDuration)
            .SetDelay(delay)
            .SetEase(flyEase);
    }

    // Place cover instantly with no animation (used on first load)
    public void PlaceCoverInstant()
    {
        if (coverPrefab == null || coverPlaced) return;
        coverPlaced = true;

        var go = Instantiate(coverPrefab, transform);
        coverInstance = go;
        var rect = go.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }

    public void ResetCover()
    {
        coverPlaced = false;

        if (coverInstance != null)
        {
            Destroy(coverInstance);
            coverInstance = null;
        }
    }
}
