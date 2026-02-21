using UnityEngine;

public class GridCell : MonoBehaviour
{
    public Vector2Int gridPos;
    public bool occupied;
    public bool hasShapeOver;

    [SerializeField] private float normalAlpha = 1f;
    [SerializeField] private float hoverAlpha = 0.5f;
    [SerializeField] private bool useTriggerHover;
    [SerializeField] private SpriteRenderer _sprite;
 [SerializeField] private Collider2D _colider;
 [SerializeField] private BoxCollider2D _boxCollider;
 

    private int shapeOverCount;
   
    private Collider2D triggerCollider;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        SyncBoxColliderToSprite();
        UpdateVisual();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
        SyncBoxColliderToSprite();
        UpdateVisual();
    }

  
    private void EnsureTriggerCollider()
    {
       
       

        var all = GetComponentsInChildren<Collider2D>(true);
        foreach (var c in all)
        {
            if (c == null)
                continue;

            c.isTrigger = true;
            c.enabled = (_colider != null && c == _colider);
        }
    }

    private void SyncBoxColliderToSprite()
    {
       // var box = GetComponent<BoxCollider2D>();
        //if (box == null)
          //  box = GetComponentInChildren<BoxCollider2D>();

      

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

       
            var c = _sprite.color;
            c.a = targetAlpha;
            _sprite.color = c;
        
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