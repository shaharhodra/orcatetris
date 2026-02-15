using UnityEngine;
using UnityEngine.EventSystems;

public class ShapeDragHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
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
    private bool pointerDown;
    private bool beganDrag;

    private void Awake()
    {
        mainCam = Camera.main;
        startPos = transform.position;
    }

    public void Init(GridBoard newBoard, GridPlacer newBoardPlacer, Shape newShape)
    {
        board = newBoard;
        boardPlacer = newBoardPlacer;
        shape = newShape;

        if (mainCam == null)
            mainCam = Camera.main;
    }

    public void IBeginDragHandler_OnBeginDrag(PointerEventData eventData) {}

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isPlaced)
            return;

        pointerDown = true;
        beganDrag = false;

        if (mainCam == null)
            mainCam = Camera.main;

        if (mainCam == null)
            return;

        float z = Mathf.Abs(transform.position.z - mainCam.transform.position.z);
        Vector3 worldPos = mainCam.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, z));

        dragOffset = transform.position - worldPos;
        dragOffset.y = minFingerOffsetY;
        startPointerY = eventData.position.y;

        transform.position = worldPos + dragOffset;

        UpdatePlacementFeedback();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced)
            return;

        beganDrag = true;

        if (mainCam == null)
            mainCam = Camera.main;

        if (mainCam == null)
            return;

        float z = Mathf.Abs(transform.position.z - mainCam.transform.position.z);
        Vector3 worldPos = mainCam.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, z));
        // נשמור את ההפרש בין מיקום הצורה למיקום האצבע בתחילת הגרירה,
        // אבל נכפה מרחק מינימלי בציר Y כדי ליצור "קפיצה" קטנה מעל האצבע
        dragOffset = transform.position - worldPos;
        dragOffset.y = minFingerOffsetY;
        startPointerY = eventData.position.y;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isPlaced)
            return;

        if (mainCam == null)
            mainCam = Camera.main;

        if (mainCam == null)
            return;

        float z = Mathf.Abs(transform.position.z - mainCam.transform.position.z);
        // worldPos = מיקום מתחת לאצבע בעולם
        Vector3 worldPos = mainCam.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, z));
        // חישוב אוף־סט נוסף לגובה מעל האצבע – גדל ככל שגוררים יותר למעלה
        float deltaY = eventData.position.y - startPointerY;
        float t = Mathf.Clamp01(deltaY / verticalOffsetRangePixels);
        float dynamicOffsetY = Mathf.Lerp(0f, maxFingerOffsetY - minFingerOffsetY, t);

        // מיקום הצורה נקבע יחסית לאצבע, בתוספת אוף־סט התחלתי + אוף־סט דינמי בציר Y
        Vector3 targetPos = worldPos + dragOffset + Vector3.up * dynamicOffsetY;
        transform.position = targetPos;

        UpdatePlacementFeedback();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isPlaced)
            return;

        pointerDown = false;

        if (beganDrag)
            return;

        transform.position = startPos;
        SetAlpha(1f);

        if (board != null)
            board.ClearHover();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isPlaced)
            return;

        pointerDown = false;

        // בסיום הגרירה נשתמש ישירות במיקום הצורה כדי לקבוע את התא
        Vector2Int cell = board.WorldToGrid((Vector2)transform.position);

        bool canPlace = boardPlacer.CanPlaceShape(shape, cell);
        if (canPlace)
        {
            boardPlacer.PlaceShape(shape, cell);
            SetAlpha(1f);

            board.ClearHover();

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

            board.ClearHover();
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

    private void UpdatePlacementFeedback()
    {
        if (board == null || boardPlacer == null || shape == null)
            return;

        Vector2Int cell = board.WorldToGrid((Vector2)transform.position);
        bool canPlace = boardPlacer.CanPlaceShape(shape, cell);
        SetAlpha(canPlace ? validAlpha : invalidAlpha);

        if (canPlace)
        {
            var offsets = shape.GetCells(board.cellSize);
            var hover = new System.Collections.Generic.List<Vector2Int>(offsets.Length);
            foreach (var o in offsets)
                hover.Add(cell + o);
            board.SetHoverCells(hover);
        }
        else
        {
            board.ClearHover();
        }
    }
}
