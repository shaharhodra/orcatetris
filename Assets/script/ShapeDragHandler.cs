using UnityEngine;
using UnityEngine.EventSystems;

public class ShapeDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private GridBoard board;
    [SerializeField] private GridPlacer boardPlacer;
    [SerializeField] private Shape shape;
    [SerializeField] private float fingerOffsetY = 1.0f;
    [SerializeField] private float validAlpha = 0.8f;
    [SerializeField] private float invalidAlpha = 0.3f;

    private Camera mainCam;
    private Vector3 startPos;
    private Vector3 dragOffset;

    private void Awake()
    {
        mainCam = Camera.main;
        startPos = transform.position;
    }

    public void IBeginDragHandler_OnBeginDrag(PointerEventData eventData) {}

    public void OnBeginDrag(PointerEventData eventData)
    {
        float z = Mathf.Abs(transform.position.z - mainCam.transform.position.z);
        Vector3 worldPos = mainCam.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, z));
        dragOffset = transform.position - worldPos;
        dragOffset += Vector3.up * fingerOffsetY;
    }

    public void OnDrag(PointerEventData eventData)
    {
        float z = Mathf.Abs(transform.position.z - mainCam.transform.position.z);
        Vector3 worldPos = mainCam.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, z));
        Vector3 targetPos = worldPos + dragOffset;
        transform.position = targetPos;

        Vector2Int cell = board.WorldToGrid(targetPos);
        bool canPlace = boardPlacer.CanPlaceShape(shape, cell);
        SetAlpha(canPlace ? validAlpha : invalidAlpha);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float z = Mathf.Abs(transform.position.z - mainCam.transform.position.z);
        Vector3 worldPos = mainCam.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, z));
        Vector3 targetPos = worldPos + dragOffset;
        Vector2Int cell = board.WorldToGrid(targetPos);

        bool canPlace = boardPlacer.CanPlaceShape(shape, cell);
        if (canPlace)
        {
            boardPlacer.PlaceShape(shape, cell);
            SetAlpha(1f);
        }
        else
        {
            transform.position = startPos;
            SetAlpha(1f);
        }
    }

    private void SetAlpha(float alpha)
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var r in renderers)
        {
            var c = r.color;
            c.a = alpha;
            r.color = c;
        }
    }
}
