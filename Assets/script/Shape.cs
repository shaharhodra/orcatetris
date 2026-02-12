using UnityEngine;

[System.Serializable]
public class ShapeOffset
{
    public Vector2Int[] cells;
}

public class Shape : MonoBehaviour
{
    public ShapeOffset shapeData;

    [Header("Gizmo Settings")]
    [SerializeField] private Color gizmoColor = Color.yellow;
    [SerializeField] private float gizmoCellSize = 1f;

    private void OnDrawGizmosSelected()
    {
        if (shapeData == null || shapeData.cells == null)
            return;

        Gizmos.color = gizmoColor;

        // מצייר ריבוע קטן לכל תא של הצורה, ביחס לפיבוט של האובייקט
        Gizmos.matrix = transform.localToWorldMatrix;

        foreach (var cell in shapeData.cells)
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
