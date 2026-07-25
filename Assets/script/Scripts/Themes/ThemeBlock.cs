using UnityEngine;
using DG.Tweening;

// Attach this to every block prefab (the child GameObjects of a Shape).
// It auto-registers with ThemeManager and updates sprite/color on theme change.
public class ThemeBlock : MonoBehaviour
{
    private SpriteRenderer sr;
    private Sprite originalSprite;
    private Vector3 originalScale;
    private bool isPlacedOnGrid;

    /// <summary>
    /// Call this after placing the block on the grid so the theme uses squareColor instead of blockColor.
    /// </summary>
    public void MarkAsPlaced()
    {
        isPlacedOnGrid = true;
        // Re-apply current theme with placed color
        if (ThemeManager.instance != null && ThemeManager.instance.CurrentTheme != null)
            ApplyTheme(ThemeManager.instance.CurrentTheme, instant: true);
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            originalSprite = sr.sprite;
            originalScale = transform.localScale;
        }
    }

    private void OnEnable()
    {
        ThemeManager.OnThemeChanged += HandleThemeChanged;

        if (ThemeManager.instance != null && ThemeManager.instance.CurrentTheme != null)
            ApplyTheme(ThemeManager.instance.CurrentTheme, instant: true);
    }

    private void OnDisable()
    {
        ThemeManager.OnThemeChanged -= HandleThemeChanged;
    }

    // While still sitting in the shape tray (not yet placed), a block keeps
    // whatever theme it spawned with — a full-board-clear theme switch shouldn't
    // visibly change a shape the player is currently looking at/deciding whether
    // to place. MarkAsPlaced() re-applies the current theme once it lands on the
    // grid, so the swap still happens, just deferred until the block is placed.
    private void HandleThemeChanged(ThemeData theme, bool instant)
    {
        if (!isPlacedOnGrid)
            return;

        ApplyTheme(theme, instant);
    }

    private void ApplyTheme(ThemeData theme, bool instant)
    {
        if (sr == null || theme == null)
            return;

        // Lazy-capture originals the first time we run, after sprite is guaranteed set
        if (originalSprite == null && sr.sprite != null)
        {
            originalSprite = sr.sprite;
            originalScale  = transform.localScale;
        }

        if (theme.blockSprite != null)
        {
            sr.sprite = theme.blockSprite;
        }
        else
        {
            // No sprite in theme — revert to original
            if (originalSprite != null)
                sr.sprite = originalSprite;
        }

        // Choose color based on whether this block is placed on grid or still in shape tray
        Color targetColor = theme.blockColor;
        if (isPlacedOnGrid)
        {
            // Use squareColor if it has non-zero alpha, otherwise fallback to blockColor
            if (theme.squareColor.a > 0f)
                targetColor = theme.squareColor;
        }

        float duration = instant ? 0f : theme.transitionDuration;
        if (instant)
            sr.color = targetColor;
        else
            sr.DOColor(targetColor, duration);
    }
}
