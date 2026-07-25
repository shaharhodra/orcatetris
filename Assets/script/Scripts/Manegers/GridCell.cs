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

    [Header("Preview Clear Particles")]
    [Tooltip("Optional placeholder — assign a particle prefab (can contain several nested ParticleSystems) to play while this cell is in preview-clear state. Leave empty to disable.")]
    [SerializeField] private GameObject previewClearParticlePrefab;

    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private BoxCollider2D _boxCollider;

    private Sprite originalSprite;
    private Vector3 originalSpriteScale;

    private GameObject previewClearParticleInstance;
    private ParticleSystem[] previewClearParticleSystems;

    private void Awake()
    {
        if (_sprite != null)
        {
            originalSprite = _sprite.sprite;
            originalSpriteScale = _sprite.transform.localScale;
        }

        InstantiatePreviewClearParticles();

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

    /// <summary>
    /// Swaps the preview-clear particle prefab (e.g. when a theme defines its own). Keeps
    /// the cell's current preview-clear state (playing/stopped) across the swap.
    /// </summary>
    public void SetPreviewClearParticlePrefab(GameObject prefab)
    {
        if (prefab == previewClearParticlePrefab)
            return;

        previewClearParticlePrefab = prefab;

        if (previewClearParticleInstance != null)
        {
            Destroy(previewClearParticleInstance);
            previewClearParticleInstance = null;
            previewClearParticleSystems = null;
        }

        InstantiatePreviewClearParticles();

        if (previewClearParticleInstance != null && previewClear)
        {
            previewClearParticleInstance.SetActive(true);
            foreach (var ps in previewClearParticleSystems)
                ps.Play();
        }
    }

    private void InstantiatePreviewClearParticles()
    {
        if (previewClearParticlePrefab == null)
            return;

        previewClearParticleInstance = Instantiate(previewClearParticlePrefab, transform);
        previewClearParticleInstance.transform.localPosition = Vector3.zero;
        previewClearParticleSystems = previewClearParticleInstance.GetComponentsInChildren<ParticleSystem>(true);
        previewClearParticleInstance.SetActive(false);
    }

    public void ApplyTheme(Sprite sprite, Color color)
    {
        if (_sprite != null && sprite != null)
        {
            _sprite.sprite = sprite;
            SyncBoxColliderToSprite();
        }

        normalColor = color;
        // Update normalAlpha to match the theme color's alpha
        normalAlpha = color.a;
        
        // Also update the sprite directly to ensure theme alpha is applied immediately
        if (_sprite != null)
        {
            Color finalColor = normalColor;
            finalColor.a = normalAlpha;
            _sprite.color = finalColor;
        }
    }

    public void ApplyThemeColor(Color color)
    {
        normalColor = color;
        // Update normalAlpha to match the theme color's alpha
        normalAlpha = color.a;
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

        if (previewClearParticleInstance == null)
            return;

        if (value)
        {
            previewClearParticleInstance.SetActive(true);
            foreach (var ps in previewClearParticleSystems)
                ps.Play();
        }
        else
        {
            foreach (var ps in previewClearParticleSystems)
                ps.Stop();
            previewClearParticleInstance.SetActive(false);
        }
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