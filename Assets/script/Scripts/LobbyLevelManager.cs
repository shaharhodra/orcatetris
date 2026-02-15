using UnityEngine;
using DG.Tweening;

public class LobbyLevelManager : MonoBehaviour
{
    [SerializeField] private LobbyLevelButton[] levelButtons;
    [SerializeField] private RectTransform pointer;

    private void Start()
    {
        if (levelButtons == null || levelButtons.Length == 0)
            return;

        int highest = GameManager.instance != null ? GameManager.instance.HighestUnlockedLevel : 0;

        LobbyLevelButton lastUnlockedButton = null;

        for (int i = 0; i < levelButtons.Length; i++)
        {
            var lb = levelButtons[i];
            if (lb == null)
                continue;

            bool isUnlocked = lb.LevelIndex <= highest;
            bool isCompleted = lb.LevelIndex < highest;

            lb.SetState(isUnlocked, isCompleted);

            if (isUnlocked)
                lastUnlockedButton = lb;
        }

        if (pointer != null && lastUnlockedButton != null)
        {
            var target = lastUnlockedButton.GetComponent<RectTransform>();
            if (target != null)
            {
                pointer.SetParent(target, worldPositionStays: false);
                pointer.anchoredPosition = Vector2.zero;
                pointer.localScale = Vector3.one;

                // אנימציית נשימה/פמפום עדינה על הסמן
                pointer.DOKill();
                pointer.DOScale(1.1f, 0.5f)
                       .SetLoops(-1, LoopType.Yoyo)
                       .SetEase(Ease.InOutSine);
            }
        }
    }
}
