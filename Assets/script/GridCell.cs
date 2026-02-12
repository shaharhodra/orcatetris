using UnityEngine;

public class GridCell : MonoBehaviour
{
    public Vector2Int gridPos;
    public bool occupied;
    public bool hasShapeOver;

    [SerializeField] private float normalAlpha = 1f;
    [SerializeField] private float hoverAlpha = 0.5f;
    [SerializeField] private bool useTriggerHover;

    private int shapeOverCount;
    private SpriteRenderer[] spriteRenderers;
    private Collider2D triggerCollider;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        CacheRenderers();
        SyncBoxColliderToSprite();
        UpdateVisual();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
        CacheRenderers();
        SyncBoxColliderToSprite();
        UpdateVisual();
    }

    private void CacheRenderers()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void EnsureTriggerCollider()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = GetComponentInChildren<SpriteRenderer>();

        Collider2D desired = null;
        if (sr != null)
            desired = sr.GetComponent<Collider2D>();

        if (desired == null)
            desired = GetComponent<Collider2D>();
        if (desired == null)
            desired = GetComponentInChildren<Collider2D>();

        triggerCollider = desired;

        var all = GetComponentsInChildren<Collider2D>(true);
        foreach (var c in all)
        {
            if (c == null)
                continue;

            c.isTrigger = true;
            c.enabled = (triggerCollider != null && c == triggerCollider);
        }
    }

    private void SyncBoxColliderToSprite()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box == null)
            box = GetComponentInChildren<BoxCollider2D>();

        var sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = GetComponentInChildren<SpriteRenderer>();

        if (box == null || sr == null || sr.sprite == null)
            return;

        var t = box.transform;

        Vector3 worldSize = sr.bounds.size;
        Vector3 localSize3 = t.InverseTransformVector(worldSize);
        box.size = new Vector2(Mathf.Abs(localSize3.x), Mathf.Abs(localSize3.y));

        Vector3 localCenter3 = t.InverseTransformPoint(sr.bounds.center);
        box.offset = new Vector2(localCenter3.x, localCenter3.y);
    }

    public void SetOccupied(bool value)
    {
        occupied = value;
        // כאן אפשר בעתיד לשנות צבע / אפקט
        UpdateVisual();
    }

    public void SetShapeOver(bool value)
    {
        hasShapeOver = value;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        float targetAlpha = (!occupied && hasShapeOver) ? hoverAlpha : normalAlpha;

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            CacheRenderers();

        if (spriteRenderers == null)
            return;

        foreach (var sr in spriteRenderers)
        {
            if (sr == null)
                continue;

            var c = sr.color;
            c.a = targetAlpha;
            sr.color = c;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!useTriggerHover)
            return;

        if (other == null)
            return;

        if (other.GetComponentInParent<Shape>() == null)
            return;

        shapeOverCount++;
        if (shapeOverCount == 1)
            SetShapeOver(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!useTriggerHover)
            return;

        if (other == null)
            return;

        if (other.GetComponentInParent<Shape>() == null)
            return;

        shapeOverCount = Mathf.Max(0, shapeOverCount - 1);
        if (shapeOverCount == 0)
            SetShapeOver(false);
    }

    private void OnDrawGizmosSelected()
    {
        if (triggerCollider == null)
            return;

        var b = triggerCollider.bounds;
        Gizmos.color = hasShapeOver ? Color.green : Color.cyan;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}