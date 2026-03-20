using UnityEngine;

public class GridCell : MonoBehaviour
{
    public Vector2Int gridPos;
    public bool occupied;
    public bool hasShapeOver;

    [SerializeField] private float normalAlpha = 1f;
    [SerializeField] private float hoverAlpha = 0.5f;
    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private BoxCollider2D _boxCollider;

    private void Awake()
    {
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

        var t = _boxCollider.transform;

        Vector3 worldSize = _sprite.bounds.size;
        Vector3 localSize3 = t.InverseTransformVector(worldSize);
        _boxCollider.size = new Vector2(Mathf.Abs(localSize3.x), Mathf.Abs(localSize3.y));

        Vector3 localCenter3 = t.InverseTransformPoint(
            _sprite.bounds.center);
        _boxCollider.offset = new Vector2(localCenter3.x, localCenter3.y);
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

    private void UpdateVisual()
    {
        float targetAlpha = (!occupied && hasShapeOver) ? hoverAlpha : normalAlpha;

        if (_sprite == null)
            return;

        var c = _sprite.color;
        c.a = targetAlpha;
        _sprite.color = c;
    }
}