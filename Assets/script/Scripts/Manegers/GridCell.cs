using UnityEngine;

public class GridCell : MonoBehaviour
{
    public Vector2Int gridPos;
    public bool occupied;
    public bool hasShapeOver;
    public bool previewClear;

    [SerializeField] private float normalAlpha = 1f;
    [SerializeField] private float hoverAlpha = 0.5f;
    [SerializeField] private float previewClearAlpha = 0.7f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color previewClearColor = Color.yellow;
    [SerializeField] private float previewClearEmissionIntensity = 2f;

    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private BoxCollider2D _boxCollider;

    private Sprite originalSprite;
    private Vector3 originalSpriteScale;

    private void Awake()
    {
        if (_sprite != null)
        {
            originalSprite = _sprite.sprite;
            originalSpriteScale = _sprite.transform.localScale;
        }
        SyncBoxColliderToSprite();
        UpdateVisual();
    }

    private void OnValidate()
    {
        SyncBoxColliderToSprite();
        UpdateVisual();
    }

    private void SyncBoxColliderToSprite()
    {
        if (_boxCollider == null || _sprite == null || _sprite.sprite == null)
            return;

        // Use sprite's local bounds directly — avoids world-space transform errors
        Bounds localBounds = _sprite.sprite.bounds;
        _boxCollider.size = new Vector2(
            Mathf.Abs(localBounds.size.x * _sprite.transform.localScale.x),
            Mathf.Abs(localBounds.size.y * _sprite.transform.localScale.y));
        _boxCollider.offset = new Vector2(
            localBounds.center.x * _sprite.transform.localScale.x,
            localBounds.center.y * _sprite.transform.localScale.y);
    }

    public void ApplyTheme(Sprite sprite, Color color)
    {
        if (_sprite != null && sprite != null)
        {
            _sprite.sprite = sprite;

            // Compensate scale so cell keeps same visual size
            if (originalSprite != null)
            {
                Vector2 origSize = originalSprite.bounds.size;
                Vector2 newSize = sprite.bounds.size;

                if (newSize.x > 0f && newSize.y > 0f)
                {
                    _sprite.transform.localScale = new Vector3(
                        originalSpriteScale.x * origSize.x / newSize.x,
                        originalSpriteScale.y * origSize.y / newSize.y,
                        originalSpriteScale.z);
                }
            }

            SyncBoxColliderToSprite();
        }

        normalColor = color;
        UpdateVisual();
    }

    public void ApplyThemeColor(Color color)
    {
        normalColor = color;
        UpdateVisual();
    }

    public void SetOccupied(bool value)
    {
        occupied = value;
        UpdateVisual();
    }

    public void SetShapeOver(bool value)
    {
        hasShapeOver = value;
        UpdateVisual();
    }

    public void SetPreviewClear(bool value)
    {
        previewClear = value;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        float targetAlpha;

        if (previewClear)
        {
            targetAlpha = previewClearAlpha;
        }
        else if (!occupied && hasShapeOver)
        {
            targetAlpha = hoverAlpha;
        }
        else
        {
            targetAlpha = normalAlpha;
        }

        if (_sprite == null)
            return;

        // בוחרים צבע בסיס לפי מצב התא
        Color baseColor;
        if (previewClear)
        {
            // הכפלת הצבע ב-intensity ליצירת אפקט emission/glow (HDR)
            baseColor = previewClearColor * previewClearEmissionIntensity;
        }
        else
        {
            baseColor = normalColor;
        }
        baseColor.a = targetAlpha;
        _sprite.color = baseColor;
    }
}