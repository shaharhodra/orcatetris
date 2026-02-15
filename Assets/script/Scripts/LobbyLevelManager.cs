using UnityEngine;
using DG.Tweening;
using System.Collections;

public class LobbyLevelManager : MonoBehaviour
{
    [SerializeField] private LobbyLevelButton[] levelButtons;
    [SerializeField] private RectTransform pointer;

    private void Start()
    {
        StartCoroutine(Init());
    }

    private void OnDisable()
    {
        if (pointer != null)
            pointer.DOKill();
    }

    private IEnumerator Init()
    {
        if (levelButtons == null || levelButtons.Length == 0)
            yield break;

        while (GameManager.instance == null)
            yield return null;

        int highest = GameManager.instance.HighestUnlockedLevel;

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

                pointer.DOKill();
                pointer.DOScale(1.1f, 0.5f)
                       .SetLoops(-1, LoopType.Yoyo)
                       .SetEase(Ease.InOutSine);
            }
        }
    }
}
