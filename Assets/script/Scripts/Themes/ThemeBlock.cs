using UnityEngine;
using DG.Tweening;

// Attach this to every block prefab (the child GameObjects of a Shape).
// It auto-registers with ThemeManager and updates sprite/color on theme change.
public class ThemeBlock : MonoBehaviour
{
    private SpriteRenderer sr;
    private Sprite originalSprite;
    private Vector3 originalScale;

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
        ThemeManager.OnThemeChanged += ApplyTheme;

        if (ThemeManager.instance != null && ThemeManager.instance.CurrentTheme != null)
            ApplyTheme(ThemeManager.instance.CurrentTheme, instant: true);
    }

    private void OnDisable()
    {
        ThemeManager.OnThemeChanged -= ApplyTheme;
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
            // Only compensate if we have a valid reference and sprites differ in size
            if (originalSprite != null && originalSprite != theme.blockSprite)
            {
                Vector2 origSize = originalSprite.bounds.size;
                Vector2 newSize  = theme.blockSprite.bounds.size;

                if (origSize.x > 0f && newSize.x > 0f)
                {
                    transform.localScale = new Vector3(
                        originalScale.x * origSize.x / newSize.x,
                        originalScale.y * origSize.y / newSize.y,
                        originalScale.z);
                }
            }
            else if (originalSprite == theme.blockSprite)
            {
                transform.localScale = originalScale;
            }

            sr.sprite = theme.blockSprite;
        }
        else
        {
            // No sprite in theme — revert to original
            if (originalSprite != null)
            {
                sr.sprite = originalSprite;
                transform.localScale = originalScale;
            }
        }

        float duration = instant ? 0f : theme.transitionDuration;
        if (instant)
            sr.color = theme.blockColor;
        else
            sr.DOColor(theme.blockColor, duration);
    }
}
