using UnityEngine;

public class GridCell : MonoBehaviour
{
    public Vector2Int gridPos;
    public bool occupied;

    public void SetOccupied(bool value)
    {
        occupied = value;
        // כאן אפשר בעתיד לשנות צבע / אפקט
    }
}