using UnityEngine;
using UnityEngine.UI;

public class LobbyLevelButton : MonoBehaviour
{
    [SerializeField] private int levelIndex = 0;
    [SerializeField] private Button button;
    [SerializeField] private GameObject completedIcon;
    [SerializeField] private GameObject coverImage;       // static cover, shown when locked (no animation)
    [SerializeField] private GameObject coverPrefab;      // animated cover prefab with LobbyLevelCover component
    [SerializeField] private RectTransform coverParent;   // where to parent the cover (usually this RectTransform)

    public int LevelIndex => levelIndex;
    public Button Button => button;

    private bool coverInstantiated = false;

    private void Reset()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    public void SetState(bool isUnlocked, bool isCompleted)
    {
        if (button != null)
            button.interactable = isUnlocked;

        if (completedIcon != null)
            completedIcon.SetActive(isCompleted);

        // static cover: visible when locked, hidden when unlocked
        if (coverImage != null)
            coverImage.SetActive(!isUnlocked);
    }

    // Call this to animate a cover flying in for a newly completed level
    public void AnimateCoverIn(float delay = 0f)
    {
        if (coverPrefab == null || coverInstantiated) return;

        coverInstantiated = true;

        RectTransform parent = coverParent != null ? coverParent : GetComponent<RectTransform>();
        var go = Instantiate(coverPrefab, parent);
        var rect = go.GetComponent<RectTransform>();
        if (rect != null)
            rect.anchoredPosition = Vector2.zero;

        var cover = go.GetComponent<LobbyLevelCover>();
        if (cover != null)
            cover.AnimateIn(delay);
    }

    // Reset so cover can be re-instantiated (e.g. after scene reload)
    public void ResetCover()
    {
        coverInstantiated = false;
    }
}
