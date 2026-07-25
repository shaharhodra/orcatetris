using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

public class ThemeManager : MonoBehaviour
{
    public static ThemeManager instance;

    // ThemeBlock components subscribe to this to update themselves
    public static System.Action<ThemeData, bool> OnThemeChanged;

    public ThemeData CurrentTheme => currentTheme;

    [Header("Themes")]
    [SerializeField] private ThemeData defaultTheme;
    [SerializeField] private ThemeData[] themes;

    [Header("Scene References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GridBoard gridBoard;

    [Header("Optional Tintable Objects")]
    [SerializeField] private List<Image> uiImages = new List<Image>();

    private ThemeData currentTheme;
    private int currentThemeIndex = -1;
    private Sequence transitionSequence;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        ApplyTheme(defaultTheme, instant: true);
    }

    // ===== Public API =====

    public void ApplyThemeToGrid(GridBoard board)
    {
        if (currentTheme == null || board == null)
            return;

        var gridCells = board.GetComponentsInChildren<GridCell>();
        foreach (var cell in gridCells)
        {
            if (currentTheme.gridCellSprite != null)
                cell.ApplyTheme(currentTheme.gridCellSprite, currentTheme.gridCellEmptyColor);
            else
                cell.ApplyThemeColor(currentTheme.gridCellEmptyColor);

            if (currentTheme.previewClearParticlePrefab != null)
                cell.SetPreviewClearParticlePrefab(currentTheme.previewClearParticlePrefab);
        }
    }

    /// <summary>
    /// Call whenever the player fully clears the grid. Advances to the next theme
    /// in the list each time (wrapping around), instead of switching by score.
    /// </summary>
    public void TriggerBoardCleared()
    {
        if (themes == null || themes.Length == 0)
            return;

        int nextIndex = (currentThemeIndex + 1) % themes.Length;
        SwitchToTheme(nextIndex);
    }

    public void SwitchToTheme(int index)
    {
        if (themes == null || index < 0 || index >= themes.Length)
            return;

        if (currentThemeIndex == index)
            return;

        currentThemeIndex = index;
        ApplyTheme(themes[index], instant: false);
    }

    public void ResetToDefault()
    {
        currentThemeIndex = -1;
        ApplyTheme(defaultTheme, instant: false);
    }

    // ===== Internal =====

    private void ApplyTheme(ThemeData theme, bool instant)
    {
        if (theme == null)
            return;

        currentTheme = theme;
        float duration = instant ? 0f : theme.transitionDuration;

        transitionSequence?.Kill();
        transitionSequence = DOTween.Sequence();

        // Camera background color
        if (mainCamera != null)
        {
            if (instant)
                mainCamera.backgroundColor = theme.cameraBackgroundColor;
            else
                transitionSequence.Join(DOTween.To(() => mainCamera.backgroundColor,
                    c => mainCamera.backgroundColor = c,
                    theme.cameraBackgroundColor, duration));
        }

        // Background SpriteRenderer
        if (backgroundRenderer != null)
        {
            if (theme.backgroundSprite != null)
                backgroundRenderer.sprite = theme.backgroundSprite;

            if (instant)
                backgroundRenderer.color = theme.backgroundTint;
            else
                transitionSequence.Join(backgroundRenderer.DOColor(theme.backgroundTint, duration));
        }

        // Background UI Image
        if (backgroundImage != null)
        {
            if (theme.backgroundSprite != null)
                backgroundImage.sprite = theme.backgroundSprite;

            if (instant)
                backgroundImage.color = theme.backgroundTint;
            else
                transitionSequence.Join(backgroundImage.DOColor(theme.backgroundTint, duration));
        }

        // Grid cells — apply sprite and/or color
        if (gridBoard != null)
        {
            var gridCells = gridBoard.GetComponentsInChildren<GridCell>();
            foreach (var cell in gridCells)
            {
                // Check if cell is occupied
                bool isOccupied = gridBoard.IsOccupied(cell.gridPos);
                Color targetColor = isOccupied ? theme.gridCellOccupiedColor : theme.gridCellEmptyColor;
                
                if (theme.gridCellSprite != null)
                    cell.ApplyTheme(theme.gridCellSprite, targetColor);
                else
                    cell.ApplyThemeColor(targetColor);

                if (theme.previewClearParticlePrefab != null)
                    cell.SetPreviewClearParticlePrefab(theme.previewClearParticlePrefab);
            }
        }

        // UI images tint
        foreach (var img in uiImages)
        {
            if (img == null) continue;
            if (instant)
                img.color = theme.uiPrimaryColor;
            else
                transitionSequence.Join(img.DOColor(theme.uiPrimaryColor, duration));
        }

        // Notify all ThemeBlock components (shapes + placed blocks)
        OnThemeChanged?.Invoke(theme, instant);
    }
}
