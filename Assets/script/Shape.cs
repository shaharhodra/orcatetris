using UnityEngine;

[System.Serializable]
public class ShapeOffset
{
    public Vector2Int[] cells;
}

public class Shape : MonoBehaviour
{
    public ShapeOffset shapeData;

    private void Reset()
    {
        EnsureRigidbody2D();
    }

    private void Awake()
    {
        EnsureRigidbody2D();
    }

    private void OnValidate()
    {
        EnsureRigidbody2D();
    }

    private void EnsureRigidbody2D()
    {
        var rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
    }

    public Vector2Int[] GetCells(float cellSize)
    {
        int childCount = transform.childCount;
        if (childCount > 0)
        {
            var result = new System.Collections.Generic.List<Vector2Int>(childCount);
            for (int i = 0; i < childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.GetComponent<Collider2D>() == null)
                    continue;

                Vector3 lp = child.localPosition;
                int x = Mathf.RoundToInt(lp.x / cellSize);
                int y = Mathf.RoundToInt(lp.y / cellSize);
                var cell = new Vector2Int(x, y);
                if (!result.Contains(cell))
                    result.Add(cell);
            }

            if (result.Count > 0)
                return result.ToArray();
        }

        if (shapeData == null)
            return System.Array.Empty<Vector2Int>();

        return shapeData.cells ?? System.Array.Empty<Vector2Int>();
    }

    [Header("Gizmo Settings")]
    [SerializeField] private Color gizmoColor = Color.yellow;
    [SerializeField] private float gizmoCellSize = 1f;

    private void OnDrawGizmosSelected()
    {
        var cells = GetCells(gizmoCellSize);
        if (cells == null || cells.Length == 0)
            return;

        Gizmos.color = gizmoColor;

        // מצייר ריבוע קטן לכל תא של הצורה, ביחס לפיבוט של האובייקט
        Gizmos.matrix = transform.localToWorldMatrix;

        foreach (var cell in cells)
        {
            Vector3 center = new Vector3(
                (cell.x + 0.5f) * gizmoCellSize,
                (cell.y + 0.5f) * gizmoCellSize,
                0f
            );

            Gizmos.DrawWireCube(center, Vector3.one * gizmoCellSize * 0.9f);
        }
    }
}
