using UnityEngine;
using DG.Tweening;

// Attach to the cover prefab that slides in to seal a completed level slot.
// Call AnimateIn() after placing it at the target position.
public class LobbyLevelCover : MonoBehaviour
{
    [Header("Fly-in Settings")]
    [SerializeField] private float flyDuration = 0.45f;
    [SerializeField] private float flyOffscreenOffset = 600f; // pixels off screen to start from
    [SerializeField] private Ease flyEase = Ease.OutBack;

    public enum EntryDirection { FromLeft, FromRight, FromTop, FromBottom, FromOutside }
    [SerializeField] private EntryDirection entryDirection = EntryDirection.FromOutside;

    private RectTransform rect;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    // Call this after parenting and setting anchoredPosition = Vector2.zero (final position)
    public void AnimateIn(float delay = 0f)
    {
        if (rect == null) return;

        Vector2 finalPos = rect.anchoredPosition;
        Vector2 startOffset = GetStartOffset();

        rect.anchoredPosition = finalPos + startOffset;
        rect.localScale = Vector3.one;

        rect.DOAnchorPos(finalPos, flyDuration)
            .SetDelay(delay)
            .SetEase(flyEase);
    }

    private Vector2 GetStartOffset()
    {
        switch (entryDirection)
        {
            case EntryDirection.FromLeft:   return new Vector2(-flyOffscreenOffset, 0f);
            case EntryDirection.FromRight:  return new Vector2( flyOffscreenOffset, 0f);
            case EntryDirection.FromTop:    return new Vector2(0f,  flyOffscreenOffset);
            case EntryDirection.FromBottom: return new Vector2(0f, -flyOffscreenOffset);
            case EntryDirection.FromOutside:
            default:
                // pick a random cardinal direction
                int r = Random.Range(0, 4);
                float o = flyOffscreenOffset;
                return r == 0 ? new Vector2(-o, 0f)
                     : r == 1 ? new Vector2( o, 0f)
                     : r == 2 ? new Vector2(0f,  o)
                              : new Vector2(0f, -o);
        }
    }
}
