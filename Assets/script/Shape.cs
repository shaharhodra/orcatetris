using UnityEngine;

[System.Serializable]
public class ShapeOffset
{
    public Vector2Int[] cells;
}

public class Shape : MonoBehaviour
{
    public ShapeOffset shapeData;
}
