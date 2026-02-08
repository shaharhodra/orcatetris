using UnityEngine;

public class GridPlacer : MonoBehaviour
{
    [SerializeField] private GridBoard board;

    public bool CanPlaceShape(Shape shape, Vector2Int targetCell)
    {
        foreach (var offset in shape.shapeData.cells)
        {
            Vector2Int cell = targetCell + offset;

            if (!board.IsInside(cell))
                return false;

            if (board.IsOccupied(cell))
                return false;
        }

        return true;
    }

    public void PlaceShape(Shape shape, Vector2Int targetCell)
    {
        foreach (var offset in shape.shapeData.cells)
        {
            Vector2Int cell = targetCell + offset;
            board.SetOccupied(cell, true);
        }

        shape.transform.position = board.GridToWorld(targetCell);
    }
}
