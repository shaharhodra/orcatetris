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

        if (theme.blockSprite != null)
        {
            sr.sprite = theme.blockSprite;

            // Compensate scale so visual size stays consistent with original prefab sprite
            if (originalSprite != null)
            {
                Vector2 origSize = originalSprite.bounds.size;
                Vector2 newSize = theme.blockSprite.bounds.size;

                if (newSize.x > 0f && newSize.y > 0f)
                {
                    transform.localScale = new Vector3(
                        originalScale.x * origSize.x / newSize.x,
                        originalScale.y * origSize.y / newSize.y,
                        originalScale.z);
                }
            }
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
