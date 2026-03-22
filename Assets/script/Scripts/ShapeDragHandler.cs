using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public class ShapeDragHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private GridBoard board;
    [SerializeField] private GridPlacer boardPlacer;
    [SerializeField] private Shape shape;
    [Header("Ghost Preview")]
    [SerializeField] private Shape ghostPrefab; // אופציונלי: אם לא הוגדר, נשתמש בעותק של הצורה עצמה
    [SerializeField] private float ghostAlpha = 0.35f;
    [SerializeField] private float minFingerOffsetX = 0f;
    [SerializeField] private float maxFingerOffsetX = 1.5f;
    [SerializeField] private float minFingerOffsetY = 0.5f;
    [SerializeField] private float maxFingerOffsetY = 3.0f;
    [SerializeField] private float horizontalOffsetRangePixels = 200f;
    [SerializeField] private float verticalOffsetRangePixels = 200f; // כמה גרירת מסך דרושה כדי להגיע למקסימום
    [SerializeField] private float validAlpha = 0.8f;
    [SerializeField] private float invalidAlpha = 0.3f;

    [Header("Press Scale")]
    [SerializeField] private bool usePressScale = true;
    [SerializeField] private float idleScaleFactor = 0.7f;      // גודל במנוחה (קטן)
    [SerializeField] private float pressedScaleFactor = 1.0f;    // 1.0 = גודל מקורי
    [SerializeField] private float pressScaleDuration = 0.15f;

    private Camera mainCam;
    private Vector3 startPos;
    private Vector3 dragOffset;
    private bool isPlaced;
    private float startPointerX;
    private float startPointerY;
    private bool pointerDown;
    private bool beganDrag;

    private Vector3 originalScale;
    private Tween scaleTween;

    private Shape currentGhost;

    private void Awake()
    {
        mainCam = Camera.main;
        startPos = transform.position;
        originalScale = transform.localScale;

        // צורה מתחילה קטנה יותר
        if (usePressScale && idleScaleFactor > 0f && idleScaleFactor < 1.5f)
        {
            transform.localScale = originalScale * idleScaleFactor;
        }
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

        if (SoundManager.instance != null)
        {
            SoundManager.instance.PlayClickShape();
        }

        pointerDown = true;
        beganDrag = false;

        if (usePressScale)
        {
            scaleTween?.Kill();
            // גדילה לגודל המקורי (או מעט יותר, לפי pressedScaleFactor)
            Vector3 targetScale = originalScale * pressedScaleFactor;
            scaleTween = transform.DOScale(targetScale, pressScaleDuration).SetEase(Ease.OutBack);
        }

        if (mainCam == null)
            mainCam = Camera.main;

        if (mainCam == null)
            return;

        float z = Mathf.Abs(transform.position.z - mainCam.transform.position.z);
        Vector3 worldPos = mainCam.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, z));

        dragOffset = transform.position - worldPos;
        dragOffset.x = minFingerOffsetX;
        dragOffset.y = minFingerOffsetY;
        startPointerX = eventData.position.x;
        startPointerY = eventData.position.y;

        transform.position = worldPos + dragOffset;

        EnsureGhostCreated();
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
        dragOffset.x = minFingerOffsetX;
        dragOffset.y = minFingerOffsetY;
        startPointerX = eventData.position.x;
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
        float deltaX = eventData.position.x - startPointerX;
        float tx = Mathf.Clamp01(Mathf.Abs(deltaX) / horizontalOffsetRangePixels);
        float dynamicOffsetX = Mathf.Lerp(0f, maxFingerOffsetX - minFingerOffsetX, tx) * Mathf.Sign(deltaX);
        // חישוב אוף־סט נוסף לגובה מעל האצבע – גדל ככל שגוררים יותר למעלה
        float deltaY = eventData.position.y - startPointerY;
        float t = Mathf.Clamp01(deltaY / verticalOffsetRangePixels);
        float dynamicOffsetY = Mathf.Lerp(0f, maxFingerOffsetY - minFingerOffsetY, t);

        // מיקום הצורה נקבע יחסית לאצבע, בתוספת אוף־סט התחלתי + אוף־סט דינמי בציר Y
        Vector3 targetPos = worldPos + dragOffset + Vector3.right * dynamicOffsetX + Vector3.up * dynamicOffsetY;
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

        if (usePressScale)
        {
            scaleTween?.Kill();
            // חזרה לגודל מנוחה קטן
            Vector3 idleScale = originalScale * idleScaleFactor;
            scaleTween = transform.DOScale(idleScale, pressScaleDuration).SetEase(Ease.OutQuad);
        }

        HideAndDestroyGhost();
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

            if (SoundManager.instance != null)
            {
                SoundManager.instance.PlayPlaceShape();
            }

            SetAlpha(1f);

            HideAndDestroyGhost();

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

            if (usePressScale)
            {
                scaleTween?.Kill();
                // חזרה לגודל מנוחה קטן
                Vector3 idleScale = originalScale * idleScaleFactor;
                scaleTween = transform.DOScale(idleScale, pressScaleDuration).SetEase(Ease.OutQuad);
            }

            HideAndDestroyGhost();
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

        UpdateGhostPreview(cell, canPlace);
    }

    private void EnsureGhostCreated()
    {
        if (currentGhost != null || board == null || shape == null)
            return;

        Shape source = ghostPrefab != null ? ghostPrefab : shape;

        currentGhost = Instantiate(source, board.transform);
        
        // Use the target scale (full size) instead of current scale
        Vector3 targetScale = usePressScale ? originalScale * pressedScaleFactor : originalScale;
        currentGhost.transform.localScale = targetScale;

        // לכבות קוליידרים על ה-Ghost כדי שלא ישפיעו על פיזיקה / קלט
        var colliders = currentGhost.GetComponentsInChildren<Collider2D>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        // להפוך את ה-Ghost לשקוף יותר
        var renderers = currentGhost.GetComponentsInChildren<SpriteRenderer>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var c = r.color;
            c.a = Mathf.Clamp01(ghostAlpha);
            r.color = c;
        }

        currentGhost.gameObject.SetActive(false);
    }

    private void UpdateGhostPreview(Vector2Int targetCell, bool canPlace)
    {
        if (currentGhost == null)
            return;

        if (!canPlace)
        {
            currentGhost.gameObject.SetActive(false);
            return;
        }

        // למקם את ה-Ghost כך שיהיה "מודבק" לגריד מתחת לצורה
        Vector3 snappedWorld = board.GridToWorld(targetCell);
        Vector3 delta = snappedWorld - transform.position;

        if (!currentGhost.gameObject.activeSelf)
            currentGhost.gameObject.SetActive(true);

        currentGhost.transform.position = shape.transform.position + delta;
    }

    private void HideAndDestroyGhost()
    {
        if (currentGhost == null)
            return;

        Destroy(currentGhost.gameObject);
        currentGhost = null;
    }
}
