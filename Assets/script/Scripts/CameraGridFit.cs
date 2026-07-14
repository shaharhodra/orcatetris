using UnityEngine;

/// <summary>
/// Fits the camera's orthographic size and position so the grid and the shape
/// tray stay fully visible regardless of the device's screen aspect ratio.
/// Bounds come from the grid's own size (origin/width/height/cellSize) plus a
/// fixed radius around each tray slot's anchor position — NOT from scanning
/// live renderers, because a shape mid-drag would otherwise drag the camera
/// along with it. The fit only recomputes when the screen size or the grid's
/// column/row count actually changes (e.g. on a new level), so placing or
/// dragging a shape never shifts the camera.
/// Attach to the Main Camera.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraGridFit : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridBoard gridBoard;
    [Tooltip("Fixed anchor points that must stay visible, e.g. the shape tray slots. Only their own position is used (not their children), so a dragged/placed shape never moves the camera.")]
    [SerializeField] private Transform[] anchorPoints;
    [Tooltip("Radius reserved around each anchor point for its content (e.g. half the biggest shape's footprint)")]
    [SerializeField] private float anchorRadius = 1.5f;

    [Header("Padding")]
    [SerializeField] private float extraPadding = 0.3f;

    [Header("Limits")]
    [SerializeField] private float minOrthoSize = 3f;
    [SerializeField] private float maxOrthoSize = 14f;

    private Camera cam;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private int lastGridColumns = -1;
    private int lastGridRows = -1;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        if (gridBoard == null)
            gridBoard = FindFirstObjectByType<GridBoard>();

        Fit();
    }

    private void LateUpdate()
    {
        bool screenChanged = Screen.width != lastScreenWidth || Screen.height != lastScreenHeight;
        bool gridChanged = gridBoard != null && (gridBoard.Columns != lastGridColumns || gridBoard.Rows != lastGridRows);

        if (screenChanged || gridChanged)
            Fit();
    }

    public void Fit()
    {
        if (cam == null)
            return;

        bool hasBounds = false;
        Bounds bounds = new Bounds();

        if (gridBoard != null)
        {
            Vector2 min = gridBoard.origin;
            Vector2 max = min + new Vector2(gridBoard.width, gridBoard.height) * gridBoard.cellSize;
            bounds.SetMinMax(new Vector3(min.x, min.y, 0f), new Vector3(max.x, max.y, 0f));
            hasBounds = true;
        }

        if (anchorPoints != null)
        {
            foreach (var anchor in anchorPoints)
            {
                if (anchor == null)
                    continue;

                Bounds anchorBounds = new Bounds(anchor.position, Vector3.one * (anchorRadius * 2f));

                if (!hasBounds)
                {
                    bounds = anchorBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(anchorBounds);
                }
            }
        }

        if (!hasBounds)
            return;

        float aspect = cam.aspect;

        float contentWidth = bounds.size.x + extraPadding * 2f;
        float contentHeight = bounds.size.y + extraPadding * 2f;

        float orthoForHeight = contentHeight * 0.5f;
        float orthoForWidth = (contentWidth * 0.5f) / aspect;

        float targetOrtho = Mathf.Clamp(Mathf.Max(orthoForHeight, orthoForWidth), minOrthoSize, maxOrthoSize);
        cam.orthographicSize = targetOrtho;

        Vector3 center = bounds.center;
        transform.position = new Vector3(center.x, center.y, transform.position.z);

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        if (gridBoard != null)
        {
            lastGridColumns = gridBoard.Columns;
            lastGridRows = gridBoard.Rows;
        }
    }
}
