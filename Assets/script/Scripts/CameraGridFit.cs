using UnityEngine;

/// <summary>
/// Automatically adjusts the camera's orthographic size and position
/// so the entire game screen (grid + shape tray + top UI) is always
/// fully visible regardless of screen aspect ratio or device.
/// Attach to the Main Camera.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraGridFit : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridBoard gridBoard;
    [Tooltip("The shape tray transform (lowest element on screen)")]
    [SerializeField] private Transform shapeTray;

    [Header("Design Reference")]
    [Tooltip("The aspect ratio you designed the game for (width/height). 9:16 = 0.5625")]
    [SerializeField] private float referenceAspect = 0.5625f;
    [Tooltip("The orthographic size that looks correct in the editor at the reference aspect")]
    [SerializeField] private float referenceOrthoSize = 8f;

    [Header("World Bounds")]
    [Tooltip("Topmost Y position that must be visible (top UI edge in world units)")]
    [SerializeField] private float worldTop = 7f;
    [Tooltip("Bottommost Y position that must be visible (bottom of shape tray in world units)")]
    [SerializeField] private float worldBottom = -7f;
    [Tooltip("Leftmost X position that must be visible")]
    [SerializeField] private float worldLeft = -4f;
    [Tooltip("Rightmost X position that must be visible")]
    [SerializeField] private float worldRight = 4f;

    [Header("Padding")]
    [SerializeField] private float extraPadding = 0.5f;

    [Header("Limits")]
    [SerializeField] private float minOrthoSize = 5f;
    [SerializeField] private float maxOrthoSize = 14f;

    [Header("Options")]
    [Tooltip("Also reposition camera Y to center the visible content")]
    [SerializeField] private bool adjustCameraPosition = true;

    private Camera cam;
    private int lastScreenWidth;
    private int lastScreenHeight;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        if (gridBoard == null)
            gridBoard = FindFirstObjectByType<GridBoard>();

        FitCamera();
    }

    private void LateUpdate()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            FitCamera();
        }
    }

    public void FitCamera()
    {
        if (cam == null)
            return;

        float aspect = cam.aspect; // width / height

        // Total world area that must be visible
        float contentHeight = (worldTop - worldBottom) + extraPadding * 2f;
        float contentWidth = (worldRight - worldLeft) + extraPadding * 2f;

        // Ortho size needed to fit height
        float orthoForHeight = contentHeight * 0.5f;
        // Ortho size needed to fit width given current aspect
        float orthoForWidth = (contentWidth * 0.5f) / aspect;

        // Use the larger to guarantee everything fits
        float targetOrtho = Mathf.Max(orthoForHeight, orthoForWidth);
        targetOrtho = Mathf.Clamp(targetOrtho, minOrthoSize, maxOrthoSize);

        cam.orthographicSize = targetOrtho;

        // Center camera vertically on the content
        if (adjustCameraPosition)
        {
            float centerY = (worldTop + worldBottom) * 0.5f;
            float centerX = (worldLeft + worldRight) * 0.5f;
            transform.position = new Vector3(centerX, centerY, transform.position.z);
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }
}
