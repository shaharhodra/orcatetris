using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public class ShapeDragHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public static bool InputBlocked;
    [SerializeField] private GridBoard board;
    [SerializeField] private GridPlacer boardPlacer;
    [SerializeField] private Shape shape;
    [Header("Ghost Preview")]
    [SerializeField] private Shape ghostPrefab; // אופציונלי: אם לא הוגדר, נשתמש בעותק של הצורה עצמה
    [SerializeField] private float ghostAlpha = 0.5f;
    [SerializeField] private Color[] ghostColors = new Color[]
    {
        Color.cyan, Color.magenta, Color.yellow, Color.green, Color.red, new Color(1f, 0.5f, 0f), new Color(0.5f, 0f, 1f)
    };
    [SerializeField, Min(0)] private int ghostSnapRadius = 0; // כמה תאים סביב התא המרכזי לחפש מיקום חוקי
    [Header("Click Particle")]
    [SerializeField] private GameObject clickParticlePrefab;
    [SerializeField] private float minFingerOffsetX = 0f;
    [SerializeField] private float maxFingerOffsetX = 1.5f;
    [SerializeField] private float minFingerOffsetY = 0.5f;
    [SerializeField] private float maxFingerOffsetY = 3.0f;
    [SerializeField] private float horizontalOffsetRangePixels = 200f;
    [SerializeField] private float verticalOffsetRangePixels = 200f; // כמה גרירת מסך דרושה כדי להגיע למקסימום
    [SerializeField] private float validAlpha = 0.8f;
    [SerializeField] private float invalidAlpha = 0.3f;
    [SerializeField] private int dragSortingOrderBoost = 100;

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
    private Vector2Int? lastValidCell;
    private System.Collections.Generic.List<Vector2Int> previewBuffer;
    private int[] originalSortingOrders;
    private bool wasPreviewingClear; // כדי להשמיע את צליל "אפשרות למחיקה" פעם אחת בלבד במעבר, לא בכל פריים

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
        if (InputBlocked)
            return;

        if (isPlaced)
            return;

        if (SoundManager.instance != null)
        {
            SoundManager.instance.PlayClickShape();
        }

        PlayClickParticle();

        pointerDown = true;
        beganDrag = false;

        // Raise sorting order so dragged shape appears above grid blocks
        RaiseSortingOrder();

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
        if (InputBlocked)
            return;

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
        wasPreviewingClear = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (InputBlocked)
            return;

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
        RestoreSortingOrder();

        if (usePressScale)
        {
            scaleTween?.Kill();
            // חזרה לגודל מנוחה קטן
            Vector3 idleScale = originalScale * idleScaleFactor;
            scaleTween = transform.DOScale(idleScale, pressScaleDuration).SetEase(Ease.OutQuad);
        }

        HideAndDestroyGhost();

        if (board != null)
        {
            board.ClearPreviewClear();
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (InputBlocked)
            return;

        if (isPlaced)
            return;

        pointerDown = false;

        // נשתמש בתא החוקי האחרון שנמצא (עם הרדיוס), ואם אין – בתא מתחת לצורה
        Vector2Int cell = lastValidCell.HasValue
            ? lastValidCell.Value
            : board.WorldToGrid((Vector2)transform.position);

        bool canPlace = boardPlacer.CanPlaceShape(shape, cell);
        if (canPlace)
        {
            bool linesCleared = boardPlacer.PlaceShape(shape, cell);

            // Skip the generic "place" sound when a line/board-clear sound is about to
            // play for this same placement (played inside PlaceShape) — one sound per
            // placement, not two stacked together.
            if (!linesCleared && SoundManager.instance != null)
            {
                SoundManager.instance.PlayPlaceShape();
            }

            SetAlpha(1f);
            RestoreSortingOrder();

            HideAndDestroyGhost();

            isPlaced = true;

            // אופציונלי: לכבות קוליידר כדי שלא יתפסו עוד דרגים
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.enabled = false;
            }

            if (board != null)
            {
                board.ClearPreviewClear();
            }
        }
        else
        {
            transform.position = startPos;
            SetAlpha(1f);
            RestoreSortingOrder();

            if (usePressScale)
            {
                scaleTween?.Kill();
                // חזרה לגודל מנוחה קטן
                Vector3 idleScale = originalScale * idleScaleFactor;
                scaleTween = transform.DOScale(idleScale, pressScaleDuration).SetEase(Ease.OutQuad);
            }

            HideAndDestroyGhost();

            if (board != null)
            {
                board.ClearPreviewClear();
            }
        }
    }

    private void PlayClickParticle()
    {
        if (clickParticlePrefab == null)
            return;

        GameObject instance = Instantiate(clickParticlePrefab, transform.position, Quaternion.identity);

        float maxDuration = 0f;
        foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Play();
            float duration = ps.main.duration + ps.main.startLifetime.constantMax;
            if (duration > maxDuration)
                maxDuration = duration;
        }

        Destroy(instance, maxDuration > 0f ? maxDuration : 2f);
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

    private void RaiseSortingOrder()
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        originalSortingOrders = new int[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalSortingOrders[i] = renderers[i].sortingOrder;
            renderers[i].sortingOrder += dragSortingOrderBoost;
        }
    }

    private void RestoreSortingOrder()
    {
        if (originalSortingOrders == null) return;
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        for (int i = 0; i < renderers.Length && i < originalSortingOrders.Length; i++)
        {
            renderers[i].sortingOrder = originalSortingOrders[i];
        }
        originalSortingOrders = null;
    }

    private void UpdatePlacementFeedback()
    {
        if (board == null || boardPlacer == null || shape == null)
            return;

        Vector2Int baseCell = board.WorldToGrid((Vector2)transform.position);

        // חיפוש תא חוקי בטווח רדיוס מסביב לתא הבסיס
        Vector2Int chosenCell = baseCell;
        bool canPlace = TryFindPlacementCell(baseCell, out chosenCell);

        if (canPlace)
            lastValidCell = chosenCell;
        else
            lastValidCell = null;

        // Keep dragged shape always at full opacity
        SetAlpha(1f);

        UpdateGhostPreview(chosenCell, canPlace);

        // עדכון היילייט של שורות/עמודות שעומדות להימחק
        if (board != null)
        {
            if (canPlace && lastValidCell.HasValue)
            {
                if (previewBuffer == null)
                    previewBuffer = new System.Collections.Generic.List<Vector2Int>();

                var cellsToClear = board.GetPreviewClearCells(shape, lastValidCell.Value, previewBuffer);
                board.SetPreviewClearCells(cellsToClear);

                bool isPreviewingClear = cellsToClear != null && cellsToClear.Count > 0;
                // Guard with beganDrag so this doesn't fire on the initial OnPointerDown
                // call (which also runs UpdatePlacementFeedback at the shape's resting
                // position) — that would stack this sound directly on top of the
                // click-pickup sound on every single grab.
                if (isPreviewingClear && !wasPreviewingClear && beganDrag && SoundManager.instance != null)
                    SoundManager.instance.PlayLineClearPossible();
                wasPreviewingClear = isPreviewingClear;
            }
            else
            {
                board.ClearPreviewClear();
                wasPreviewingClear = false;
            }
        }
    }

    /// <summary>
    /// מחפש תא חוקי להצבת הצורה, החל מהתא הנתון ובטווח רדיוס ghostSnapRadius מסביבו.
    /// אם הרדיוס 0 – בודק רק את התא הנתון (התנהגות ישנה).
    /// </summary>
    private bool TryFindPlacementCell(Vector2Int baseCell, out Vector2Int chosenCell)
    {
        chosenCell = baseCell;

        int radius = Mathf.Max(ghostSnapRadius, 0);
        if (radius == 0)
        {
            bool canPlaceBase = boardPlacer.CanPlaceShape(shape, baseCell);
            return canPlaceBase;
        }

        bool foundAny = false;
        float bestDistSq = float.MaxValue;
        Vector3 shapePos = transform.position;

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                Vector2Int testCell = new Vector2Int(baseCell.x + dx, baseCell.y + dy);

                if (!board.IsInside(testCell))
                    continue;

                if (!boardPlacer.CanPlaceShape(shape, testCell))
                    continue;

                // מודדים מרחק בין מרכז התא למיקום הצורה כדי לבחור את התא הקרוב ביותר
                Vector3 cellWorld = board.GridToWorld(testCell);
                float distSq = (cellWorld - shapePos).sqrMagnitude;

                if (!foundAny || distSq < bestDistSq)
                {
                    foundAny = true;
                    bestDistSq = distSq;
                    chosenCell = testCell;
                }
            }
        }

        return foundAny;
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

        // להוריד את ה-sortingOrder של ה-Ghost כדי שיהיה מתחת לצורה הנגררת
        var ghostRenderers = currentGhost.GetComponentsInChildren<SpriteRenderer>();
        foreach (var r in ghostRenderers)
        {
            r.sortingOrder -= 10;
        }

        // להסיר ThemeBlock מה-Ghost כדי שלא ידרוס את האלפה
        var themeBlocks = currentGhost.GetComponentsInChildren<ThemeBlock>();
        foreach (var tb in themeBlocks)
        {
            Destroy(tb);
        }

        // להפוך את ה-Ghost לשקוף יותר
        ApplyGhostAlpha();

        currentGhost.gameObject.SetActive(false);
    }

    private Color currentGhostColor;

    private void ApplyGhostAlpha()
    {
        if (currentGhost == null) return;

        // Pick a random color for this ghost instance
        if (ghostColors != null && ghostColors.Length > 0)
            currentGhostColor = ghostColors[Random.Range(0, ghostColors.Length)];
        else
            currentGhostColor = Color.white;

        currentGhostColor.a = Mathf.Clamp(ghostAlpha, 0.1f, 0.9f);

        var renderers = currentGhost.GetComponentsInChildren<SpriteRenderer>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.color = currentGhostColor;
        }
    }

    private void RefreshGhostColor()
    {
        if (currentGhost == null) return;
        var renderers = currentGhost.GetComponentsInChildren<SpriteRenderer>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.color = currentGhostColor;
        }
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

        // Refresh color every frame to prevent overrides
        RefreshGhostColor();
    }

    private void HideAndDestroyGhost()
    {
        if (currentGhost == null)
            return;

        Destroy(currentGhost.gameObject);
        currentGhost = null;
    }
}
