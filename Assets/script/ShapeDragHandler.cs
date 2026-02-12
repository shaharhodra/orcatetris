using UnityEngine;
using UnityEngine.EventSystems;

public class ShapeDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private GridBoard board;
    [SerializeField] private GridPlacer boardPlacer;
    [SerializeField] private Shape shape;
    [SerializeField] private float minFingerOffsetY = 0.5f;
    [SerializeField] private float maxFingerOffsetY = 3.0f;
    [SerializeField] private float verticalOffsetRangePixels = 200f; // כמה גרירת מסך דרושה כדי להגיע למקסימום
    [SerializeField] private float validAlpha = 0.8f;
    [SerializeField] private float invalidAlpha = 0.3f;

    private Camera mainCam;
    private Vector3 startPos;
    private Vector3 dragOffset;
    private bool isPlaced;
    private float startPointerY;

    private void Awake()
    {
        mainCam = Camera.main;
        startPos = transform.position;
    }

    public void IBeginDragHandler_OnBeginDrag(PointerEventData eventData) {}

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced)
            return;

        float z = Mathf.Abs(transform.position.z - mainCam.transform.position.z);
        Vector3 worldPos = mainCam.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, z));
        // נשמור את ההפרש בין מיקום הצורה למיקום האצבע בתחילת הגרירה
        dragOffset = transform.position - worldPos;
        startPointerY = eventData.position.y;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isPlaced)
            return;

        float z = Mathf.Abs(transform.position.z - mainCam.transform.position.z);
        // worldPos = מיקום מתחת לאצבע בעולם
        Vector3 worldPos = mainCam.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, z));
        // חישוב אוף־סט לגובה מעל האצבע – גדל ככל שגוררים יותר למעלה
        float deltaY = eventData.position.y - startPointerY;
        float t = Mathf.Clamp01(deltaY / verticalOffsetRangePixels);
        float dynamicOffsetY = Mathf.Lerp(minFingerOffsetY, maxFingerOffsetY, t);

        // מיקום הצורה נקבע יחסית לאצבע, בתוספת אוף־סט לגובה
        Vector3 targetPos = worldPos + dragOffset + Vector3.up * dynamicOffsetY;
        transform.position = targetPos;

        // לחישוב התא בגריד נשתמש במיקום של הצורה בפועל (transform.position)
        Vector2Int cell = board.WorldToGrid((Vector2)transform.position);
        bool canPlace = boardPlacer.CanPlaceShape(shape, cell);
        SetAlpha(canPlace ? validAlpha : invalidAlpha);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isPlaced)
            return;

        // בסיום הגרירה נשתמש ישירות במיקום הצורה כדי לקבוע את התא
        Vector2Int cell = board.WorldToGrid((Vector2)transform.position);

        bool canPlace = boardPlacer.CanPlaceShape(shape, cell);
        if (canPlace)
        {
            boardPlacer.PlaceShape(shape, cell);
            SetAlpha(1f);

            isPlaced = true;

            // אופציונלי: לכבות קוליידר כדי שלא יתפסו עוד דרגים
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.enabled = false;
            }
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
